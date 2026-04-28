using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using HiddenCats.Core;
using HiddenCats.UI;

namespace HiddenCats.Interactable
{
    /// <summary>
    /// Component for jigsaw puzzle piece interactable items.
    /// Handles click detection, collection animation, collection tracking, and state persistence.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Hidden Cats/Jigsaw Interactable")]
    public class JigsawInteractable : MonoBehaviour
    {
        [Header("Jigsaw Configuration")]
        [Tooltip("Unique identifier for this puzzle piece (e.g., Jigsaw01, Jigsaw05)")]
        [SerializeField] private string puzzlePieceId;

        [Header("Animation Configuration")]
        [Tooltip("Collection animation duration in seconds")]
        [SerializeField] private float collectionAnimationDuration = 0.3f;

        [Tooltip("Enable scale animation")]
        [SerializeField] private bool enableScaleAnimation = true;

        [Tooltip("Scale animation curve")]
        [SerializeField] private AnimationCurve scaleCurve;

        [Tooltip("Enable rotation animation")]
        [SerializeField] private bool enableRotationAnimation = false;

        [Tooltip("Rotation angle in degrees")]
        [SerializeField] private float rotationAngle = 360f;

        [Tooltip("Enable fade-out animation")]
        [SerializeField] private bool enableFadeAnimation = true;

        [Tooltip("Fade-out animation curve")]
        [SerializeField] private AnimationCurve fadeCurve;

        [Header("Scene Configuration")]
        [Tooltip("Scene name (should be RoomWnd, FlowerWnd, CafeWnd)")]
        [SerializeField] private string sceneName = HiddenCats.Core.SceneName.RoomWnd;

        [Header("Interaction Events")]
        [Tooltip("Configuration for interaction events triggered at different stages")]
        [SerializeField] private HiddenCats.Core.EventConfiguration[] eventConfigurations;

        [Header("Audio")]
        [Tooltip("Play audio when jigsaw piece is collected")]
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
        private float _initialRotation;
        private float _initialAlpha;

        // Events
        public event Action<JigsawInteractable> OnJigsawCollected;

        private void Awake()
        {
            _initiallyActiveInPrefab = gameObject.activeSelf;
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();

            if (_image == null)
            {
                Debug.LogError($"[JigsawInteractable] Image component not found on {gameObject.name}");
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

            // Generate unique ID for this jigsaw piece
            _uniqueId = GenerateUniqueId();

            // Initialize animation curves if not set
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = CreateEaseOutCurve(0f, 1f, 1f, 1.2987443f);
            }
            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = CreateEaseOutCurve(0f, 1f, 1f, 0f);
            }

            // Store initial values
            _initialScale = _rectTransform.localScale;
            _initialRotation = _rectTransform.localEulerAngles.z;
            _initialAlpha = _canvasGroup.alpha;

            // Initialize state
            _canvasGroup.alpha = _initialAlpha;
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
        /// Generate a unique ID for this jigsaw piece based on puzzle piece ID and scene.
        /// </summary>
        private string GenerateUniqueId()
        {
            if (!string.IsNullOrEmpty(puzzlePieceId))
            {
                return $"{sceneName}_Jigsaw_{puzzlePieceId}";
            }
            return $"{sceneName}_Jigsaw_{gameObject.name}";
        }

        /// <summary>
        /// Handle click event from SimpleClickDetector.
        /// </summary>
        private void HandleClick()
        {
            // In speedrun mode, jigsaw collection is disabled.
            if (SpeedrunService.Instance != null && SpeedrunService.Instance.IsSpeedrunEnabled)
                return;

            if (_isCollected || _isAnimating)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[JigsawInteractable] Jigsaw piece already collected or animating: {gameObject.name}");
                }
                return;
            }

