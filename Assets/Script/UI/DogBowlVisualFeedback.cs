using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HiddenCats.Core;
using HiddenCats.Interactable;

namespace HiddenCats.UI
{
    /// <summary>
    /// Visual feedback controller for the dog bowl on MainWnd.
    /// Shows up to 4 fish sprites (Fish01–Fish04) based on the global fish collection progress.
    /// </summary>
    [AddComponentMenu("Hidden Cats/UI/DogBowl Visual Feedback")]
    [DefaultExecutionOrder(100)] // 确保在 WindowManager.Start 之后再执行 Start，避免需要多等一帧
    public sealed class DogBowlVisualFeedback : MonoBehaviour
    {
        [Header("Root References (Optional)")]
        [Tooltip("Optional root GameObject for the dog (for future animations or visibility control).")]
        [SerializeField] private GameObject dogRoot;

        [Tooltip("Optional root GameObject for the bowl (for future animations or visibility control).")]
        [SerializeField] private GameObject bowlRoot;

        [Header("Fish Images (Bottom to Top)")]
        [Tooltip("Fish images inside the bowl, ordered from bottom (Fish01) to top (Fish04).")]
        [SerializeField] private GameObject[] fishImages;

        [Header("Progress Configuration")]
        [Tooltip("If enabled, automatically detect the total fish count by scanning all FishInteractable in loaded objects (including inactive). This is recommended.")]
        [SerializeField] private bool autoDetectTotalFishCount = true;

        [Tooltip("When auto-detecting total fish count, only include FishInteractable whose SceneName is in this list. Leave empty to include all SceneNames.")]
        [SerializeField] private string[] includedFishSceneNames = new string[] { SceneName.RoomWnd };

        [Tooltip("Fallback total number of fish across all scenes. Used when auto-detection is disabled or returns 0.")]
        [SerializeField] private int totalFishCount = 0;

        [Tooltip("Automatically calculate thresholds based on TotalFishCount (Total / NumberOfFishImages). If disabled, use ManualThresholds instead.")]
        [SerializeField] private bool autoCalculateThresholds = true;

        [Tooltip("Manual thresholds for each fish image. Each value is the required global fish count to show the corresponding fish.")]
        [SerializeField] private int[] manualThresholds = new int[4];

        [Header("Crossfade (Normal ↔ Speedrun)")]
        [Tooltip("When fish visibility changes (e.g. switching save slot), fade out then fade in. First paint uses no fade.")]
        [SerializeField] private float crossfadeDuration = 0.3f;

        [Header("Debug")]
        [Tooltip("Enable debug logging for dog bowl updates.")]
        [SerializeField] private bool enableDebugLog = false;

        // Internal state
        private int[] _thresholds;
        private int _resolvedTotalFishCount;
        private bool _hasAppliedInitialFishVisual;
        private bool[] _lastFishShownState;
        private Coroutine _crossfadeRoutine;

        // 缓存相关：基于 active 鱼的 UniqueId 生成缓存键，确保重置后一致性
        private string _cachedFishCountKey = string.Empty;
        private int _cachedTotalFishCount = -1;

        private void Awake()
        {
            if (fishImages == null || fishImages.Length == 0)
            {
                Debug.LogWarning("[DogBowlVisualFeedback] FishImages are not configured. Component will be disabled.");
                enabled = false;
                return;
            }

            // 不在 Awake 里做任何基于 totalFishCount 的“快速估算”初始化，
            // 避免与后续自动探测结果不一致，确保多次启动前后一致。

            // Subscribe to global collection events if service is ready
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged += HandleGlobalCountChanged;
            }
            else if (enableDebugLog)
            {
                Debug.LogWarning("[DogBowlVisualFeedback] CollectionService.Instance is null in Awake. Will retry subscription in Start.");
            }

            // 在真正完成初始化前，先隐藏所有鱼，等 Start 协程根据自动探测结果统一刷新。
            for (int i = 0; i < fishImages.Length; i++)
            {
                if (fishImages[i] != null)
                {
                    fishImages[i].SetActive(false);
                }
            }

            // 订阅进度重置事件，确保重置后重新计算鱼的数量
            GameProgressResetService.OnGameProgressReset += OnGameProgressReset;
        }

        private void Start()
        {
            // WindowManager 会在默认执行顺序 (0) 的 Start 中预热 Room/Flower/Cafe。
            // 这里通过 DefaultExecutionOrder(100) 保证它先执行完，我们再初始化，
            // 就不需要额外再等一帧，从而避免第二次进入时“先空后亮”的闪一下。
            InitializeAndRefresh();
        }

