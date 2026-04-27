using UnityEngine;
using UnityEngine.UI;
using HiddenCats.UI;

/// <summary>
/// Maintains a fixed aspect ratio for the game, adding black bars (letterboxing/pillarboxing)
/// when the window is resized to a different aspect ratio.
///
/// Target resolution: 2560x1440 (16:9).
///
/// This controller:
/// 1. Finds the main UICamera (ScreenSpaceCamera Canvas)
/// 2. Sets its rect to create black bars
/// 3. Updates its background color to match the letterbox bars
/// 4. Provides a full-screen RawImage as visual backup
/// </summary>
public class LetterboxController : MonoBehaviour
{
    public static LetterboxController Instance { get; private set; }

    /// <summary>
    /// The visible game area in screen coordinates (0-1 normalized).
    /// Use this to check if mouse is within the game area.
    /// </summary>
    public Rect GameArea => _mainCamera != null ? _mainCamera.rect : new Rect(0f, 0f, 1f, 1f);

    /// <summary>
    /// The visible game area in absolute screen pixel coordinates.
    /// </summary>
    public Rect GetGameAreaInPixels()
    {
        if (_mainCamera == null) return new Rect(0, 0, Screen.width, Screen.height);

        Rect normalizedRect = _mainCamera.rect;
        return new Rect(
            normalizedRect.x * Screen.width,
            normalizedRect.y * Screen.height,
            normalizedRect.width * Screen.width,
            normalizedRect.height * Screen.height
        );
    }

    /// <summary>
    /// Check if a screen position is within the visible game area.
    /// </summary>
    public bool IsPositionInGameArea(Vector2 screenPosition)
    {
        Rect pixelArea = GetGameAreaInPixels();
        return pixelArea.Contains(screenPosition);
    }

    [Header("Target Aspect Ratio")]
    [Tooltip("The target aspect ratio to maintain (width / height). Default 16:9 = 1.777... (2560x1440)")]
    [SerializeField] private float targetAspectRatio = 2560f / 1440f;

