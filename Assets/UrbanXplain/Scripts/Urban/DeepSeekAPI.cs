// DeepSeekAPI.cs (Modified for Streaming with Debug Logs)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices.ComTypes;

namespace UrbanXplain
{
    public class DeepSeekAPI : MonoBehaviour
    {
        public BuildingSpawnerJson buildingSpawnerJson;

        [Header("API Settings")]
        private string apiKey = "";
        // private string modelR1Name = "deepseek-reasoner"; // Reserved for future use
        private string apiUrl = "https://api.deepseek.com/v1/chat/completions";

        [System.Serializable]
        private class Config
        {
            public string deepseek_api_key;
            public string deepseek_api_url;
        }

        [Header("Dialogue Settings")]
        [Range(0, 1)] public float temperature = 0.5f;
        // private int maxTokens = 16384; // Reserved for future use
        // private string responseFormatType = "json_object"; // Reserved for future use
        public ChildColorToggler[] colorTogglerArray;

        [System.Serializable]
        public class NPCCharacter
        {
            public string name = "urban planning expert proficient in Unity";
            [TextArea(3, 10)]
            public string personalityPrompt = ""; // Will be loaded from prompt.txt
        }
        [SerializeField] public NPCCharacter npcCharacter;

        [Header("Prompt Settings")]
        [Tooltip("Filename of the prompt in StreamingAssets folder")]
        public string promptFileName = "prompt.txt";

        [TextArea(3, 10)]
        public string TestJsonInput = @"";

        // 流式总控架构：八个独立的队列和完成标志
        private ConcurrentQueue<EmptyLandData> _landDataQueue1;  // 地块1-5
        private ConcurrentQueue<EmptyLandData> _landDataQueue2;  // 地块6-10
        private ConcurrentQueue<EmptyLandData> _landDataQueue3;  // 地块11-16
        private ConcurrentQueue<EmptyLandData> _landDataQueue4;  // 地块17-22
        private ConcurrentQueue<EmptyLandData> _landDataQueue5;  // 地块23-27
        private ConcurrentQueue<EmptyLandData> _landDataQueue6;  // 地块28-32
        private ConcurrentQueue<EmptyLandData> _landDataQueue7;  // 地块33-38
        private ConcurrentQueue<EmptyLandData> _landDataQueue8;  // 地块39-43
        private bool _isStreamingComplete1;
        private bool _isStreamingComplete2;
        private bool _isStreamingComplete3;
        private bool _isStreamingComplete4;
        private bool _isStreamingComplete5;
        private bool _isStreamingComplete6;
        private bool _isStreamingComplete7;
        private bool _isStreamingComplete8;

        // 总控流相关
        private bool _isControllerComplete;
        // private int _executorLaunchedCount = 0;  // Reserved for future use - 已启动的执行LLM数量
        private string _currentUserMessage;      // 保存用户消息用于执行LLM
        private DialogueCallback _currentCallback; // 保存回调
        private ConcurrentQueue<ControllerStreamingHandler.RegionStrategy> _strategyQueue; // 区域策略队列

        // ⏱️ Performance Timing - 记录每个LLM的开始和结束时间
        private float _overallStartTime;  // 整体流程开始时间
        private Dictionary<int, float> _executorStartTimes = new Dictionary<int, float>();  // 每个执行LLM的开始时间
        private Dictionary<int, float> _executorEndTimes = new Dictionary<int, float>();    // 每个执行LLM的结束时间
        private Dictionary<int, float> _executorFirstDataTimes = new Dictionary<int, float>(); // 每个执行LLM首次输出数据的时间
        private PerformanceLogger _perfLogger => PerformanceLogger.Instance; // 性能日志记录器

        // 🏙️ Fixed Region Configuration - 固定的8个区域配置
        [System.Serializable]
        public class RegionConfig
        {
            public int RegionID;
            public string LandRange;
            public int[] PlotIDs;
            public string LocationDescription;  // 区域位置描述
            public string FunctionalRole;       // 功能定位
            public string PlanningGuidelines;   // 详细规划指导
        }

