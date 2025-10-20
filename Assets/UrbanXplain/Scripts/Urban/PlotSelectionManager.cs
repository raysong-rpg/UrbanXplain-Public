using System.Collections.Generic;
using UnityEngine;

namespace UrbanXplain
{
    /// <summary>
    /// 地块圈选管理器 - Demo版本
    /// 负责处理圈地模式切换、鼠标绘制、地块检测和选中状态管理
    /// </summary>
    public class PlotSelectionManager : MonoBehaviour
    {
        [Header("Required References")]
        [Tooltip("BuildingSpawnerJson引用，用于获取地块数据")]
        public BuildingSpawnerJson buildingSpawner;

        [Tooltip("BuildingColorChanger引用，用于控制建筑颜色")]
        public BuildingColorChanger buildingColorChanger;

        [Tooltip("主相机引用")]
        public Camera mainCamera;

        [Header("Visual Settings")]
        [Tooltip("圈选线条颜色")]
        public Color selectionLineColor = Color.red;

        [Tooltip("圈选线条宽度（世界空间单位）")]
        public float lineWidth = 2.0f;

        [Tooltip("选中地块的高亮颜色")]
        public Color highlightColor = new Color(0f, 1f, 1f, 1f); // 青色

        [Header("Selection Settings")]
        [Tooltip("采样间隔（像素），控制线条密度")]
        public float samplingDistance = 10f;

        // 圈地模式状态
        private bool isSelectionMode = false;
        private bool isDrawing = false;

        // 绘制路径数据
        private List<Vector3> drawPath = new List<Vector3>(); // 屏幕坐标
        private List<Vector3> worldPath = new List<Vector3>(); // 世界坐标

        // LineRenderer组件
        private LineRenderer lineRenderer;
        private GameObject lineRendererObject;

        // 选中的地块ID列表
        private HashSet<int> selectedPlotIDs = new HashSet<int>();

        // 当前高亮的ChildColorToggler列表
        private List<ChildColorToggler> highlightedTogglers = new List<ChildColorToggler>();

        // 地块中心点缓存（从emptyland.csv计算）
        private Dictionary<int, Vector3> plotCenters = new Dictionary<int, Vector3>();

        // 地面投射平面
        private Plane groundPlane;

        void Start()
        {
            // 初始化地面平面（Y=0）
            groundPlane = new Plane(Vector3.up, Vector3.zero);

            // 验证引用
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("[PlotSelectionManager] Main Camera not found!");
                    enabled = false;
                    return;
                }
            }

            if (buildingSpawner == null)
            {
                Debug.LogError("[PlotSelectionManager] BuildingSpawnerJson reference is missing!");
            }

            if (buildingColorChanger == null)
            {
                Debug.LogError("[PlotSelectionManager] BuildingColorChanger reference is missing!");
            }

            // 创建LineRenderer对象
            CreateLineRenderer();