    [Header("Reference Resolution (for scaling reference)")]
    [Tooltip("The reference resolution used for UI scaling. Keep as 2560x1440.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(2560f, 1440f);

    [Header("Letterbox Background")]
    [Tooltip("Color of the letterbox bars (used during gameplay).")]
    [SerializeField] private Color letterboxColor = Color.black;

    [Header("Loading Phase Background")]
    [Tooltip("Color of the letterbox bars during loading phase. If not set, falls back to letterboxColor.")]
    [SerializeField] private Color loadingPhaseColor = default;

    [Header("Letterbox Canvas Sorting Order")]
    [Tooltip("Sorting order for the letterbox background canvas. Must be > MainWndCanvas(0) and < TransitionCanvas(1000) so letterbox renders above game UI but below transition. Default 1500.")]
    [SerializeField] private int letterboxSortingOrder = 1500;

    [Header("Letterbox Camera")]
    [Tooltip("Use a dedicated LetterboxCamera to render the letterbox bars. More reliable than RawImage on Canvas.")]
    [SerializeField] private bool useLetterboxCamera = true;
    private Camera _letterboxCamera;

    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    [SerializeField] private bool enableDebugLog = true;

    private bool _debugLoggedThisSession;

    private Camera _mainCamera;
    private Rect _lastViewport;
    private float _lastTargetAspect;
    private float _lastScreenWidth = -1;
    private float _lastScreenHeight = -1;
    private RawImage[] _letterboxBars;
    private RawImage _loadingBackgroundFill;
    private Texture2D _letterboxTexture;
    private bool _lastApplied;
    private Canvas _letterboxCanvas;
    private bool _transitionActive;

    /// <summary>
    /// 标记是否正在Loading期间
    /// 当为true时，ApplyLetterbox不会覆盖UICamera的背景色
    /// </summary>
    public bool IsInLoadingPhase { get; private set; }

    /// <summary>
    /// 获取当前应该使用的letterbox颜色
    /// Loading阶段使用loadingPhaseColor（如果设置），否则使用letterboxColor
    /// </summary>
    private Color GetCurrentLetterboxColor()
    {
        if (IsInLoadingPhase && loadingPhaseColor != default)
        {
            return loadingPhaseColor;
        }
        return letterboxColor;
    }

    private void Awake()
    {
        _transitionActive = false;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupLetterboxBackground();
        FindAndCacheMainCamera();
        SubscribeToTransitionEvents();
    }

    private void Start()
    {
        // OnEnable already calls ApplyLetterbox() when the component is first enabled.
        // This Start() is intentionally empty to avoid double-calling.
        // OnPreCull serves as a safety net for unusual activation orders.
    }

    private void SubscribeToTransitionEvents()
    {
        // Subscribe to WindowTransitionEffect events to enable/disable letterbox background
        // at the right moments during window transitions.
        // We use a coroutine-based approach: enable letterbox at transition start,
        // and disable it once transition completes.
        Debug.Log("[LetterboxController] Subscribed to WindowTransitionEffect events");
    }

    /// <summary>
    /// Enables the letterbox background canvas (used during window transitions).
    /// </summary>
    public void EnableLetterboxBackground()
    {
        if (_letterboxCanvas != null && !_letterboxCanvas.enabled)
        {
            _letterboxCanvas.enabled = true;
            if (enableDebugLog) Debug.Log("[LetterboxController] EnableLetterboxBackground: LetterboxCanvas enabled");
        }
    }

    /// <summary>
    /// Disables the letterbox background canvas (used during gameplay).
    /// The UICamera's black background naturally fills the pillarbox area.
    /// </summary>
    public void DisableLetterboxBackground()
    {
        if (_letterboxCanvas != null && _letterboxCanvas.enabled)
        {
            _letterboxCanvas.enabled = false;
            if (enableDebugLog) Debug.Log("[LetterboxController] DisableLetterboxBackground: LetterboxCanvas disabled");
        }
    }

    /// <summary>
    /// 设置Loading阶段
    /// 在Loading期间，使用loadingPhaseColor
    /// </summary>
    public void SetLoadingPhase(bool isLoading)
    {
        IsInLoadingPhase = isLoading;
        if (enableDebugLog) Debug.Log($"[LetterboxController] SetLoadingPhase: {isLoading}");

        // 进入Loading阶段时，自动从LoadingUI获取背景色
        if (isLoading)
        {
            TryGetLoadingBackgroundColor();
        }

        // 当状态改变时，重新应用letterbox
        // When state changes, reapply letterbox
        if (_mainCamera != null || _letterboxBars != null)
        {
            ApplyLetterbox();
        }
    }

    /// <summary>
    /// 尝试从LoadingUI获取背景色
    /// </summary>
    private void TryGetLoadingBackgroundColor()
    {
        // 如果已经设置了loadingPhaseColor，跳过
        if (loadingPhaseColor != default)
        {
            if (enableDebugLog) Debug.Log($"[LetterboxController] loadingPhaseColor already set to {loadingPhaseColor}, skipping");
            return;
        }

        // 尝试从LoadingUI获取背景色
        var loadingUI = Object.FindObjectOfType<HiddenCats.UI.LoadingUI>();
        if (loadingUI != null)
        {
            // 使用反射获取私有字段 loadingBackgroundColor
            var field = typeof(HiddenCats.UI.LoadingUI).GetField("loadingBackgroundColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var color = (Color)field.GetValue(loadingUI);
                loadingPhaseColor = color;
                if (enableDebugLog) Debug.Log($"[LetterboxController] Got loadingBackgroundColor from LoadingUI: {color}");
            }
        }
        else
        {
            if (enableDebugLog) Debug.Log("[LetterboxController] LoadingUI not found, using letterboxColor as fallback");
        }
    }

    /// <summary>
    /// Find the main UICamera used by the ScreenSpaceCamera Canvas.
    /// </summary>
    private void FindAndCacheMainCamera()
    {
        Debug.Log($"[LetterboxController] FindAndCacheMainCamera called. _mainCamera={_mainCamera?.name ?? "null"}, gameObject={gameObject.name}, scene={gameObject.scene.name}");

        // First try: Find UICamera by name
        GameObject uiCameraObj = GameObject.Find("UICamera");
        if (uiCameraObj != null)
        {
            _mainCamera = uiCameraObj.GetComponent<Camera>();
            Debug.Log($"[LetterboxController] Try-1 (Find by name 'UICamera'): uiCameraObj={uiCameraObj != null}, _mainCamera={_mainCamera?.name ?? "null"}");
        }

        // Second try: Find Camera used by Canvas
        if (_mainCamera == null)
        {
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Debug.Log($"[LetterboxController] Try-2 Canvas[{i}]: name={canvases[i].name}, renderMode={canvases[i].renderMode}, worldCamera={canvases[i].worldCamera?.name ?? "null"}, bgColor={canvases[i].worldCamera?.backgroundColor}");
                if (canvases[i].renderMode == RenderMode.ScreenSpaceCamera)
                {
                    _mainCamera = canvases[i].worldCamera;
                    if (_mainCamera != null)
                    {
                        Debug.Log($"[LetterboxController] Try-2 (ScreenSpaceCamera Canvas): canvas={canvases[i].name}, camera={_mainCamera.name}, camera.bgColor={_mainCamera.backgroundColor}");
                        break;
                    }
                }
            }
        }

        // Third try: Find any Camera tagged as MainCamera
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            Debug.Log($"[LetterboxController] Try-3 (Camera.main): {_mainCamera?.name ?? "null"}");
        }

        // Also check all cameras to find potential conflicts
        Camera[] allCameras = Object.FindObjectsOfType<Camera>(true);
        Debug.Log($"[LetterboxController] All cameras in scene ({allCameras.Length}):");
        foreach (var cam in allCameras)
        {
            Debug.Log($"  Camera: name={cam.name}, backgroundColor={cam.backgroundColor}, clearFlags={cam.clearFlags}, tag={cam.tag}, cullingMask={cam.cullingMask}");
        }

        if (_mainCamera == null)
        {
            Debug.LogError("[LetterboxController] FAILED TO FIND ANY CAMERA!");
        }
        else
        {
            // Check if the cached reference is still valid (not destroyed)
            if (_mainCamera.gameObject == null)
            {
                Debug.LogWarning("[LetterboxController] Cached camera GameObject was destroyed! Clearing reference.");
                _mainCamera = null;
                return;
            }

            Debug.Log($"[LetterboxController] Final camera: name={_mainCamera.name}, backgroundColor={_mainCamera.backgroundColor}, clearFlags={_mainCamera.clearFlags}, gameObject.scene={_mainCamera.gameObject.scene.name}");
            // Immediately set black background
            _mainCamera.backgroundColor = letterboxColor;
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            Debug.Log($"[LetterboxController] After setting: backgroundColor={_mainCamera.backgroundColor}, clearFlags={_mainCamera.clearFlags}");
        }
    }

