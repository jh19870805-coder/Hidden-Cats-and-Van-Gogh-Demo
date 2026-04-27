using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using HiddenCats.Core;
using HiddenCats.UI;

namespace HiddenCats.Interactable
{
    /// <summary>
    /// Component for fish interactable items.
    /// Handles click detection, fade-out animation, collection tracking, and state persistence.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Hidden Cats/Fish Interactable")]
    public class FishInteractable : MonoBehaviour
    {
        [Header("Animation Configuration")]
        [Tooltip("Fade-out animation duration in seconds")]
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        [Tooltip("Animation curve for fade-out (default: smooth ease-out)")]
        [SerializeField] private AnimationCurve fadeCurve;
        
        [Tooltip("Enable scale animation")]
        [SerializeField] private bool enableScaleAnimation = true;
        
        [Tooltip("Scale animation curve")]
        [SerializeField] private AnimationCurve scaleCurve;

        [Header("Scene Configuration")]
        [Tooltip("Scene name where this fish is located (e.g., SceneName.RoomWnd)")]
        [SerializeField] private string sceneName = HiddenCats.Core.SceneName.RoomWnd;

        [Header("Identity (Save Key)")]
        [Tooltip("Stable unique ID for this fish. Strongly recommended to set manually (e.g., FishCafe0101). " +
                 "If empty, a deterministic ID will be generated from the hierarchy path (sceneName + transform path + sibling indices).")]
        [SerializeField] private string stableFishId = string.Empty;

        [Header("Interaction Events")]
        [Tooltip("配置该鱼在不同阶段要触发的交互事件（点击、收集、完成等）")]
        [SerializeField] private HiddenCats.Core.EventConfiguration[] eventConfigurations;

        [Header("Audio")]
        [Tooltip("Play audio when fish is collected")]
        [SerializeField] private bool playAudioOnCollect = true;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        // Components
        private Image _image;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private SimpleClickDetector _clickDetector;
        
        // State
        private bool _isCollected = false;
        private bool _isAnimating = false;
        private string _uniqueId;
        private bool _initiallyActiveInPrefab;
        
        // Animation state
        private Vector3 _initialScale;

        // Events
        public event Action<FishInteractable> OnFishCollected;

        private void Awake()
        {
            // 记录该鱼在 Prefab 里是否默认活跃，用于 ResolveTotalFishCount 判断是否属于本局鱼。
            // 这个值在任何 SetActive 调用之前读取，是稳定的。
            _initiallyActiveInPrefab = gameObject.activeSelf;

            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            
            if (_image == null)
            {
                Debug.LogError($"[FishInteractable] Image component not found on {gameObject.name}");
                enabled = false;
                return;
            }

            // Try to get CanvasGroup, add if not exists
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Get or add SimpleClickDetector
            _clickDetector = GetComponent<SimpleClickDetector>();
            if (_clickDetector == null)
            {
                _clickDetector = gameObject.AddComponent<SimpleClickDetector>();
            }

            // Enable pixel-perfect detection for accurate click detection
            _clickDetector.SetPixelPerfectDetection(true);

            // Subscribe to click events
            _clickDetector.OnClickDetected += HandleClick;

            // Generate unique ID for this fish (based on scene and position)
            _uniqueId = GenerateStableUniqueId();

            // Initialize animation curves if not set
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = CreateEaseOutCurve(0f, 1f, 1f, 0.5f);
            }
            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = CreateEaseOutCurve(0f, 1f, 1f, 0f);
            }

            // Store initial values
            _initialScale = _rectTransform.localScale;

