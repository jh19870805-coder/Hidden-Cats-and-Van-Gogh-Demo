using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HiddenCats.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// Loading 界面控制器
    /// - 显示"Loading..."文字，末尾三个点循环闪烁动画
    /// - 执行资源加载，至少显示2秒
    /// - 加载完成后，执行波点过渡动画进入MainWnd
    /// </summary>
    public sealed class LoadingUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TMP_Text loadingText;

        [Header("加载设置")]
        [SerializeField, Tooltip("最小加载时间，即使资源加载完成也强制等待这么多秒")]
        private float minLoadTime = 2f;

        [Header("Loading 背景色")]
        [Tooltip("Loading 页面的背景色（用于覆盖 UICamera 的黑色背景）")]
        [SerializeField] private Color loadingBackgroundColor = new Color(0.3f, 0.5f, 0.8f, 1f); // 蓝色背景

        [Header("层级设置")]
#pragma warning disable CS0414
        [SerializeField, Tooltip("Loading UI 的 Canvas 排序顺序（最大32767）")]
        private int canvasSortingOrder = 30000;
#pragma warning restore CS0414

        [Header("调试")]
        [Tooltip("启用调试日志")]
        [SerializeField] private bool enableDebugLog = false;

        // 波点动画 sortingOrder = 1000
        // LoadingUI 需要在波点动画下层，所以 sortingOrder < 1000
        private const int k_LoadingSortingOrder = 999;

        private Canvas _canvas;
        private float _startTime;
        private bool _isLoadingComplete = false;
        private bool _transitionStarted = false;
        private Camera _cachedUICamera;
        private Color _originalCameraBackgroundColor;

        private void Awake()
        {
            if (enableDebugLog) Debug.Log("[LoadingUI] Awake() called");

            // 设置 RectTransform 为全屏覆盖（锚点到中心）
            SetupRectTransform();

            // 自动查找组件
            if (loadingText == null)
                loadingText = GetComponentInChildren<TMP_Text>();

            // 确保 Loading UI 显示在过渡效果之上
            EnsureHighestSortingOrder();

            // 验证配置
            ValidateConfiguration();

            // 初始化文字
            UpdateLoadingText(0);
        }

        /// <summary>
        /// 设置 RectTransform 为全屏覆盖
        /// </summary>
        private void SetupRectTransform()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
                if (enableDebugLog) Debug.Log($"[LoadingUI] RectTransform 设置完成: anchorMin={rectTransform.anchorMin}, anchorMax={rectTransform.anchorMax}");
            }
            else
            {
                Debug.LogWarning("[LoadingUI] 未找到 RectTransform！");
            }

            // 调试：检查子元素
            if (enableDebugLog)
            {
                Debug.Log("[LoadingUI] === 子元素列表 ===");
                int childCount = transform.childCount;
                Debug.Log($"[LoadingUI] 子对象数量: {childCount}");
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    var image = child.GetComponent<UnityEngine.UI.Image>();
                    var tmpText = child.GetComponent<TMP_Text>();
                    Debug.Log($"[LoadingUI]   [{i}] {child.name}: Image={image != null}, TMP={tmpText != null}, activeSelf={child.gameObject.activeSelf}");
                    if (image != null)
                    {
                        Debug.Log($"[LoadingUI]      Image: enabled={image.enabled}, color={image.color}, raycastTarget={image.raycastTarget}");
                    }
                    if (tmpText != null)
                    {
                        Debug.Log($"[LoadingUI]      TMP: enabled={tmpText.enabled}, text='{tmpText.text}', color={tmpText.color}");
                    }
                }
                Debug.Log("[LoadingUI] ====================");
            }
        }

        /// <summary>
        /// 验证配置是否正确
        /// </summary>
        private void ValidateConfiguration()
        {
            if (_canvas == null)
            {
                Debug.LogError("[LoadingUI] ValidateConfiguration: _canvas is null!");
                return;
            }

            if (enableDebugLog)
            {
                Debug.Log($"[LoadingUI] ValidateConfiguration:");
                Debug.Log($"  renderMode: {_canvas.renderMode}");
                Debug.Log($"  sortingOrder: {_canvas.sortingOrder}");
                Debug.Log($"  overrideSorting: {_canvas.overrideSorting}");
            }

            // LoadingUI 的 sortingOrder 应该小于 1000（波点动画 sortingOrder）
            // 这样 Logo 和 LoadingText 会被波点动画覆盖
            if (_canvas.sortingOrder != k_LoadingSortingOrder)
            {
                _canvas.sortingOrder = k_LoadingSortingOrder;
            }
            if (_canvas.overrideSorting != true)
            {
                _canvas.overrideSorting = true;
            }
        }

        private void EnsureHighestSortingOrder()
        {
            // 获取或检查 Canvas
            _canvas = GetComponent<Canvas>();

            if (_canvas == null)
            {
                // 预制体没有 Canvas，创建并配置
                _canvas = gameObject.AddComponent<Canvas>();
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();

                // 配置 Canvas
                // 注意：Unity Canvas.sortingOrder 是 short 类型，最大值是 32767
                // 使用 ScreenSpaceOverlay 模式，避免受 UICamera 视口裁剪影响
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = k_LoadingSortingOrder;
                _canvas.overrideSorting = true;

                // 配置 CanvasScaler - 使用 ScaleWithScreenSize 模式实现响应式适配
                // referenceResolution = 2560x1440 (16:9)
                // matchWidthOrHeight 根据屏幕宽高比动态调整，确保 Logo 和 LoadingText 等比例缩放
                // 更宽屏幕(21:9) → 以高适配(matchWidthOrHeight→1)
                // 更窄屏幕(4:3) → 以宽适配(matchWidthOrHeight→0)
                float currentAspect = (float)Screen.width / Screen.height;
                float targetAspect = 16f / 9f;
                float aspectRatio = currentAspect / targetAspect;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2560, 1440);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = aspectRatio >= 1f ? 1f : 0f; // 更宽以高适配，更窄以宽适配

                if (enableDebugLog) Debug.Log($"[LoadingUI] CanvasScaler 配置: ScaleWithScreenSize, referenceResolution=2560x1440, matchWidthOrHeight={scaler.matchWidthOrHeight} (currentAspect={currentAspect:F3}, targetAspect={targetAspect:F3})");
            }
            else
            {
                // 预制体有 Canvas，确保配置正确
                // 注意：Unity Canvas.sortingOrder 是 short 类型，最大值是 32767
                // 使用 ScreenSpaceOverlay 模式，避免受 UICamera 视口裁剪影响
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = k_LoadingSortingOrder;
                _canvas.overrideSorting = true;

                // 检查或添加 CanvasScaler
                CanvasScaler existingScaler = GetComponent<CanvasScaler>();
                if (existingScaler == null)
                {
                    existingScaler = gameObject.AddComponent<CanvasScaler>();
                    if (enableDebugLog) Debug.Log("[LoadingUI] 预制体缺少 CanvasScaler，已添加");
                }

                // 配置 CanvasScaler - 使用 ScaleWithScreenSize 模式实现响应式适配
                // 更宽屏幕(21:9) → 以高适配
                // 更窄屏幕(4:3) → 以宽适配
                float currentAspect = (float)Screen.width / Screen.height;
                float targetAspect = 16f / 9f;
                float aspectRatio = currentAspect / targetAspect;

                existingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                existingScaler.referenceResolution = new Vector2(2560, 1440);
                existingScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                existingScaler.matchWidthOrHeight = aspectRatio >= 1f ? 1f : 0f;

                if (enableDebugLog)
                {
                    Debug.Log($"[LoadingUI] CanvasScaler 配置: ScaleWithScreenSize, referenceResolution=2560x1440, matchWidthOrHeight={existingScaler.matchWidthOrHeight}");
                    Debug.Log($"[LoadingUI] 实际 Canvas sortingOrder = {_canvas.sortingOrder}, overrideSorting = {_canvas.overrideSorting}");

                    // 打印当前所有 Canvas 的排序顺序，方便调试
                    Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                    foreach (var c in allCanvases)
                    {
                        Debug.Log($"[LoadingUI] 发现 Canvas: {c.name}, sortingOrder={c.sortingOrder}, overrideSorting={c.overrideSorting}, renderMode={c.renderMode}, worldCamera={c.worldCamera?.name ?? "null"}");
                    }
                }
            }
        }

        /// <summary>
        /// 获取 UICamera，优先从 LetterboxController 获取
        /// </summary>
        private Camera GetUICamera()
        {
            // 优先从 LetterboxController 获取 UICamera
            if (LetterboxController.Instance != null)
            {
                Camera uiCamera = LetterboxController.Instance.GetComponent<Camera>();
                if (uiCamera != null)
                {
                    if (enableDebugLog) Debug.Log($"[LoadingUI] 使用 LetterboxController 的相机: {uiCamera.name}");
                    return uiCamera;
                }
            }

            // 备选方案：按名称查找 UICamera
            GameObject uiCameraObj = GameObject.Find("UICamera");
            if (uiCameraObj != null)
            {
                Camera cam = uiCameraObj.GetComponent<Camera>();
                if (cam != null)
                {
                    if (enableDebugLog) Debug.Log($"[LoadingUI] 使用 UICamera (按名称查找): {uiCameraObj.name}");
                    return cam;
                }
            }

            // 最后的备选：使用 Camera.main
            if (enableDebugLog) Debug.LogWarning("[LoadingUI] 未找到 UICamera，使用 Camera.main");
            return Camera.main;
        }

        /// <summary>
        /// 刷新 worldCamera 引用（用于运行时更新）
        /// </summary>
        private void RefreshWorldCamera()
        {
            if (_canvas != null)
            {
                Camera newCamera = GetUICamera();
                _canvas.worldCamera = newCamera;
                if (enableDebugLog) Debug.Log($"[LoadingUI] RefreshWorldCamera: worldCamera 设置为 {newCamera?.name ?? "null"}");
            }
        }

        private void OnEnable()
        {
            if (enableDebugLog) Debug.Log("[LoadingUI] OnEnable() called - 开始加载流程");
            _startTime = Time.time;
            _isLoadingComplete = false;
            _transitionStarted = false;

            // 缓存 UICamera 引用
            _cachedUICamera = GetUICamera();

            // 确保 Canvas 配置正确（这会更新 _canvas 引用）
            EnsureHighestSortingOrder();

            // 再次验证配置（防止配置被覆盖）
            ValidateConfiguration();

            // 设置 LetterboxController 的 Loading 阶段背景色
            LetterboxController.Instance?.SetLoadingPhaseColor(loadingBackgroundColor);

            // 通知 LetterboxController 进入 Loading 阶段
            LetterboxController.Instance?.SetLoadingPhase(true);

            // 临时改变 UICamera 背景色为 Loading 背景色
            ApplyLoadingBackgroundColor();

            StartCoroutine(LoadResourcesAndEnterMainCoroutine());
        }

        private void OnDisable()
        {
            // 注意：不在这里通知 LetterboxController 退出 Loading 阶段
            // 因为 OnDisable 可能在过渡开始时就被调用
            // LetterboxController 的退出将在 EnterMainWindow 的过渡完成回调中处理
        }

        /// <summary>
        /// 临时改变 UICamera 背景色为 Loading 背景色
        /// 注意：仅在 ScreenSpaceCamera 模式下需要改变 Camera 背景色
        /// </summary>
        private void ApplyLoadingBackgroundColor()
        {
            // ScreenSpaceOverlay 模式下不需要改变 Camera 背景色
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (enableDebugLog) Debug.Log("[LoadingUI] ScreenSpaceOverlay 模式，不需要改变 Camera 背景色");
                return;
            }

            if (_cachedUICamera != null)
            {
                _originalCameraBackgroundColor = _cachedUICamera.backgroundColor;
                _cachedUICamera.backgroundColor = loadingBackgroundColor;
                if (enableDebugLog) Debug.Log($"[LoadingUI] 临时改变 UICamera 背景色: {_originalCameraBackgroundColor} -> {loadingBackgroundColor}");
            }
        }

        /// <summary>
        /// 恢复 UICamera 原始背景色
        /// 注意：仅在 ScreenSpaceCamera 模式下需要恢复 Camera 背景色
        /// </summary>
        private void RestoreOriginalBackgroundColor()
        {
            // ScreenSpaceOverlay 模式下不需要恢复 Camera 背景色
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return;
            }

            if (_cachedUICamera != null)
            {
                _cachedUICamera.backgroundColor = _originalCameraBackgroundColor;
                if (enableDebugLog) Debug.Log($"[LoadingUI] 恢复 UICamera 背景色: {_cachedUICamera.backgroundColor}");
            }
        }

        private void Update()
        {
            // 更新Loading文字动画
            if (!_isLoadingComplete)
            {
                float elapsed = Time.time - _startTime;
                int dotCount = Mathf.FloorToInt(elapsed) % 4;
                UpdateLoadingText(dotCount);
            }
        }

        private void UpdateLoadingText(int dotCount)
        {
            if (loadingText != null)
            {
                loadingText.text = "Loading" + new string('.', dotCount);
            }
        }

        private IEnumerator LoadResourcesAndEnterMainCoroutine()
        {
            // 开始异步加载所有Resources资源
            // 使用Resources.LoadAll预加载常用资源
            var loadOp = Resources.LoadAsync("LanguageConfig");
            yield return loadOp;

            // 预加载音效配置
            loadOp = Resources.LoadAsync("PuzzleData");
            yield return loadOp;

            // 预加载一些关键资源
            loadOp = Resources.LoadAsync("LocalizationTable");
            yield return loadOp;

            // 强制等待至少 minLoadTime 秒
            float elapsed = Time.time - _startTime;
            if (elapsed < minLoadTime)
            {
                yield return new WaitForSeconds(minLoadTime - elapsed);
            }

            _isLoadingComplete = true;

            // 开始过渡进入MainWnd
            EnterMainWindow();
        }

        private void EnterMainWindow()
        {
            if (enableDebugLog) Debug.Log("[LoadingUI] EnterMainWindow() called");
            if (_transitionStarted)
            {
                Debug.LogWarning("[LoadingUI] 过渡已开始，跳过");
                return;
            }
            _transitionStarted = true;

            // 标记Loading结束，允许WindowManager处理后续切换
            WindowManager.IsInLoadingPhase = false;
            if (enableDebugLog) Debug.Log("[LoadingUI] 已设置 IsInLoadingPhase = false");

            // 确保WindowManager存在
            if (WindowManager.Instance == null)
            {
                Debug.LogError("[LoadingUI] WindowManager not found!");
                return;
            }

            // 获取MainWnd预制体
            GameObject mainWndPrefab = WindowManager.Instance.GetMainWndPrefab();
            if (mainWndPrefab == null)
            {
                Debug.LogError("[LoadingUI] MainWndPrefab not set in WindowManager!");
                return;
            }

            // 获取首次进入的特殊过渡配置
            TransitionConfig config = WindowManager.Instance.GetFirstEntryTransitionConfig();

            // 记录是否需要手动恢复背景色（当有过渡效果时，背景色会在过渡中恢复）
            bool needsManualRestore = (WindowTransitionEffect.Instance == null || config == null);

            // 执行过渡
            if (WindowTransitionEffect.Instance != null && config != null)
            {
                WindowTransitionEffect.Instance.PerformTransition(
                    config,
                    () =>
                    {
                        // 过渡开始时隐藏Loading，并切换到MainWnd（跳过过渡效果，避免重复播放）
                        gameObject.SetActive(false);
                        WindowManager.Instance.PublicSwitchToWindow(mainWndPrefab, skipTransition: true);
                    },
                    () =>
                    {
                        // 过渡完成回调：恢复UICamera背景色和LetterboxController状态
                        RestoreOriginalBackgroundColor();
                        LetterboxController.Instance?.SetLoadingPhase(false);
                    }
                );
            }
            else
            {
                // 直接切换：立即恢复
                RestoreOriginalBackgroundColor();
                LetterboxController.Instance?.SetLoadingPhase(false);
                gameObject.SetActive(false);
                WindowManager.Instance.PublicSwitchToWindow(mainWndPrefab, skipTransition: true);
            }
        }
    }
}