    private void SetupLetterboxBackground()
    {
        Debug.Log("[LetterboxController] SetupLetterboxBackground called");

        // Create a dedicated Canvas for the letterbox background with high sorting order
        // This ensures black bars always render above the transition effect
        var canvasObj = new GameObject("LetterboxCanvas");
        canvasObj.transform.SetParent(transform);
        _letterboxCanvas = canvasObj.AddComponent<Canvas>();
        _letterboxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _letterboxCanvas.sortingOrder = letterboxSortingOrder;
        _letterboxCanvas.overrideSorting = true;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create 1x1 texture for letterbox bars (will be colored appropriately)
        _letterboxTexture = new Texture2D(1, 1);
        _letterboxTexture.SetPixel(0, 0, letterboxColor);
        _letterboxTexture.Apply();

        // Create an array to hold letterbox bar images (max 4 bars: left, right, top, bottom)
        _letterboxBars = new RawImage[4];

        // Create each bar with initial position at origin (will be repositioned in ApplyLetterbox)
        for (int i = 0; i < 4; i++)
        {
            var barObj = new GameObject($"LetterboxBar_{i}", typeof(RectTransform), typeof(RawImage));
            barObj.transform.SetParent(canvasObj.transform);

            var rt = barObj.GetComponent<RectTransform>();
            // Initial position - will be updated in ApplyLetterbox
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var rawImage = barObj.GetComponent<RawImage>();
            rawImage.raycastTarget = false;
            rawImage.texture = _letterboxTexture;
            rawImage.color = letterboxColor;
            rawImage.enabled = false; // Will be enabled in ApplyLetterbox

            _letterboxBars[i] = rawImage;
        }

        Debug.Log($"[LetterboxController] LetterboxBars created, canvas sortingOrder={_letterboxCanvas.sortingOrder}");

        // Debug: log ALL canvases in scene after creation
        LogAllCanvasesInScene("[LetterboxController] After SetupLetterboxBackground - all canvases in scene:");

        // Create a dedicated LetterboxCamera that renders behind everything else
        SetupLetterboxCamera();
    }