        private static readonly RegionConfig[] FIXED_REGIONS = new RegionConfig[]
        {
            new RegionConfig
            {
                RegionID = 1,
                LandRange = "1-2,4-7",
                PlotIDs = new int[] { 1, 2, 4, 5, 6, 7 },
                LocationDescription = "Far western edge of the city (X: -636 to -364, Z: -386 to -86)",
                FunctionalRole = "Western Residential Gateway with Community Facilities",
                PlanningGuidelines = @"
**Regional Context**: Far western edge serving as the city's western residential gateway. Compact residential neighborhood with small community plots.

**Plot Characteristics**:
- Plot 1: Large plot (172x72m) on southwest corner
- Plot 2: Small plot (100x36m) west-central area
- Plots 4 & 5: Standard residential plots (172x72m) in south-central area
- Plots 6 & 7: Small square plots (72x72m), flexible community use

**Planning Strategy**:
1. **Residential-Oriented**: Primarily residential (Function=1), avoid super high-rise (FloorType ≠ 4)
2. **Community Services**: Plots 2, 6, 7 ideal for neighborhood facilities (small shops, parks, community centers)
3. **Height Gradient**: Low-to-mid-rise (FloorType=1-2), maintain human scale
4. **Materials**: Prefer concrete (Material=2) for residential warmth
5. **Energy Efficiency**: Residential 20-40, community facilities 25-45

**Key Considerations**:
- Create livable, walkable gateway
- Ensure sunlight and ventilation
- Balance density with comfort
"
            },
            new RegionConfig
            {
                RegionID = 2,
                LandRange = "3,8,10-13",
                PlotIDs = new int[] { 3, 8, 10, 11, 12, 13 },
                LocationDescription = "Northwest quadrant of the city (X: -636 to -264, Z: -86 to 486)",
                FunctionalRole = "Cultural and Educational Hub with 172x172 Cultural Landmark (Plot 8)",
                PlanningGuidelines = @"
**Regional Context**: Northwest cultural district spanning from city center north to far northern edge. Contains Plot 8 (172x172 super-large cultural landmark).

**Plot Characteristics**:
- Plot 8: **SUPER-LARGE** 172x172m plot - MANDATORY cultural landmark (Function=4, FloorType=3, Material=1)
- Plot 3: Extra-long plot (272x72m) on far northwest edge
- Plots 12 & 13: Large support plots (212-272m) in northern area
- Plots 10 & 11: Medium support plots

**Planning Strategy**:
1. **Cultural Anchor**: Plot 8 MUST be high-rise glass curtain wall cultural facility (museum, concert hall, science center)
2. **Educational Support**: Plots 10-13 ideal for schools, libraries, research centers (Function=3)
3. **Northern Gateway**: Plot 3 (far north) can be major educational or public institution
4. **Height Strategy**: High-rise cultural landmark (Plot 8 FloorType=3), mid-to-high-rise support (FloorType=2-3)
5. **Energy Range**: Cultural landmark 60-85; educational 35-55

**Mandatory Constraint**:
⚠️ Plot 8 (172x172): MUST be Function=4, FloorType=3, Material=1
"
            },
            new RegionConfig
            {
                RegionID = 3,
                LandRange = "14-17,19-20",
                PlotIDs = new int[] { 14, 15, 16, 17, 19, 20 },
                LocationDescription = "West-central transition area (X: -264 to -64, Z: -414 to 186)",
                FunctionalRole = "Mixed Residential, Commercial, and Public Facilities",
                PlanningGuidelines = @"
**Regional Context**: West-central transition zone connecting far west residential to city center. Vertical span from far south to north of center.

**Plot Characteristics**:
- Plot 14: Large plot (272x72m) on far southern edge
- Plot 15: Long plot (272x72m) west-central area
- Plots 16 & 17: Standard residential plots (172x72m) south-central
- Plots 19 & 20: Medium plots (186x72m) near city center

**Planning Strategy**:
1. **Mixed-Use Balance**: Distribute residential (Function=1), commercial (Function=2), and public (Function=3) evenly
2. **Southern Gateway**: Plot 14 (far south) suitable for gateway commercial or public building
3. **Residential Core**: Plots 15-17 residential-oriented, avoid super high-rise (FloorType ≠ 4)
4. **Central Connection**: Plots 19-20 (near center) can be commercial or mixed-use
5. **Height Variety**: Low-to-high-rise variety (FloorType=1-3), avoid uniformity
6. **Energy Range**: Commercial 50-70; residential 25-45; public 35-55

**Key Considerations**:
- Create smooth transition from residential west to urban center
- Ensure pedestrian-friendly connections
- Balance commercial vitality with residential livability
"
            },
            new RegionConfig
            {
                RegionID = 4,
                LandRange = "9,18,21-24",
                PlotIDs = new int[] { 9, 18, 21, 22, 23, 24 },
                LocationDescription = "City center core district (X: -300 to 136, Z: -186 to 386)",
                FunctionalRole = "Central Commercial and Residential Hub",
                PlanningGuidelines = @"
**Regional Context**: Prime city center location surrounding (0,0,0). Highest land value, mixed commercial and residential core.

**Plot Characteristics**:
- Plot 9: Large plot (272x72m) west-central area - **Flexible use** (public, cultural support, or commercial)
- Plot 18: Large plot (272x72m) south of center
- Plot 21: Large plot (272x72m) north of center - major axis
- Plots 22 & 23: Small square plots (72x72m) northwest quadrant
- Plot 24: Small plot (72x72m) far north

**Planning Strategy**:
1. **Commercial-Residential Mix**: Balance commercial (Function=2) and residential (Function=1) carefully
2. **Plot 9 Flexibility**: Can be public facility (Function=3), cultural support, or commercial - adapt to user's request
3. **Commercial Height Constraints**: Commercial CANNOT be mid-rise (FloorType ≠ 2), use low-rise (1) or high-rise (3)
4. **Residential Constraints**: Residential CANNOT be super high-rise (FloorType ≠ 4)
5. **Strategic Distribution**:
   - Plots 18 & 21: High-rise commercial or mixed-use landmarks
   - Plot 9: Flexible anchor - public, cultural, or commercial
   - Plots 22, 23, 24: Small support plots - flexible functions
6. **Urban Vitality**: Create active pedestrian environment with diverse uses
7. **Energy Range**: Commercial 50-75; residential 30-50; public/cultural 35-60

**Key Considerations**:
- Maximize prime central location
- Create 15-minute living circle
- Ensure architectural quality and landmark presence
"
            },
            new RegionConfig
            {
                RegionID = 5,
                LandRange = "25-28,31-32",
                PlotIDs = new int[] { 25, 26, 27, 28, 31, 32 },
                LocationDescription = "East-central transition area (X: 24 to 436, Z: -314 to 186)",
                FunctionalRole = "Mixed Commercial, Residential, and Public Facilities",
                PlanningGuidelines = @"
**Regional Context**: East-central transition zone connecting city center to eastern districts. Spans from south-central to north of center.

**Plot Characteristics**:
- Plots 31 & 32: Large plots (272x72m) - major development corridors
- Plots 25 & 26: Large plots (212x72m) south-central area
- Plots 27 & 28: Medium plots (186x72m) near city center

**Planning Strategy**:
1. **Balanced Mixed-Use**: Distribute residential, commercial, and public functions evenly
2. **Development Corridors**: Plots 31-32 major commercial/residential corridors
3. **Southern Area**: Plots 25-26 mixed commercial-residential
4. **Central Connection**: Plots 27-28 (near center) mixed commercial-residential
5. **Height Diversity**: Varied heights (FloorType=1-3) for dynamic skyline
6. **Constraints Compliance**:
   - Residential: No super high-rise (FloorType ≠ 4)
   - Commercial: No mid-rise (FloorType ≠ 2)
   - Public: No low-rise (FloorType ≠ 1)
7. **Energy Range**: Commercial 50-70; residential 30-50; public 35-55

**Key Considerations**:
- Create smooth transition from center to eastern districts
- Balance commercial vitality with residential quality
- Ensure pedestrian connectivity
"
            },
            new RegionConfig
            {
                RegionID = 6,
                LandRange = "29,33-35",
                PlotIDs = new int[] { 29, 33, 34, 35 },
                LocationDescription = "Northeast cultural district (X: 64 to 436, Z: 186 to 486)",
                FunctionalRole = "Cultural and Community Hub with 172x172 Cultural Landmark (Plot 29)",
                PlanningGuidelines = @"
**Regional Context**: Northeast cultural district, eastern counterpart to Region 2's cultural hub. Contains Plot 29 (172x172 super-large cultural landmark). Smallest region with only 4 plots but high cultural significance.

**Plot Characteristics**:
- Plot 29: **SUPER-LARGE** 172x172m plot - MANDATORY cultural landmark (Function=4, FloorType=3, Material=1)
- Plot 35: Large plot (272x72m) on far northern edge
- Plots 33 & 34: Small square plots (72x72m) supporting cultural district

**Planning Strategy**:
1. **Cultural Anchor**: Plot 29 MUST be high-rise glass curtain wall cultural facility (museum, theater, cultural center)
2. **Northern Gateway**: Plot 35 (far north) ideal for complementary public facility or educational institution
3. **Supporting Services**: Plots 33-34 flexible - cultural shops, cafes, small public facilities
4. **Architectural Harmony**: Create visual coherence around cultural landmark
5. **Height Strategy**: High-rise landmark (Plot 29 FloorType=3), mid-to-high-rise support (FloorType=2-3)
6. **Energy Range**: Cultural landmark 60-85; support facilities 30-55

**Mandatory Constraint**:
⚠️ Plot 29 (172x172): MUST be Function=4, FloorType=3, Material=1

**Key Considerations**:
- Create distinctive cultural district identity
- Ensure pedestrian-friendly cultural plaza
- Balance iconic landmark with human-scale supporting buildings
"
            },
            new RegionConfig
            {
                RegionID = 7,
                LandRange = "30,36-39",
                PlotIDs = new int[] { 30, 36, 37, 38, 39 },
                LocationDescription = "Eastern residential and community zone (X: 300 to 536, Z: -486 to 86)",
                FunctionalRole = "Eastern Residential District with Community Services",
                PlanningGuidelines = @"
**Regional Context**: Eastern residential zone spanning from far south to central area. Serves as eastern residential gateway.

**Plot Characteristics**:
- Plot 30: Large plot (272x72m) on far southern edge
- Plots 36 & 37: Large plots (212x72m) on southern-central edge
- Plots 38 & 39: Standard plots (172x72m) mid-central area

**Planning Strategy**:
1. **Residential Focus**: Primarily residential (Function=1), avoid super high-rise (FloorType ≠ 4)
2. **Community Services**: Include public facilities (schools, healthcare, parks) and local commercial
3. **Southern Gateway**: Plot 30 can be residential or community facility anchor
4. **North-South Balance**: Distribute functions evenly from south to central
5. **Height Gradient**: Low-to-mid-rise (FloorType=1-2), maintain livable human scale
6. **Materials**: Prefer concrete (Material=2) for residential warmth; glass for public/commercial
7. **Energy Efficiency**: Residential 20-45; public facilities 30-50; local commercial 40-60

**Key Considerations**:
- Create self-sufficient neighborhood with local services
- Ensure walkability and adequate green space
- Balance density with quality of life
"
            },
            new RegionConfig
            {
                RegionID = 8,
                LandRange = "40-43",
                PlotIDs = new int[] { 40, 41, 42, 43 },
                LocationDescription = "Far eastern gateway corridor (X: 364 to 636, Z: -286 to 386)",
                FunctionalRole = "Eastern Gateway with Signature Buildings",
                PlanningGuidelines = @"
**Regional Context**: Far eastern gateway corridor at city boundary. Spans from central to northern edge, creating eastern entrance experience.

**Plot Characteristics**:
- Plot 40: Standard plot (172x72m) in northern-central area
- Plot 41: Standard plot (172x72m) in far northern area
- Plot 42: Large plot (272x72m) on southern side of far eastern edge
- Plot 43: Large plot (272x72m) on northern side of far eastern edge

**Planning Strategy**:
1. **Gateway Identity**: Create strong, memorable visual identity as eastern entrance
2. **Northern Extension**: Plots 40-41 extend gateway experience northward
3. **Landmark Buildings**: Plots 42-43 (far east) should be signature buildings, not ordinary residential
4. **Functional Options**:
   - Commercial (Function=2): Office towers, hotels (FloorType=3, NOT 2)
   - Public (Function=3): Convention center, administrative buildings (FloorType≥2, NOT 1)
   - Residential (Function=1): Premium residential for plots 40-41 (no super high-rise, FloorType ≠ 4)
5. **Architectural Quality**: Prioritize modern, iconic design especially for plots 42-43
6. **Materials**: Glass curtain wall (Material=1) preferred for contemporary gateway image
7. **Height**: Mid-to-high-rise (FloorType=2-3) for landmark presence and visibility
8. **Energy Range**: 40-80 depending on function (larger modern buildings 55-80; residential 30-50)

**Key Considerations**:
- Create memorable first impression for eastbound arrivals
- Ensure architectural significance and quality
- Balance aesthetics with functional needs
- Coordinate designs for north-south visual harmony
"
            }
        };

