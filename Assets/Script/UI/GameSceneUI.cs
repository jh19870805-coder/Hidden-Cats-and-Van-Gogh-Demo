using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using HiddenCats.UI;

/// <summary>
/// Game scene UI controller for RoomWnd.
/// Manages objective panel, progress text, hint button, pause menu, and navigation buttons.
/// </summary>
public sealed class GameSceneUI : MonoBehaviour
{
    [Header("Zoom & Pan (Content Only)")]
    [Tooltip("Enable mouse wheel zoom + mouse drag pan for scene content. UI stays fixed.")]
    [SerializeField] private bool enableZoomPan = true;

    [Tooltip("Minimum scale multiplier for the content root.")]
    [SerializeField] private float minZoomScale = 0.5f;

    [Tooltip("Maximum scale multiplier for the content root.")]
    [SerializeField] private float maxZoomScale = 2.0f;

    [Tooltip("Mouse wheel zoom speed.")]
    [SerializeField] private float scrollZoomSpeed = 0.15f;

    [Tooltip("Allow a bit of overscroll (in pixels) beyond the strict bounds to make dragging feel nicer.")]
    [SerializeField] private float overscrollPixels = 20f;

    [Tooltip("Mouse button to drag with. 0=Left, 1=Right, 2=Middle.")]
    [SerializeField] private int dragButton = 0;

    [Tooltip("Drag sensitivity multiplier. Lower = slower drag, Higher = faster drag.")]
    [SerializeField] private float dragSensitivity = 1.0f;

    [Tooltip("Reference screen resolution used to normalize drag sensitivity across all resolutions. Set to the resolution where the feel is correct (e.g. 2560x1440).")]
    [SerializeField] private Vector2 referenceScreenResolution = new Vector2(2560f, 1440f);

    [Tooltip("If enabled, dragging won't start when the pointer is over excluded UI roots (e.g., buttons).")]
    [SerializeField] private bool ignoreDragWhenPointerOverUI = true;

    [Tooltip("Small deadzone (in reference resolution pixels) before a drag actually starts.")]
    [SerializeField] private float dragDeadzonePixels = 2f;

    [Tooltip("Names of roots to exclude from the content root (UI stays fixed). Case-insensitive exact name match.")]
    [SerializeField] private List<string> excludeFromContentRoot = new List<string> { "Ui", "UI", "WinPop", "SettingPop", "RankPop", "CatHand" };

        [Tooltip("Optional: RectTransform used as content bounds reference (e.g., RoomBg). If null, the largest RectTransform under content root will be auto-selected at runtime.")]
        [SerializeField] private RectTransform contentBoundsReference;

    [Header("Initial view (first open / after reset memory)")]
    [Tooltip("首次进入本窗口且尚无浏览记忆时的内容缩放（__ContentRoot.localScale 的 xy）。≤0 时使用内容根建立时的默认缩放（通常为 1）。")]
    [SerializeField] private float initialContentZoom;

    [Tooltip("首次进入本窗口且尚无浏览记忆时 __ContentRoot 的 anchoredPosition。")]
    [SerializeField] private Vector2 initialContentPan;

    [Tooltip("If enabled, attach a nested Canvas to __ContentRoot to isolate UI rebuild cost during pan/zoom. NOTE: this can bypass parent masking/clipping in some UI setups.")]
    [SerializeField] private bool useSubCanvasForContent = false;

    [Header("Objective UI")]
    [SerializeField] private GameObject objectivePanel;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Hint Button")]
    [SerializeField] private Button hintButton;
    [SerializeField] private TextMeshProUGUI hintCountText;
    [SerializeField] private GameObject hintButtonDisabled;

