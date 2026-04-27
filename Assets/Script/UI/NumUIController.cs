using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using HiddenCats.Core;
using HiddenCats.Interactable;

namespace HiddenCats.UI
{
    public class NumUIController : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("Scene name for this NumUI (e.g., SceneName.RoomWnd)")]
        [SerializeField] public string sceneName = SceneName.RoomWnd;

        [Header("UI References")]
        [Tooltip("NormalCats sub-node (should contain NUm prefab)")]
        [SerializeField] public Transform normalCatsNode;

        [Tooltip("HiddenCats sub-node (should contain NUm prefab)")]
        [SerializeField] public Transform hiddenCatsNode;

        [Tooltip("Fish sub-node (should contain NUm prefab)")]
        [SerializeField] public Transform fishNode;

        [Tooltip("Firework sub-node (should contain NUm prefab, only for CafeWnd)")]
        [SerializeField] public Transform fireworkNode;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        [Header("Speedrun UI Layout")]
        [Tooltip("When speedrun mode hides elements, auto-center the remaining HUD elements (including Search).")]
        [SerializeField] private bool autoLayoutInSpeedrun = true;

        [Tooltip("Spacing between visible HUD elements in speedrun (brings Search closer to remaining UI).")]
        [SerializeField] private float layoutSpacingSpeedrun = 140f;

        [Header("Normal UI Layout")]
        [Tooltip("Auto-center the HUD elements (including Search).")]
        [SerializeField] private bool autoLayoutWhenFireworkMissingInNormal = true;

        [Tooltip("Spacing between visible HUD elements.")]
        [SerializeField] private float layoutSpacingNormal = 200f;

        // Cache for maximum counts (calculated once at start)
        private Dictionary<CollectibleType, int> _maxCounts = new Dictionary<CollectibleType, int>();

        // UI component references for each item type
        private Dictionary<CollectibleType, NumItemDisplay> _displays = new Dictionary<CollectibleType, NumItemDisplay>();

        // Cached anchored positions so we can restore the original layout after leaving speedrun.
        private readonly Dictionary<RectTransform, Vector2> _originalAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private RectTransform _searchRect;

        private void Awake()
        {
            Debug.Log($"[NumUIController] Awake() called on {gameObject.name}, sceneName='{sceneName}'");

            // Validate scene name
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[NumUIController] Scene name is not set on {gameObject.name}");
                enabled = false;
                return;
            }

            // Subscribe to collection events
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnSceneCountChanged += HandleSceneCountChanged;
                Debug.Log($"[NumUIController] Subscribed to OnSceneCountChanged on {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[NumUIController] CollectionService.Instance is null. Will retry in Start().");
            }
        }

        private void Start()
        {
            // Retry subscribing if CollectionService wasn't ready in Awake
            if (CollectionService.Instance != null && !IsSubscribed())
            {
                CollectionService.Instance.OnSceneCountChanged += HandleSceneCountChanged;
            }

            // Calculate maximum counts for each item type (moved to Start to ensure all objects are loaded)
            CalculateMaxCounts();

            // Initialize UI displays (after calculating max counts)
            InitializeDisplays();

            CacheOriginalHudAnchorsIfNeeded();

            // Apply speedrun visibility (hide non-cat items if in speedrun mode)
            ApplySpeedrunVisibility();

            // Update all displays with current counts
            UpdateAllDisplays();
        }

        private void OnEnable()
        {
            // Guard: Only apply layout if Start() has already initialized the cached positions.
            // OnEnable can be called before Start() (e.g., when a window is first activated).
            // If _originalAnchoredPositions is empty, Start() hasn't run yet, so we skip layout
            // changes to avoid using uninitialized/intermediate positions.
            if (_originalAnchoredPositions.Count == 0)
                return;

            // When the GameObject is enabled (e.g., scene activated), update displays
            // This ensures correct counts are shown even if the controller wasn't destroyed/recreated
            if (_displays.Count > 0)
            {
                // Speedrun mode may have changed since last time this window was active.
                ApplySpeedrunVisibility();
                ApplyHudLayoutForCurrentMode();
                UpdateAllDisplays();
            }
        }

        private void OnDestroy()
        {
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnSceneCountChanged -= HandleSceneCountChanged;
            }
        }

        /// <summary>
        /// Calculate maximum counts for each item type by counting items in the scene.
        /// Note: Since content windows are prefabs instantiated in StartUp scene,
        /// we only check the SceneName property, not the Unity scene name.
        /// </summary>
        private void CalculateMaxCounts()
        {
            // Count NormalCats (use InitiallyActiveInPrefab to distinguish "inactive in prefab" vs "collected")
            NormalCatInteractable[] normalCats = FindObjectsOfType<NormalCatInteractable>(true);
            int normalCatCount = 0;
            foreach (var cat in normalCats)
            {
                if (cat != null && cat.SceneName == sceneName && cat.InitiallyActiveInPrefab)
                {
                    normalCatCount++;
                }
            }
            _maxCounts[CollectibleType.NormalCat] = normalCatCount;
            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Found {normalCatCount} NormalCats in {sceneName} (total found: {normalCats.Length})");
            }

            // Count HiddenCats (use InitiallyActiveInPrefab to distinguish "inactive in prefab" vs "collected")
            HiddenCatInteractable[] hiddenCats = FindObjectsOfType<HiddenCatInteractable>(true);
            int hiddenCatCount = 0;
            foreach (var cat in hiddenCats)
            {
                if (cat != null && cat.SceneName == sceneName && cat.InitiallyActiveInPrefab)
                {
                    hiddenCatCount++;
                }
            }
            _maxCounts[CollectibleType.HiddenCat] = hiddenCatCount;
            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Found {hiddenCatCount} HiddenCats in {sceneName} (total found: {hiddenCats.Length})");
            }

            // Count Fish (use InitiallyActiveInPrefab to distinguish "inactive in prefab" vs "collected")
            FishInteractable[] fish = FindObjectsOfType<FishInteractable>(true);
            int fishCount = 0;
            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Scanning for Fish in {sceneName}... (total Fish objects found: {fish.Length})");
            }
            foreach (var f in fish)
            {
                if (f != null)
                {
                    if (enableDebugLog)
                    {
                        Debug.Log($"[NumUIController] Fish found: GameObject='{f.gameObject.name}', SceneName='{f.SceneName}', InitiallyActiveInPrefab={f.InitiallyActiveInPrefab}, Enabled={f.enabled}");
                    }
                    if (f.SceneName == sceneName && f.InitiallyActiveInPrefab)
                    {
                        fishCount++;
                        if (enableDebugLog)
                        {
                            Debug.Log($"[NumUIController] ✓ Fish matched {sceneName}: {f.gameObject.name}");
                        }
                    }
                }
            }
            _maxCounts[CollectibleType.Fish] = fishCount;
            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Final Fish count for {sceneName}: {fishCount} (out of {fish.Length} total Fish objects)");
            }

            // Count Fireworks (only count if GameObject is active)
            // If there are fireworks in this scene, count them
            FireworkInteractable[] fireworks = FindObjectsOfType<FireworkInteractable>(true);
            int fireworkCount = 0;
            foreach (var firework in fireworks)
            {
                if (firework != null && firework.SceneName == sceneName && firework.InitiallyActiveInPrefab)
                {
                    fireworkCount++;
                }
            }
            _maxCounts[CollectibleType.Firework] = fireworkCount;

            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Calculated max counts for {sceneName}: " +
                    $"NormalCat={_maxCounts.GetValueOrDefault(CollectibleType.NormalCat, 0)}, " +
                    $"HiddenCat={_maxCounts.GetValueOrDefault(CollectibleType.HiddenCat, 0)}, " +
                    $"Fish={_maxCounts.GetValueOrDefault(CollectibleType.Fish, 0)}, " +
                    $"Firework={_maxCounts.GetValueOrDefault(CollectibleType.Firework, 0)}");
            }
        }

        /// <summary>
        /// Initialize UI displays for each item type.
        /// </summary>
        private void InitializeDisplays()
        {
            // Initialize NormalCats display
            if (normalCatsNode != null)
            {
                var display = CreateDisplay(normalCatsNode, CollectibleType.NormalCat);
                if (display != null)
                {
                    _displays[CollectibleType.NormalCat] = display;
                }
            }

            // Initialize HiddenCats display
            if (hiddenCatsNode != null)
            {
                var display = CreateDisplay(hiddenCatsNode, CollectibleType.HiddenCat);
                if (display != null)
                {
                    _displays[CollectibleType.HiddenCat] = display;
                }
            }

            // Initialize Fish display
            if (fishNode != null)
            {
                var display = CreateDisplay(fishNode, CollectibleType.Fish);
                if (display != null)
                {
                    _displays[CollectibleType.Fish] = display;
                }
            }

            // Initialize Firework display if fireworkNode is assigned
            if (fireworkNode != null)
            {
                var display = CreateDisplay(fireworkNode, CollectibleType.Firework);
                if (display != null)
                {
                    _displays[CollectibleType.Firework] = display;
                }
            }
        }

        /// <summary>
        /// Create a display component for a NumUI sub-node.
        /// </summary>
        private NumItemDisplay CreateDisplay(Transform node, CollectibleType type)
        {
            Debug.Log($"[NumUIController] CreateDisplay called: node={node?.name}, type={type}");

            // Find the NUm prefab instance within the node
            // The NUm prefab has children: StaffFindedText (TMP), StaffNumText (TMP), '/Text (TMP)', Finished
            Transform numPrefab = node.Find("NUm");
            if (numPrefab == null)
            {
                Debug.LogWarning($"[NumUIController] NUm child not found in {node.name}, trying fallback");
                if (node.name == "NUm" || node.name.Contains("NUm"))
                {
                    numPrefab = node;
                }
                else
                {
                    numPrefab = node.GetComponentInChildren<Transform>();
                    if (numPrefab != null && numPrefab.name != "NUm" && !numPrefab.name.Contains("NUm"))
                    {
                        Debug.LogWarning($"[NumUIController] Fallback transform {numPrefab.name} is not NUm, setting null");
                        numPrefab = null;
                    }
                }
            }

            if (numPrefab == null)
            {
                Debug.LogError($"[NumUIController] Could not find NUm prefab in {node.name} for {type}!");
                // List all children for debugging
                Debug.Log($"[NumUIController] Children of {node.name}:");
                foreach (Transform child in node)
                {
                    Debug.Log($"  - {child.name}");
                }
                return null;
            }

            Debug.Log($"[NumUIController] Found numPrefab: {numPrefab.name}");

            // Find StaffFindedText (TMP) — current count
            TextMeshProUGUI currentText = null;
            Transform currentT = numPrefab.Find("StaffFindedText (TMP)");
            if (currentT != null) currentText = currentT.GetComponent<TextMeshProUGUI>();
            Debug.Log($"[NumUIController] StaffFindedText (TMP): {(currentT != null ? "found" : "NOT FOUND")}");

            // Find StaffNumText (TMP) — max count
            TextMeshProUGUI maxText = null;
            Transform maxT = numPrefab.Find("StaffNumText (TMP)");
            if (maxT != null) maxText = maxT.GetComponent<TextMeshProUGUI>();
            Debug.Log($"[NumUIController] StaffNumText (TMP): {(maxT != null ? "found" : "NOT FOUND")}");

            // Find '/Text (TMP)' — slash separator
            TextMeshProUGUI slashText = null;
            Transform slashT = numPrefab.Find("/Text (TMP) ");
            if (slashT != null) slashText = slashT.GetComponent<TextMeshProUGUI>();

            if (currentText == null || maxText == null)
            {
                Debug.LogError($"[NumUIController] Could not find text components in {numPrefab.name} for {type}!");
                // List all children for debugging
                Debug.Log($"[NumUIController] Children of {numPrefab.name}:");
                foreach (Transform child in numPrefab)
                {
                    Debug.Log($"  - {child.name}");
                }
                return null;
            }

            // Create display component on the NUm GameObject
            NumItemDisplay display = numPrefab.gameObject.AddComponent<NumItemDisplay>();
            display.Initialize(currentText, maxText, slashText, type, _maxCounts.GetValueOrDefault(type, 0));

            Debug.Log($"[NumUIController] Created NumItemDisplay for {type} with maxCount={_maxCounts.GetValueOrDefault(type, 0)}");

            return display;
        }

        /// <summary>
        /// Handle scene count changed event from CollectionService.
        /// </summary>
        private void HandleSceneCountChanged(string changedSceneName, CollectibleType type, int newCount)
        {
            Debug.Log($"[NumUIController] HandleSceneCountChanged: changedSceneName='{changedSceneName}', sceneName='{sceneName}', type={type}, newCount={newCount}");

            if (changedSceneName != sceneName)
            {
                Debug.Log($"[NumUIController] HandleSceneCountChanged: Scene mismatch, ignoring");
                return; // Not for this scene
            }

            if (_displays.ContainsKey(type))
            {
                Debug.Log($"[NumUIController] HandleSceneCountChanged: Updating display for {type} to {newCount}");
                _displays[type].UpdateCurrentCount(newCount);
            }
            else
            {
                Debug.LogWarning($"[NumUIController] HandleSceneCountChanged: No display found for type {type}");
            }
        }

        /// <summary>
        /// Update all displays with current counts from CollectionService.
        /// </summary>
        private void UpdateAllDisplays()
        {
            if (CollectionService.Instance == null)
            {
                return;
            }

            foreach (var kvp in _displays)
            {
                CollectibleType type = kvp.Key;
                NumItemDisplay display = kvp.Value;

                int currentCount = CollectionService.Instance.GetSceneCount(sceneName, type);
                display.UpdateCurrentCount(currentCount);
            }
        }

        /// <summary>
        /// ========== 竞速模式切换逻辑 ==========
        /// 竞速模式：隐藏非猫咪节点，只显示普通猫和隐藏猫计数
        /// 退出竞速：恢复所有节点可见性，重新居中布局
        /// </summary>
        private void ApplySpeedrunVisibility()
        {
            bool isSpeedrun = SpeedrunService.Instance != null
                           && SpeedrunService.Instance.IsSpeedrunEnabled;

            // 竞速模式：隐藏可选的节点
            if (fishNode != null)
                fishNode.gameObject.SetActive(!isSpeedrun);

            if (fireworkNode != null)
                fireworkNode.gameObject.SetActive(!isSpeedrun);

            ApplyHudLayoutForCurrentMode();
        }

        /// <summary>
        /// 根据当前模式（竞速/普通）应用 HUD 布局
        /// 竞速模式：所有可见元素重新居中（间距为 layoutSpacingSpeedrun）
        /// 普通模式：自动居中布局
        /// </summary>
        private void ApplyHudLayoutForCurrentMode()
        {
            // 立即恢复原始布局，确保后续计算基于正确的基础位置
            RestoreOriginalHudLayout();

            bool isSpeedrun = SpeedrunService.Instance != null
                           && SpeedrunService.Instance.IsSpeedrunEnabled;

            if (isSpeedrun)
            {
                if (autoLayoutInSpeedrun)
                    ApplyCenteredHudLayout(layoutSpacingSpeedrun);
                return;
            }

            // 普通模式：自动居中布局
            if (autoLayoutWhenFireworkMissingInNormal)
            {
                ApplyCenteredHudLayout(layoutSpacingNormal);
            }
        }

        private void CacheOriginalHudAnchorsIfNeeded()
        {
            if (_originalAnchoredPositions.Count > 0)
                return;

            CacheRectTransformAnchor(normalCatsNode);
            CacheRectTransformAnchor(hiddenCatsNode);
            CacheRectTransformAnchor(fishNode);
            CacheRectTransformAnchor(fireworkNode);

            _searchRect = FindSearchRect();
            if (_searchRect != null)
                CacheRectTransformAnchor(_searchRect);
        }

        private void CacheRectTransformAnchor(Transform t)
        {
            if (t == null)
                return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
                CacheRectTransformAnchor(rt);
        }

        private void CacheRectTransformAnchor(RectTransform rt)
        {
            if (rt == null)
                return;
            if (_originalAnchoredPositions.ContainsKey(rt))
                return;
            _originalAnchoredPositions[rt] = rt.anchoredPosition;
        }

        private RectTransform FindSearchRect()
        {
            Transform numUIRoot = transform;
            Transform searchNode = numUIRoot.Find("Search");

            if (searchNode == null)
            {
                foreach (Transform child in numUIRoot)
                {
                    if (child.name == "Search" || child.name.Contains("Search"))
                    {
                        searchNode = child;
                        break;
                    }
                }
            }

            return searchNode != null ? searchNode.GetComponent<RectTransform>() : null;
        }

        private void RestoreOriginalHudLayout()
        {
            foreach (var kvp in _originalAnchoredPositions)
            {
                if (kvp.Key == null)
                    continue;
                kvp.Key.anchoredPosition = kvp.Value;
            }
        }

        private void ApplyCenteredHudLayout(float spacing)
        {
            CacheOriginalHudAnchorsIfNeeded();

            // 使用固定的节点顺序（按 NormalCats → HiddenCats → Search），
            // 避免依赖 x 坐标排序（竞速模式修改后 x 坐标会变化，导致排序不稳定）
            List<RectTransform> visible = new List<RectTransform>();
            AddIfVisible(normalCatsNode, visible);
            AddIfVisible(hiddenCatsNode, visible);
            AddIfVisible(fishNode, visible);
            AddIfVisible(fireworkNode, visible);

            if (_searchRect == null)
                _searchRect = FindSearchRect();
            if (_searchRect != null && _searchRect.gameObject.activeInHierarchy)
                visible.Add(_searchRect);

            if (visible.Count == 0)
                return;

            float totalWidth = 0f;
            for (int i = 0; i < visible.Count; i++)
            {
                totalWidth += visible[i].rect.width;
                if (i < visible.Count - 1)
                    totalWidth += spacing;
            }

            float startX = -totalWidth / 2f;
            float cursorX = startX;
            for (int i = 0; i < visible.Count; i++)
            {
                RectTransform rt = visible[i];
                float w = rt.rect.width;
                float centerX = cursorX + w / 2f;
                Vector2 pos = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(centerX, pos.y);
                cursorX += w + spacing;
            }

            if (enableDebugLog)
            {
                Debug.Log($"[NumUIController] Speedrun HUD layout applied. Elements={visible.Count}, spacing={spacing}, totalWidth={totalWidth}");
            }
        }

        private void AddIfVisible(Transform t, List<RectTransform> list)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
                list.Add(rt);
        }

        /// <summary>
        /// Check if we're subscribed to CollectionService events.
        /// </summary>
        private bool IsSubscribed()
        {
            // Simple check - if displays are initialized, we should be subscribed
            return _displays.Count > 0;
        }

        /// <summary>
        /// Re-center all visible UI elements.
        /// </summary>
        private void AdjustLayout()
        {
            // Legacy entry point: now we just apply the unified layout rules.
            ApplyHudLayoutForCurrentMode();
        }
    }

    /// <summary>
    /// Component for displaying a single item type's count.
    /// </summary>
    public class NumItemDisplay : MonoBehaviour
    {
        // Made public for debug logging access
        public TextMeshProUGUI _currentText;
        private TextMeshProUGUI _maxText;
        private TextMeshProUGUI _slashText;
        private CollectibleType _type;
        private int _maxCount;
        private GameObject _finishedNode;

        private const float NormalAlpha = 1f;
        private const float CompletedAlpha = 0.5f;

        public void Initialize(TextMeshProUGUI currentText, TextMeshProUGUI maxText, TextMeshProUGUI slashText, CollectibleType type, int maxCount)
        {
            _currentText = currentText;
            _maxText = maxText;
            _slashText = slashText;
            _type = type;
            _maxCount = maxCount;

            Debug.Log($"[NumItemDisplay] Initialize: type={type}, maxCount={maxCount}, currentText={currentText?.name}, maxText={maxText?.name}");

            // Find Finished node — it is a child of this NUm GameObject (same object the component is on)
            _finishedNode = transform.Find("Finished")?.gameObject;

            // Set initial max count display
            if (_maxText != null)
            {
                _maxText.text = _maxCount.ToString();
                Debug.Log($"[NumItemDisplay] Set maxText to: {_maxText.text}");
            }

            // Set initial current count to 0
            if (_currentText != null)
            {
                _currentText.text = "0";
                Debug.Log($"[NumItemDisplay] Set initial currentText to: 0");
            }

            // Hide Finished by default
            if (_finishedNode != null)
            {
                _finishedNode.SetActive(false);
            }
        }

        public void UpdateCurrentCount(int currentCount)
        {
            Debug.Log($"[NumItemDisplay] UpdateCurrentCount called: type={_type}, currentCount={currentCount}, maxCount={_maxCount}");

            if (_currentText != null)
            {
                _currentText.text = currentCount.ToString();
                Debug.Log($"[NumItemDisplay] Updated _currentText.text to: {currentCount}");
            }
            else
            {
                Debug.LogWarning($"[NumItemDisplay] _currentText is null!");
            }

            bool isCompleted = _maxCount > 0 && currentCount >= _maxCount;
            Debug.Log($"[NumItemDisplay] isCompleted={isCompleted} (maxCount={_maxCount}, currentCount={currentCount})");

            // Show/hide Finished node
            if (_finishedNode != null)
            {
                _finishedNode.SetActive(isCompleted);
            }

            float alpha = isCompleted ? CompletedAlpha : NormalAlpha;

            // Fade all three text elements: current count, slash, and max count
            if (_currentText != null)
            {
                Color c = _currentText.color;
                c.a = alpha;
                _currentText.color = c;
            }

            if (_slashText != null)
            {
                Color c = _slashText.color;
                c.a = alpha;
                _slashText.color = c;
            }

            if (_maxText != null)
            {
                Color c = _maxText.color;
                c.a = alpha;
                _maxText.color = c;
            }
        }

        public void UpdateMaxCount(int maxCount)
        {
            _maxCount = maxCount;
            if (_maxText != null)
            {
                _maxText.text = _maxCount.ToString();
            }
        }
    }
}