        private void Start()
        {
            LoadConfig();
            LoadPromptFromFile();
            StartCoroutine(ExecuteTestJsonProcessing());
        }

        /// <summary>
        /// Loads API configuration from config.json file
        /// </summary>
        private void LoadConfig()
        {
            // 尝试从项目根目录读取config.json
            string configPath = Path.Combine(Application.dataPath, "..", "config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string configJson = File.ReadAllText(configPath);
                    Config config = JsonConvert.DeserializeObject<Config>(configJson);

                    if (config != null)
                    {
                        apiKey = config.deepseek_api_key;
                        if (!string.IsNullOrEmpty(config.deepseek_api_url))
                        {
                            apiUrl = config.deepseek_api_url;
                        }
                        Debug.Log("[DeepSeekAPI] Successfully loaded API configuration from config.json");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DeepSeekAPI] Failed to parse config.json: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[DeepSeekAPI] config.json not found at: {configPath}\n" +
                    "Please copy config.example.json to config.json and fill in your DeepSeek API key.\n" +
                    "See README.md for setup instructions.");
            }

            // 验证API Key是否已配置
            if (string.IsNullOrEmpty(apiKey) || apiKey == "your-deepseek-api-key-here")
            {
                Debug.LogError("[DeepSeekAPI] API Key not configured! Please edit config.json with your valid DeepSeek API key.");
            }
        }

