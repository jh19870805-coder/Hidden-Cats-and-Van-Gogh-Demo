using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Switches between two custom cursors (settings: small / large). Optionally rescales textures so on-screen size
    /// stays consistent across resolutions (Cursor.SetCursor uses texture pixels 1:1, which can look huge on some aspect ratios / DPI).
    /// </summary>
    public sealed class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        [Header("Cursor Textures")]
        [Tooltip("Small cursor (MouseX1). Leave empty to load from Resources path.")]
        [SerializeField] private Texture2D normalCursor;

        [Tooltip("Large cursor (MouseX2). Should be authored ~2× the small texture for a visibly larger pointer.")]
        [SerializeField] private Texture2D largeCursor;

        [SerializeField] private string normalCursorResourcePath = "Cursor/MouseX1";
        [SerializeField] private string largeCursorResourcePath = "Cursor/MouseX2";

        [Header("Hotspots (pixels in each source texture)")]
        [SerializeField] private Vector2 normalCursorHotspot;
        [SerializeField] private Vector2 largeCursorHotspot;

        [Header("Resolution-aware scaling")]
        [Tooltip("When on, cursor height is derived from a fraction of Screen.height so it stays visually similar across resolutions.")]
        [SerializeField] private bool scaleCursorToScreen = true;

        [Tooltip("Target height as a fraction of screen height (e.g. 0.0275 ≈ 2.75% of screen height for MouseX1).")]
        [SerializeField] private float normalCursorScreenHeightFraction = 0.0275f;

        [Tooltip("Target height fraction for MouseX2 (large mode). Typically slightly larger than normal.")]
        [SerializeField] private float largeCursorScreenHeightFraction = 0.041f;

        [Tooltip("Clamp for the computed uniform scale factor (avoids microscopic or enormous cursors).")]
        [SerializeField] private float minCursorUniformScale = 0.2f;

        [SerializeField] private float maxCursorUniformScale = 2.5f;

        [Header("Display")]
        [Tooltip("ForceSoftware: cursor draws at texture pixel size (recommended so small/large differ). Auto may clamp on Windows.")]
        [SerializeField] private bool useSoftwareCursor = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog;

        private Texture2D _normal;
        private Texture2D _large;
        private bool _isLarge;

        private Texture2D _scaledForCursor;
        private int _scaledCacheW;
        private int _scaledCacheH;
        private bool _scaledCacheIsLarge;
        private int _lastScreenW = -1;
        private int _lastScreenH = -1;

        /// <summary>
        /// Stores the last valid cursor position within the game area.
        /// </summary>
        private Vector2 _lastValidCursorPosition;

        /// <summary>
        /// Whether the cursor is currently outside the game area.
        /// </summary>
        private bool _isCursorOutsideGameArea;

        private void Awake()
        {
            // Force default values to override Inspector-serialized values
            normalCursorScreenHeightFraction = 0.0275f;
            largeCursorScreenHeightFraction = 0.041f;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            ResolveTextures();
            _isLarge = false;
            ApplyActiveCursor();
        }

        private void Start()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += OnSettingsChanged;
            }

            StartCoroutine(LoadCursorSizeFromSettingsDelayed());
        }

        private System.Collections.IEnumerator LoadCursorSizeFromSettingsDelayed()
        {
            yield return null;
            LoadCursorSizeFromSettings();
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged -= OnSettingsChanged;
            }

            if (_scaledForCursor != null)
            {
                Destroy(_scaledForCursor);
                _scaledForCursor = null;
            }
        }

        private void LoadCursorSizeFromSettings()
        {
            if (SettingsManager.Instance != null)
            {
                _isLarge = SettingsManager.Instance.GetSettings().isCursorLarge;
            }
            else
            {
                _isLarge = false;
                Debug.LogWarning("[CursorManager] SettingsManager missing; using small cursor.");
            }

            ApplyActiveCursor();
        }

        private void OnSettingsChanged(SettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            _isLarge = settings.isCursorLarge;
            ApplyActiveCursor();

            if (enableDebugLog)
            {
                Debug.Log($"[CursorManager] Setting → {(_isLarge ? "large (MouseX2)" : "small (MouseX1)")}");
            }
        }

        private void ResolveTextures()
        {
            _normal = normalCursor != null ? normalCursor : LoadTex(normalCursorResourcePath, "MouseX1");
            _large = largeCursor != null ? largeCursor : LoadTex(largeCursorResourcePath, "MouseX2");

            if (_large == null && _normal != null)
            {
                _large = _normal;
                Debug.LogWarning("[CursorManager] MouseX2 missing; large mode uses MouseX1 texture.");
            }

            if (_normal == null && _large != null)
            {
                _normal = _large;
                Debug.LogWarning("[CursorManager] MouseX1 missing; small mode uses MouseX2 texture.");
            }

            if (enableDebugLog && _normal != null && _large != null)
            {
                Debug.Log($"[CursorManager] MouseX1 {_normal.width}×{_normal.height}, MouseX2 {_large.width}×{_large.height}; screen scaling={scaleCursorToScreen}");
            }
        }

        private static Texture2D LoadTex(string path, string label)
        {
            string p = string.IsNullOrEmpty(path) ? (label == "MouseX1" ? "Cursor/MouseX1" : "Cursor/MouseX2") : path;
            Texture2D t = Resources.Load<Texture2D>(p);
            if (t == null)
            {
                Debug.LogWarning($"[CursorManager] {label} not found at Resources/{p}");
            }

            return t;
        }

        private void LateUpdate()
        {
            // Check if cursor is within game area using LetterboxController
            Vector2 mousePos = Input.mousePosition;

            if (LetterboxController.Instance != null)
            {
                bool isInGameArea = LetterboxController.Instance.IsPositionInGameArea(mousePos);

                if (isInGameArea)
                {
                    // Cursor is inside game area
                    if (_isCursorOutsideGameArea)
                    {
                        // Just returned to game area - restore custom cursor and position
                        _isCursorOutsideGameArea = false;
                        ApplyActiveCursor();

                        if (enableDebugLog)
                        {
                            Debug.Log("[CursorManager] Cursor returned to game area");
                        }
                    }

                    // Update last valid position while in game area
                    _lastValidCursorPosition = mousePos;
                }
                else
                {
                    // Cursor is outside game area
                    if (!_isCursorOutsideGameArea)
                    {
                        // Just left game area - hide custom cursor, show system cursor
                        _isCursorOutsideGameArea = true;
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

                        if (enableDebugLog)
                        {
                            Debug.Log($"[CursorManager] Cursor left game area, last valid pos: {_lastValidCursorPosition}");
                        }
                    }
                }
            }

            // Handle resolution-based cursor rescaling
            if (!scaleCursorToScreen)
            {
                return;
            }

            if (Screen.width == _lastScreenW && Screen.height == _lastScreenH)
            {
                return;
            }

            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            ApplyActiveCursor();
        }

        private void ApplyActiveCursor()
        {
            Texture2D src = _isLarge ? _large : _normal;
            Vector2 hotSrc = _isLarge ? largeCursorHotspot : normalCursorHotspot;

            if (src == null)
            {
                ResolveTextures();
                src = _isLarge ? _large : _normal;
            }

            if (src == null)
            {
                Debug.LogError("[CursorManager] No cursor texture.");
                return;
            }

            Texture2D tex;
            Vector2 hot;

            if (scaleCursorToScreen)
            {
                float frac = _isLarge ? largeCursorScreenHeightFraction : normalCursorScreenHeightFraction;
                float uniform = (Screen.height * frac) / Mathf.Max(1f, (float)src.height);
                uniform = Mathf.Clamp(uniform, minCursorUniformScale, maxCursorUniformScale);

                int nw = Mathf.Max(1, Mathf.RoundToInt(src.width * uniform));
                int nh = Mathf.Max(1, Mathf.RoundToInt(src.height * uniform));

                if (_scaledForCursor != null &&
                    _scaledCacheW == nw &&
                    _scaledCacheH == nh &&
                    _scaledCacheIsLarge == _isLarge)
                {
                    tex = _scaledForCursor;
                    hot = new Vector2(
                        hotSrc.x * nw / Mathf.Max(1f, src.width),
                        hotSrc.y * nh / Mathf.Max(1f, src.height));
                }
                else
                {
                    if (_scaledForCursor != null)
                    {
                        Destroy(_scaledForCursor);
                        _scaledForCursor = null;
                    }

                    _scaledForCursor = ScaleTexture(src, nw, nh);
                    _scaledCacheW = nw;
                    _scaledCacheH = nh;
                    _scaledCacheIsLarge = _isLarge;
                    tex = _scaledForCursor;
                    hot = new Vector2(
                        hotSrc.x * nw / Mathf.Max(1f, src.width),
                        hotSrc.y * nh / Mathf.Max(1f, src.height));
                }
            }
            else
            {
                tex = src;
                hot = hotSrc;
            }

            try
            {
                CursorMode mode = useSoftwareCursor ? CursorMode.ForceSoftware : CursorMode.Auto;
                Cursor.SetCursor(tex, hot, mode);

                if (scaleCursorToScreen)
                {
                    _lastScreenW = Screen.width;
                    _lastScreenH = Screen.height;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CursorManager] SetCursor failed: {e.Message}");
            }
        }

        private static Texture2D ScaleTexture(Texture2D src, int newWidth, int newHeight)
        {
            if (src == null || newWidth < 1 || newHeight < 1)
            {
                return null;
            }

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                Texture2D dest = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
                dest.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                dest.Apply(false, false);
                return dest;
            }
            finally
            {
                RenderTexture.active = prev;
                if (RenderTexture.active == rt)
                {
                    RenderTexture.active = null;
                }

                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public void SetNormalCursor()
        {
            _isLarge = false;
            ApplyActiveCursor();
        }

        public void SetLargeCursor()
        {
            _isLarge = true;
            ApplyActiveCursor();
        }

        public void SetCursorSize(bool isLarge)
        {
            _isLarge = isLarge;
            ApplyActiveCursor();
        }

        public bool IsLarge => _isLarge;

        /// <summary>
        /// Get the last valid cursor position within the game area.
        /// </summary>
        public Vector2 LastValidCursorPosition => _lastValidCursorPosition;

        /// <summary>
        /// Check if cursor is currently outside the game area.
        /// </summary>
        public bool IsCursorOutsideGameArea => _isCursorOutsideGameArea;

        public void ResetCursor()
        {
            SetNormalCursor();
        }

        [ContextMenu("Log cursor texture info")]
        private void LogCursorTextureInfo()
        {
            ResolveTextures();
            Debug.Log($"[CursorManager] normal={(_normal != null ? $"{_normal.width}×{_normal.height}" : "null")}, large={(_large != null ? $"{_large.width}×{_large.height}" : "null")}");
        }
    }
}
