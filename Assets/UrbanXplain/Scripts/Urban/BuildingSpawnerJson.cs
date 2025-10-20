// BuildingSpawnerJson.cs (Modified for Streaming Robustness)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using System;

namespace UrbanXplain
{
    public class BuildingSpawnerJson : MonoBehaviour
    {
        [Header("CSV File Names (in StreamingAssets)")]
        public string buildingPrefabCsvFileName = "buildingprefab.csv";
        public string emptyLandCsvFileName = "emptyland.csv";

        [Header("Building Prefabs")]
        [SerializeField] public GameObject[] buildings;

        [Header("Empty Lands")]
        public GameObject emptyLandsParent;
        [SerializeField] public GameObject[] landArray;

        private float minBuildingDistance = 10f;

        // CHANGE: Changed Dictionary value from struct to class.
        private Dictionary<int, BuildingPrefabData> buildingPrefabCache = new Dictionary<int, BuildingPrefabData>();
        private Dictionary<int, EmptyLandData> emptyLandCache = new Dictionary<int, EmptyLandData>();

        private static HashSet<int> usedSpecialBuildings = new HashSet<int>();
        private static HashSet<int> usedCulturalBuildings = new HashSet<int>();
        public bool IsCsvDataLoaded { get; private set; } = false;

        void Start()
        {
            StartCoroutine(LoadDataFromCsv());
        }

        public IEnumerator LoadDataFromCsv()
        {
            buildingPrefabCache.Clear();
            emptyLandCache.Clear();

            yield return StartCoroutine(LoadBuildingPrefabsFromCsv());
            yield return StartCoroutine(LoadEmptyLandsFromCsv());

            if (buildingPrefabCache.Count > 0 && emptyLandCache.Count > 0)
            {
                IsCsvDataLoaded = true;
                Debug.Log("CSV data loading complete.");
            }
            else
            {
                IsCsvDataLoaded = false;
                Debug.LogError("CSV data loading failed or no data was retrieved. Check that CSV files are in StreamingAssets and format is correct.");
            }
        }

        IEnumerator LoadBuildingPrefabsFromCsv()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, buildingPrefabCsvFileName);
            string csvText = "";
            bool loadSuccess = false;

