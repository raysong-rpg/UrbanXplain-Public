// ControllerStreamingHandler.cs - 处理总控LLM的流式策略输出
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace UrbanXplain
{
    /// <summary>
    /// 用于解析总控LLM流式输出的区域策略，并动态触发执行LLM
    /// </summary>
    public class ControllerStreamingHandler : DownloadHandlerScript
    {
        // 区域策略数据结构
        [System.Serializable]
        public class RegionStrategy
        {
            public int RegionID;           // 区域编号 1-6
            public string LandRange;       // 地块范围 "1-7", "8-14"等
            public string FunctionalRole;  // 功能定位（居住区/商业区/文化区等）
            public string DesignGuideline; // 设计指导原则
            public string KeyConstraints;  // 关键约束
        }

        private ConcurrentQueue<RegionStrategy> _strategyQueue;  // 线程安全队列
        private Action _onAllComplete;

        private StringBuilder _lineBuffer = new StringBuilder();
        private StringBuilder _jsonContentBuffer = new StringBuilder();
        private int _braceDepth = 0;

        private HashSet<int> _receivedRegions = new HashSet<int>(); // 追踪已接收的区域

        public ControllerStreamingHandler(ConcurrentQueue<RegionStrategy> queue, Action onComplete)
        {
            _strategyQueue = queue;
            _onAllComplete = onComplete;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
            {
                return false;
            }

            string textChunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _lineBuffer.Append(textChunk);

            ProcessLineBuffer();

            return true;
        }

        private void ProcessLineBuffer()
        {
            int newlineIndex;
            while ((newlineIndex = _lineBuffer.ToString().IndexOf('\n')) != -1)
            {
                string line = _lineBuffer.ToString(0, newlineIndex).Trim();
                _lineBuffer.Remove(0, newlineIndex + 1);

                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring(5).Trim();

                    if (jsonData == "[DONE]")
                    {
                        continue;
                    }

                    try
                    {
                        var chunk = JObject.Parse(jsonData);
                        string contentFragment = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();

                        if (!string.IsNullOrEmpty(contentFragment))
                        {
                            _jsonContentBuffer.Append(contentFragment);
                            TryParseCompleteRegionStrategies();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ControllerStreamingHandler] 无法解析JSON块: {jsonData}. 错误: {e.Message}");
                    }
                }
            }
        }

        private void TryParseCompleteRegionStrategies()
        {
            string content = _jsonContentBuffer.ToString();
            int searchStartIndex = 0;

            while (searchStartIndex < content.Length)
            {
                int objectStartIndex = content.IndexOf('{', searchStartIndex);
                if (objectStartIndex == -1) break;

                int objectEndIndex = -1;
                _braceDepth = 0;

                for (int i = objectStartIndex; i < content.Length; i++)
                {
                    if (content[i] == '{')
                    {
                        _braceDepth++;
                    }
                    else if (content[i] == '}')
                    {
                        _braceDepth--;
                        if (_braceDepth == 0)
                        {
                            objectEndIndex = i;
                            break;
                        }
                    }
                }

                if (objectEndIndex != -1)
                {
                    string objectJson = content.Substring(objectStartIndex, objectEndIndex - objectStartIndex + 1);

                    try
                    {
                        var regionStrategy = JsonConvert.DeserializeObject<RegionStrategy>(objectJson);
                        if (regionStrategy != null && !_receivedRegions.Contains(regionStrategy.RegionID))
                        {
                            _receivedRegions.Add(regionStrategy.RegionID);
                            Debug.Log($"[ControllerStreamingHandler] 解析到区域{regionStrategy.RegionID}策略，加入队列等待主线程处理");

                            // 使用线程安全队列，避免在后台线程直接调用StartCoroutine
                            _strategyQueue.Enqueue(regionStrategy);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ControllerStreamingHandler] 反序列化区域策略失败: {objectJson}. 错误: {e.Message}");
                    }

                    searchStartIndex = objectEndIndex + 1;
                }
                else
                {
                    break;
                }
            }

            if (searchStartIndex > 0)
            {
                _jsonContentBuffer.Remove(0, searchStartIndex);
            }
        }

        protected override void CompleteContent()
        {
            ProcessLineBuffer();
            Debug.Log($"[ControllerStreamingHandler] 总控流结束，共接收到{_receivedRegions.Count}个区域策略");
            _onAllComplete?.Invoke();
        }
    }
}