    private void SetupLetterboxCamera()
    {
        if (!useLetterboxCamera)
        {
            Debug.Log("[LetterboxController] LetterboxCamera disabled by setting.");
            return;
        }

        // Create the camera as a sibling of LetterboxController's gameObject
        var cameraObj = new GameObject("LetterboxCamera");
        _letterboxCamera = cameraObj.AddComponent<Camera>();

        // Configure for letterbox: orthographic, black background, renders nothing but background
        _letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
        _letterboxCamera.backgroundColor = letterboxColor;
        _letterboxCamera.orthographic = true;
        _letterboxCamera.orthographicSize = 1f;
        _letterboxCamera.nearClipPlane = -10f;
        _letterboxCamera.farClipPlane = 10f;

        // Very low depth so it renders behind everything
        _letterboxCamera.depth = -100f;

        // Only render the background plane (RawImage), nothing else
        // Use culling layer that only the LetterboxBackground uses
        // Default to Nothing so it only shows solid color
        _letterboxCamera.cullingMask = 0; // Will only show solid backgroundColor

        // Set depth texture mode to None so it doesn't waste fillrate
        _letterboxCamera.renderingPath = RenderingPath.Forward;

        Debug.Log($"[LetterboxController] LetterboxCamera created: depth={_letterboxCamera.depth}, cullingMask={_letterboxCamera.cullingMask}, orthographic={_letterboxCamera.orthographic}, bgColor={_letterboxCamera.backgroundColor}");
    }

    private void Update()
    {
        if (Screen.width != (int)_lastScreenWidth || Screen.height != (int)_lastScreenHeight)
        {
            ApplyLetterbox();
        }
    }

    /// <summary>
    /// Apply letterboxing based on the current screen size and target aspect ratio.
    /// Sets the main UICamera's rect to create black bars, and updates background color.
    /// </summary>
    public void ApplyLetterbox()
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float aspectDiff = Mathf.Abs(screenAspect - targetAspectRatio);

        Debug.Log($"[LetterboxController] ApplyLetterbox: screen={Screen.width}x{Screen.height}, aspect={screenAspect:F3} vs target={targetAspectRatio:F3}, diff={aspectDiff:F4}, _mainCamera={_mainCamera?.name ?? "null"}, _letterboxCamera={_letterboxCamera?.name ?? "null"}");

        // Track current screen dimensions
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        // Try to find camera if not cached yet
        if (_mainCamera == null)
        {
            Debug.Log("[LetterboxController] ApplyLetterbox: _mainCamera is null, re-finding camera...");
            FindAndCacheMainCamera();
        }
        else if (_mainCamera.gameObject == null)
        {
            Debug.LogWarning("[LetterboxController] ApplyLetterbox: _mainCamera.gameObject is null (destroyed), re-finding camera...");
            _mainCamera = null;
            FindAndCacheMainCamera();
        }

        _lastTargetAspect = screenAspect;

        // Update camera background color to match letterbox color
        // 根据当前阶段选择正确的颜色
        Color currentColor = GetCurrentLetterboxColor();
        if (_mainCamera != null)
        {
            if (IsInLoadingPhase)
            {
                // Loading期间：UICamera背景色显示loadingPhaseColor（用于LoadingUI的透明区域背景）
                // Letterbox区域由LetterboxCamera的纯黑背景覆盖
                _mainCamera.backgroundColor = loadingPhaseColor;
                Debug.Log($"[LetterboxController] Loading phase: UICamera backgroundColor={loadingPhaseColor}");
            }
            else
            {
                // 游戏期间：UICamera背景色显示letterboxColor
                _mainCamera.backgroundColor = currentColor;
                Debug.Log($"[LetterboxController] Gameplay: UICamera backgroundColor={currentColor}");
            }
        }

