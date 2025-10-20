using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.IO;
using System;

namespace UrbanXplain
{
    /// <summary>
    /// 快捷指令面板 - 当输入框激活时显示预设的专业规划指令
    /// </summary>
    public class QuickCommandPanel : MonoBehaviour
    {
        [System.Serializable]
        public class QuickCommand
        {
            public int id;
            public string icon;
            public string icon_color;
            public string command_en;
        }

        [System.Serializable]
        public class QuickCommandData
        {
            public List<QuickCommand> commands;
        }

        [Header("UI References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Transform commandButtonsContainer;
        [SerializeField] private GameObject commandButtonPrefab;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Button Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        [SerializeField] private Color highlightColor = new Color(0f, 0.75f, 1f, 1f);
        [SerializeField] private Color pressedColor = new Color(0f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color selectedColor = new Color(0f, 0.6f, 0.9f, 1f);

        private QuickCommandData commandData;
        private List<GameObject> commandButtons = new List<GameObject>();
        private bool isVisible = false;

        public event Action<string> OnCommandSelected;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 初始隐藏
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            panelRoot.gameObject.SetActive(false);

            LoadCommands();
            CreateCommandButtons();
        }

        /// <summary>
        /// 从JSON文件加载快捷指令
        /// </summary>
        private void LoadCommands()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "QuickCommands.json");

            if (File.Exists(filePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    commandData = JsonUtility.FromJson<QuickCommandData>(jsonContent);
                    Debug.Log($"[QuickCommandPanel] Loaded {commandData.commands.Count} quick commands");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[QuickCommandPanel] Failed to load commands: {e.Message}");
                    CreateDefaultCommands();
                }
            }
            else
            {
                Debug.LogWarning($"[QuickCommandPanel] QuickCommands.json not found, using defaults");
                CreateDefaultCommands();
            }
        }

        private void CreateDefaultCommands()
        {
            commandData = new QuickCommandData
            {
                commands = new List<QuickCommand>
                {
                    new QuickCommand
                    {
                        id = 1,
                        icon = "[RES]",
                        icon_color = "#4169E1",
                        command_en = "Plan a high-quality residential community with plot ratio <=2.5, building density <=25%, green space ratio >=35%, high-rise spacing >=0.4x southern building height, ensuring min. 1-hour sunlight on winter solstice"
                    }
                }
            };
        }

        /// <summary>
        /// 动态创建指令按钮
        /// </summary>
        private void CreateCommandButtons()
        {
            if (commandData == null || commandData.commands == null) return;

            // 清除旧按钮
            foreach (var btn in commandButtons)
            {
                if (btn != null) Destroy(btn);
            }
            commandButtons.Clear();

            // 创建新按钮
            foreach (var command in commandData.commands)
            {
                GameObject buttonObj = Instantiate(commandButtonPrefab, commandButtonsContainer);
                commandButtons.Add(buttonObj);

                // 强制设置按钮尺寸（避免被Layout Group压缩）
                RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    // 保持预制体的原始尺寸
                    btnRect.sizeDelta = new Vector2(1420, 80); // 与你的预制体尺寸一致

                    // 添加Layout Element组件来控制布局
                    UnityEngine.UI.LayoutElement layoutElement = buttonObj.GetComponent<UnityEngine.UI.LayoutElement>();
                    if (layoutElement == null)
                    {
                        layoutElement = buttonObj.AddComponent<UnityEngine.UI.LayoutElement>();
                    }
                    layoutElement.preferredWidth = 1420;
                    layoutElement.preferredHeight = 80;
                    layoutElement.minHeight = 80;
                }

                // 设置按钮文字为完整指令（带彩色图标标签）
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    // 使用TextMeshPro的富文本标签来设置颜色
                    string displayText;
                    if (string.IsNullOrEmpty(command.icon))
                    {
                        displayText = command.command_en;
                    }
                    else
                    {
                        // 解析颜色（默认白色）
                        string colorHex = string.IsNullOrEmpty(command.icon_color) ? "#FFFFFF" : command.icon_color;

                        // 使用富文本标签：<color=#RRGGBB>文本</color>
                        // 图标固定5字符宽度，后面加一个空格
                        displayText = $"<color={colorHex}><b>{command.icon}</b></color> {command.command_en}";
                    }

                    buttonText.text = displayText;
                    buttonText.alignment = TextAlignmentOptions.Left;
                    buttonText.enableWordWrapping = true;
                    buttonText.richText = true; // 启用富文本
                }

                // 设置按钮颜色
                Button button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    // 创建颜色块并应用
                    ColorBlock colors = button.colors;
                    colors.normalColor = normalColor;
                    colors.highlightedColor = highlightColor;
                    colors.pressedColor = pressedColor;
                    colors.selectedColor = selectedColor;
                    colors.colorMultiplier = 1f;
                    colors.fadeDuration = 0.1f;
                    button.colors = colors;

                    // 绑定点击事件
                    string commandText = command.command_en;
                    button.onClick.AddListener(() => SelectCommand(commandText));
                }
            }
        }

        /// <summary>
        /// 选择某个指令
        /// </summary>
        private void SelectCommand(string commandText)
        {
            OnCommandSelected?.Invoke(commandText);
            Hide();
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void Show()
        {
            if (isVisible) return;

            isVisible = true;
            panelRoot.gameObject.SetActive(true);

            // 淡入动画
            DOTween.Kill(canvasGroup);
            canvasGroup.DOFade(1f, fadeInDuration).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });

            // 按钮依次弹出动画
            for (int i = 0; i < commandButtons.Count; i++)
            {
                if (commandButtons[i] != null)
                {
                    RectTransform btnRect = commandButtons[i].GetComponent<RectTransform>();
                    btnRect.localScale = Vector3.zero;
                    btnRect.DOScale(1f, 0.2f).SetDelay(i * 0.05f).SetEase(Ease.OutBack);
                }
            }
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;

            isVisible = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 淡出动画
            DOTween.Kill(canvasGroup);
            canvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() =>
            {
                panelRoot.gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// 切换显示/隐藏
        /// </summary>
        public void Toggle()
        {
            if (isVisible) Hide();
            else Show();
        }

        public bool IsVisible => isVisible;

        private void OnDestroy()
        {
            // 清理所有与此对象相关的DOTween动画
            DOTween.Kill(canvasGroup);

            // 遍历所有按钮并清理缩放动画
            foreach (var btn in commandButtons)
            {
                if (btn != null)
                {
                    DOTween.Kill(btn.transform); // 清理按钮的缩放动画

                    Button button = btn.GetComponent<Button>();
                    if (button != null) button.onClick.RemoveAllListeners();
                }
            }
        }

        private void OnApplicationQuit()
        {
            // 在应用退出时清理所有DOTween实例
            DOTween.Clear();
        }
    }
}