        /// <summary>
        /// Loads the system prompt from a text file in the StreamingAssets folder
        /// </summary>
        private void LoadPromptFromFile()
        {
            string promptPath = Path.Combine(Application.streamingAssetsPath, promptFileName);

            if (File.Exists(promptPath))
            {
                try
                {
                    npcCharacter.personalityPrompt = File.ReadAllText(promptPath);
                    Debug.Log($"[DeepSeekAPI] Successfully loaded prompt from: {promptPath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DeepSeekAPI] Failed to load prompt file: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[DeepSeekAPI] Prompt file not found at: {promptPath}. Using default prompt.");
            }
        }

        private void Update()
        {
            // 🚀 优化后架构：无总控LLM，直接处理8个执行LLM的输出

            // 处理第一个流的数据（地块1-5）
            if (_landDataQueue1 != null && _landDataQueue1.TryDequeue(out EmptyLandData landData1))
            {
                LogFirstDataIfNeeded(1);
                Debug.Log($"[DeepSeekAPI-Update] [流1] 处理地块 {landData1.EmptyID}");
                ProcessSingleLandPlot(landData1);
            }

            // 处理第二个流的数据（地块6-10）
            if (_landDataQueue2 != null && _landDataQueue2.TryDequeue(out EmptyLandData landData2))
            {
                LogFirstDataIfNeeded(2);
                Debug.Log($"[DeepSeekAPI-Update] [流2] 处理地块 {landData2.EmptyID}");
                ProcessSingleLandPlot(landData2);
            }

            // 处理第三个流的数据（地块11-16）
            if (_landDataQueue3 != null && _landDataQueue3.TryDequeue(out EmptyLandData landData3))
            {
                LogFirstDataIfNeeded(3);
                Debug.Log($"[DeepSeekAPI-Update] [流3] 处理地块 {landData3.EmptyID}");
                ProcessSingleLandPlot(landData3);
            }

            // 处理第四个流的数据（地块17-22）
            if (_landDataQueue4 != null && _landDataQueue4.TryDequeue(out EmptyLandData landData4))
            {
                LogFirstDataIfNeeded(4);
                Debug.Log($"[DeepSeekAPI-Update] [流4] 处理地块 {landData4.EmptyID}");
                ProcessSingleLandPlot(landData4);
            }

            // 处理第五个流的数据（地块23-27）
            if (_landDataQueue5 != null && _landDataQueue5.TryDequeue(out EmptyLandData landData5))
            {
                LogFirstDataIfNeeded(5);
                Debug.Log($"[DeepSeekAPI-Update] [流5] 处理地块 {landData5.EmptyID}");
                ProcessSingleLandPlot(landData5);
            }

            // 处理第六个流的数据（地块28-32）
            if (_landDataQueue6 != null && _landDataQueue6.TryDequeue(out EmptyLandData landData6))
            {
                LogFirstDataIfNeeded(6);
                Debug.Log($"[DeepSeekAPI-Update] [流6] 处理地块 {landData6.EmptyID}");
                ProcessSingleLandPlot(landData6);
            }

            // 处理第七个流的数据（地块33-38）
            if (_landDataQueue7 != null && _landDataQueue7.TryDequeue(out EmptyLandData landData7))
            {
                LogFirstDataIfNeeded(7);
                Debug.Log($"[DeepSeekAPI-Update] [流7] 处理地块 {landData7.EmptyID}");
                ProcessSingleLandPlot(landData7);
            }

            // 处理第八个流的数据（地块39-43）
            if (_landDataQueue8 != null && _landDataQueue8.TryDequeue(out EmptyLandData landData8))
            {
                LogFirstDataIfNeeded(8);
                Debug.Log($"[DeepSeekAPI-Update] [流8] 处理地块 {landData8.EmptyID}");
                ProcessSingleLandPlot(landData8);
            }

            // 当八个流都完成时，初始化颜色切换器
            if (_isStreamingComplete1 && _isStreamingComplete2 && _isStreamingComplete3 &&
                _isStreamingComplete4 && _isStreamingComplete5 && _isStreamingComplete6 &&
                _isStreamingComplete7 && _isStreamingComplete8)
            {
                // ⏱️ 记录整体完成时间并输出性能统计摘要
                float overallEndTime = Time.realtimeSinceStartup;
                float totalDuration = overallEndTime - _overallStartTime;
                _perfLogger.LogSeparator();
                _perfLogger.Log($"⏱️ ========== 整体流程结束 at {overallEndTime:F2}s (总耗时 {totalDuration:F2}s) ==========");
                _perfLogger.LogSeparator();
                LogPerformanceSummary();

                Debug.Log($"[DeepSeekAPI-Update] 八个流都已完成，初始化颜色切换器。性能日志已保存到文件。");
                InitializeAllColorTogglers();

                // ✅ 在所有LLM完成时调用callback，通知输入框解锁
                _currentCallback?.Invoke($"All 8 LLMs completed successfully in {totalDuration:F2}s.", true);

                _isStreamingComplete1 = false;
                _isStreamingComplete2 = false;
                _isStreamingComplete3 = false;
                _isStreamingComplete4 = false;
                _isStreamingComplete5 = false;
                _isStreamingComplete6 = false;
                _isStreamingComplete7 = false;
                _isStreamingComplete8 = false;
            }
        }

        /// <summary>
        /// ⏱️ 辅助方法：记录执行LLM首次输出数据时间
        /// </summary>
        private void LogFirstDataIfNeeded(int streamId)
        {
            if (!_executorFirstDataTimes.ContainsKey(streamId) && _executorStartTimes.ContainsKey(streamId))
            {
                float firstDataTime = Time.realtimeSinceStartup;
                _executorFirstDataTimes[streamId] = firstDataTime;
                float timeSinceLaunch = firstDataTime - _executorStartTimes[streamId];
                float elapsedFromStart = firstDataTime - _overallStartTime;
                _perfLogger.Log($"⏱️ 执行LLM-{streamId} 首次输出数据 at {firstDataTime:F2}s (距启动 +{timeSinceLaunch:F2}s, 距流程开始 +{elapsedFromStart:F2}s)");
            }
        }

        /// <summary>
        /// ⏱️ 输出性能统计摘要（优化后的无总控架构）
        /// </summary>
        private void LogPerformanceSummary()
        {
            _perfLogger.LogEmptyLine();
            _perfLogger.LogSeparator();
            _perfLogger.LogRaw("==================== ⏱️ PERFORMANCE SUMMARY ====================");
            _perfLogger.LogSeparator();

            _perfLogger.LogEmptyLine();
            _perfLogger.LogRaw("架构: 无总控LLM，8个执行LLM直接并行");
            _perfLogger.LogEmptyLine();

            // 执行LLM统计
            _perfLogger.LogRaw("[执行LLM统计] (8个并行流):");
            float maxExecutorDuration = 0f;
            float minExecutorDuration = float.MaxValue;
            int slowestExecutor = -1;
            int fastestExecutor = -1;
            float totalExecutorTime = 0f;
            float maxLaunchDelay = 0f;
            float minLaunchDelay = float.MaxValue;

            for (int i = 1; i <= 8; i++)
            {
                if (_executorStartTimes.ContainsKey(i) && _executorEndTimes.ContainsKey(i))
                {
                    float startTime = _executorStartTimes[i];
                    float endTime = _executorEndTimes[i];
                    float duration = endTime - startTime;
                    float launchDelay = startTime - _overallStartTime;
                    float firstDataDelay = _executorFirstDataTimes.ContainsKey(i)
                        ? _executorFirstDataTimes[i] - startTime
                        : 0f;

                    _perfLogger.LogRaw($"  LLM-{i}: 耗时 {duration:F2}s | 首次数据延迟 {firstDataDelay:F2}s | 启动延迟 {launchDelay:F2}s");

                    totalExecutorTime += duration;
                    maxLaunchDelay = Mathf.Max(maxLaunchDelay, launchDelay);
                    minLaunchDelay = Mathf.Min(minLaunchDelay, launchDelay);

                    if (duration > maxExecutorDuration)
                    {
                        maxExecutorDuration = duration;
                        slowestExecutor = i;
                    }
                    if (duration < minExecutorDuration)
                    {
                        minExecutorDuration = duration;
                        fastestExecutor = i;
                    }
                }
            }

            float avgExecutorDuration = totalExecutorTime / 8f;
            float actualTotal = Time.realtimeSinceStartup - _overallStartTime;

            // 瓶颈分析
            _perfLogger.LogEmptyLine();
            _perfLogger.LogRaw("[关键指标]");
            _perfLogger.LogRaw($"  最慢的执行LLM: LLM-{slowestExecutor} (耗时 {maxExecutorDuration:F2}s)");
            _perfLogger.LogRaw($"  最快的执行LLM: LLM-{fastestExecutor} (耗时 {minExecutorDuration:F2}s)");
            _perfLogger.LogRaw($"  平均执行LLM耗时: {avgExecutorDuration:F2}s");
            _perfLogger.LogRaw($"  启动跨度: {maxLaunchDelay - minLaunchDelay:F2}s (理想应<0.5s)");
            _perfLogger.LogRaw($"  理论最优耗时: {maxExecutorDuration:F2}s (=最慢LLM耗时，假设0启动延迟)");
            _perfLogger.LogRaw($"  实际总耗时: {actualTotal:F2}s");

            float parallelEfficiency = (maxExecutorDuration / actualTotal) * 100f;
            _perfLogger.LogRaw($"  并行效率: {parallelEfficiency:F1}%");

            // 优化建议
            _perfLogger.LogEmptyLine();
            _perfLogger.LogRaw("[优化建议]");

            if (parallelEfficiency < 85f)
            {
                _perfLogger.LogRaw("  ⚠️  并行效率偏低，可能原因：");
                if (maxLaunchDelay - minLaunchDelay > 1.0f)
                {
                    _perfLogger.LogRaw($"     - 启动跨度过大 ({maxLaunchDelay - minLaunchDelay:F2}s)，应接近0秒");
                }
                _perfLogger.LogRaw("     - 可能存在API限流，尝试错开启动时间");
            }
            else if (parallelEfficiency >= 95f)
            {
                _perfLogger.LogRaw("  ✅  并行效率优秀！架构接近理论最优");
            }

            if (maxExecutorDuration > avgExecutorDuration * 1.5f)
            {
                _perfLogger.LogRaw($"  ⚠️  LLM-{slowestExecutor}明显慢于平均水平，检查该区域负责的地块数量或复杂度");
                _perfLogger.LogRaw($"     提示：LLM-{slowestExecutor}负责 {FIXED_REGIONS[slowestExecutor - 1].PlotIDs.Length} 个地块");
            }

            // 与旧架构对比
            _perfLogger.LogEmptyLine();
            _perfLogger.LogRaw("[架构对比]");
            _perfLogger.LogRaw($"  旧架构（有总控）预估耗时: ~50s");
            _perfLogger.LogRaw($"  新架构（无总控）实际耗时: {actualTotal:F2}s");
            float improvement = ((50f - actualTotal) / 50f) * 100f;
            if (improvement > 0)
            {
                _perfLogger.LogRaw($"  性能提升: {improvement:F1}% ⬇️");
            }
            else
            {
                _perfLogger.LogRaw($"  性能下降: {-improvement:F1}% ⬆️ (异常，请检查)");
            }

            _perfLogger.LogSeparator();
            _perfLogger.LogRaw("===============================================================");
            _perfLogger.LogSeparator();
        }

        private IEnumerator ExecuteTestJsonProcessing()
        {
            yield return new WaitUntil(() => buildingSpawnerJson != null && buildingSpawnerJson.IsCsvDataLoaded);
            ProcessTestJsonData();
        }

        void ProcessTestJsonData()
        {
            if (!string.IsNullOrEmpty(TestJsonInput))
            {
                Debug.Log("[DeepSeekAPI] Using Test JSON Input for city planning.");
                List<EmptyLandData> landDataList = null;
                try
                {
                    landDataList = JsonConvert.DeserializeObject<List<EmptyLandData>>(TestJsonInput);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DeepSeekAPI] Error deserializing TestJsonInput: {e.Message}");
                    landDataList = null;
                }

                if (landDataList != null)
                {
                    ProcessLandDataList(landDataList);
                }
            }
        }