        // Update LetterboxCamera if it exists
        // LetterboxCamera显示纯黑（在Loading期间用于黑边区域）
        if (_letterboxCamera != null)
        {
            // Loading期间用纯黑显示黑边区域，游戏期间用letterboxColor
            _letterboxCamera.backgroundColor = IsInLoadingPhase ? Color.black : currentColor;

            if (aspectDiff < 0.001f)
            {
                // Full screen - LetterboxCamera shows full background
                _letterboxCamera.rect = new Rect(0f, 0f, 1f, 1f);
                Debug.Log("[LetterboxController] LetterboxCamera: full screen");
            }
            else if (screenAspect > targetAspectRatio)
            {
                // Wider than target: pillarbox (bars on left/right)
                _letterboxCamera.rect = new Rect(0f, 0f, 1f, 1f); // Full screen black - bars show as background
                Debug.Log($"[LetterboxController] LetterboxCamera: pillarbox, rect={_letterboxCamera.rect}");
            }
            else
            {
                // Taller than target: letterbox (bars on top/bottom)
                _letterboxCamera.rect = new Rect(0f, 0f, 1f, 1f); // Full screen black
                Debug.Log($"[LetterboxController] LetterboxCamera: letterbox, rect={_letterboxCamera.rect}");
            }
        }

        // Update letterbox texture color
        if (_letterboxTexture != null)
        {
            _letterboxTexture.SetPixel(0, 0, currentColor);
            _letterboxTexture.Apply();
        }

        // Check if screen matches target aspect ratio (with small tolerance)
        if (aspectDiff < 0.001f)
        {
            // Perfect match - use full screen for camera
            if (_mainCamera != null)
            {
                _mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }

            // Disable all letterbox bars
            DisableAllLetterboxBars();

            _lastApplied = true;
            return;
        }

        // Calculate the visible rect for UICamera
        Rect rect = new Rect(0f, 0f, 1f, 1f);

        if (screenAspect > targetAspectRatio)
        {
            // Screen is wider than target: add pillarbox (vertical bars on sides)
            float visibleWidthRatio = targetAspectRatio / screenAspect;
            float blackBarRatio = (1f - visibleWidthRatio) / 2f;

            rect.x = blackBarRatio;
            rect.y = 0f;
            rect.width = visibleWidthRatio;
            rect.height = 1f;

            // Position the left and right bars
            PositionLetterboxBarsPillarbox(blackBarRatio);
        }
        else
        {
            // Screen is taller than target: add letterbox (horizontal bars top/bottom)
            float visibleHeightRatio = screenAspect / targetAspectRatio;
            float blackBarRatio = (1f - visibleHeightRatio) / 2f;

            rect.x = 0f;
            rect.y = blackBarRatio;
            rect.width = 1f;
            rect.height = visibleHeightRatio;

            // Position the top and bottom bars
            PositionLetterboxBarsLetterbox(blackBarRatio);
        }

        // Apply to main camera
        if (_mainCamera != null)
        {
            _mainCamera.rect = rect;
            Debug.Log($"[LetterboxController] UICamera rect={rect}");
        }

        // During Loading phase: LetterboxBars should be DISABLED
        // The LetterboxCamera (showing pure black) handles the letterbox area
        // Loading UI (which uses loadingPhaseColor) fills the 16:9 area
        // During gameplay: LetterboxBars should be enabled with letterboxColor
        if (IsInLoadingPhase)
        {
            // Disable LetterboxBars during loading - LetterboxCamera shows pure black for letterbox
            DisableAllLetterboxBars();
        }
        else
        {
            // During gameplay: Update bar colors and enable them
            UpdateLetterboxBarColors(currentColor);
        }

        _lastApplied = true;