    [Header("Navigation Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button backButton;

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private RectTransform _windowRoot;
    private RectTransform _contentRoot;
    private Vector3 _baseContentScale = Vector3.one;
    private RectTransform _contentBoundsRefRuntime;
    private Vector2 _cachedContentBoundsSize;
    private bool _isDragging;
    private Vector2 _dragStartPointerLocal;
    private Vector2 _dragStartAnchoredPos;
    private bool _dragMovedBeyondDeadzone;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);

    private void Awake()
    {
        SetupZoomPanIfNeeded();
        AutoWireComponentsIfNeeded();
        SetupEventListeners();

        // Initialize pause menu as hidden
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (!enableZoomPan)
        {
            return;
        }

        if (_windowRoot == null || _contentRoot == null)
        {
            SetupZoomPanIfNeeded();
        }

        if (_windowRoot == null || _contentRoot == null)
        {
            return;
        }

        CacheContentBoundsReferenceAndSize();
        RestoreOrApplySavedOrInitialView();
    }

    private void OnDisable()
    {
        SaveZoomPanStateIfNeeded();
    }

    private void Update()
    {
        if (!enableZoomPan)
        {
            return;
        }

        if (_windowRoot == null || _contentRoot == null)
        {
            return;
        }

        HandleScrollZoom();
        HandleDragPan();
    }

    private void SetupZoomPanIfNeeded()
    {
        // IMPORTANT:
        // This component may be placed on a small helper node inside the window prefab (often 100x100),
        // but zoom/pan must operate in the coordinate space of the *top-level window instance*.
        // If we use this.transform as the window root, __ContentRoot gets created under that tiny node
        // and we end up reparenting/scaling the wrong subtree (can look like "tiled/repeated" content).
        _windowRoot = ResolveWindowRoot();
        if (_windowRoot == null)
        {
            // Only supports UI (RectTransform) roots.
            enableZoomPan = false;
            return;
        }

        if (!enableZoomPan)
        {
            return;
        }

        _contentRoot = EnsureContentRoot();
        if (_contentRoot == null)
        {
            enableZoomPan = false;
            return;
        }

        _baseContentScale = _contentRoot.localScale;
        CacheContentBoundsReferenceAndSize();
        ClampContentWithinBounds();
    }

    private string GetZoomPanStorageKey()
    {
        return _windowRoot != null ? _windowRoot.gameObject.name : null;
    }

    private void SaveZoomPanStateIfNeeded()
    {
        if (!enableZoomPan || _contentRoot == null)
        {
            return;
        }

        string key = GetZoomPanStorageKey();
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        GameSceneZoomPanPersistence.Save(key, _contentRoot.localScale, _contentRoot.anchoredPosition);
    }

    private void RestoreOrApplySavedOrInitialView()
    {
        if (!enableZoomPan || _contentRoot == null || _windowRoot == null)
        {
            return;
        }

        string key = GetZoomPanStorageKey();
        if (!string.IsNullOrEmpty(key) && GameSceneZoomPanPersistence.TryGet(key, out Vector3 savedScale, out Vector2 savedPos))
        {
            _contentRoot.localScale = savedScale;
            _contentRoot.anchoredPosition = savedPos;
        }
        else
        {
            ApplyInitialViewFromInspector();
        }

        CacheContentBoundsReferenceAndSize();
        ClampContentWithinBounds();
    }

    private void ApplyInitialViewFromInspector()
    {
        if (_contentRoot == null)
        {
            return;
        }

        float z = initialContentZoom > 0f ? initialContentZoom : _baseContentScale.x;
        _contentRoot.localScale = new Vector3(z, z, 1f);
        _contentRoot.anchoredPosition = initialContentPan;
    }

    /// <summary>
    /// 将视图恢复为 Inspector 中的「初始缩放/位置」，并清除本窗口在会话内的浏览记忆（下次进入将按初始状态）。
    /// 游戏内「重置进度」会清空所有窗口的浏览记忆，效果类似。
    /// </summary>
    public void ResetViewToInitialDefaults()
    {
        if (!enableZoomPan || _contentRoot == null)
        {
            return;
        }

        string key = GetZoomPanStorageKey();
        GameSceneZoomPanPersistence.Clear(key);
        ApplyInitialViewFromInspector();
        CacheContentBoundsReferenceAndSize();
        ClampContentWithinBounds();
        _isDragging = false;
        _dragMovedBeyondDeadzone = false;
    }

    private RectTransform ResolveWindowRoot()
    {
        // Walk up from this component's GameObject toward the scene root.
        // Stop at the first valid window root (not MainWndCanvas, not __ContentRoot).
        // Window hierarchy: Canvas -> windowRoot -> __ContentRoot -> (content)
        // GameSceneUI sits under windowRoot (not __ContentRoot).
        Transform t = transform;
        while (t != null)
        {
            // Skip the main canvas
            if (t.name == "MainWndCanvas")
            {
                t = t.parent;
                continue;
            }

            // Skip __ContentRoot - it's created by EnsureContentRoot and is not the window root
            if (t.name == "__ContentRoot")
            {
                t = t.parent;
                continue;
            }

            var rt = t as RectTransform;
            if (rt != null)
            {
                // Found a candidate - check if this is the actual window root
                // Window roots are typically direct children of Canvas with stretch anchors
                // and are the parent of __ContentRoot
                Transform parent = t.parent;
                if (parent != null)
                {
                    // Check if parent is MainWndCanvas (direct child of canvas = window root)
                    if (parent.name == "MainWndCanvas")
                    {
                        return rt;
                    }
                }

                // Also check if this has the pattern of a window root
                // (RectTransform with Canvas or being a direct child of window hierarchy)
                if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
                {
                    // This looks like a stretch anchor - likely the window root
                    // But verify it's not __ContentRoot (which also has stretch anchors)
                    return rt;
                }
            }

            t = t.parent;
        }

        // Ultimate fallback: use our own RectTransform
        return transform as RectTransform;
    }

    private void CacheContentBoundsReferenceAndSize()
    {
        _contentBoundsRefRuntime = contentBoundsReference != null ? contentBoundsReference : FindLargestRectTransformUnder(_contentRoot);
        if (_contentBoundsRefRuntime != null)
        {
            // Cache unscaled size once to avoid per-frame scanning.
            _cachedContentBoundsSize = _contentBoundsRefRuntime.rect.size;
        }
        else
        {
            // Fallback: safe default (window size). This prevents NaN/huge clamp math.
            _cachedContentBoundsSize = _windowRoot != null ? _windowRoot.rect.size : Vector2.zero;
        }
    }

    private static RectTransform FindLargestRectTransformUnder(RectTransform root)
    {
        if (root == null)
        {
            return null;
        }

        RectTransform best = null;
        float bestArea = 0f;

        // Include inactive because some windows keep content inactive initially.
        RectTransform[] all = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            RectTransform rt = all[i];
            if (rt == null || rt == root)
            {
                continue;
            }

            var size = rt.rect.size;
            float area = Mathf.Abs(size.x * size.y);
            if (area > bestArea && area > 1f)
            {
                bestArea = area;
                best = rt;
            }
        }

        return best;
    }