        public void SendMessageToDeepSeek(string userMessage, DialogueCallback callback)
        {
            // ⏱️ 记录整体流程开始时间
            _overallStartTime = Time.realtimeSinceStartup;
            _perfLogger.LogSeparator();
            _perfLogger.Log($"⏱️ ========== 整体流程开始 at {_overallStartTime:F2}s ==========");
            _perfLogger.Log($"用户消息: {userMessage}");
            _perfLogger.Log($"架构: 无总控LLM，直接并行启动8个执行LLM");
            _perfLogger.LogSeparator();
            Debug.Log("[DeepSeekAPI] 🚀 优化架构：直接并行启动8个执行LLM（无总控等待）");

            buildingSpawnerJson.RemoveAllBuildings();

            // 清空计时字典
            _executorStartTimes.Clear();
            _executorEndTimes.Clear();
            _executorFirstDataTimes.Clear();

            // 初始化八个队列
            _landDataQueue1 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue2 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue3 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue4 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue5 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue6 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue7 = new ConcurrentQueue<EmptyLandData>();
            _landDataQueue8 = new ConcurrentQueue<EmptyLandData>();
            _isStreamingComplete1 = false;
            _isStreamingComplete2 = false;
            _isStreamingComplete3 = false;
            _isStreamingComplete4 = false;
            _isStreamingComplete5 = false;
            _isStreamingComplete6 = false;
            _isStreamingComplete7 = false;
            _isStreamingComplete8 = false;

            _currentUserMessage = userMessage;
            _currentCallback = callback;

            // 🚀 直接批量启动8个执行LLM（无需等待总控）
            StartCoroutine(LaunchAllExecutorsSimultaneously(userMessage, callback));
        }