            CollectJigsaw();
        }

        /// <summary>
        /// Collect this jigsaw piece: play collection animation, record collection, trigger events.
        /// </summary>
        public void CollectJigsaw()
        {
            if (_isCollected || _isAnimating)
            {
                return;
            }

            _isCollected = true;
            _isAnimating = true;

            ClickCollectFx.PlayAt(transform);

            // Disable interaction during animation
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // Trigger interaction events
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Collect,
                HiddenCats.Core.EventTriggerTiming.OnCollectStart);

            // Start collection animation coroutine
            StartCoroutine(CollectionAnimationCoroutine());

            // Play audio immediately when collection starts
            if (playAudioOnCollect)
            {
                AudioManager.Instance?.PlaySfx("FireFind");
            }
        }

        /// <summary>
        /// Coroutine that handles the collection animation.
        /// </summary>
        private IEnumerator CollectionAnimationCoroutine()
        {
            float elapsedTime = 0f;

            if (enableDebugLog)
            {
                Debug.Log($"[JigsawInteractable] Starting collection animation: {gameObject.name}");
            }

            while (elapsedTime < collectionAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / collectionAnimationDuration;

                // Scale animation
                if (enableScaleAnimation)
                {
                    float scaleValue = scaleCurve.Evaluate(normalizedTime);
                    _rectTransform.localScale = _initialScale * scaleValue;
                }

                // Rotation animation
                if (enableRotationAnimation)
                {
                    float rotationValue = Mathf.Lerp(0f, rotationAngle, normalizedTime);
                    Vector3 eulerAngles = _rectTransform.localEulerAngles;
                    eulerAngles.z = _initialRotation + rotationValue;
                    _rectTransform.localEulerAngles = eulerAngles;
                }

                // Fade animation
                if (enableFadeAnimation)
                {
                    float fadeValue = fadeCurve.Evaluate(normalizedTime);
                    _canvasGroup.alpha = _initialAlpha * fadeValue;
                }

                yield return null;
            }

            // Ensure final state
            if (enableScaleAnimation)
            {
                _rectTransform.localScale = _initialScale * scaleCurve.Evaluate(1f);
            }
            if (enableRotationAnimation)
            {
                Vector3 eulerAngles = _rectTransform.localEulerAngles;
                eulerAngles.z = _initialRotation + rotationAngle;
                _rectTransform.localEulerAngles = eulerAngles;
            }
            if (enableFadeAnimation)
            {
                _canvasGroup.alpha = 0f;
            }

            // Record collection in CollectionService
            if (CollectionService.Instance != null)
            {
                bool success = CollectionService.Instance.CollectItem(sceneName, CollectibleType.Jigsaw);
                if (enableDebugLog)
                {
                    Debug.Log($"[JigsawInteractable] Collection recorded: {gameObject.name}, Success: {success}");
                }
            }
            else
            {
                Debug.LogError($"[JigsawInteractable] CollectionService.Instance is null! Cannot record collection for {gameObject.name}");
            }

            // Invoke event
            OnJigsawCollected?.Invoke(this);

            // Save collection state
            SaveCollectionState();

            // Deactivate GameObject after animation completes
            gameObject.SetActive(false);

            _isAnimating = false;

            if (enableDebugLog)
            {
                Debug.Log($"[JigsawInteractable] Collection animation completed, GameObject deactivated: {gameObject.name}");
            }
        }

        /// <summary>
        /// Trigger matching interaction events from the configuration list.
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
        /// </summary>
        private void LoadCollectionState()
        {
            string key = GetSaveKey();

            // Apply reset-on-load for this key if the latest reset version has not been processed yet.
            if (Core.GameProgressResetService.ShouldApplyResetForKey(key))
            {
                _isCollected = false;
                _canvasGroup.alpha = _initialAlpha;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
                _rectTransform.localScale = _initialScale;
                Vector3 eulerAngles = _rectTransform.localEulerAngles;
                eulerAngles.z = _initialRotation;
                _rectTransform.localEulerAngles = eulerAngles;

                // Only activate if it was initially active in the prefab
                if (_initiallyActiveInPrefab)
                {
                    gameObject.SetActive(true);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                // Clear the key if it exists
                PlayerPrefs.DeleteKey(key);
                Core.GameProgressResetService.MarkResetAppliedForKey(key);

                if (enableDebugLog)
                {
                    Debug.Log($"[JigsawInteractable] Reset version detected, using uncollected state: {gameObject.name}");
                }
                return;
            }

            // Normal load: restore state from PlayerPrefs
            bool savedState = PlayerPrefs.GetInt(key, 0) == 1;

            if (savedState)
            {
                _isCollected = true;

                // Set to final animation state and deactivate
                if (enableFadeAnimation)
                {
                    _canvasGroup.alpha = 0f;
                }
                if (enableScaleAnimation)
                {
                    _rectTransform.localScale = _initialScale * scaleCurve.Evaluate(1f);
                }
                if (enableRotationAnimation)
                {
                    Vector3 eulerAngles = _rectTransform.localEulerAngles;
                    eulerAngles.z = _initialRotation + rotationAngle;
                    _rectTransform.localEulerAngles = eulerAngles;
                }
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);

                if (enableDebugLog)
                {
                    Debug.Log($"[JigsawInteractable] Loaded collected state: {gameObject.name}");
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
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Get the PlayerPrefs key for saving this jigsaw piece's state.
        /// </summary>
        private string GetSaveKey()
        {
            return $"Jigsaw_{_uniqueId}_Collected";
        }

        /// <summary>
        /// Check if this jigsaw piece has been collected.
        /// </summary>
        public bool IsCollected => _isCollected;

        /// <summary>
        /// Get the puzzle piece ID.
        /// </summary>
        public string PuzzlePieceId => puzzlePieceId;

        /// <summary>
        /// Get the scene name where this jigsaw is located.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Get the unique ID of this jigsaw.
        /// </summary>
        public string UniqueId => _uniqueId;

        public bool InitiallyActiveInPrefab => _initiallyActiveInPrefab;

        /// <summary>
        /// Reset this jigsaw piece to uncollected state.
        /// Only reactivates the GameObject if it was initially active in the prefab.
        /// </summary>
        public void ResetCollection()
        {
            _isCollected = false;
            _isAnimating = false;

            // Reset to initial state
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _initialAlpha;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = _initialScale;
                Vector3 eulerAngles = _rectTransform.localEulerAngles;
                eulerAngles.z = _initialRotation;
                _rectTransform.localEulerAngles = eulerAngles;
            }

            // Only activate if it was initially active in the prefab
            if (_initiallyActiveInPrefab)
            {
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }

            // Clear save data
            string key = GetSaveKey();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (enableDebugLog)
            {
                Debug.Log($"[JigsawInteractable] Reset collection state: {gameObject.name}");
            }
        }

        /// <summary>
        /// Create an ease-out animation curve.
        /// </summary>
        private static AnimationCurve CreateEaseOutCurve(float timeStart, float valueStart, float timeEnd, float valueEnd)
        {
            AnimationCurve curve = new AnimationCurve();
            Keyframe startKey = new Keyframe(timeStart, valueStart);
            startKey.outTangent = (valueEnd - valueStart) / (timeEnd - timeStart);
            curve.AddKey(startKey);

            Keyframe endKey = new Keyframe(timeEnd, valueEnd);
            endKey.inTangent = 0f;
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

            if (collectionAnimationDuration < 0f)
            {
                collectionAnimationDuration = 0.3f;
            }

            // Ensure animation curves are valid
            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = CreateEaseOutCurve(0f, 1f, 1f, 1.2987443f);
            }
            if (fadeCurve == null || fadeCurve.length == 0)
            {
                fadeCurve = CreateEaseOutCurve(0f, 1f, 1f, 0f);
            }
        }
        #endif
    }
}