        private void OnDestroy()
        {
            GameProgressResetService.OnGameProgressReset -= OnGameProgressReset;

            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged -= HandleGlobalCountChanged;
            }

            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = null;
            }
        }

        private void OnGameProgressReset()
        {
            // 进度重置时，清除缓存并重新计算
            _cachedFishCountKey = string.Empty;
            _cachedTotalFishCount = -1;

            // 重新初始化
            InitializeAndRefresh();

            if (enableDebugLog)
            {
                Debug.Log("[DogBowlVisualFeedback] Fish count cache invalidated due to progress reset");
            }
        }

        private void InitializeAndRefresh()
        {
            // 每次都基于“真实总鱼数”重新计算，确保与设计文档的 1/4、1/2、3/4、4/4 逻辑一致，
            // 并且多次启动前后一致。
            ResolveTotalFishCount();
            InitializeThresholds();

            // Retry subscription in case CollectionService wasn't ready in Awake
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged -= HandleGlobalCountChanged; // avoid double subscription
                CollectionService.Instance.OnGlobalCountChanged += HandleGlobalCountChanged;

                // Re-initialize visual state based on current global fish count
                // This ensures correct state even if thresholds were recalculated
                int currentFishCount = CollectionService.Instance.GetGlobalCount(CollectibleType.Fish);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[DogBowlVisualFeedback] Initializing. TotalFishCount={_resolvedTotalFishCount}, " +
                              $"CurrentFishCount={currentFishCount}, Thresholds=[{(_thresholds != null ? string.Join(", ", _thresholds) : "null")}]");
                }
                
                UpdateFishVisual(currentFishCount);
            }
            else if (enableDebugLog)
            {
                Debug.LogWarning("[DogBowlVisualFeedback] CollectionService.Instance is still null after initialization. Dog bowl will not react to collection events.");
            }
        }

        private void ResolveTotalFishCount()
        {
            _resolvedTotalFishCount = 0;

            if (autoDetectTotalFishCount)
            {
                try
                {
                    FishInteractable[] fish = FindObjectsOfType<FishInteractable>(true);
                    if (fish != null && fish.Length > 0)
                    {
                        HashSet<string> allowedScenes = BuildAllowedSceneSet();
                        HashSet<string> uniqueIds = new HashSet<string>();
                        int skippedByScene = 0;
                        int processedCount = 0;

                        foreach (var f in fish)
                        {
                            if (f == null)
                            {
                                continue;
                            }

                            // 使用 InitiallyActiveInPrefab 判断，可以正确区分「Prefabrication 里默认就是 Inactive」
                            // 和「被收集后 SetActive(false)」。
                            if (!f.InitiallyActiveInPrefab)
                            {
                                continue;
                            }

                            if (allowedScenes != null && allowedScenes.Count > 0 && !allowedScenes.Contains(f.SceneName))
                            {
                                skippedByScene++;
                                if (enableDebugLog)
                                {
                                    Debug.Log($"[DogBowlVisualFeedback] Skipping fish from scene '{f.SceneName}' (not in included scenes)");
                                }
                                continue;
                            }

                            // Use UniqueId to guard against accidental duplicates.
                            // If UniqueId is empty for some reason, fall back to instance ID.
                            string key = !string.IsNullOrEmpty(f.UniqueId) ? f.UniqueId : f.GetInstanceID().ToString();
                            if (uniqueIds.Add(key))
                            {
                                processedCount++;
                                if (enableDebugLog)
                                {
                                    Debug.Log($"[DogBowlVisualFeedback] Found fish: Scene={f.SceneName}, UniqueId={f.UniqueId}, Collected={f.IsCollected}");
                                }
                            }
                        }

                        // 生成缓存键（按字母排序确保顺序一致）
                        var sortedIds = new List<string>(uniqueIds);
                        sortedIds.Sort();
                        string newCacheKey = string.Join("|", sortedIds);

                        // 如果缓存键没变，直接返回缓存的总数
                        if (_cachedFishCountKey == newCacheKey && _cachedTotalFishCount >= 0)
                        {
                            _resolvedTotalFishCount = _cachedTotalFishCount;
                            if (enableDebugLog)
                            {
                                Debug.Log($"[DogBowlVisualFeedback] Using cached fish total: {_cachedTotalFishCount}");
                            }
                            return;
                        }

                        // 缓存失效，更新缓存键和总数
                        _cachedFishCountKey = newCacheKey;
                        _cachedTotalFishCount = uniqueIds.Count;
                        _resolvedTotalFishCount = _cachedTotalFishCount;

                        if (enableDebugLog)
                        {
                            Debug.Log($"[DogBowlVisualFeedback] Fish total updated: {_cachedTotalFishCount}, key={_cachedFishCountKey}");
                        }
                    }
                    else
                    {
                        if (enableDebugLog)
                        {
                            Debug.LogWarning("[DogBowlVisualFeedback] No FishInteractable found in loaded scenes. " +
                                           "This may happen if other scene prefabs are not yet loaded.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DogBowlVisualFeedback] Failed to auto-detect total fish count: {e.Message}");
                    _resolvedTotalFishCount = 0;
                }
            }

            if (_resolvedTotalFishCount <= 0)
            {
                _resolvedTotalFishCount = Mathf.Max(0, totalFishCount);
                if (enableDebugLog)
                {
                    Debug.Log($"[DogBowlVisualFeedback] Using fallback TotalFishCount from Inspector: {_resolvedTotalFishCount}");
                }
                
                if (_resolvedTotalFishCount <= 0)
                {
                    Debug.LogWarning("[DogBowlVisualFeedback] Total fish count is 0! " +
                                    "Fish images will not display correctly. " +
                                    "Please either:\n" +
                                    "1. Ensure autoDetectTotalFishCount can find fish (check if scene prefabs are loaded), or\n" +
                                    "2. Set totalFishCount manually in the Inspector.");
                }
            }
        }

        private HashSet<string> BuildAllowedSceneSet()
        {
            if (includedFishSceneNames == null || includedFishSceneNames.Length == 0)
            {
                return null; // Treat as "include all"
            }

            HashSet<string> set = new HashSet<string>();
            foreach (var s in includedFishSceneNames)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    set.Add(s);
                }
            }
            return set;
        }

        /// <summary>
        /// Quick initialization of thresholds using a known total fish count.
        /// Used in Awake() to prevent flash on startup.
        /// </summary>
        private void InitializeThresholdsQuick(int totalCount)
        {
            int fishCount = fishImages.Length;
            _thresholds = new int[fishCount];
            _resolvedTotalFishCount = totalCount;

            if (autoCalculateThresholds && totalCount > 0)
            {
                // Evenly split totalCount into N segments
                float segment = totalCount / (float)fishCount;
                for (int i = 0; i < fishCount; i++)
                {
                    int t = Mathf.CeilToInt(segment * (i + 1));
                    _thresholds[i] = Mathf.Clamp(t, 1, totalCount);
                }
            }
            else if (manualThresholds != null && manualThresholds.Length > 0)
            {
                for (int i = 0; i < fishCount; i++)
                {
                    _thresholds[i] = i < manualThresholds.Length ? Mathf.Max(0, manualThresholds[i]) : 0;
                }
            }
            else
            {
                // Default progressive thresholds
                for (int i = 0; i < fishCount; i++)
                {
                    _thresholds[i] = i + 1;
                }
            }
        }

        /// <summary>
        /// Initialize thresholds array either automatically from totalFishCount or from manualThresholds.
        /// </summary>
        private void InitializeThresholds()
        {
            int fishCount = fishImages.Length;
            _thresholds = new int[fishCount];

            if (autoCalculateThresholds && _resolvedTotalFishCount > 0)
            {
                // Evenly split totalFishCount into N segments (ceil to ensure progress feels fair)
                float segment = _resolvedTotalFishCount / (float)fishCount;
                for (int i = 0; i < fishCount; i++)
                {
                    int t = Mathf.CeilToInt(segment * (i + 1));
                    _thresholds[i] = Mathf.Clamp(t, 1, _resolvedTotalFishCount);
                }

                if (enableDebugLog)
                {
                    Debug.Log($"[DogBowlVisualFeedback] Auto thresholds from totalFishCount={_resolvedTotalFishCount}: " +
                              string.Join(", ", _thresholds));
                }
            }
            else
            {
                // Use manual thresholds; if array size mismatches, resize safely
                if (manualThresholds == null || manualThresholds.Length == 0)
                {
                    manualThresholds = new int[fishCount];
                }
                else if (manualThresholds.Length != fishCount)
                {
                    int[] resized = new int[fishCount];
                    for (int i = 0; i < fishCount; i++)
                    {
                        resized[i] = i < manualThresholds.Length ? manualThresholds[i] : 0;
                    }
                    manualThresholds = resized;
                }

                // Check if all manual thresholds are zero (unconfigured)
                bool allZero = true;
                for (int i = 0; i < fishCount; i++)
                {
                    if (manualThresholds[i] > 0)
                    {
                        allZero = false;
                        break;
                    }
                }

                if (allZero && autoCalculateThresholds)
                {
                    // Auto-calculate was requested but totalFishCount is 0, use default progressive thresholds
                    Debug.LogWarning($"[DogBowlVisualFeedback] Auto-calculate enabled but totalFishCount is 0. " +
                                    $"Using default progressive thresholds (1, 2, 3, 4...). " +
                                    $"Please check autoDetectTotalFishCount or set totalFishCount manually.");
                    for (int i = 0; i < fishCount; i++)
                    {
                        _thresholds[i] = i + 1; // Progressive: 1, 2, 3, 4...
                    }
                }
                else
                {
                    for (int i = 0; i < fishCount; i++)
                    {
                        _thresholds[i] = Mathf.Max(0, manualThresholds[i]);
                    }
                }

                if (enableDebugLog)
                {
                    Debug.Log("[DogBowlVisualFeedback] Using manual thresholds: " +
                              string.Join(", ", _thresholds));
                }
            }

            // Don't hide fish here if thresholds are already initialized (from quick init in Awake)
            // The visual state should already be set correctly
            if (_thresholds == null || _thresholds.Length == 0)
            {
                // Ensure all fish start hidden until we apply the first update
                for (int i = 0; i < fishImages.Length; i++)
                {
                    if (fishImages[i] != null)
                    {
                        fishImages[i].SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Event handler for global count changes from CollectionService.
        /// Only reacts to fish collection.
        /// </summary>
        private void HandleGlobalCountChanged(CollectibleType type, int newGlobalCount)
        {
            if (type != CollectibleType.Fish)
            {
                return;
            }

            // Ensure thresholds are initialized before updating visual
            // This handles the case where event fires before InitializeAndRefreshNextFrame completes
            if (_thresholds == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[DogBowlVisualFeedback] Thresholds not initialized yet, initializing now.");
                }
                ResolveTotalFishCount();
                InitializeThresholds();
            }

            UpdateFishVisual(newGlobalCount);
        }

        /// <summary>
        /// Update fish visuals based on current global fish count.
        /// </summary>
        private void UpdateFishVisual(int currentFishCount)
        {
            if (fishImages == null || fishImages.Length == 0)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[DogBowlVisualFeedback] FishImages is null or empty, cannot update visual.");
                }
                return;
            }

            if (_thresholds == null || _thresholds.Length == 0)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[DogBowlVisualFeedback] Thresholds not initialized, cannot update visual. " +
                                     $"CurrentFishCount={currentFishCount}");
                }
                return;
            }

            bool[] newState = ComputeFishShownState(currentFishCount);
            if (newState == null)
            {
                return;
            }

            // First paint: no crossfade (avoids fade-in from Awake hidden state).
            if (!_hasAppliedInitialFishVisual)
            {
                ApplyFishVisualImmediate(currentFishCount, newState);
                _hasAppliedInitialFishVisual = true;
                return;
            }

            if (crossfadeDuration <= 0f || StatesEqual(_lastFishShownState, newState))
            {
                ApplyFishVisualImmediate(currentFishCount, newState);
                return;
            }

            // Coroutines do not run while this hierarchy is inactive (e.g. MainWnd hidden during RoomWnd).
            // Starting crossfade here would leave visuals stuck until restart / mode switch forces another path.
            if (!gameObject.activeInHierarchy)
            {
                ApplyFishVisualImmediate(currentFishCount, newState);
                return;
            }

            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = null;
            }

            _crossfadeRoutine = StartCoroutine(CrossfadeFishVisualRoutine(newState, currentFishCount));
        }

        /// <summary>
        /// When MainWnd becomes visible again, resync from CollectionService so we never show stale fish
        /// if an update fired while the window was inactive.
        /// </summary>
        private void OnEnable()
        {
            if (CollectionService.Instance == null || fishImages == null || fishImages.Length == 0)
            {
                return;
            }

            if (!_hasAppliedInitialFishVisual || _thresholds == null || _thresholds.Length == 0)
            {
                return;
            }

            int count = CollectionService.Instance.GetGlobalCount(CollectibleType.Fish);
            UpdateFishVisual(count);
        }

        private bool[] ComputeFishShownState(int currentFishCount)
        {
            var state = new bool[fishImages.Length];
            for (int i = 0; i < fishImages.Length; i++)
            {
                int threshold = i < _thresholds.Length ? _thresholds[i] : 0;
                state[i] = currentFishCount >= threshold && threshold > 0;
            }

            return state;
        }

        private static bool StatesEqual(bool[] a, bool[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyFishVisualImmediate(int currentFishCount, bool[] shouldShow)
        {
            for (int i = 0; i < fishImages.Length; i++)
            {
                GameObject fish = fishImages[i];
                if (fish == null)
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[DogBowlVisualFeedback] Fish image at index {i} is null.");
                    }

                    continue;
                }

                bool show = i < shouldShow.Length && shouldShow[i];
                bool wasActive = fish.activeSelf;
                fish.SetActive(show);
                SetFishImageAlpha(fish, show ? 1f : 0f);
                if (enableDebugLog && wasActive != show)
                {
                    int threshold = i < _thresholds.Length ? _thresholds[i] : 0;
                    Debug.Log($"[DogBowlVisualFeedback] Fish {i + 1} {(show ? "shown" : "hidden")}. " +
                              $"CurrentCount={currentFishCount}, Threshold={threshold}");
                }
            }

            _lastFishShownState = shouldShow;

            if (enableDebugLog)
            {
                Debug.Log($"[DogBowlVisualFeedback] Updated fish visual. CurrentFishCount={currentFishCount}, " +
                          $"Thresholds=[{string.Join(", ", _thresholds)}], " +
                          $"TotalFishCount={_resolvedTotalFishCount}");
            }
        }

        private static void SetFishImageAlpha(GameObject fishRoot, float alpha)
        {
            if (fishRoot == null)
            {
                return;
            }

            Image img = fishRoot.GetComponent<Image>();
            if (img == null)
            {
                img = fishRoot.GetComponentInChildren<Image>(true);
            }

            if (img == null)
            {
                return;
            }

            Color c = img.color;
            c.a = Mathf.Clamp01(alpha);
            img.color = c;
        }

        private IEnumerator CrossfadeFishVisualRoutine(bool[] newState, int currentFishCount)
        {
            float half = Mathf.Max(0.0001f, crossfadeDuration * 0.5f);

            // Fade out currently visible fish.
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                float a = 1f - k;
                for (int i = 0; i < fishImages.Length; i++)
                {
                    if (fishImages[i] != null && fishImages[i].activeSelf)
                    {
                        SetFishImageAlpha(fishImages[i], a);
                    }
                }

                yield return null;
            }

            // Apply new visibility at alpha 0, then fade in.
            for (int i = 0; i < fishImages.Length; i++)
            {
                if (fishImages[i] == null)
                {
                    continue;
                }

                bool show = i < newState.Length && newState[i];
                fishImages[i].SetActive(show);
                SetFishImageAlpha(fishImages[i], show ? 0f : 0f);
            }

            _lastFishShownState = newState;

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / half);
                for (int i = 0; i < fishImages.Length; i++)
                {
                    if (fishImages[i] != null && fishImages[i].activeSelf && (i < newState.Length && newState[i]))
                    {
                        SetFishImageAlpha(fishImages[i], a);
                    }
                }

                yield return null;
            }

            for (int i = 0; i < fishImages.Length; i++)
            {
                if (fishImages[i] != null && fishImages[i].activeSelf)
                {
                    SetFishImageAlpha(fishImages[i], 1f);
                }
            }

            if (enableDebugLog)
            {
                Debug.Log($"[DogBowlVisualFeedback] Crossfade complete. CurrentFishCount={currentFishCount}");
            }

            _crossfadeRoutine = null;
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only validation to keep arrays in sync and provide basic safety checks.
        /// </summary>
        private void OnValidate()
        {
            if (fishImages == null)
            {
                return;
            }

            if (manualThresholds == null || manualThresholds.Length != fishImages.Length)
            {
                int fishCount = fishImages.Length;
                int[] resized = new int[fishCount];
                if (manualThresholds != null)
                {
                    for (int i = 0; i < fishCount; i++)
                    {
                        resized[i] = i < manualThresholds.Length ? manualThresholds[i] : 0;
                    }
                }
                manualThresholds = resized;
            }

            if (totalFishCount < 0)
            {
                totalFishCount = 0;
            }
        }
        #endif
    }
}

