// --- START OF FILE: BuildingClickHandler.cs (FIXED) ---

using UnityEngine;
using UnityEngine.EventSystems;

namespace UrbanXplain
{
    public class BuildingClickHandler : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The color to apply to a building/plot when it is selected.")]
        public Color highlightColor = Color.magenta;
        [Tooltip("The layers that this script should consider clickable.")]
        public LayerMask clickableLayers;
        [Tooltip("The maximum distance for the raycast to detect clickable objects.")]
        public float maxRaycastDistance = 1000f;

        [Header("Required References")]
        [Tooltip("Reference to the BuildingColorChanger script to handle color states.")]
        public BuildingColorChanger buildingColorChanger;
        [Tooltip("Reference to the new BuildingInfoPanel script for displaying detailed information.")]
        public BuildingInfoPanel buildingInfoPanel;
        [Tooltip("Reference to the UIControl script to check for input mode.")]
        public UIControl uIControl;
        [Tooltip("Reference to the PlotSelectionManager to check if selection mode is active.")]
        public PlotSelectionManager plotSelectionManager;

        private Camera mainCamera;
        private ChildColorToggler currentlyHighlightedToggler = null;
        private const string EmptyLandsParentName = "EmptyLands000";

        void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("BuildingClickHandler: Main Camera not found! The script will be disabled.", this);
                enabled = false;
                return;
            }
            if (buildingColorChanger == null) Debug.LogWarning("BuildingClickHandler: BuildingColorChanger reference is not set.", this);
            if (buildingInfoPanel == null) Debug.LogWarning("BuildingClickHandler: BuildingInfoPanel reference is not set.", this);
            if (uIControl == null) Debug.LogWarning("BuildingClickHandler: UIControl reference is not set.", this);
        }

        void Update()
        {
            if (GlobalInputManager.GetGameMouseButtonDown(0))
            {
                ProcessClick();
            }
        }

        private void ProcessClick()
        {
            // 如果处于圈地模式，禁用建筑点击功能
            if (plotSelectionManager != null && plotSelectionManager.IsSelectionMode())
            {
                return;
            }

            if (uIControl != null && uIControl.IsInputMode())
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
                HandleWorldRaycast();
            }
            else if (uIControl != null && !uIControl.IsInputMode())
            {
                DeselectCurrentBuilding();
            }
            else
            {
                HandleWorldRaycast();
            }
        }

        private void HandleWorldRaycast()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, clickableLayers))
            {
                ChildColorToggler clickedPlotToggler = FindPlotToggler(hit.collider.gameObject);
                if (clickedPlotToggler != null)
                {
                    if (currentlyHighlightedToggler == clickedPlotToggler)
                    {
                        DeselectCurrentBuilding();
                    }
                    else
                    {
                        DeselectCurrentBuilding();
                        SelectBuilding(clickedPlotToggler);
                    }
                }
                else
                {
                    DeselectCurrentBuilding();
                }
            }
            else
            {
                DeselectCurrentBuilding();
            }
        }

        private ChildColorToggler FindPlotToggler(GameObject hitObject)
        {
            Transform currentTransform = hitObject.transform;
            while (currentTransform != null)
            {
                if (currentTransform.parent != null && currentTransform.parent.name == EmptyLandsParentName)
                {
                    return currentTransform.GetComponent<ChildColorToggler>();
                }
                currentTransform = currentTransform.parent;
            }
            return null;
        }

        private void SelectBuilding(ChildColorToggler plotToggler)
        {
            currentlyHighlightedToggler = plotToggler;
            currentlyHighlightedToggler.SetChildrenColor(highlightColor, false);
            ShowBuildingDetails(plotToggler.gameObject);
        }

        private void DeselectCurrentBuilding()
        {
            if (currentlyHighlightedToggler != null)
            {
                RestorePreviousColor(currentlyHighlightedToggler);
                currentlyHighlightedToggler = null;
            }
            if (buildingInfoPanel != null)
            {
                buildingInfoPanel.Hide();
            }
        }

        /// <summary>
        /// Public method to clear any current building highlight.
        /// Can be called by other scripts (e.g., UIControl when switching to gameplay mode).
        /// </summary>
        public void ClearHighlight()
        {
            DeselectCurrentBuilding();
        }

        private void ShowBuildingDetails(GameObject landPlotRoot)
        {
            if (buildingInfoPanel == null || buildingColorChanger?.deepSeekAPI?.buildingSpawnerJson == null)
            {
                Debug.LogWarning("Cannot show building details: required references are missing.");
                return;
            }

            BuildingSpawnerJson spawner = buildingColorChanger.deepSeekAPI.buildingSpawnerJson;
            if (spawner.landArray == null) { Debug.LogError("Spawner landArray is null!"); return; }

            int landId = -1;
            for (int i = 0; i < spawner.landArray.Length; i++)
            {
                if (spawner.landArray[i] == landPlotRoot)
                {
                    landId = i + 1;
                    break;
                }
            }

            if (landId != -1)
            {
                // FIX: Changed from .HasValue to a null check because EmptyLandData is now a class.
                var landData = spawner.GetLandData(landId);
                if (landData != null)
                {
                    // No .Value is needed anymore, landData is the object itself.
                    BuildingDisplayData displayData = new BuildingDisplayData
                    {
                        LotID = landData.ID.ToString(),
                        Function = MapFunctionCodeToString(landData.Function),
                        FloorType = MapFloorTypeCodeToString(landData.FloorType),
                        Material = MapMaterialCodeToString(landData.Material),
                        Rationale = string.IsNullOrEmpty(landData.Summary) ? "No design rationale is available for this plot." : landData.Summary
                    };
                    buildingInfoPanel.Show(displayData);
                }
                else
                {
                    Debug.LogWarning($"Could not retrieve land data for Land ID {landId}.");
                    buildingInfoPanel.Hide();
                }
            }
            else
            {
                Debug.LogWarning($"Could not find a Land ID for the clicked plot '{landPlotRoot.name}'.");
                buildingInfoPanel.Hide();
            }
        }

        #region Helper Methods for String Mapping
        private string MapFunctionCodeToString(string code)
        {
            switch (code)
            {
                case "1": return "Residential";
                case "2": return "Commercial";
                case "3": return "Public";
                case "4": return "Cultural & Entertainment";
                default: return "Unknown";
            }
        }

        private string MapFloorTypeCodeToString(string code)
        {
            switch (code)
            {
                case "1": return "Low-rise";
                case "2": return "Mid-rise";
                case "3": return "High-rise";
                case "4": return "Super High-rise";
                default: return "";
            }
        }

        private string MapMaterialCodeToString(string code)
        {
            switch (code)
            {
                case "1": return "Glass Curtain Wall";
                case "2": return "Concrete";
                default: return "Unknown";
            }
        }
        #endregion

        private void RestorePreviousColor(ChildColorToggler togglerToRestore)
        {
            if (togglerToRestore == null) return;
            togglerToRestore.RestoreToPreviousState(buildingColorChanger);
        }
    }
}
// --- END OF FILE: BuildingClickHandler.cs (FIXED) ---