        /// <summary>
        /// 🚀 同时启动所有8个执行LLM
        /// </summary>
        IEnumerator LaunchAllExecutorsSimultaneously(string userMessage, DialogueCallback callback)
        {
            _perfLogger.Log($"⏱️ 开始批量启动8个执行LLM...");

            // 遍历8个固定区域，几乎同时启动所有执行LLM
            foreach (var region in FIXED_REGIONS)
            {
                LaunchExecutorForFixedRegion(region, userMessage, callback);
                // 添加极小延迟以避免Unity协程调度问题（可选）
                yield return null;
            }

            _perfLogger.Log($"⏱️ 所有8个执行LLM已启动");
            // ❌ 移除错误的callback调用 - 不应该在启动时就调用callback
            // callback?.Invoke("All 8 executor LLMs started simultaneously.", true);
        }

        /// <summary>
        /// 🚀 根据固定区域配置启动对应的执行LLM
        /// </summary>
        void LaunchExecutorForFixedRegion(RegionConfig region, string userMessage, DialogueCallback callback)
        {
            // ⏱️ 记录执行LLM启动时间
            float launchTime = Time.realtimeSinceStartup;
            _executorStartTimes[region.RegionID] = launchTime;
            float elapsedFromStart = launchTime - _overallStartTime;
            _perfLogger.Log($"⏱️ 执行LLM-{region.RegionID} 启动 at {launchTime:F2}s (距流程开始 +{elapsedFromStart:F2}s) [地块 {region.LandRange}] - {region.FunctionalRole}");
            Debug.Log($"[DeepSeekAPI-Executor] 区域{region.RegionID} [{region.FunctionalRole}] 启动执行LLM");

            // 根据区域ID选择对应的队列和完成标志
            ConcurrentQueue<EmptyLandData> queue = null;
            System.Action onComplete = null;

            switch (region.RegionID)
            {
                case 1:
                    queue = _landDataQueue1;
                    onComplete = () => _isStreamingComplete1 = true;
                    break;
                case 2:
                    queue = _landDataQueue2;
                    onComplete = () => _isStreamingComplete2 = true;
                    break;
                case 3:
                    queue = _landDataQueue3;
                    onComplete = () => _isStreamingComplete3 = true;
                    break;
                case 4:
                    queue = _landDataQueue4;
                    onComplete = () => _isStreamingComplete4 = true;
                    break;
                case 5:
                    queue = _landDataQueue5;
                    onComplete = () => _isStreamingComplete5 = true;
                    break;
                case 6:
                    queue = _landDataQueue6;
                    onComplete = () => _isStreamingComplete6 = true;
                    break;
                case 7:
                    queue = _landDataQueue7;
                    onComplete = () => _isStreamingComplete7 = true;
                    break;
                case 8:
                    queue = _landDataQueue8;
                    onComplete = () => _isStreamingComplete8 = true;
                    break;
            }

            if (queue != null)
            {
                // 构建执行LLM的用户消息，包含区域规划指导
                string executorUserMessage = BuildExecutorUserMessageForRegion(userMessage, region);

                // 启动执行LLM
                StartCoroutine(PostRequestParallel(
                    executorUserMessage,
                    callback,
                    npcCharacter.personalityPrompt,
                    region.RegionID,
                    queue,
                    onComplete
                ));
            }
        }

