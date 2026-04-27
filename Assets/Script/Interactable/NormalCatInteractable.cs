using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using HiddenCats.Core;
using HiddenCats.UI;

namespace HiddenCats.Interactable
{
    /// <summary>
    /// Component for normal cat interactable items.
    /// Handles click detection, sprite switching, collection tracking, and special events.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Hidden Cats/Normal Cat Interactable")]
    public class NormalCatInteractable : MonoBehaviour
    {
        [Header("Sprite Configuration")]
        [Tooltip("Normal state sprite (before collection)")]
        [SerializeField] private Sprite normalSprite;
        
        [Tooltip("Collected state sprite (after collection)")]
        [SerializeField] private Sprite collectedSprite;

        [Header("Scene Configuration")]
        [Tooltip("Scene name where this cat is located (e.g., SceneName.RoomWnd)")]
        [SerializeField] private string sceneName = HiddenCats.Core.SceneName.RoomWnd;

        [Header("Interaction Events")]
        [Tooltip("配置该猫在不同阶段要触发的交互事件（点击、收集、完成等）")]
        [SerializeField] private HiddenCats.Core.EventConfiguration[] eventConfigurations;

        [Header("Special Events (Legacy Support)")]
        [Tooltip("Enable special event trigger when this cat is collected")]
        [SerializeField] private bool enableSpecialEvent = false;
        
        [Tooltip("GameObject to activate when this cat is collected (e.g., window that opens)")]
        [SerializeField] private GameObject specialEventTarget;

        [Header("Room window overlay (optional)")]
        [Tooltip("Only assign on the one cat that should close the overlay (e.g. RoomWnd 024CatRoom → Window). Other cats must leave this empty or they will drive Window/CafeBtn.")]
        [SerializeField] private GameObject roomWindowOverlay;

        [Tooltip("Optional; pair with roomWindowOverlay. Leave empty on all other cats.")]
        [SerializeField] private Button roomCafeButton;

        [Header("Audio")]
        [Tooltip("Play audio when cat is collected")]
        [SerializeField] private bool playAudioOnCollect = true;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        // Components
        private Image _image;
        private SimpleClickDetector _clickDetector;
        
        // State
        private bool _isCollected = false;
        private string _uniqueId;
        private bool _initiallyActiveInPrefab;

        // Events
        public event Action<NormalCatInteractable> OnCatCollected;

        private Coroutine _overlayReassertCo;

        private void Awake()
        {
            // 记录该猫在 Prefab 里是否默认活跃，用于统计总猫数时区分「Prefab 默认 inactive」和「被收集后 inactive」。
            _initiallyActiveInPrefab = gameObject.activeSelf;

            _image = GetComponent<Image>();
            if (_image == null)
            {
                Debug.LogError($"[NormalCatInteractable] Image component not found on {gameObject.name}");
                enabled = false;
                return;
            }

            // Get or add SimpleClickDetector
            _clickDetector = GetComponent<SimpleClickDetector>();
            if (_clickDetector == null)
            {
                _clickDetector = gameObject.AddComponent<SimpleClickDetector>();
            }

            // Pixel-perfect ignores clicks on transparent pixels; 018CatRoom-style sprites are often mostly alpha — use rect hits when this cat drives the RoomWnd overlay.
            _clickDetector.SetPixelPerfectDetection(GetEffectiveRoomWindowOverlay() == null);

            // Subscribe to click events
            _clickDetector.OnClickDetected += HandleClick;

            // Generate unique ID for this cat (based on scene and position)
            _uniqueId = GenerateUniqueId();

            // Initialize sprite
            if (normalSprite != null)
            {
                _image.sprite = normalSprite;
            }
            else if (_image.sprite == null)
            {
                Debug.LogWarning($"[NormalCatInteractable] Normal sprite not set on {gameObject.name}");
            }

            // Check if already collected (load from save data)
            LoadCollectionState();
        }

        private void OnEnable()
        {
            // Re-sync overlay / CafeBtn when returning to RoomWnd or after hierarchy changes (e.g. GameSceneUI content root).
            ApplyRoomWindowOverlayState();
        }

        private void OnDestroy()
        {
            if (_overlayReassertCo != null)
            {
                StopCoroutine(_overlayReassertCo);
                _overlayReassertCo = null;
            }

            if (_clickDetector != null)
            {
                _clickDetector.OnClickDetected -= HandleClick;
            }
        }

