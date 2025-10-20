// PerformanceLogger.cs - 性能日志文件记录器
using System;
using System.IO;
using UnityEngine;

namespace UrbanXplain
{
    /// <summary>
    /// 将性能日志输出到独立的文本文件中
    /// </summary>
    public class PerformanceLogger
    {
        private static PerformanceLogger _instance;
        public static PerformanceLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PerformanceLogger();
                }
                return _instance;
            }
        }

        private string _logFilePath;
        private StreamWriter _writer;
        private bool _isInitialized = false;

        private PerformanceLogger()
        {
            InitializeLogFile();
        }

        /// <summary>
        /// 初始化日志文件
        /// </summary>
        private void InitializeLogFile()
        {
            try
            {
                // 使用项目根目录，便于查找
                string logDirectory = Application.dataPath + "/../PerformanceLogs";

                // 确保目录存在
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // 文件名包含时间戳
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(logDirectory, $"LLM_Performance_{timestamp}.log");

                // 创建StreamWriter（追加模式）
                _writer = new StreamWriter(_logFilePath, true);
                _writer.AutoFlush = true; // 自动刷新，确保实时写入

                _isInitialized = true;

                // 写入文件头
                WriteHeader();

                Debug.Log($"[PerformanceLogger] 性能日志已初始化，文件路径: {_logFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PerformanceLogger] 初始化失败: {e.Message}");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 写入文件头信息
        /// </summary>
        private void WriteHeader()
        {
            if (!_isInitialized) return;

            _writer.WriteLine("================================================================================");
            _writer.WriteLine("           UrbanXplain - LLM Performance Log");
            _writer.WriteLine("================================================================================");
            _writer.WriteLine($"Session Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine($"Unity Version: {Application.unityVersion}");
            _writer.WriteLine($"Platform: {Application.platform}");
            _writer.WriteLine("================================================================================");
            _writer.WriteLine();
        }

        /// <summary>
        /// 记录日志（带时间戳）
        /// </summary>
        public void Log(string message)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[PerformanceLogger] Logger not initialized, skipping log.");
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                _writer.WriteLine($"[{timestamp}] {message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PerformanceLogger] 写入日志失败: {e.Message}");
            }
        }

        /// <summary>
        /// 记录日志（不带时间戳，用于格式化输出）
        /// </summary>
        public void LogRaw(string message)
        {
            if (!_isInitialized) return;

            try
            {
                _writer.WriteLine(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PerformanceLogger] 写入日志失败: {e.Message}");
            }
        }

        /// <summary>
        /// 写入分隔线
        /// </summary>
        public void LogSeparator()
        {
            LogRaw("--------------------------------------------------------------------------------");
        }

        /// <summary>
        /// 写入空行
        /// </summary>
        public void LogEmptyLine()
        {
            LogRaw("");
        }

        /// <summary>
        /// 关闭日志文件
        /// </summary>
        public void Close()
        {
            if (_writer != null && _isInitialized)
            {
                try
                {
                    _writer.WriteLine();
                    _writer.WriteLine("================================================================================");
                    _writer.WriteLine($"Session End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _writer.WriteLine("================================================================================");
                    _writer.Close();
                }
                catch (Exception e)
                {
                    // 只在非disposed状态下记录错误
                    if (!(e is ObjectDisposedException))
                    {
                        Debug.LogError($"[PerformanceLogger] 关闭日志文件失败: {e.Message}");
                    }
                }
                finally
                {
                    _writer = null;
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// 析构函数，确保文件关闭（静默处理，避免垃圾回收时的错误）
        /// </summary>
        ~PerformanceLogger()
        {
            // 在析构函数中静默关闭，不报错
            if (_writer != null && _isInitialized)
            {
                try
                {
                    _writer?.Dispose();
                }
                catch
                {
                    // 静默处理，析构函数中不应该抛出异常或记录日志
                }
                finally
                {
                    _writer = null;
                    _isInitialized = false;
                }
            }
        }
    }
}