        // 并行版本的PostRequest，支持指定队列和完成回调
        IEnumerator PostRequestParallel(string userMessage, DialogueCallback callback, string systemPrompt,
            int streamId, ConcurrentQueue<EmptyLandData> queue, System.Action onComplete)
        {
            Debug.Log($"[DeepSeekAPI] [流{streamId}] PostRequest协程开始");

            List<Message> messages = new List<Message>
            {
                new Message { role = "system", content = systemPrompt },
                new Message { role = "user", content = userMessage }
            };

            ChatRequest requestBodyV3 = new ChatRequest
            {
                model = "deepseek-chat",
                messages = messages,
                temperature = temperature,
                max_tokens = 6000,
                stream = true
            };

            string jsonBody = JsonConvert.SerializeObject(requestBodyV3, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Debug.Log($"[DeepSeekAPI] [流{streamId}] 发送请求到DeepSeek API...");

            UnityWebRequest request = CreateWebRequest(jsonBody);

            request.downloadHandler = new StreamingDownloadHandler(queue, () =>
            {
                // ⏱️ 记录执行LLM结束时间
                float endTime = Time.realtimeSinceStartup;
                _executorEndTimes[streamId] = endTime;
                float duration = endTime - _executorStartTimes[streamId];
                float elapsedFromStart = endTime - _overallStartTime;
                _perfLogger.Log($"⏱️ 执行LLM-{streamId} 完成 at {endTime:F2}s (耗时 {duration:F2}s, 距流程开始 +{elapsedFromStart:F2}s)");
                Debug.Log($"[DeepSeekAPI-Callback] [流{streamId}] 流数据接收完成");
                onComplete?.Invoke();
            });

            yield return request.SendWebRequest();

            Debug.Log($"[DeepSeekAPI] [流{streamId}] Web请求完成。Result: {request.result}");

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                if (request.responseCode == 429)
                {
                    Debug.LogWarning($"[DeepSeekAPI] [流{streamId}] 触发限流，5秒后重试...");
                    yield return new WaitForSeconds(5);
                    StartCoroutine(PostRequestParallel(userMessage, callback, systemPrompt, streamId, queue, onComplete));
                }
                else
                {
                    Debug.LogError($"[DeepSeekAPI] [流{streamId}] API错误: {request.responseCode} - {request.error}");
                    // ❌ 移除错误时的callback调用 - 应该只在所有LLM完成时统一调用
                    // callback?.Invoke($"Stream {streamId} failed: {request.downloadHandler.text}", false);
                }
            }
            else
            {
                Debug.Log($"[DeepSeekAPI] [流{streamId}] 连接成功，等待数据...");
                // ❌ 移除连接成功时的callback调用 - 应该只在所有LLM完成时统一调用
                // callback?.Invoke($"Stream {streamId} started.", true);
            }
            request.Dispose();
        }

        // 原有的单流PostRequest方法已废弃，现在统一使用并行版本PostRequestParallel

        private void ProcessLandDataList(List<EmptyLandData> landDataList)
        {
            if (buildingSpawnerJson == null)
            {
                Debug.LogError("[DeepSeekAPI] BuildingSpawnerJson reference is not set. Cannot process land data.");
                return;
            }
            buildingSpawnerJson.RemoveAllBuildings();

            if (landDataList == null || landDataList.Count == 0)
            {
                Debug.LogWarning("[DeepSeekAPI] ProcessLandDataList received a null or empty list. No buildings will be spawned.");
                return;
            }

            foreach (var landInfo in landDataList)
            {
                ProcessSingleLandPlot(landInfo);
            }

            InitializeAllColorTogglers();
        }

