using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// 通用的“遮罩 + 弹窗”控制组件。
    /// - 弹窗显示时，会同时显示 BackgroundMask，并拦截其他 UI 的点击。
    /// - 点击遮罩（弹窗之外区域）时，会自动关闭弹窗和遮罩。
    /// - 适用于帮助提示、门上的小贴士、二次确认弹窗等所有需要遮罩的弹窗。
    ///
    /// 使用方式（示例：HelpTipsPanel）：
    /// 1. 在 Canvas 下放两个节点：
    ///    - BackgroundMask：全屏 Image（颜色随意），RectTransform 设为 Stretch，铺满画面。
    ///    - HelpTipsPanel：实际内容面板（图片、文字、按钮等）。
    /// 2. 在某个 GameObject 上挂本脚本（可以直接挂在 HelpTipsPanel 上）。
    /// 3. 在 Inspector 里把：
    ///    - Panel 指到 HelpTipsPanel
    ///    - Background Mask 指到 BackgroundMask
    /// 4. 把“帮助按钮”的 OnClick 事件，绑定到本脚本的 Show()。
    /// 5. 把“关闭按钮”的 OnClick 事件，绑定到本脚本的 Hide()。
    ///    （也可以只用点击遮罩关闭，不放单独关闭按钮）
    /// </summary>
    public sealed class BackgroundMaskPopup : MonoBehaviour
    {
        [Header("Popup References")]
        [Tooltip("实际显示内容的弹窗面板，比如 HelpTipsPanel / 门上的提示面板 / 二次确认面板等")]
        [SerializeField] private GameObject panel;

        [Tooltip("全屏遮罩，用于挡住并拦截其他 UI 的点击")]
        [SerializeField] private GameObject backgroundMask;

        [Header("Debug")]
        [Tooltip("是否打印调试日志")]
        [SerializeField] private bool enableDebugLog = true;

        [Header("Behavior")]
        [Tooltip("点击遮罩（弹窗之外区域）时是否关闭弹窗")]
        [SerializeField] private bool closeOnMaskClick = true;

        [Tooltip("是否在弹窗显示时响应 ESC 键关闭（可选）")]
        [SerializeField] private bool closeOnEscapeKey = false;

        /// <summary>
        /// 是否已经完成一次初始化（用于避免首次 Show 之后又被晚到的 Awake/初始化逻辑关掉）。
        /// </summary>
        private bool initialized = false;

        private void Awake()
        {
            if (enableDebugLog)
            {
                Debug.Log(
                    $"[BackgroundMaskPopup] Awake(): panel={(panel != null ? panel.name : "null")}, " +
                    $"backgroundMask={(backgroundMask != null ? backgroundMask.name : "null")}"
                );
            }

            InitializeIfNeeded();
        }

        private void Update()
        {
            if (!closeOnEscapeKey)
            {
                return;
            }

            if (panel != null && panel.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
            {
                if (enableDebugLog)
                {
                    Debug.Log("[BackgroundMaskPopup] Update(): Escape pressed, calling Hide()");
                }
                Hide();
            }
        }

        /// <summary>
        /// 首次初始化弹窗和遮罩的状态。
        /// - 确保面板和遮罩初始为关闭状态；
        /// - 配置遮罩的 Image / Button 等组件。
        /// 该方法只会执行一次，避免晚到的 Awake 把已经 Show 出来的弹窗又关掉。
        /// </summary>
        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            // 初始化时先全部关闭
            if (panel != null)
            {
                panel.SetActive(false);
            }

            SetupBackgroundMask();
        }

        /// <summary>
        /// 初始化遮罩，使其可以挡住其他 UI，并在需要时点击关闭。
        /// </summary>
        private void SetupBackgroundMask()
        {
            if (backgroundMask == null)
            {
                TryCreateRuntimeSiblingMask();
            }

            if (backgroundMask == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[BackgroundMaskPopup] SetupBackgroundMask(): backgroundMask is null (no parent to create sibling mask)");
                }
                return;
            }

            ApplyBackgroundMaskComponents();
        }

        /// <summary>
        /// 配置已存在的 <see cref="backgroundMask"/>：Image、可选点击关闭、初始隐藏。
        /// </summary>
        private void ApplyBackgroundMaskComponents()
        {
            if (backgroundMask == null)
            {
                return;
            }

            // 确保有 Image 组件，并开启 raycastTarget 用于拦截点击
            Image maskImage = backgroundMask.GetComponent<Image>();
            if (maskImage == null)
            {
                maskImage = backgroundMask.AddComponent<Image>();
                if (enableDebugLog)
                {
                    Debug.Log("[BackgroundMaskPopup] ApplyBackgroundMaskComponents(): added Image component to backgroundMask");
                }
            }

            // 如果没有设置颜色，给一个默认半透明黑色（项目里可自行改）
            if (maskImage.color.a <= 0.001f)
            {
                maskImage.color = new Color(0f, 0f, 0f, 0.5f);
                if (enableDebugLog)
                {
                    Debug.Log("[BackgroundMaskPopup] ApplyBackgroundMaskComponents(): set default semi-transparent color for backgroundMask");
                }
            }

            maskImage.raycastTarget = true;

            // 可选：点击遮罩关闭弹窗
            if (closeOnMaskClick)
            {
                Button maskButton = backgroundMask.GetComponent<Button>();
                if (maskButton == null)
                {
                    maskButton = backgroundMask.AddComponent<Button>();
                    if (enableDebugLog)
                    {
                        Debug.Log("[BackgroundMaskPopup] ApplyBackgroundMaskComponents(): added Button component to backgroundMask");
                    }
                }

                maskButton.transition = Selectable.Transition.None;
                maskButton.onClick.RemoveAllListeners();
                maskButton.onClick.AddListener(Hide);

                if (enableDebugLog)
                {
                    Debug.Log("[BackgroundMaskPopup] ApplyBackgroundMaskComponents(): configured backgroundMask Button to call Hide()");
                }
            }

            // 初始隐藏遮罩
            backgroundMask.SetActive(false);

            if (enableDebugLog)
            {
                Debug.Log("[BackgroundMaskPopup] ApplyBackgroundMaskComponents(): backgroundMask.SetActive(false)");
            }
        }

        /// <summary>
        /// 未在 Inspector 指定遮罩时，在同父节点下创建一个全屏 sibling（排在自身之前绘制，靠后 Show 时仍会提到最前）。
        /// 用于提示面板等仅挂了本脚本、未配 BackgroundMask 的预制体。
        /// </summary>
        private void TryCreateRuntimeSiblingMask()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            var go = new GameObject($"{name}_AutoMask", typeof(RectTransform), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            int idx = transform.GetSiblingIndex();
            go.transform.SetSiblingIndex(idx);

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = true;

            backgroundMask = go;

            if (enableDebugLog)
            {
                Debug.Log($"[BackgroundMaskPopup] TryCreateRuntimeSiblingMask(): created {go.name} under {parent.name}");
            }
        }

        /// <summary>
        /// 显示弹窗和遮罩。
        /// </summary>
        public void Show()
        {
            // 旧版本可能在未配置遮罩时已完成 Initialize；此处补建遮罩并补全组件，否则 Show 后无 dimmer / 无法点遮罩关闭。
            if (initialized && backgroundMask == null)
            {
                TryCreateRuntimeSiblingMask();
                if (backgroundMask != null)
                {
                    ApplyBackgroundMaskComponents();
                }
            }

            // 确保在第一次 Show 时也完成初始化，防止先 Show 再 Awake 的调用顺序导致首次点击无效。
            InitializeIfNeeded();

            if (enableDebugLog)
            {
                Debug.Log(
                    $"[BackgroundMaskPopup] Show(): panel={(panel != null ? panel.name : "null")}, " +
                    $"backgroundMask={(backgroundMask != null ? backgroundMask.name : "null")}"
                );
            }

            // 先显示遮罩，保证遮罩在其他 UI 之上
            if (backgroundMask != null)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[BackgroundMaskPopup] Show(): enabling backgroundMask (wasActive={backgroundMask.activeSelf})");
                }
                backgroundMask.SetActive(true);
                backgroundMask.transform.SetAsLastSibling();
            }

            // 再显示弹窗，并保证弹窗在遮罩之上
            if (panel != null)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[BackgroundMaskPopup] Show(): enabling panel (wasActive={panel.activeSelf})");
                }
                panel.SetActive(true);
                panel.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 隐藏弹窗和遮罩。
        /// </summary>
        public void Hide()
        {
            if (panel != null)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[BackgroundMaskPopup] Hide(): disabling panel (wasActive={panel.activeSelf})");
                }
                panel.SetActive(false);
            }

            if (backgroundMask != null)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[BackgroundMaskPopup] Hide(): disabling backgroundMask (wasActive={backgroundMask.activeSelf})");
                }
                backgroundMask.SetActive(false);
            }
        }

        /// <summary>
        /// 供其它脚本查询当前弹窗是否显示中。
        /// </summary>
        public bool IsVisible
        {
            get { return panel != null && panel.activeInHierarchy; }
        }
    }
}

