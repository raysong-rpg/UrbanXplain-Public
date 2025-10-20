// StreamingDownloadHandler.cs (已重写以正确处理SSE流式数据)
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace UrbanXplain
{
    public class StreamingDownloadHandler : DownloadHandlerScript
    {
        private ConcurrentQueue<DeepSeekAPI.EmptyLandData> _dataQueue;
        private Action _onComplete;

        // 用于缓冲原始文本数据的 StringBuilder
        private StringBuilder _lineBuffer = new StringBuilder();
        // 用于拼接JSON内容碎片的 StringBuilder
        private StringBuilder _jsonContentBuffer = new StringBuilder();

        // 用于追踪JSON对象 { } 的嵌套深度，以找到完整的对象
        private int _braceDepth = 0;

        public StreamingDownloadHandler(ConcurrentQueue<DeepSeekAPI.EmptyLandData> queue, Action onComplete)
        {
            _dataQueue = queue;
            _onComplete = onComplete;
        }

        // 这个方法在后台线程被Unity调用，每当有新数据块到达时
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
            {
                return false;
            }

            // 将新数据块转换为文本，追加到我们的行缓冲区并处理
            string textChunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _lineBuffer.Append(textChunk);

            ProcessLineBuffer();

            return true;
        }

        // 逐行处理缓冲区的文本
        private void ProcessLineBuffer()
        {
            // 只要能找到完整的行（以\n结尾），就持续处理
            int newlineIndex;
            while ((newlineIndex = _lineBuffer.ToString().IndexOf('\n')) != -1)
            {
                // 提取单行
                string line = _lineBuffer.ToString(0, newlineIndex).Trim();
                // 从缓冲区中移除已处理的行
                _lineBuffer.Remove(0, newlineIndex + 1);

                // 忽略空行
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                // 这是处理服务器发送事件（SSE）的核心逻辑
                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring(5).Trim();

                    // 检查是否是流结束的标志
                    if (jsonData == "[DONE]")
                    {
                        continue;
                    }

                    try
                    {
                        // 解析这个JSON小数据块，以提取内容碎片
                        var chunk = JObject.Parse(jsonData);
                        string contentFragment = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();

                        if (!string.IsNullOrEmpty(contentFragment))
                        {
                            // 拼接触内容碎片，并尝试从中解析出完整的对象
                            _jsonContentBuffer.Append(contentFragment);
                            TryParseCompleteObjects();
                        }
                    }
                    catch (Exception e)
                    {
                        // 如果某一行格式错误，记录警告并继续
                        Debug.LogWarning($"[StreamingDownloadHandler] 无法解析JSON块: {jsonData}. 错误: {e.Message}");
                    }
                }
            }
        }

        // 扫描已拼接的JSON内容缓冲区，寻找完整的 {...} 对象
        private void TryParseCompleteObjects()
        {
            string content = _jsonContentBuffer.ToString();
            int searchStartIndex = 0;

            while (searchStartIndex < content.Length)
            {
                // 寻找下一个对象的开始 '{'
                int objectStartIndex = content.IndexOf('{', searchStartIndex);
                if (objectStartIndex == -1) break; // 找不到更多对象了

                int objectEndIndex = -1;
                _braceDepth = 0;

                // 从 '{' 开始，通过计算括号嵌套来找到匹配的 '}'
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
                    // 我们找到了一个从 { 到 } 的完整对象
                    string objectJson = content.Substring(objectStartIndex, objectEndIndex - objectStartIndex + 1);

                    // 容错处理：修复常见的JSON格式错误
                    objectJson = FixCommonJsonErrors(objectJson);

                    try
                    {
                        var landData = JsonConvert.DeserializeObject<DeepSeekAPI.EmptyLandData>(objectJson);
                        if (landData != null)
                        {
                            // 记录原始JSON到日志文件
                            PerformanceLogger.Instance.LogEmptyLine();
                            PerformanceLogger.Instance.Log($"📦 接收到地块 {landData.EmptyID} 的JSON数据:");
                            PerformanceLogger.Instance.LogRaw(objectJson);
                            PerformanceLogger.Instance.LogEmptyLine();

                            // 成功！添加到队列，等待主线程处理
                            _dataQueue.Enqueue(landData);
                        }
                    }
                    catch (Exception e)
                    {
                        // 记录失败的JSON到日志文件
                        PerformanceLogger.Instance.LogEmptyLine();
                        PerformanceLogger.Instance.Log($"❌ 地块JSON反序列化失败:");
                        PerformanceLogger.Instance.LogRaw($"JSON内容: {objectJson}");
                        PerformanceLogger.Instance.LogRaw($"错误信息: {e.Message}");
                        PerformanceLogger.Instance.LogEmptyLine();

                        Debug.LogError($"[StreamingDownloadHandler] 反序列化完整JSON对象时失败: {objectJson}. 错误: {e.Message}");
                    }

                    // 将搜索位置移动到刚处理完的对象的后面
                    searchStartIndex = objectEndIndex + 1;
                }
                else
                {
                    // 对象不完整，需要更多的数据才能闭合
                    break;
                }
            }

            // 如果我们处理了一些对象，就将它们从缓冲区前端移除
            if (searchStartIndex > 0)
            {
                _jsonContentBuffer.Remove(0, searchStartIndex);
            }
        }

        // 当下载完全结束时被Unity调用
        protected override void CompleteContent()
        {
            // 处理缓冲区中可能剩余的任何文本
            ProcessLineBuffer();
            Debug.Log("[StreamingDownloadHandler] 流式下载过程结束。");
            _onComplete?.Invoke();
        }

        /// <summary>
        /// 修复LLM输出的常见JSON格式错误
        /// </summary>
        private string FixCommonJsonErrors(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            // 修复1: EmptyID字段中多余的引号和空格
            // 错误: "EmptyID": " "16"  或 "EmptyID": ""16""
            // 正确: "EmptyID": "16"
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                @"""EmptyID""\s*:\s*""\s*""(\d+)""",
                @"""EmptyID"": ""$1"""
            );

            // 修复2: EmptyID字段开头有空格
            // 错误: "EmptyID": " 16"
            // 正确: "EmptyID": "16"
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                @"""EmptyID""\s*:\s*""\s+(\d+)""",
                @"""EmptyID"": ""$1"""
            );

            // 修复3: 数字字段被错误地加了引号
            // 错误: "Function": "1" (Function应该是数字)
            // 但保留EmptyID等应该是字符串的字段

            return json;
        }
    }
}