        private void ProcessSingleLandPlot(EmptyLandData landInfo)
        {
            if (landInfo == null)
            {
                Debug.LogWarning("[DeepSeekAPI] Encountered a null land entry. Skipping.");
                return;
            }

            // ADDED: Log to confirm we're about to spawn a specific building.
            Debug.Log($"[DeepSeekAPI] Processing single plot ID: {landInfo.EmptyID}. Handing off to BuildingSpawnerJson.");

            if (buildingSpawnerJson == null || !buildingSpawnerJson.IsCsvDataLoaded)
            {
                Debug.LogWarning("[DeepSeekAPI] BuildingSpawnerJson not ready, cannot process plot " + landInfo.EmptyID);
                return;
            }

            buildingSpawnerJson.StoreLandProperties(
                landInfo.EmptyID, landInfo.Function, landInfo.FloorType, landInfo.Material, landInfo.Summary
            );

            buildingSpawnerJson.SpawnBuilding(
                landInfo.EmptyID, landInfo.Function, landInfo.FloorType, landInfo.Material
            );

            if (int.TryParse(landInfo.EmptyID, out int landIdNumeric))
            {
                int togglerIndex = landIdNumeric - 1;
                if (colorTogglerArray != null && togglerIndex >= 0 && togglerIndex < colorTogglerArray.Length)
                {
                    ChildColorToggler currentToggler = colorTogglerArray[togglerIndex];
                    if (currentToggler != null)
                    {
                        if (int.TryParse(landInfo.Function, out int functionNumeric))
                            currentToggler.landFunction = functionNumeric;
                        else
                            currentToggler.landFunction = 0;

                        // 能耗数据处理：如果LLM没有返回，根据建筑参数计算默认值
                        if (int.TryParse(landInfo.EnergyConsumption, out int energyNumeric) && energyNumeric >= 1 && energyNumeric <= 100)
                        {
                            currentToggler.landEnergyConsumption = energyNumeric;
                        }
                        else
                        {
                            // LLM遗漏了能耗数据，计算默认值
                            int defaultEnergy = CalculateDefaultEnergyConsumption(landInfo);
                            currentToggler.landEnergyConsumption = defaultEnergy;

                            // 记录警告和默认值到日志
                            _perfLogger.Log($"⚠️ 地块 {landInfo.EmptyID} 缺失能耗数据，使用默认值: {defaultEnergy}");
                            Debug.LogWarning($"[DeepSeekAPI] 地块 {landInfo.EmptyID} 的EnergyConsumption字段缺失或无效 ('{landInfo.EnergyConsumption}')，使用默认值 {defaultEnergy}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 当LLM遗漏能耗数据时，根据建筑参数计算合理的默认值
        /// </summary>
        private int CalculateDefaultEnergyConsumption(EmptyLandData landInfo)
        {
            int baseEnergy = 50; // 基础值

            // 根据功能类型调整
            if (int.TryParse(landInfo.Function, out int function))
            {
                switch (function)
                {
                    case 1: // 住宅
                        baseEnergy = 30;
                        break;
                    case 2: // 商业
                        baseEnergy = 60;
                        break;
                    case 3: // 公共
                        baseEnergy = 45;
                        break;
                    case 4: // 文化娱乐
                        baseEnergy = 70;
                        break;
                }
            }

            // 根据楼层类型调整（楼层越高，能耗越大）
            if (int.TryParse(landInfo.FloorType, out int floorType))
            {
                switch (floorType)
                {
                    case 1: // 低层
                        baseEnergy -= 5;
                        break;
                    case 2: // 中层
                        // 不变
                        break;
                    case 3: // 高层
                        baseEnergy += 10;
                        break;
                    case 4: // 超高层
                        baseEnergy += 20;
                        break;
                }
            }

            // 根据材质调整（玻璃幕墙能耗更高）
            if (int.TryParse(landInfo.Material, out int material))
            {
                if (material == 1) // 玻璃幕墙
                {
                    baseEnergy += 15;
                }
                else // 混凝土
                {
                    baseEnergy -= 5;
                }
            }

            // 确保在1-100范围内
            return Mathf.Clamp(baseEnergy, 1, 100);
        }

        private void InitializeAllColorTogglers()
        {
            if (colorTogglerArray == null) return;

            for (int i = 0; i < colorTogglerArray.Length; i++)
            {
                if (colorTogglerArray[i] != null)
                {
                    colorTogglerArray[i].PublicInitializeRenderers();
                }
            }
        }

        private UnityWebRequest CreateWebRequest(string jsonBody)
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            return request;
        }

        /// <summary>
        /// 🏙️ 构建执行LLM的用户消息，包含详细的区域规划指导
        /// </summary>
        private string BuildExecutorUserMessageForRegion(string originalUserMessage, RegionConfig region)
        {
            return $@"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎯 PRIMARY GOAL: USER'S PLANNING REQUEST
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**User Request**: ""{originalUserMessage}""

**YOUR MISSION**: Design this region to BEST FULFILL the user's vision above. The user's request is your PRIMARY constraint and goal.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🏙️ YOUR ASSIGNED REGION - CONTEXT INFORMATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Region ID**: {region.RegionID}
**Plots Under Your Responsibility**: {region.LandRange} (Plots: {string.Join(", ", region.PlotIDs)})
**Location**: {region.LocationDescription}
**Suggested Functional Role**: {region.FunctionalRole}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📋 RECOMMENDED PLANNING STRATEGIES (Adapt to User's Request!)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{region.PlanningGuidelines}

**IMPORTANT**: The above guidelines are SUGGESTIONS to help you understand this region's characteristics.
If the user's request conflicts with these suggestions, PRIORITIZE THE USER'S REQUEST.

For example:
- If user wants ""industrial city"" but guidelines suggest residential → Design industrial/commercial instead
- If user wants ""low-rise eco-friendly"" but guidelines suggest high-rise → Use low-rise with green materials
- If user wants ""futuristic glass city"" → Use glass materials (Material=1) even if guidelines suggest concrete

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️ CRITICAL INSTRUCTIONS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. **PRIORITY ORDER**:
   a) FIRST: User's planning request ""{originalUserMessage}""
   b) SECOND: Mandatory technical constraints (see below)
   c) THIRD: Regional planning suggestions (adapt as needed)

2. **MANDATORY TECHNICAL CONSTRAINTS** (Cannot be violated):
   - Residential (Function=1): CANNOT be super high-rise (FloorType ≠ 4)
   - Commercial (Function=2): CANNOT be mid-rise (FloorType ≠ 2)
   - Public (Function=3): CANNOT be low-rise (FloorType ≠ 1)
   - 172x172 plots: MUST be high-rise glass curtain wall cultural facilities (Function=4, FloorType=3, Material=1)

3. **SCOPE**: Process ONLY plots in range {region.LandRange} (IDs: {string.Join(", ", region.PlotIDs)})

4. **OUTPUT**: Generate exactly {region.PlotIDs.Length} JSON objects (one per plot)

5. **SUMMARY QUALITY**: Each plot's Summary must explain:
   - **How it fulfills the user's specific request**: ""{originalUserMessage}""
   - How it fits the regional context (location, adjacency)
   - Why you chose these specific parameters (Function, FloorType, Material, EnergyConsumption)

**Output ONLY the JSON array. No other text before or after.**";
        }

        [System.Serializable]
        public class EmptyLandData
        {
            public string EmptyID;
            public string Function;
            public string FloorType;
            public string Material;
            public string EnergyConsumption;
            public string Summary;
        }

        public delegate void DialogueCallback(string content, bool isSuccess);

        [System.Serializable]
        private class ResponseFormat
        {
            public string type;
        }

        [System.Serializable]
        private class ChatRequest
        {
            public string model;
            public List<Message> messages;
            public float temperature;
            public int max_tokens;
            public ResponseFormat response_format;
            public bool stream;
        }

        [System.Serializable]
        public class Message
        {
            public string role;
            public string content;
        }

        public void ApplyJsonLayout(string jsonString)
        {
            if (buildingSpawnerJson == null || !buildingSpawnerJson.IsCsvDataLoaded)
            {
                Debug.LogWarning("[DeepSeekAPI] BuildingSpawnerJson not ready. Layout not applied.");
                return;
            }

            if (string.IsNullOrEmpty(jsonString))
            {
                Debug.LogWarning("[DeepSeekAPI] ApplyJsonLayout received an empty JSON string.");
                return;
            }

            Debug.Log("[DeepSeekAPI] Applying JSON layout from provided string.");
            try
            {
                var landDataList = JsonConvert.DeserializeObject<List<EmptyLandData>>(jsonString);
                if (landDataList != null)
                {
                    ProcessLandDataList(landDataList);
                    Debug.Log("[DeepSeekAPI] JSON layout applied successfully.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeekAPI] Error deserializing provided JSON: {e.Message}");
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(DeepSeekAPI))]
    public class DeepSeekApiEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DeepSeekAPI deepSeekApiScript = (DeepSeekAPI)target;
            if (GUILayout.Button("Clear TestJsonInput"))
            {
                deepSeekApiScript.TestJsonInput = "";
                UnityEditor.EditorUtility.SetDirty(deepSeekApiScript);
            }
        }
    }
#endif
}