            // Initialize alpha to 1 (fully visible)
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // Check if already collected (load from save data)
            LoadCollectionState();
        }

        private void OnDestroy()
        {
            if (_clickDetector != null)
            {
                _clickDetector.OnClickDetected -= HandleClick;
            }
        }

        /// <summary>
        /// Generate a stable unique ID for this fish.
        /// 
        /// 强烈建议：在 Inspector 里显式填写 stableFishId（例如 FishCafe0101 / FishFlower03 / FishRoom01），
        /// 这样 ID 完全由你控制，不会受层级结构、实例顺序等影响。
        /// 
        /// 为了避免「每次运行 ID 都变 → 每次都被当成新鱼并触发 ResetVersion」这种情况，
        /// 这里不再依赖 siblingIndex / 动态 WindowRoot 路径，而是在没有 stableFishId 时退回到 GameObject.name。
        /// </summary>
        private string GenerateStableUniqueId()
        {
            if (!string.IsNullOrEmpty(stableFishId))
            {
                return $"{sceneName}_Fish_{stableFishId}";
            }

            // Fallback：使用「场景逻辑名 + 物体名」作为 ID。
            // 在当前设计下，每个 Wnd 里只有一条鱼时，这已经足够唯一、而且在运行之间是稳定的。
            // 如果后续在同一个 sceneName 里增加多条鱼，请务必为它们分别填写 stableFishId。
            return $"{sceneName}_Fish_{gameObject.name}";
        }

        /// <summary>
        /// Legacy ID generation (position-based). Kept only for save migration compatibility.
        /// </summary>
        private string GenerateLegacyPositionBasedId()
        {
            Vector3 position = transform.position;
            return $"{sceneName}_Fish_{position.x:F2}_{position.y:F2}_{position.z:F2}";
        }

        // 旧版曾经使用「WindowRoot + Transform 路径 + siblingIndex」来构建 ID，
        // 但在 StartUp 场景里动态实例化多个 Wnd 预制体、或 UI Layout 重新排序子物体时，
        // siblingIndex / WindowRoot 实例顺序都可能变化，从而导致 UniqueId 每次运行都不同，
        // 进而触发「每次启动都当成新鱼并应用 ResetVersion」的严重问题。
        //
        // 因此，这套路径算法已经废弃，仅保留为注释说明，不再参与运行时逻辑。

        /// <summary>
        /// Handle click event from SimpleClickDetector.
        /// </summary>
        private void HandleClick()
        {
            // In speedrun mode, fish collection is disabled.
            if (SpeedrunService.Instance != null && SpeedrunService.Instance.IsSpeedrunEnabled)
                return;

            if (_isCollected || _isAnimating)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[FishInteractable] Fish already collected or animating: {gameObject.name}");
                }
                return;
            }

            CollectFish();
        }

        /// <summary>
        /// Collect this fish: play fade-out animation, record collection, trigger events.
        /// </summary>
        public void CollectFish()
        {
            if (_isCollected || _isAnimating)
            {
                return;
            }

            // Play sound effect immediately on click, before any animation
            if (playAudioOnCollect)
            {
                AudioManager.PlayFishCollect();
            }

            _isCollected = true;
            _isAnimating = true;

            ClickCollectFx.PlayAt(transform);

            // Disable interaction during animation
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // Start fade-out animation coroutine (events will be triggered in the coroutine)
            StartCoroutine(FadeOutCoroutine());
        }

        /// <summary>
        /// Coroutine that handles the fade-out animation.
        /// </summary>
        private IEnumerator FadeOutCoroutine()
        {
            // Ensure fadeOutDuration is valid
            if (fadeOutDuration <= 0f)
            {
                Debug.LogWarning($"[FishInteractable] Invalid fadeOutDuration: {fadeOutDuration}, using default 0.5f");
                fadeOutDuration = 0.5f;
            }

            // Ensure fadeCurve is valid
            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = CreateEaseOutCurve(0f, 1f, 1f, 0f);
            }

            // 触发"收集开始"类型事件（在动画开始前触发，但确保不会影响动画）
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Collect,
                HiddenCats.Core.EventTriggerTiming.OnCollectStart);

            float elapsedTime = 0f;
            float startAlpha = _canvasGroup.alpha;

            if (enableDebugLog)
            {
                Debug.Log($"[FishInteractable] Starting fade-out animation: {gameObject.name}, Duration: {fadeOutDuration}, StartAlpha: {startAlpha}");
            }

            // Wait one frame before starting animation to ensure everything is set up
            yield return null;

            // Check if GameObject is still active (in case event deactivated it)
            if (!gameObject.activeInHierarchy)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[FishInteractable] GameObject was deactivated during event trigger, aborting animation: {gameObject.name}");
                }
                _isAnimating = false;
                yield break;
            }

            float lastLogTime = 0f;
            while (elapsedTime < fadeOutDuration)
            {
                // Check if GameObject is still active (in case something deactivated it)
                if (!gameObject.activeInHierarchy)
                {
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[FishInteractable] GameObject was deactivated during animation, aborting: {gameObject.name}");
                    }
                    _isAnimating = false;
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / fadeOutDuration);

                // Scale animation
                if (enableScaleAnimation)
                {
                    float scaleValue = scaleCurve.Evaluate(normalizedTime);
                    _rectTransform.localScale = _initialScale * scaleValue;
                }

                // Fade animation
                float curveValue = fadeCurve.Evaluate(normalizedTime);
                // curveValue goes from 1 (at start) to 0 (at end), so multiply directly to fade from startAlpha to 0
                _canvasGroup.alpha = startAlpha * curveValue;

                // Log progress every 0.1 seconds
                if (enableDebugLog && elapsedTime - lastLogTime >= 0.1f)
                {
                    Debug.Log($"[FishInteractable] Animation progress: {gameObject.name}, Elapsed: {elapsedTime:F2}s, Normalized: {normalizedTime:F2}, CurveValue: {curveValue:F2}, Alpha: {_canvasGroup.alpha:F2}");
                    lastLogTime = elapsedTime;
                }

                yield return null;
            }

            // Ensure final state
            if (enableScaleAnimation)
            {
                _rectTransform.localScale = _initialScale * scaleCurve.Evaluate(1f);
            }
            _canvasGroup.alpha = 0f;

            // Record collection in CollectionService
            if (CollectionService.Instance != null)
            {
                bool success = CollectionService.Instance.CollectItem(sceneName, CollectibleType.Fish);
                if (enableDebugLog)
                {
                    Debug.Log($"[FishInteractable] Collection recorded: {gameObject.name}, Success: {success}");
                }
            }
            else
            {
                Debug.LogError($"[FishInteractable] CollectionService.Instance is null! Cannot record collection for {gameObject.name}");
            }

            // Invoke event
            OnFishCollected?.Invoke(this);

            // Notify HintMagnifierService if active
            HiddenCats.UI.HintMagnifierService.Instance?.OnItemCollected(this);

            // Save collection state
            SaveCollectionState();

            _isAnimating = false;

            // Ensure alpha is 0 before deactivating
            _canvasGroup.alpha = 0f;
            
            // Wait one frame to ensure the alpha change is rendered before deactivating
            yield return null;
            
            // Deactivate GameObject after animation completes
            gameObject.SetActive(false);

            if (enableDebugLog)
            {
                Debug.Log($"[FishInteractable] Fade-out animation completed, GameObject deactivated: {gameObject.name}");
            }
        }

        /// <summary>
        /// 在配置列表中查找并触发匹配的交互事件。
        /// </summary>
        private void TriggerInteractionEvents(HiddenCats.Core.InteractionEventType type, HiddenCats.Core.EventTriggerTiming timing)
        {
            if (eventConfigurations == null || eventConfigurations.Length == 0)
            {
                return;
            }

            foreach (var config in eventConfigurations)
            {
                if (config == null)
                    continue;

                if (config.eventType == type && config.triggerTiming == timing)
                {
                    config.Trigger(this);
                }
            }
        }

        /// <summary>
        /// Load collection state from PlayerPrefs.
        /// Reset is applied once per save key using GameProgressResetService reset version.
        /// </summary>
        private void LoadCollectionState()
        {
            string key = GetSaveKey();
            string legacyKey = GetLegacySaveKey();
            
            // Apply reset-on-load for this key if the latest reset version has not been processed yet.
            if (Core.GameProgressResetService.ShouldApplyResetForKey(key))
            {
                _isCollected = false;
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
                _rectTransform.localScale = _initialScale;
                gameObject.SetActive(true);
                
                // Clear the key if it exists
                PlayerPrefs.DeleteKey(key);
                if (!string.IsNullOrEmpty(legacyKey))
                {
                    PlayerPrefs.DeleteKey(legacyKey);
                }
                Core.GameProgressResetService.MarkResetAppliedForKey(key);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[FishInteractable] Reset version detected, using uncollected state: {gameObject.name}");
                }
                return;
            }

            // Normal load: restore state from PlayerPrefs (scene switch should preserve state).
            // Migration: if the new key is empty but legacy key exists, migrate to new key.
            bool savedState = PlayerPrefs.GetInt(key, 0) == 1;
            if (!savedState && !string.IsNullOrEmpty(legacyKey))
            {
                bool legacySaved = PlayerPrefs.GetInt(legacyKey, 0) == 1;
                if (legacySaved)
                {
                    savedState = true;
                    PlayerPrefs.SetInt(key, 1);
                    PlayerPrefs.DeleteKey(legacyKey);
                    PlayerPrefs.Save();

                    if (enableDebugLog)
                    {
                        Debug.Log($"[FishInteractable] Migrated legacy save key to stable key: {gameObject.name}");
                    }
                }
            }
            
            if (savedState)
            {
                _isCollected = true;
                
                // Set to final animation state and deactivate
                _canvasGroup.alpha = 0f;
                if (enableScaleAnimation)
                {
                    _rectTransform.localScale = _initialScale * scaleCurve.Evaluate(1f);
                }
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);

                if (enableDebugLog)
                {
                    Debug.Log($"[FishInteractable] Loaded collected state: {gameObject.name}");
                }
            }
        }

        /// <summary>
        /// Save collection state to PlayerPrefs.
        /// </summary>
        private void SaveCollectionState()
        {
            string key = GetSaveKey();
            PlayerPrefs.SetInt(key, _isCollected ? 1 : 0);

            // Best-effort cleanup of legacy key to prevent collisions / confusion
            string legacyKey = GetLegacySaveKey();
            if (!string.IsNullOrEmpty(legacyKey))
            {
                PlayerPrefs.DeleteKey(legacyKey);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Get the PlayerPrefs key for saving this fish's state.
        /// </summary>
        private string GetSaveKey()
        {
            return $"Fish_{_uniqueId}_Collected";
        }

        private string GetLegacySaveKey()
        {
            // Only attempt legacy key if stableFishId is empty (i.e., likely old position-based ID was used)
            // or if old saves exist from previous versions.
            string legacyId = GenerateLegacyPositionBasedId();
            return $"Fish_{legacyId}_Collected";
        }

        /// <summary>
        /// Check if this fish has been collected.
        /// </summary>
        public bool IsCollected => _isCollected;

        /// <summary>
        /// Get the scene name where this fish is located.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Get the unique ID of this fish.
        /// </summary>
        public string UniqueId => _uniqueId;

        public bool InitiallyActiveInPrefab => _initiallyActiveInPrefab;

        /// <summary>
        /// Reset this fish to uncollected state (for testing or reset functionality).
        /// </summary>
        public void ResetCollection()
        {
            _isCollected = false;
            _isAnimating = false;

            // Reset to initial state and reactivate
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = _initialScale;
            }

            gameObject.SetActive(true);

            // Clear save data
            string key = GetSaveKey();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (enableDebugLog)
            {
                Debug.Log($"[FishInteractable] Reset collection state: {gameObject.name}");
            }
        }

        /// <summary>
        /// Create an ease-out animation curve.
        /// </summary>
        private static AnimationCurve CreateEaseOutCurve(float timeStart, float valueStart, float timeEnd, float valueEnd)
        {
            AnimationCurve curve = new AnimationCurve();
            // Add start keyframe with upward tangent
            Keyframe startKey = new Keyframe(timeStart, valueStart);
            startKey.outTangent = (valueEnd - valueStart) / (timeEnd - timeStart);
            curve.AddKey(startKey);
            
            // Add end keyframe with horizontal tangent (ease-out)
            Keyframe endKey = new Keyframe(timeEnd, valueEnd);
            endKey.inTangent = 0f; // Horizontal tangent for ease-out
            curve.AddKey(endKey);
            
            return curve;
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Validate configuration in Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = HiddenCats.Core.SceneName.RoomWnd;
            }

            if (fadeOutDuration < 0f)
            {
                fadeOutDuration = 0.5f;
            }

            // Ensure animation curves are valid
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = CreateEaseOutCurve(0f, 1f, 1f, 0.5f);
            }
            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = CreateEaseOutCurve(0f, 1f, 1f, 0f);
            }
        }
        #endif
    }
}