        /// <summary>
        /// RoomWnd UI lives under a canvas; <see cref="Transform.root"/> may be the Canvas, not the window prefab root.
        /// </summary>
        private Transform GetRoomWindowRoot()
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.GetComponent<RoomWindowGate>() != null)
                {
                    return t;
                }

                t = t.parent;
            }

            return transform.root;
        }

        /// <summary>
        /// Inspector ref only — no auto path-find when unset, so other cats never affect Window.
        /// If the serialized ref breaks after hierarchy moves, repair from path once.
        /// </summary>
        private GameObject GetEffectiveRoomWindowOverlay()
        {
            if (roomWindowOverlay == null)
            {
                return null;
            }

            Transform roomRoot = GetRoomWindowRoot();
            if (roomWindowOverlay.transform.IsChildOf(roomRoot))
            {
                return roomWindowOverlay;
            }

            return RoomWindowGate.FindWindowOverlay(roomRoot);
        }

        private Button GetEffectiveRoomCafeButton()
        {
            if (roomCafeButton == null)
            {
                return null;
            }

            Transform roomRoot = GetRoomWindowRoot();
            if (roomCafeButton.transform.IsChildOf(roomRoot))
            {
                return roomCafeButton;
            }

            return RoomWindowGate.FindCafeButton(roomRoot);
        }

        /// <summary>
        /// Generate a unique ID for this cat based on scene name and world position.
        /// </summary>
        private string GenerateUniqueId()
        {
            Vector3 position = transform.position;
            return $"{sceneName}_NormalCat_{position.x:F2}_{position.y:F2}_{position.z:F2}";
        }

        /// <summary>
        /// Handle click event from SimpleClickDetector.
        /// </summary>
        private void HandleClick()
        {
            if (_isCollected)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[NormalCatInteractable] Cat already collected: {gameObject.name}");
                }
                return;
            }

            // 触发“点击”阶段的交互事件（例如气泡提示、光效等）
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Click,
                HiddenCats.Core.EventTriggerTiming.Immediate);

            CollectCat();
        }

        /// <summary>
        /// Collect this cat: switch sprite, record collection, trigger events.
        /// </summary>
        public void CollectCat()
        {
            if (_isCollected)
            {
                return;
            }

            _isCollected = true;

            ClickCollectFx.PlayAt(transform);

            // 在正式记录收集前，触发“收集开始”阶段事件
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Collect,
                HiddenCats.Core.EventTriggerTiming.OnCollectStart);

            // Switch sprite
            if (collectedSprite != null)
            {
                _image.sprite = collectedSprite;
                
                if (enableDebugLog)
                {
                    Debug.Log($"[NormalCatInteractable] Switched sprite to collected state: {gameObject.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[NormalCatInteractable] Collected sprite not set on {gameObject.name}, sprite will not change");
            }

            // Record collection in CollectionService
            if (CollectionService.Instance != null)
            {
                bool success = CollectionService.Instance.CollectItem(sceneName, CollectibleType.NormalCat);
                if (enableDebugLog)
                {
                    Debug.Log($"[NormalCatInteractable] Collection recorded: {gameObject.name}, Success: {success}");
                }
            }
            else
            {
                Debug.LogError($"[NormalCatInteractable] CollectionService.Instance is null! Cannot record collection for {gameObject.name}");
            }

            if (playAudioOnCollect)
            {
                AudioManager.Instance?.PlayRandomCatMeow();
            }

            // Invoke event
            OnCatCollected?.Invoke(this);

            // Notify HintMagnifierService if active
            HintMagnifierService.Instance?.OnItemCollected(this);

            // Save collection state
            SaveCollectionState();

            ApplyRoomWindowOverlayState();
            if (GetEffectiveRoomWindowOverlay() != null)
            {
                AudioManager.Instance?.PlaySfx("WindowOpen");

                if (_overlayReassertCo != null)
                {
                    StopCoroutine(_overlayReassertCo);
                }

                _overlayReassertCo = StartCoroutine(CoReassertRoomWindowOverlayAfterEvents());
            }
        }

        /// <summary>
        /// Interaction events / delayed UnityEvents may toggle RoomBg/Window after CollectCat; re-apply overlay state a few times.
        /// </summary>
        private IEnumerator CoReassertRoomWindowOverlayAfterEvents()
        {
            yield return null;
            ApplyRoomWindowOverlayState();
            yield return new WaitForSecondsRealtime(0.05f);
            ApplyRoomWindowOverlayState();
            yield return new WaitForSecondsRealtime(0.2f);
            ApplyRoomWindowOverlayState();
            yield return new WaitForSecondsRealtime(0.5f);
            ApplyRoomWindowOverlayState();
            _overlayReassertCo = null;
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
            
            // Apply reset-on-load for this key if the latest reset version has not been processed yet.
            if (Core.GameProgressResetService.ShouldApplyResetForKey(key))
            {
                _isCollected = false;
                // Clear the key if it exists
                PlayerPrefs.DeleteKey(key);
                Core.GameProgressResetService.MarkResetAppliedForKey(key);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[NormalCatInteractable] Reset version detected, using uncollected state: {gameObject.name}");
                }
                ApplyRoomWindowOverlayState();
                return;
            }
            
            // Normal load: restore state from PlayerPrefs (scene switch should preserve state)
            bool savedState = PlayerPrefs.GetInt(key, 0) == 1;
            
            if (savedState)
            {
                _isCollected = true;
                
                // Switch to collected sprite
                if (collectedSprite != null)
                {
                    _image.sprite = collectedSprite;
                }

                // Activate special event target if enabled
                if (enableSpecialEvent && specialEventTarget != null)
                {
                    specialEventTarget.SetActive(true);
                }

                if (enableDebugLog)
                {
                    Debug.Log($"[NormalCatInteractable] Loaded collected state: {gameObject.name}");
                }
            }

            ApplyRoomWindowOverlayState();
        }

        /// <summary>
        /// When <see cref="roomWindowOverlay"/> is assigned, show it only while this cat is not yet collected.
        /// </summary>
        private void ApplyRoomWindowOverlayState()
        {
            GameObject overlay = GetEffectiveRoomWindowOverlay();
            Button cafe = GetEffectiveRoomCafeButton();

            if (overlay != null)
            {
                overlay.SetActive(!_isCollected);
            }

            // Drive CafeBtn directly when this cat owns the overlay (spec: 018CatRoom). Singleton gate can miss if Awake order/instance differs during prewarm.
            if (cafe != null && overlay != null)
            {
                cafe.interactable = !overlay.activeSelf;
            }

            // Cats sit above RoomBg in __ContentRoot sibling order; after collection, keep this Image from blocking rays to CafeBtn.
            if (overlay != null && _image != null)
            {
                _image.raycastTarget = !_isCollected;
            }

            RoomWindowGate.RefreshIfInstanceExists();
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
        /// Get the PlayerPrefs key for saving this cat's state.
        /// Uses "SR_" prefix when speedrun mode is active so normal and speedrun
        /// save data stay independent.
        /// </summary>
        private string GetSaveKey()
        {
            return $"{SpeedrunService.GetSaveKeyPrefix()}NormalCat_{_uniqueId}_Collected";
        }

        /// <summary>
        /// Check if this cat has been collected.
        /// </summary>
        public bool IsCollected => _isCollected;

        /// <summary>
        /// 该猫在 Prefab 里是否默认活跃（实例化时的 activeSelf）。
        /// </summary>
        public bool InitiallyActiveInPrefab => _initiallyActiveInPrefab;

        /// <summary>
        /// Get the scene name where this cat is located.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Get the unique ID of this cat.
        /// </summary>
        public string UniqueId => _uniqueId;

        /// <summary>
        /// Reload visual / collection state from the current mode's save key.
        /// Called by SpeedrunService when the mode toggles on/off so each cat
        /// picks up the correct save data.
        /// </summary>
        public void ReloadCollectionState()
        {
            LoadCollectionState();
        }

        /// <summary>
        /// Reset this cat to uncollected state (for testing or reset functionality).
        /// </summary>
        public void ResetCollection()
        {
            _isCollected = false;
            
            // Switch back to normal sprite
            if (normalSprite != null && _image != null)
            {
                _image.sprite = normalSprite;
            }

            // Deactivate special event target
            if (enableSpecialEvent && specialEventTarget != null)
            {
                specialEventTarget.SetActive(false);
            }

            ApplyRoomWindowOverlayState();

            // Clear save data
            string key = GetSaveKey();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (enableDebugLog)
            {
                Debug.Log($"[NormalCatInteractable] Reset collection state: {gameObject.name}");
            }
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
        }
        #endif
    }
}
