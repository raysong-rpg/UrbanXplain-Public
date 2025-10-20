using UnityEngine;
using UnityEngine.EventSystems;

namespace UrbanXplain
{
    // Manages UI interaction mode, controlling cursor lock state, player movement,
    // and visibility/interactivity of a UI CanvasGroup.
    public class UIControl : MonoBehaviour
    {
        // Flag to indicate if the UI is currently in "input mode" (e.g., for interacting with UI elements).
        private bool isInputMode = false; // Renamed from 'inputMode' for clarity.
        // Reference to the PlayerControl script to enable/disable player movement.
        public PlayerControl playerControl;
        // Reference to the CanvasGroup component of the main UI panel to control its visibility and interactivity.
        public CanvasGroup canvasGroup;
        // Reference to the BuildingClickHandler to clear building highlights when exiting input mode.
        public BuildingClickHandler buildingClickHandler;

        private void Start()
        {
            // Initially lock the cursor and set to non-input mode (gameplay mode).
            Cursor.lockState = CursorLockMode.Locked;
            isInputMode = false; // Ensure starting in non-input mode.
            ToggleCanvasGroup(isInputMode); // Initially hide UI if starting in non-input mode.
        }

        private void Update()
        {
            // Check for input to toggle between input mode and gameplay mode.
            HandleModeToggleInput(); // Renamed from ChangeMode for clarity.
        }

        // Handles the input (Left Alt key) to toggle between UI input mode and gameplay mode.
        void HandleModeToggleInput() // Renamed from ChangeMode for clarity.
        {
            // Toggle mode when Left Alt key is pressed.
            if (GlobalInputManager.GetGameKeyDown(KeyCode.LeftAlt))
            {
                isInputMode = !isInputMode;
                UpdateModeState(); // Apply changes based on the new mode.
            }
        }

        // Updates the game state based on the current 'isInputMode'.
        // This includes cursor lock, player movement, and UI visibility.
        private void UpdateModeState()
        {
            if (isInputMode)
            {
                // Entering UI Input Mode
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Entering Gameplay Mode
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                // --- 核心修复 ---
                // << 2. 强制取消UI元素的焦点 >>
                // 当进入游玩模式时，告诉EventSystem取消对任何UI元素的选中状态，
                // 这样输入框就会失去焦点，不再接收键盘输入。
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                // << 3. 清空建筑高亮 >>
                // 当切换到游玩模式时，清除任何选中的建筑高亮状态
                if (buildingClickHandler != null)
                {
                    buildingClickHandler.ClearHighlight();
                }
            }

            // Toggle player movement based on the current mode.
            if (playerControl != null)
            {
                playerControl.ToggleMovement(isInputMode); // Pass true if input mode is active (disabling movement).
            }

            // Toggle the visibility and interactivity of the UI CanvasGroup.
            ToggleCanvasGroup(isInputMode);
        }

        // Controls the visibility, interactability, and raycast blocking of the assigned CanvasGroup.
        public void ToggleCanvasGroup(bool isVisible) // Renamed from ToggleVisibility for clarity.
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isVisible ? 1f : 0f;          // Controls transparency.
                canvasGroup.interactable = isVisible;          // Controls if UI elements can be interacted with.
                canvasGroup.blocksRaycasts = isVisible;        // Controls if the UI blocks mouse raycasts.
            }
            else
            {
                Debug.LogWarning("UIControl: CanvasGroup reference is not set. Cannot toggle UI visibility.", this);
            }
        }

        // Public method to check if the UI is currently in input mode.
        // This can be used by other scripts (e.g., BuildingClickHandler) to alter their behavior.
        public bool IsInputMode()
        {
            return isInputMode;
        }
    }
}