    private RectTransform EnsureContentRoot()
    {
        // If already created, reuse.
        var existing = _windowRoot.Find("__ContentRoot") as RectTransform;
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("__ContentRoot", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_windowRoot, worldPositionStays: false);
        rt.SetAsFirstSibling(); // keep content behind UI
        // Stretch to fill the window so scaling/positioning doesn't interact with zero-size rects.
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.sizeDelta = Vector2.zero;

        // Optional: isolate pan/zoom rebuild cost by adding a nested Canvas.
        // This prevents changes under __ContentRoot from forcing the entire window UI to rebuild.
        if (useSubCanvasForContent)
        {
            var subCanvas = go.AddComponent<Canvas>();
            // Keep default sorting so we don't accidentally bypass masking/clipping setups.
            subCanvas.overrideSorting = false;

            // Ensure raycasts still work for content (cats/fish might be UI Graphics).
            if (go.GetComponent<GraphicRaycaster>() == null)
            {
                go.AddComponent<GraphicRaycaster>();
            }
        }

        // Move all direct children (except excluded roots + our new root) under content root.
        // This keeps UI fixed by leaving excluded roots as direct children of window root.
        var childrenToMove = new List<Transform>(16);
        for (int i = 0; i < _windowRoot.childCount; i++)
        {
            var child = _windowRoot.GetChild(i);
            if (child == rt)
            {
                continue;
            }

            if (IsExcludedRootName(child.name))
            {
                continue;
            }

            // Also exclude NumUI from being moved to __ContentRoot - it needs to stay under Ui for proper rendering
            if (child.name == "NumUI")
            {
                Debug.LogWarning($"[GameSceneUI] Skipping NumUI from being moved to __ContentRoot - ensuring it stays in original hierarchy");
                continue;
            }

            if (child.name == "CatHand")
            {
                continue;
            }

            childrenToMove.Add(child);
        }

        for (int i = 0; i < childrenToMove.Count; i++)
        {
            childrenToMove[i].SetParent(rt, worldPositionStays: false);
        }

        // Also check if NumUI is inside __ContentRoot and needs to be moved back to a proper UI parent
        var numUIInContentRoot = rt.Find("NumUI");
        if (numUIInContentRoot != null)
        {
            Debug.LogWarning($"[GameSceneUI] Found NumUI inside __ContentRoot - moving it to window root for proper rendering");
            // Move to window root, but try to preserve position
            numUIInContentRoot.SetParent(_windowRoot, worldPositionStays: true);
            // Make it a sibling after excluded roots
            numUIInContentRoot.SetAsLastSibling();
        }

        return rt;
    }