            if (Application.platform == RuntimePlatform.WebGLPlayer || Application.platform == RuntimePlatform.Android)
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        csvText = www.downloadHandler.text;
                        loadSuccess = true;
                    }
                    else Debug.LogError($"Failed to load {buildingPrefabCsvFileName} (WebGL/Android): {www.error}");
                }
            }
            else
            {
                if (File.Exists(filePath))
                {
                    csvText = File.ReadAllText(filePath);
                    loadSuccess = true;
                }
                else Debug.LogError($"File not found: {filePath}");
            }

            if (loadSuccess && !string.IsNullOrEmpty(csvText))
            {
                ParseBuildingPrefabCsv(csvText);
            }
        }

        IEnumerator LoadEmptyLandsFromCsv()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, emptyLandCsvFileName);
            string csvText = "";
            bool loadSuccess = false;

            if (Application.platform == RuntimePlatform.WebGLPlayer || Application.platform == RuntimePlatform.Android)
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        csvText = www.downloadHandler.text;
                        loadSuccess = true;
                    }
                    else Debug.LogError($"Failed to load {emptyLandCsvFileName} (WebGL/Android): {www.error}");
                }
            }
            else
            {
                if (File.Exists(filePath))
                {
                    csvText = File.ReadAllText(filePath);
                    loadSuccess = true;
                }
                else Debug.LogError($"File not found: {filePath}");
            }

            if (loadSuccess && !string.IsNullOrEmpty(csvText))
            {
                ParseEmptyLandCsv(csvText);
            }
        }

        void ParseBuildingPrefabCsv(string csvText)
        {
            StringReader reader = new StringReader(csvText);
            string line = reader.ReadLine(); // Read header
            if (line == null) return;
            var headerMap = line.Split(',').Select((v, i) => new { Name = v.Trim('"'), Index = i }).ToDictionary(x => x.Name, x => x.Index);

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] values = ParseCsvLine(line);
                try
                {
                    BuildingPrefabData data = new BuildingPrefabData
                    {
                        ID = int.Parse(values[headerMap["ID"]].Trim('"')),
                        Length = float.Parse(values[headerMap["Length"]].Trim('"')),
                        Width = float.Parse(values[headerMap["Width"]].Trim('"')),
                        Function = int.Parse(values[headerMap["Function"]].Trim('"')),
                        FloorType = int.Parse(values[headerMap["FloorType"]].Trim('"')),
                        Material = int.Parse(values[headerMap["Material"]].Trim('"'))
                    };
                    buildingPrefabCache[data.ID] = data;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing building prefab CSV line: '{line}'. Error: {ex.Message}");
                }
            }
        }

        void ParseEmptyLandCsv(string csvText)
        {
            StringReader reader = new StringReader(csvText);
            string line = reader.ReadLine(); // Read header
            if (line == null) return;
            var headerMap = line.Split(',').Select((v, i) => new { Name = v.Trim('"'), Index = i }).ToDictionary(x => x.Name, x => x.Index);

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] values = ParseCsvLine(line);
                try
                {
                    EmptyLandData data = new EmptyLandData
                    {
                        ID = int.Parse(values[headerMap["ID"]].Trim('"')),
                        Length = float.Parse(values[headerMap["Length"]].Trim('"')),
                        Width = float.Parse(values[headerMap["Width"]].Trim('"')),
                        Position = new Vector3(
                            float.Parse(values[headerMap["StartPosX"]].Trim('"')),
                            float.Parse(values[headerMap["StartPosY"]].Trim('"')),
                            float.Parse(values[headerMap["StartPosZ"]].Trim('"'))
                        ),
                        EndPosition = new Vector3(
                            float.Parse(values[headerMap["EndPosX"]].Trim('"')),
                            float.Parse(values[headerMap["EndPosY"]].Trim('"')),
                            float.Parse(values[headerMap["EndPosZ"]].Trim('"'))
                        ),
                        RotationY = float.Parse(values[headerMap["RotationY"]].Trim('"')),
                        T = int.Parse(values[headerMap["T"]].Trim('"')),
                        S = int.Parse(values[headerMap["S"]].Trim('"'))
                    };
                    emptyLandCache[data.ID] = data;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing empty land CSV line: '{line}'. Error: {ex.Message}");
                }
            }
        }

        private string[] ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            System.Text.StringBuilder currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
            result.Add(currentField.ToString());
            return result.ToArray();
        }

        // CHANGE: Made this method more robust with TryParse
        public void SpawnBuilding(string emptyID, string function, string floorType, string material)
        {
            // ADDED: Log to easily track streaming progress
            Debug.Log($"Attempting to spawn building for land ID: {emptyID}");

            if (!IsCsvDataLoaded)
            {
                Debug.LogError("CSV data is not loaded. Cannot spawn building.");
                return;
            }

            if (!int.TryParse(emptyID, out int landId) || !emptyLandCache.ContainsKey(landId))
            {
                Debug.LogError($"Invalid or non-existent land ID: {emptyID}.");
                return;
            }

            // CHANGE: Use TryParse for robustness against bad API data
            if (!int.TryParse(function, out int targetFunction) ||
                !int.TryParse(floorType, out int targetFloor) ||
                !int.TryParse(material, out int targetMaterial))
            {
                Debug.LogError($"Failed to parse properties for land ID {landId}. Function: '{function}', FloorType: '{floorType}', Material: '{material}'. Skipping this plot.");
                return;
            }

            EmptyLandData landData = emptyLandCache[landId];
            GameObject landObject = landArray[landId - 1];

            if (landObject == null)
            {
                Debug.LogError($"Land GameObject for ID {landId} is not assigned in landArray.");
                return;
            }

            if (landData.S == 1)
            {
                GenerateS1Building(landData, landObject);
                return;
            }

            if (targetFunction == 4)
            {
                GenerateCulturalBuilding(landData, landObject);
            }
            else
            {
                GenerateStandardBuildings(landData, landObject, targetFunction, targetFloor, targetMaterial);
            }

            if (landData.T == 1 && targetFunction != 4)
            {
                GenerateAdditionalBuildings(landData, landObject, targetFunction, targetFloor, targetMaterial);
            }
        }

        // --- The rest of the generation logic (GenerateS1Building, etc.) remains unchanged ---
        // --- as it already operates on a single plot, which is perfect for streaming.   ---

        void GenerateS1Building(EmptyLandData landData, GameObject landObject)
        {
            List<int> availableIds = new List<int> { 235, 236 };
            availableIds.RemoveAll(id => usedSpecialBuildings.Contains(id) || GetUsageCount(landData, id) >= 2);

            if (availableIds.Count == 0)
            {
                Debug.LogError("Special buildings for S=1 plots (IDs 235/236) are all used or have reached their usage limit on this plot!");
                return;
            }

            int selectedId = availableIds[UnityEngine.Random.Range(0, availableIds.Count)];
            usedSpecialBuildings.Add(selectedId);
            IncrementUsageCount(landData, selectedId);

            if (!buildingPrefabCache.ContainsKey(selectedId))
            {
                Debug.LogError($"Building configuration does not exist for ID: {selectedId}. Cannot spawn S1 building.");
                return;
            }

            Vector3 offset = landData.RotationY switch
            {
                90f => new Vector3(86f, 0f, -86f),
                180f => new Vector3(-86f, 0f, -86f),
                270f => new Vector3(-86f, 0f, 86f),
                _ => new Vector3(86f, 0f, 86f)
            };

            Vector3 spawnPos = landData.Position + offset;
            Quaternion rotation = Quaternion.Euler(0f, landObject.transform.eulerAngles.y, 0f);
            InstantiateBuilding(selectedId, spawnPos, rotation, landObject.transform);
        }

        void GenerateCulturalBuilding(EmptyLandData landData, GameObject landObject)
        {
            if (Mathf.Approximately(landData.Width, 72f) == false)
            {
                Debug.LogError($"Cultural building plot {landData.ID} (Position: {landData.Position}) width ({landData.Width}) is not 72. Cannot generate cultural building.");
                return;
            }

            Vector3 basePos = landData.Position;
            Quaternion buildingWorldRotation = Quaternion.Euler(0, landObject.transform.eulerAngles.y, 0);
            List<Vector3> relativeSpawnOffsets = new List<Vector3>();

            if (landData.T == 0)
            {
                int maxBuildings = Mathf.FloorToInt(landData.Length / 72f);

                if (maxBuildings == 0)
                {
                    Debug.LogWarning($"Plot {landData.ID} (T=0, Position: {basePos}, Length: {landData.Length}) does not have enough length to place any 72x72 cultural buildings.");
                    return;
                }

                for (int i = 0; i < maxBuildings; i++)
                {
                    Vector3 offset;
                    float spacing = 72f + minBuildingDistance;

                    switch (landData.RotationY)
                    {
                        case 0f:
                            offset = new Vector3(36f + i * spacing, 0f, 36f);
                            break;
                        case 90f:
                            offset = new Vector3(36f, 0f, -36f - i * spacing);
                            break;
                        case 180f:
                            offset = new Vector3(-36f - i * spacing, 0f, -36f);
                            break;
                        case 270f:
                            offset = new Vector3(-36f, 0f, 36f + i * spacing);
                            break;
                        default:
                            Debug.LogError($"Plot {landData.ID} (T=0) has an unsupported RotationY value ({landData.RotationY}). Skipping this building index.");
                            continue;
                    }

                    relativeSpawnOffsets.Add(offset);
                }
            }
            else if (landData.T == 1)
            {
                switch (landData.RotationY)
                {
                    case 0f:
                        relativeSpawnOffsets.Add(new Vector3(36f, 0f, 36f));
                        relativeSpawnOffsets.Add(new Vector3(136f, 0f, 36f));
                        relativeSpawnOffsets.Add(new Vector3(236f, 0f, 36f));
                        relativeSpawnOffsets.Add(new Vector3(136f, 0f, -64f));
                        break;
                    case 90f:
                        relativeSpawnOffsets.Add(new Vector3(36f, 0f, -36f));
                        relativeSpawnOffsets.Add(new Vector3(36f, 0f, -136f));
                        relativeSpawnOffsets.Add(new Vector3(36f, 0f, -236f));
                        relativeSpawnOffsets.Add(new Vector3(-64f, 0f, -136f));
                        break;
                    case 180f:
                        relativeSpawnOffsets.Add(new Vector3(-36f, 0f, -36f));
                        relativeSpawnOffsets.Add(new Vector3(-136f, 0f, -36f));
                        relativeSpawnOffsets.Add(new Vector3(-236f, 0f, -36f));
                        relativeSpawnOffsets.Add(new Vector3(-136f, 0f, 64f));
                        break;
                    case 270f:
                        relativeSpawnOffsets.Add(new Vector3(-36f, 0f, 36f));
                        relativeSpawnOffsets.Add(new Vector3(-36f, 0f, 136f));
                        relativeSpawnOffsets.Add(new Vector3(-36f, 0f, 236f));
                        relativeSpawnOffsets.Add(new Vector3(64f, 0f, 136f));
                        break;
                    default:
                        Debug.LogError($"Plot {landData.ID} (T=1) has an unsupported RotationY value ({landData.RotationY}). Cannot determine fixed positions.");
                        return;
                }
            }

            if (relativeSpawnOffsets.Count == 0)
            {
                Debug.LogWarning($"Plot {landData.ID} (Position {basePos}) could not calculate any valid spawn positions for cultural buildings.");
                return;
            }

            int spawnedCount = 0;

            foreach (Vector3 relativeOffset in relativeSpawnOffsets)
            {
                List<BuildingPrefabData> validPrefabs = new List<BuildingPrefabData>();

                foreach (BuildingPrefabData prefab_iter in buildingPrefabCache.Values)
                {
                    if (prefab_iter.Function == 4)
                        validPrefabs.Add(prefab_iter);
                }

                if (validPrefabs.Count == 0)
                    continue;

                BuildingPrefabData selectedPrefab = validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
                usedCulturalBuildings.Add(selectedPrefab.ID);
                IncrementUsageCount(landData, selectedPrefab.ID);

                Vector3 spawnPosition = basePos + relativeOffset;
                InstantiateBuilding(selectedPrefab.ID, spawnPosition, buildingWorldRotation, landObject.transform);
                spawnedCount++;
            }

            if (spawnedCount == 0 && relativeSpawnOffsets.Count > 0)
            {
                Debug.LogWarning($"Plot {landData.ID} (Position {basePos}): Planned {relativeSpawnOffsets.Count} cultural building positions, but failed to generate any.");
            }
        }

        void GenerateStandardBuildings(EmptyLandData landData, GameObject landObject, int function, int floorType, int material)
        {
            float L = landData.Length;
            Vector3 basePos = landData.Position;
            float parentRotationY = landObject.transform.eulerAngles.y;

            var (row1Start, row2Start, direction) = GetGenerationParameters(parentRotationY, basePos);
            GenerateBuildingRow(L, row1Start, direction, function, floorType, material, landObject.transform, parentRotationY, landData);

            if (Mathf.Approximately(landData.Width, 72f))
            {
                GenerateBuildingRow(L, row2Start, direction, function, floorType, material, landObject.transform, parentRotationY, landData);
            }
        }

        void GenerateAdditionalBuildings(EmptyLandData landData, GameObject landObject, int function, int floorType, int material)
        {
            float newLength = 100f;
            float newWidth = 72f;
            float newSceneRotationY = landObject.transform.eulerAngles.y + 90f;

            Vector3 newBasePosForRow1 = CalculateNewPosition(36, landData.Position, landData.RotationY);
            Vector3 newBasePosForRow2 = CalculateNewPosition(72, landData.Position, landData.RotationY);

            EmptyLandData newLandForAdditional = new EmptyLandData
            {
                Length = newLength,
                Width = newWidth,
                Position = newBasePosForRow1,
                RotationY = landData.RotationY + 90f,
                BuildingUsageCount = landData.BuildingUsageCount
            };

            Vector3 placementDirection = GetNewDirection(newLandForAdditional.RotationY);

            GenerateBuildingRow(newLength, newBasePosForRow1, placementDirection, function, floorType, material, landObject.transform, newSceneRotationY, newLandForAdditional);
            GenerateBuildingRow(newLength, newBasePosForRow2, placementDirection, function, floorType, material, landObject.transform, newSceneRotationY, newLandForAdditional);
        }

        Vector3 CalculateNewPosition(float offsetWithinNewArea, Vector3 originalPos, float originalDbRotation)
        {
            float totalOffsetFromOriginalEdge = 100f + offsetWithinNewArea;

            return originalDbRotation switch
            {
                90f => new Vector3(originalPos.x, originalPos.y, originalPos.z - totalOffsetFromOriginalEdge),
                180f => new Vector3(originalPos.x - totalOffsetFromOriginalEdge, originalPos.y, originalPos.z),
                270f => new Vector3(originalPos.x, originalPos.y, originalPos.z + totalOffsetFromOriginalEdge),
                _ => new Vector3(originalPos.x + totalOffsetFromOriginalEdge, originalPos.y, originalPos.z)
            };
        }

        Vector3 GetNewDirection(float rotationY)
        {
            float normalizedRotation = Mathf.Repeat(rotationY, 360f);

            if (Mathf.Abs(normalizedRotation - 90f) < 1f)
                return new Vector3(0, 0, -1);
            if (Mathf.Abs(normalizedRotation - 180f) < 1f)
                return new Vector3(-1, 0, 0);
            if (Mathf.Abs(normalizedRotation - 270f) < 1f)
                return new Vector3(0, 0, 1);

            return new Vector3(1, 0, 0);
        }

        (Vector3, Vector3, Vector3) GetGenerationParameters(float sceneRotationY, Vector3 basePos)
        {
            float normalizedRotation = Mathf.Repeat(sceneRotationY, 360f);

            if (Mathf.Abs(normalizedRotation - 90f) < 1f)
                return (new Vector3(basePos.x + 36f, basePos.y, basePos.z),
                       new Vector3(basePos.x + 72f, basePos.y, basePos.z),
                       new Vector3(0, 0, -1));

            if (Mathf.Abs(normalizedRotation - 180f) < 1f)
                return (new Vector3(basePos.x, basePos.y, basePos.z - 36f),
                       new Vector3(basePos.x, basePos.y, basePos.z - 72f),
                       new Vector3(-1, 0, 0));

            if (Mathf.Abs(normalizedRotation - 270f) < 1f)
                return (new Vector3(basePos.x - 36f, basePos.y, basePos.z),
                       new Vector3(basePos.x - 72f, basePos.y, basePos.z),
                       new Vector3(0, 0, 1));

            return (new Vector3(basePos.x, basePos.y, basePos.z + 36f),
                   new Vector3(basePos.x, basePos.y, basePos.z + 72f),
                   new Vector3(1, 0, 0));
        }

        void GenerateBuildingRow(float maxLength, Vector3 startPos, Vector3 direction, int function, int floorType, int material, Transform parent, float buildingRotationY, EmptyLandData landData)
        {
            Vector3 currentPos = startPos;
            float remainingLength = maxLength;

            while (remainingLength > 20f)
            {
                float adjustedMaxLength = remainingLength - (currentPos == startPos ? 0 : minBuildingDistance);

                // 三级降级策略
                // 第1级：严格匹配（功能+楼层+材料）
                var candidates = GetValidBuildings(function, floorType, material, adjustedMaxLength, landData);

                if (candidates.Count == 0)
                {
                    // 第2级：放弃材料（功能+楼层）
                    Debug.LogWarning($"[BuildingSpawner] 地块{landData.ID}：未找到匹配材料的建筑，放宽到只匹配功能+楼层");
                    candidates = GetValidBuildings(function, floorType, null, adjustedMaxLength, landData);
                }

                if (candidates.Count == 0)
                {
                    // 第3级：只看楼层
                    Debug.LogWarning($"[BuildingSpawner] 地块{landData.ID}：未找到匹配功能的建筑，放宽到只匹配楼层");
                    candidates = GetValidBuildings(null, floorType, null, adjustedMaxLength, landData);
                }

                if (candidates.Count == 0)
                {
                    Debug.LogWarning($"[BuildingSpawner] 地块{landData.ID}：即使放宽到只匹配楼层也无可用建筑，停止铺设。剩余空间：{remainingLength}米");
                    break;
                }

                BuildingPrefabData selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                Vector3 spawnOffsetDueToLength = direction * (selected.Length / 2f);
                Vector3 spawnPos = currentPos + spawnOffsetDueToLength;

                if (currentPos != startPos)
                {
                    spawnPos = currentPos + direction * (minBuildingDistance + selected.Length / 2f);
                }

                InstantiateBuilding(selected.ID, spawnPos, Quaternion.Euler(0, buildingRotationY, 0), parent);
                IncrementUsageCount(landData, selected.ID);

                float consumedLength = selected.Length;
                if (currentPos != startPos || (currentPos == startPos && remainingLength < maxLength))
                {
                    consumedLength += minBuildingDistance;
                }

                currentPos += direction * consumedLength;
                remainingLength -= consumedLength;
            }
        }

        List<BuildingPrefabData> GetValidBuildings(int? function, int? floorType, int? material, float maxLength, EmptyLandData landData)
        {
            return buildingPrefabCache.Values
                .Where(p => (!function.HasValue || p.Function == function.Value) &&
                            (!floorType.HasValue || p.FloorType == floorType.Value) &&
                            (!material.HasValue || p.Material == material.Value) &&
                            p.Length <= maxLength &&
                            GetUsageCount(landData, p.ID) < 3)
                .ToList();
        }

        GameObject InstantiateBuilding(int prefabId, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefabId < 0 || prefabId >= buildings.Length || buildings[prefabId] == null)
            {
                Debug.LogError($"Prefab ID {prefabId} is invalid or not assigned in the 'buildings' array.");
                return null;
            }
            return Instantiate(buildings[prefabId], position, rotation, parent);
        }

        public void RemoveAllBuildings()
        {
            foreach (var landGameObject in landArray)
            {
                if (landGameObject != null)
                {
                    for (int i = landGameObject.transform.childCount - 1; i >= 0; i--)
                    {
                        Destroy(landGameObject.transform.GetChild(i).gameObject);
                    }
                }
            }

            foreach (var landData in emptyLandCache.Values)
            {
                landData.BuildingUsageCount.Clear();
                landData.Summary = "";
                landData.Function = "";
                landData.FloorType = "";
                landData.Material = "";
            }

            usedSpecialBuildings.Clear();
            usedCulturalBuildings.Clear();
            Debug.Log("All spawned buildings and plot data have been cleared.");
        }

        private int GetUsageCount(EmptyLandData landData, int buildingId)
        {
            return landData.BuildingUsageCount.TryGetValue(buildingId, out int count) ? count : 0;
        }

        private void IncrementUsageCount(EmptyLandData landData, int buildingId)
        {
            if (landData.BuildingUsageCount.ContainsKey(buildingId))
                landData.BuildingUsageCount[buildingId]++;
            else
                landData.BuildingUsageCount[buildingId] = 1;
        }

        public void StoreLandProperties(string emptyID, string function, string floorType, string material, string summary)
        {
            if (int.TryParse(emptyID, out int landId) && emptyLandCache.TryGetValue(landId, out EmptyLandData landData))
            {
                landData.Function = function;
                landData.FloorType = floorType;
                landData.Material = material;
                landData.Summary = summary;
                // NOTE: Since EmptyLandData is now a class, we don't need to write it back to the dictionary.
            }
            else
            {
                Debug.LogError($"StoreLandProperties: Could not find or parse land ID: {emptyID}.");
            }
        }

        public EmptyLandData GetLandData(int landId)
        {
            return emptyLandCache.TryGetValue(landId, out EmptyLandData landData) ? landData : null;
        }

        // NOTE: Changed from struct to class for easier state management (pass by reference).
        public class BuildingPrefabData
        {
            public int ID;
            public float Length;
            public float Width;
            public int Function;
            public int FloorType;
            public int Material;
        }

        // NOTE: Changed from struct to class.
        public class EmptyLandData
        {
            public int ID;
            public float Length;
            public float Width;
            public Vector3 Position; // StartPos
            public Vector3 EndPosition; // EndPos（新增用于计算地块中心点）
            public float RotationY;
            public int T;
            public int S;
            public Dictionary<int, int> BuildingUsageCount = new Dictionary<int, int>(); // Initialize here
            public string Function;
            public string FloorType;
            public string Material;
            public string Summary;
        }
    }
}