        // Debug: log ALL canvases after apply
        LogAllCanvasesInScene("[LetterboxController] After ApplyLetterbox - all canvases:");
    }

    /// <summary>
    /// Disable all letterbox bars
    /// </summary>
    private void DisableAllLetterboxBars()
    {
        if (_letterboxBars != null)
        {
            for (int i = 0; i < _letterboxBars.Length; i++)
            {
                if (_letterboxBars[i] != null)
                {
                    _letterboxBars[i].enabled = false;
                }
            }
        }
        if (_letterboxCanvas != null)
        {
            _letterboxCanvas.enabled = false;
        }
        Debug.Log("[LetterboxController] All letterbox bars disabled");
    }

    /// <summary>
    /// Position letterbox bars for pillarbox mode (left/right bars)
    /// </summary>
    private void PositionLetterboxBarsPillarbox(float barWidthRatio)
    {
        if (_letterboxBars == null || _letterboxBars.Length < 2) return;

        // Left bar: anchor to left edge, full height
        var leftBar = _letterboxBars[0];
        var leftRT = leftBar.GetComponent<RectTransform>();
        leftRT.anchorMin = new Vector2(0f, 0f);
        leftRT.anchorMax = new Vector2(0f, 1f);
        leftRT.pivot = new Vector2(0f, 0.5f);
        leftRT.sizeDelta = new Vector2(Screen.width * barWidthRatio, 0f);
        leftRT.anchoredPosition = Vector2.zero;
        leftBar.enabled = true;

        // Right bar: anchor to right edge, full height
        var rightBar = _letterboxBars[1];
        var rightRT = rightBar.GetComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(1f, 0f);
        rightRT.anchorMax = new Vector2(1f, 1f);
        rightRT.pivot = new Vector2(1f, 0.5f);
        rightRT.sizeDelta = new Vector2(Screen.width * barWidthRatio, 0f);
        rightRT.anchoredPosition = Vector2.zero;
        rightBar.enabled = true;

        // Disable top and bottom bars
        if (_letterboxBars.Length > 2) _letterboxBars[2].enabled = false;
        if (_letterboxBars.Length > 3) _letterboxBars[3].enabled = false;

        // Enable canvas
        if (_letterboxCanvas != null) _letterboxCanvas.enabled = true;

        Debug.Log($"[LetterboxController] Pillarbox bars positioned: left/right width={Screen.width * barWidthRatio}");
    }

    /// <summary>
    /// Position letterbox bars for letterbox mode (top/bottom bars)
    /// </summary>
    private void PositionLetterboxBarsLetterbox(float barHeightRatio)
    {
        if (_letterboxBars == null || _letterboxBars.Length < 2) return;

        // Top bar: anchor to top edge, full width
        var topBar = _letterboxBars[0];
        var topRT = topBar.GetComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0f, 1f);
        topRT.anchorMax = new Vector2(1f, 1f);
        topRT.pivot = new Vector2(0.5f, 1f);
        topRT.sizeDelta = new Vector2(0f, Screen.height * barHeightRatio);
        topRT.anchoredPosition = Vector2.zero;
        topBar.enabled = true;

        // Bottom bar: anchor to bottom edge, full width
        var bottomBar = _letterboxBars[1];
        var bottomRT = bottomBar.GetComponent<RectTransform>();
        bottomRT.anchorMin = new Vector2(0f, 0f);
        bottomRT.anchorMax = new Vector2(1f, 0f);
        bottomRT.pivot = new Vector2(0.5f, 0f);
        bottomRT.sizeDelta = new Vector2(0f, Screen.height * barHeightRatio);
        bottomRT.anchoredPosition = Vector2.zero;
        bottomBar.enabled = true;

        // Disable left and right bars
        if (_letterboxBars.Length > 2) _letterboxBars[2].enabled = false;
        if (_letterboxBars.Length > 3) _letterboxBars[3].enabled = false;

        // Enable canvas
        if (_letterboxCanvas != null) _letterboxCanvas.enabled = true;

        Debug.Log($"[LetterboxController] Letterbox bars positioned: top/bottom height={Screen.height * barHeightRatio}");
    }

    /// <summary>
    /// Update all letterbox bar colors
    /// </summary>
    private void UpdateLetterboxBarColors(Color color)
    {
        if (_letterboxBars != null)
        {
            for (int i = 0; i < _letterboxBars.Length; i++)
            {
                if (_letterboxBars[i] != null)
                {
                    _letterboxBars[i].color = color;
                }
            }
        }
    }

    /// <summary>
    /// Set the target aspect ratio.
    /// </summary>
    public void SetTargetAspectRatio(float aspect)
    {
        targetAspectRatio = aspect;
        ApplyLetterbox();
    }

    private void LogAllCanvasesInScene(string header)
    {
        Canvas[] allCanvases = Object.FindObjectsOfType<Canvas>(true);
        Debug.Log($"{header} ({allCanvases.Length} canvases):");
        for (int i = 0; i < allCanvases.Length; i++)
        {
            var c = allCanvases[i];
            bool isOurs = (c == _letterboxCanvas);
            bool isMainWnd = c.name.Contains("MainWnd");
            bool isLoading = c.name.Contains("Loading");
            bool isTransition = c.name.Contains("Transition");
            string highlight = isOurs ? " <<< LETTERBOX" : (isMainWnd ? " (MainWnd)" : (isLoading ? " (Loading)" : (isTransition ? " (Transition)" : "")));
            Debug.Log($"  [{i}] '{c.name}' sortOrder={c.sortingOrder} renderMode={c.renderMode} enabled={c.enabled} overrideSorting={c.overrideSorting}{highlight}");
        }
    }

    /// <summary>
    /// Set letterbox background color.
    /// </summary>
    public void SetLetterboxColor(Color color)
    {
        letterboxColor = color;
        if (_mainCamera != null && !IsInLoadingPhase)
        {
            _mainCamera.backgroundColor = color;
        }
        if (_letterboxCamera != null)
        {
            _letterboxCamera.backgroundColor = GetCurrentLetterboxColor();
        }
        if (_letterboxTexture != null)
        {
            _letterboxTexture.SetPixel(0, 0, GetCurrentLetterboxColor());
            _letterboxTexture.Apply();
        }
        UpdateLetterboxBarColors(GetCurrentLetterboxColor());
    }

    /// <summary>
    /// Set loading phase background color.
    /// This color will be used for letterbox bars during the loading phase.
    /// Always apply immediately to UICamera background (which shows through Loading UI's transparent areas).
    /// </summary>
    public void SetLoadingPhaseColor(Color color)
    {
        loadingPhaseColor = color;

        // Always update UICamera background color when loadingPhaseColor changes
        // This ensures LoadingUI (ScreenSpaceOverlay with transparency) shows the correct background
        if (_mainCamera != null)
        {
            _mainCamera.backgroundColor = color;
            Debug.Log($"[LetterboxController] SetLoadingPhaseColor: UICamera backgroundColor set to {color}");
        }

        // LetterboxCamera should show pure black for letterbox area during loading
        if (_letterboxCamera != null)
        {
            _letterboxCamera.backgroundColor = Color.black;
        }

        if (_letterboxTexture != null)
        {
            _letterboxTexture.SetPixel(0, 0, color);
            _letterboxTexture.Apply();
        }

        Debug.Log($"[LetterboxController] SetLoadingPhaseColor: {color}, IsInLoadingPhase={IsInLoadingPhase}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_letterboxCamera != null)
        {
            Destroy(_letterboxCamera.gameObject);
            _letterboxCamera = null;
        }

        if (_letterboxCanvas != null)
        {
            Destroy(_letterboxCanvas.gameObject);
            _letterboxCanvas = null;
        }

        if (_letterboxTexture != null)
        {
            Destroy(_letterboxTexture);
            _letterboxTexture = null;
        }
    }

    private void OnEnable()
    {
        if (enableDebugLog) Debug.Log($"[LetterboxController] OnEnable called, invoking ApplyLetterbox");
        ApplyLetterbox();
    }

    private void OnRectTransformDimensionsChanged()
    {
        if (enableDebugLog) Debug.Log($"[LetterboxController] OnRectTransformDimensionsChanged called, invoking ApplyLetterbox");
        ApplyLetterbox();
    }

    private void OnPreCull()
    {
        if (!_lastApplied || _mainCamera == null)
        {
            if (enableDebugLog) Debug.Log($"[LetterboxController] OnPreCull safety net: _lastApplied={_lastApplied}, _mainCamera={_mainCamera?.name ?? "null"}, invoking ApplyLetterbox");
            ApplyLetterbox();
        }
    }
}