            Debug.Log("[PlotSelectionManager] Initialized. Press the selection button to enter selection mode.");
        }

        void Update()
        {
            if (!isSelectionMode) return;

            HandleSelectionInput();
        }

        /// <summary>
        /// 切换圈地模式（由UI按钮调用）
        /// </summary>
        public void ToggleSelectionMode()
        {
            isSelectionMode = !isSelectionMode;

            if (isSelectionMode)
            {
                Debug.Log("[PlotSelectionManager] Selection mode ENABLED. Draw with left mouse button.");
                CalculatePlotCenters(); // 进入圈地模式时计算地块中心点
            }
            else
            {
                Debug.Log("[PlotSelectionManager] Selection mode DISABLED.");
                ClearDrawing();
            }
        }

        /// <summary>
        /// 检查是否处于圈地模式
        /// </summary>
        public bool IsSelectionMode()
        {
            return isSelectionMode;
        }

        /// <summary>
        /// 处理圈选输入
        /// </summary>
        private void HandleSelectionInput()
        {
            // 鼠标按下 - 开始绘制
            if (Input.GetMouseButtonDown(0))
            {
                StartDrawing();
            }

            // 鼠标拖动 - 更新路径
            if (Input.GetMouseButton(0) && isDrawing)
            {
                UpdateDrawing();
            }

            // 鼠标释放 - 完成绘制
            if (Input.GetMouseButtonUp(0) && isDrawing)
            {
                FinishDrawing();
            }

            // ESC键 - 取消圈选
            if (Input.GetKeyDown(KeyCode.Escape) && isDrawing)
            {
                CancelDrawing();
            }
        }

        /// <summary>
        /// 开始绘制
        /// </summary>
        private void StartDrawing()
        {
            isDrawing = true;
            drawPath.Clear();
            worldPath.Clear();

            // 添加起始点
            Vector3 mousePos = Input.mousePosition;
            drawPath.Add(mousePos);

            // 转换到世界坐标
            Vector3 worldPos = ScreenToWorldPoint(mousePos);
            worldPath.Add(worldPos);

            // 显示LineRenderer
            if (lineRendererObject != null)
            {
                lineRendererObject.SetActive(true);
            }

            Debug.Log($"[PlotSelectionManager] Started drawing at screen: {mousePos}, world: {worldPos}");
        }

        /// <summary>
        /// 更新绘制路径
        /// </summary>
        private void UpdateDrawing()
        {
            Vector3 mousePos = Input.mousePosition;

            // 检查距离上一个点的距离，避免采样过密
            if (drawPath.Count > 0)
            {
                Vector3 lastPoint = drawPath[drawPath.Count - 1];
                float distance = Vector2.Distance(new Vector2(mousePos.x, mousePos.y),
                                                   new Vector2(lastPoint.x, lastPoint.y));

                if (distance < samplingDistance)
                {
                    return; // 距离太近，跳过
                }
            }

            // 添加新点
            drawPath.Add(mousePos);

            // 转换到世界坐标
            Vector3 worldPos = ScreenToWorldPoint(mousePos);
            worldPath.Add(worldPos);

            // 更新LineRenderer
            UpdateLineRenderer();
        }

        /// <summary>
        /// 完成绘制，执行地块检测
        /// </summary>
        private void FinishDrawing()
        {
            isDrawing = false;

            Debug.Log($"[PlotSelectionManager] Finished drawing. Total points: {worldPath.Count}");

            // 检查是否有足够的点形成多边形
            if (worldPath.Count < 3)
            {
                Debug.LogWarning("[PlotSelectionManager] Not enough points to form a polygon (need at least 3).");
                ClearDrawing();
                return;
            }

            // 执行地块检测
            DetectSelectedPlots();

            // 清除绘制线条（保留高亮状态）
            ClearDrawing();
        }

        /// <summary>
        /// 取消绘制
        /// </summary>
        private void CancelDrawing()
        {
            isDrawing = false;
            ClearDrawing();
            Debug.Log("[PlotSelectionManager] Drawing cancelled.");
        }

        /// <summary>
        /// 清除绘制线条
        /// </summary>
        private void ClearDrawing()
        {
            drawPath.Clear();
            worldPath.Clear();

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            if (lineRendererObject != null)
            {
                lineRendererObject.SetActive(false);
            }
        }

        /// <summary>
        /// 屏幕坐标转世界坐标（投射到地面Y=0平面）
        /// </summary>
        private Vector3 ScreenToWorldPoint(Vector3 screenPos)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            // 如果射线不与地面相交，返回零点
            Debug.LogWarning($"[PlotSelectionManager] Screen point {screenPos} did not intersect ground plane!");
            return Vector3.zero;
        }

        /// <summary>
        /// 创建LineRenderer对象
        /// </summary>
        private void CreateLineRenderer()
        {
            lineRendererObject = new GameObject("SelectionLine");
            lineRendererObject.transform.SetParent(transform);
            lineRendererObject.SetActive(false);

            lineRenderer = lineRendererObject.AddComponent<LineRenderer>();

            // 优先使用自定义shader，确保线条始终在最前面
            Shader shader = Shader.Find("Custom/AlwaysOnTopLine");
            if (shader == null)
            {
                Debug.LogWarning("[PlotSelectionManager] Custom shader 'Custom/AlwaysOnTopLine' not found, trying fallback shaders.");

                // 备用方案1：Hidden/Internal-Colored
                shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    // 备用方案2：Sprites/Default
                    shader = Shader.Find("Sprites/Default");
                }
            }

            Material lineMaterial = new Material(shader);
            lineMaterial.color = selectionLineColor;

            // 如果不是自定义shader，手动设置渲染参数
            if (shader.name != "Custom/AlwaysOnTopLine")
            {
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMaterial.renderQueue = 4000;
            }

            lineRenderer.material = lineMaterial;
            lineRenderer.startColor = selectionLineColor;
            lineRenderer.endColor = selectionLineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = true;

            Debug.Log($"[PlotSelectionManager] LineRenderer created with shader: {shader.name}");
        }

        /// <summary>
        /// 更新LineRenderer显示
        /// </summary>
        private void UpdateLineRenderer()
        {
            if (lineRenderer == null || worldPath.Count == 0) return;

            lineRenderer.positionCount = worldPath.Count;
            for (int i = 0; i < worldPath.Count; i++)
            {
                // 稍微提升Y坐标，避免Z-fighting
                Vector3 pos = worldPath[i];
                pos.y = 1f;
                lineRenderer.SetPosition(i, pos);
            }
        }

        /// <summary>
        /// 计算所有地块的中心点（从emptyland.csv的StartPos和EndPos）
        /// </summary>
        private void CalculatePlotCenters()
        {
            plotCenters.Clear();

            if (buildingSpawner == null || !buildingSpawner.IsCsvDataLoaded)
            {
                Debug.LogError("[PlotSelectionManager] Cannot calculate plot centers: BuildingSpawner or CSV data not available.");
                return;
            }

            // 遍历所有地块
            for (int i = 1; i <= 43; i++)
            {
                var landData = buildingSpawner.GetLandData(i);
                if (landData == null)
                {
                    Debug.LogWarning($"[PlotSelectionManager] Land data for ID {i} not found.");
                    continue;
                }

                // 使用GeometryUtils计算中心点
                Vector3 center = GeometryUtils.CalculatePlotCenter(landData.Position, landData.EndPosition);

                plotCenters[i] = center;

                Debug.Log($"[PlotSelectionManager] Plot {i}: StartPos={landData.Position}, EndPos={landData.EndPosition}, Center={center}");
            }

            Debug.Log($"[PlotSelectionManager] Calculated {plotCenters.Count} plot centers.");
        }

        /// <summary>
        /// 检测哪些地块被圈选中
        /// </summary>
        private void DetectSelectedPlots()
        {
            if (worldPath.Count < 3)
            {
                Debug.LogWarning("[PlotSelectionManager] Path has less than 3 points, cannot detect plots.");
                return;
            }

            // 清除之前的选中状态
            ClearSelection();

            // 将worldPath转换为数组，用于几何判断
            Vector3[] polygon = worldPath.ToArray();

            // 遍历所有地块，检查中心点是否在多边形内
            foreach (var kvp in plotCenters)
            {
                int plotID = kvp.Key;
                Vector3 center = kvp.Value;

                if (GeometryUtils.IsPointInPolygon(center, polygon))
                {
                    selectedPlotIDs.Add(plotID);
                    Debug.Log($"[PlotSelectionManager] Plot {plotID} is selected (center: {center})");
                }
            }

            Debug.Log($"[PlotSelectionManager] Total selected plots: {selectedPlotIDs.Count}");

            // 高亮选中的地块
            HighlightSelectedPlots();
        }

        /// <summary>
        /// 高亮选中的地块（复用现有高亮系统）
        /// </summary>
        private void HighlightSelectedPlots()
        {
            if (buildingSpawner == null || buildingSpawner.landArray == null)
            {
                Debug.LogError("[PlotSelectionManager] Cannot highlight plots: BuildingSpawner.landArray is null.");
                return;
            }

            foreach (int plotID in selectedPlotIDs)
            {
                // landArray是从0开始的，plotID是从1开始的
                int arrayIndex = plotID - 1;

                if (arrayIndex < 0 || arrayIndex >= buildingSpawner.landArray.Length)
                {
                    Debug.LogWarning($"[PlotSelectionManager] Plot ID {plotID} is out of range.");
                    continue;
                }

                GameObject landObject = buildingSpawner.landArray[arrayIndex];
                if (landObject == null)
                {
                    Debug.LogWarning($"[PlotSelectionManager] Land GameObject for ID {plotID} is null.");
                    continue;
                }

                // 获取ChildColorToggler组件
                ChildColorToggler toggler = landObject.GetComponent<ChildColorToggler>();
                if (toggler == null)
                {
                    Debug.LogWarning($"[PlotSelectionManager] Land GameObject for ID {plotID} does not have ChildColorToggler component.");
                    continue;
                }

                // 应用高亮颜色
                toggler.SetChildrenColor(highlightColor, false);
                highlightedTogglers.Add(toggler);

                Debug.Log($"[PlotSelectionManager] Highlighted plot {plotID} (array index {arrayIndex})");
            }

            Debug.Log($"[PlotSelectionManager] Highlighted {highlightedTogglers.Count} plots.");
        }

        /// <summary>
        /// 清除所有选中状态
        /// </summary>
        public void ClearSelection()
        {
            // 恢复之前高亮地块的颜色
            foreach (var toggler in highlightedTogglers)
            {
                if (toggler != null && buildingColorChanger != null)
                {
                    toggler.RestoreToPreviousState(buildingColorChanger);
                }
            }

            highlightedTogglers.Clear();
            selectedPlotIDs.Clear();

            Debug.Log("[PlotSelectionManager] Selection cleared.");
        }

        /// <summary>
        /// 获取当前选中的地块ID列表
        /// </summary>
        public List<int> GetSelectedPlotIDs()
        {
            return new List<int>(selectedPlotIDs);
        }
    }
}