    private bool IsExcludedRootName(string name)
    {
        if (excludeFromContentRoot == null || excludeFromContentRoot.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < excludeFromContentRoot.Count; i++)
        {
            var ex = excludeFromContentRoot[i];
            if (string.IsNullOrEmpty(ex))
            {
                continue;
            }

            if (string.Equals(name, ex, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleScrollZoom()
    {
        if (IsSettlementPopupVisible())
            return;

        Vector2 scroll = Input.mouseScrollDelta;
        if (Mathf.Abs(scroll.y) <= 0.0001f)
        {
            return;
        }

        float currentScale = _contentRoot.localScale.x;

        // Use additive zoom instead of multiplicative to avoid compounding issues
        float zoomDelta = scroll.y * scrollZoomSpeed;
        float targetScale = currentScale + zoomDelta;

        // Calculate dynamic min scale: content height should equal window height
        float dynamicMinScale = CalculateDynamicMinScale();

        // Clamp to absolute min/max (not relative to base scale)
        float minAbs = dynamicMinScale;
        float maxAbs = maxZoomScale;
        targetScale = Mathf.Clamp(targetScale, minAbs, maxAbs);

        if (Mathf.Approximately(targetScale, currentScale))
        {
            return;
        }

        // Zoom centered on content root (smoother, no position jump)
        _contentRoot.localScale = new Vector3(targetScale, targetScale, 1f);

        ClampContentWithinBounds();
    }

    private float CalculateDynamicMinScale()
    {
        if (_windowRoot == null || _cachedContentBoundsSize.sqrMagnitude <= 0.0001f)
        {
            return minZoomScale;
        }

        float windowHeight = _windowRoot.rect.size.y;

        // Calculate actual rendered content height
        // Account for localScale of bounds reference (e.g., RoomBg has localScale 0.5)
        float contentHeight = _cachedContentBoundsSize.y;
        if (_contentBoundsRefRuntime != null)
        {
            contentHeight *= _contentBoundsRefRuntime.localScale.y;
        }

        if (contentHeight <= 0f)
        {
            return minZoomScale;
        }

        // Minimum scale is when content height equals window height
        float calculatedMinScale = windowHeight / contentHeight;

        // Use the larger of the calculated value and the serialized minZoomScale
        // (in case user wants a larger minimum)
        return Mathf.Max(calculatedMinScale, minZoomScale);
    }

    private void HandleDragPan()
    {
        if (IsSettlementPopupVisible())
        {
            _isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(dragButton))
        {
            if (ignoreDragWhenPointerOverUI && IsPointerOverExcludedUI())
            {
                _isDragging = false;
                return;
            }

            _isDragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(_windowRoot, Input.mousePosition, null, out _dragStartPointerLocal);
            _dragStartAnchoredPos = _contentRoot.anchoredPosition;
            _dragMovedBeyondDeadzone = false;
        }

        if (!Input.GetMouseButton(dragButton))
        {
            _isDragging = false;
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_windowRoot, Input.mousePosition, null, out var pointerLocalNow);
        Vector2 delta = pointerLocalNow - _dragStartPointerLocal;

        // Get window and content sizes for proper normalization
        Vector2 windowSize = _windowRoot.rect.size;
        Vector2 contentSize = _cachedContentBoundsSize.sqrMagnitude > 0.0001f
            ? _cachedContentBoundsSize
            : windowSize;

        // Calculate the ratio between content and window sizes
        // This ensures drag feels consistent regardless of content scale
        float contentToWindowRatioX = windowSize.x > 0 ? contentSize.x / windowSize.x : 1f;
        float contentToWindowRatioY = windowSize.y > 0 ? contentSize.y / windowSize.y : 1f;

        // Normalize delta to reference resolution, then scale by content ratio
        float refW = referenceScreenResolution.x > 0f ? referenceScreenResolution.x : Screen.width;
        float refH = referenceScreenResolution.y > 0f ? referenceScreenResolution.y : Screen.height;
        float normX = refW / Screen.width;
        float normY = refH / Screen.height;

        // Apply both screen normalization and content scale correction
        Vector2 normalizedDelta = new Vector2(
            delta.x * normX * contentToWindowRatioX,
            delta.y * normY * contentToWindowRatioY
        );

        if (!_dragMovedBeyondDeadzone && normalizedDelta.magnitude < dragDeadzonePixels)
        {
            return;
        }

        _dragMovedBeyondDeadzone = true;
        // Apply sensitivity multiplier to normalized delta
        Vector2 targetPos = _dragStartAnchoredPos + normalizedDelta * dragSensitivity;
        if ((_contentRoot.anchoredPosition - targetPos).sqrMagnitude > 0.0001f)
        {
            _contentRoot.anchoredPosition = targetPos;
        }
        ClampContentWithinBounds();
    }

    private bool IsPointerOverExcludedUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        _raycastResults.Clear();
        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        EventSystem.current.RaycastAll(pointer, _raycastResults);
        if (_raycastResults.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var go = _raycastResults[i].gameObject;
            if (go == null)
            {
                continue;
            }

            // If the hit is NOT under content root, it's likely UI (since we moved most content under __ContentRoot).
            if (_contentRoot != null && !go.transform.IsChildOf(_contentRoot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 竞速结算弹窗（WinPop / RankPop）显示时，应禁用缩放/拖动。
    /// </summary>
    private bool IsSettlementPopupVisible()
    {
        if (_windowRoot == null)
            return false;

        Transform winPop = _windowRoot.Find("WinPop");
        if (winPop != null && winPop.gameObject.activeSelf)
            return true;

        Transform rankPop = _windowRoot.Find("RankPop");
        if (rankPop != null && rankPop.gameObject.activeSelf)
            return true;

        return false;
    }

    private void ClampContentWithinBounds()
    {
        if (_windowRoot == null || _contentRoot == null)
        {
            return;
        }

        Vector2 windowSize = _windowRoot.rect.size;

        // Get the base content size (unscaled RectTransform size)
        Vector2 baseContentSize = _cachedContentBoundsSize.sqrMagnitude > 0.0001f ? _cachedContentBoundsSize : _windowRoot.rect.size;

        // Calculate actual rendered content size
        // Account for localScale of bounds reference (e.g., RoomBg has localScale 0.5)
        float contentScaleX = _contentRoot.localScale.x;
        float contentScaleY = _contentRoot.localScale.y;

        if (_contentBoundsRefRuntime != null)
        {
            contentScaleX *= _contentBoundsRefRuntime.localScale.x;
            contentScaleY *= _contentBoundsRefRuntime.localScale.y;
        }

        Vector2 scaledContent = new Vector2(
            baseContentSize.x * contentScaleX,
            baseContentSize.y * contentScaleY
        );

        // Calculate the range for content to stay within window bounds
        // Content can move freely within the window, but not go outside
        float maxX = 0f;
        float maxY = 0f;

        if (scaledContent.x > windowSize.x)
        {
            // Content is wider than window: can move horizontally
            maxX = (scaledContent.x - windowSize.x) * 0.5f;
        }
        // If content fits in window horizontally, maxX stays 0 (centered)

        if (scaledContent.y > windowSize.y)
        {
            // Content is taller than window: can move vertically
            maxY = (scaledContent.y - windowSize.y) * 0.5f;
        }
        // If content fits in window vertically, maxY stays 0 (centered)

        Vector2 pos = _contentRoot.anchoredPosition;
        float overscroll = Mathf.Max(0f, overscrollPixels);
        float clampedX = Mathf.Clamp(pos.x, -maxX - overscroll, maxX + overscroll);
        float clampedY = Mathf.Clamp(pos.y, -maxY - overscroll, maxY + overscroll);
        // Only write back if actually changed (prevents redundant UI rebuilds).
        if (Mathf.Abs(clampedX - pos.x) > 0.0001f || Mathf.Abs(clampedY - pos.y) > 0.0001f)
        {
            _contentRoot.anchoredPosition = new Vector2(clampedX, clampedY);
        }
    }

    private void OnTransformChildrenChanged()
    {
        // If window content hierarchy changes at runtime, refresh cached bounds.
        // This avoids stale clamp ranges without doing per-frame scanning.
        if (!enableZoomPan)
        {
            return;
        }
        if (_contentRoot == null || _windowRoot == null)
        {
            return;
        }
        CacheContentBoundsReferenceAndSize();
    }

    /// <summary>
    /// 尝试在未手动绑定的情况下，按约定名称自动查找常用 UI 组件，
    /// 防止因为 Inspector 漏填引用导致按钮点击无反应。
    /// </summary>
    private void AutoWireComponentsIfNeeded()
    {
        // Navigation buttons
        if (backButton == null)
        {
            var back = transform.Find("BackBtn");
            if (back != null)
            {
                backButton = back.GetComponent<Button>();
            }
        }

        if (pauseButton == null)
        {
            var pause = transform.Find("PauseBtn");
            if (pause != null)
            {
                pauseButton = pause.GetComponent<Button>();
            }
        }

        // Hint button and its disabled visual root
        if (hintButton == null)
        {
            var hint = transform.Find("HintBtn");
            if (hint != null)
            {
                hintButton = hint.GetComponent<Button>();
            }
        }

        if (hintButtonDisabled == null)
        {
            var disabledRoot = transform.Find("HintBtnDisabled");
            if (disabledRoot != null)
            {
                hintButtonDisabled = disabledRoot.gameObject;
            }
        }

        // Pause menu panel
        if (pauseMenuPanel == null)
        {
            var pauseMenu = transform.Find("PauseMenuPanel");
            if (pauseMenu != null)
            {
                pauseMenuPanel = pauseMenu.gameObject;
            }
        }
    }

    private void SetupEventListeners()
    {
        // Hint button
        if (hintButton != null)
        {
            hintButton.onClick.AddListener(OnClick_Hint);
        }

        // Pause button
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(OnClick_Pause);
        }

        // BackBtn: do NOT add a runtime listener here. RoomWnd prefab already wires
        // BackBtn to RoomWndUI. A second listener was
        // firing after that and could call ShowMainWindow(), overriding the correct navigation.

        // Pause menu buttons
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(OnClick_Resume);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnClick_Settings);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnClick_Quit);
        }
    }

    private void OnClick_Hint()
    {
        // Try to trigger hint via HintMagnifierService
        if (HintMagnifierService.Instance != null)
        {
            // HintMagnifierService handles the actual hint logic
            Debug.Log("[GameSceneUI] Hint button clicked - HintMagnifierService will handle");
        }
        else
        {
            DialogService.ShowInfo("Hint", "Hint system is not available in this scene.");
        }
    }

    private void OnClick_Pause()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
        // TODO: Pause game logic if needed
    }

    private void OnClick_Resume()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
        // TODO: Resume game logic if needed
    }

    private void OnClick_Settings()
    {
        if (WindowManager.Instance != null)
        {
            WindowManager.Instance.ShowSettingPopup();
        }
        else
        {
            Debug.LogError("[GameSceneUI] WindowManager.Instance is null.");
        }
    }

    private void OnClick_Quit()
    {
        // Show quit confirmation dialog
        DialogService.ShowConfirmCancel(
            "Quit Game",
            "Are you sure you want to quit?",
            "Quit",
            "Cancel",
            () =>
            {
                Debug.Log("[GameSceneUI] User confirmed quit");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            },
            () =>
            {
                Debug.Log("[GameSceneUI] User cancelled quit");
            }
        );
    }

    /// <summary>
    /// Update the objective text.
    /// </summary>
    public void SetObjectiveText(string text)
    {
        if (objectiveText != null)
        {
            objectiveText.text = text;
        }
    }

    /// <summary>
    /// Update the progress text.
    /// </summary>
    public void SetProgressText(string text)
    {
        if (progressText != null)
        {
            progressText.text = text;
        }
    }

    /// <summary>
    /// Update the hint count text.
    /// </summary>
    public void SetHintCount(int count)
    {
        if (hintCountText != null)
        {
            hintCountText.text = count.ToString();
        }

        // Enable/disable hint button based on count
        if (hintButton != null)
        {
            hintButton.interactable = count > 0;
        }

        if (hintButtonDisabled != null)
        {
            hintButtonDisabled.SetActive(count <= 0);
        }
    }

    /// <summary>
    /// Show or hide the objective panel.
    /// </summary>
    public void SetObjectivePanelVisible(bool visible)
    {
        if (objectivePanel != null)
        {
            objectivePanel.SetActive(visible);
        }
    }
}
