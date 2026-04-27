using UnityEngine;
using UnityEngine.UI;
using System;
using HiddenCats.Core;
using HiddenCats.UI;

namespace HiddenCats.Interactable
{
    /// <summary>
    /// Simplified component for hidden cat interactable items.
    /// Implements a two-stage interaction mechanism:
    /// 1. Click hidden trigger area → Image B appears
    /// 2. Click Image B → Image C appears (hidden cat found)
    /// </summary>
    [AddComponentMenu("Hidden Cats/Hidden Cat Interactable")]
    public class HiddenCatInteractable : MonoBehaviour
    {
        [Header("Stage 1: Trigger Area")]
        [Tooltip("The trigger area GameObject (must have Image component)")]
        [SerializeField] private GameObject triggerArea;
        
        [Header("Stage 2: Image B")]
        [Tooltip("Image B GameObject that appears after clicking trigger")]
        [SerializeField] private GameObject imageB;
        
        [Header("Stage 3: Image C (Final)")]
        [Tooltip("Image C GameObject that appears after clicking Image B")]
        [SerializeField] private GameObject imageC;

        [Header("Scene Configuration")]
        [Tooltip("Scene name where this hidden cat is located")]
        [SerializeField] private string sceneName = HiddenCats.Core.SceneName.RoomWnd;

        [Header("Interaction Events")]
        [Tooltip("配置该隐藏猫在不同阶段要触发的交互事件（点击、收集、完成等）")]
        [SerializeField] private HiddenCats.Core.EventConfiguration[] eventConfigurations;

        [Header("Audio")]
        [Tooltip("Play audio when hidden cat is found")]
        [SerializeField] private bool playAudioOnFound = true;

        [Header("Editor Preview")]
        [Tooltip("Preview state in editor (for layout purposes)")]
        [SerializeField] private EditorPreviewState editorPreviewState = EditorPreviewState.TriggerArea;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        // Components
        private SimpleClickDetector _triggerDetector;
        private SimpleClickDetector _imageBDetector;

        // State
        private State _currentState = State.Initial;
        private string _uniqueId;
        private bool _initiallyActiveInPrefab;

        // Events
        public event Action<HiddenCatInteractable> OnHiddenCatFound;

        /// <summary>
        /// Current state of the hidden cat interaction.
        /// </summary>
        private enum State
        {
            Initial = 0,  // Waiting for trigger click
            Stage2 = 1,   // Image B visible, waiting for click
            Found = 2     // Hidden cat found (Image C visible)
        }

        /// <summary>
        /// Editor preview state for layout purposes.
        /// </summary>
        public enum EditorPreviewState
        {
            TriggerArea,
            ImageB,
            ImageC
        }

        private void Awake()
        {
            // 记录该猫在 Prefab 里是否默认活跃，用于统计总猫数时区分「Prefab 默认 inactive」和「被收集后 inactive」。
            _initiallyActiveInPrefab = gameObject.activeSelf;

            _uniqueId = GenerateUniqueId();

            if (enableDebugLog)
            {
                Debug.Log($"[HiddenCatInteractable] Initializing {gameObject.name}");
            }

            // Initialize trigger area
            if (triggerArea == null)
            {
                Debug.LogError($"[HiddenCatInteractable] Trigger area is not set on {gameObject.name}");
                enabled = false;
                return;
            }

            // Ensure trigger area has Image component
            Image triggerImage = triggerArea.GetComponent<Image>();
            if (triggerImage == null)
            {
                triggerImage = triggerArea.AddComponent<Image>();
            }
            triggerImage.raycastTarget = true;

            // Get or add click detector for trigger
            _triggerDetector = triggerArea.GetComponent<SimpleClickDetector>();
            if (_triggerDetector == null)
            {
                _triggerDetector = triggerArea.AddComponent<SimpleClickDetector>();
            }
            // Enable pixel-perfect detection for accurate click detection
            _triggerDetector.SetPixelPerfectDetection(true);
            _triggerDetector.OnClickDetected += HandleTriggerClick;

            // Initialize Image B
            if (imageB == null)
            {
                Debug.LogError($"[HiddenCatInteractable] Image B is not set on {gameObject.name}");
                enabled = false;
                return;
            }

            // Ensure Image B has Image component
            Image imageBImage = imageB.GetComponent<Image>();
            if (imageBImage == null)
            {
                imageBImage = imageB.AddComponent<Image>();
            }
            imageBImage.raycastTarget = true;

            // Get or add click detector for Image B
            _imageBDetector = imageB.GetComponent<SimpleClickDetector>();
            if (_imageBDetector == null)
            {
                _imageBDetector = imageB.AddComponent<SimpleClickDetector>();
            }
            // Enable pixel-perfect detection for accurate click detection
            _imageBDetector.SetPixelPerfectDetection(true);
            _imageBDetector.OnClickDetected += HandleImageBClick;

            // Initialize Image C
            if (imageC == null)
            {
                Debug.LogError($"[HiddenCatInteractable] Image C is not set on {gameObject.name}");
                enabled = false;
                return;
            }

            // Ensure Image C has Image component
            Image imageCImage = imageC.GetComponent<Image>();
            if (imageCImage == null)
            {
                imageCImage = imageC.AddComponent<Image>();
            }

            // Load saved state
            LoadState();

            // Apply state
            if (Application.isPlaying)
            {
                ApplyState();
            }
        }

        private void OnDestroy()
        {
            if (_triggerDetector != null)
            {
                _triggerDetector.OnClickDetected -= HandleTriggerClick;
            }

            if (_imageBDetector != null)
            {
                _imageBDetector.OnClickDetected -= HandleImageBClick;
            }
        }

        /// <summary>
        /// Handle click on trigger area (Stage 1 → Stage 2).
        /// </summary>
        private void HandleTriggerClick()
        {
            if (_currentState != State.Initial)
            {
                return;
            }

            _currentState = State.Stage2;

            // 触发“点击/收集开始”类型事件
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Click,
                HiddenCats.Core.EventTriggerTiming.Immediate);

            // Hide trigger, show Image B
            if (triggerArea != null)
            {
                triggerArea.SetActive(false);
            }

            if (imageB != null)
            {
                imageB.SetActive(true);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenCatInteractable] Image B appeared: {imageB.name}");
                }
            }

            AudioManager.Instance?.PlaySfx("HiddenCatsFinded");

            SaveState();
        }

        /// <summary>
        /// Handle click on Image B (Stage 2 → Stage 3).
        /// </summary>
        private void HandleImageBClick()
        {
            if (_currentState != State.Stage2)
            {
                return;
            }

            _currentState = State.Found;

            // Hide Image B, show Image C
            if (imageB != null)
            {
                imageB.SetActive(false);
            }

            if (imageC != null)
            {
                imageC.SetActive(true);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenCatInteractable] Hidden cat found! Image C appeared: {imageC.name}");
                }
            }

            // Record collection
            if (CollectionService.Instance != null)
            {
                bool success = CollectionService.Instance.CollectItem(sceneName, CollectibleType.HiddenCat);
                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenCatInteractable] Collection recorded: Success={success}");
                }
            }
            else
            {
                Debug.LogError($"[HiddenCatInteractable] CollectionService.Instance is null!");
            }

            if (playAudioOnFound)
            {
                AudioManager.Instance?.PlayRandomCatMeow();
            }

            // 触发“收集/完成”类型事件
            TriggerInteractionEvents(HiddenCats.Core.InteractionEventType.Complete,
                HiddenCats.Core.EventTriggerTiming.OnFlowComplete);

            // Invoke event
            OnHiddenCatFound?.Invoke(this);

            // Notify HintMagnifierService if active
            HiddenCats.UI.HintMagnifierService.Instance?.OnItemCollected(this);

            // Use imageC (visible cat graphic) — RoomWnd roots are often empty/non-RectTransform; transform alone can place FX off-screen.
            ClickCollectFx.PlayAt(imageC != null ? imageC.transform : transform);

            SaveState();
        }

        /// <summary>
        /// Apply current state to GameObjects.
        /// </summary>
        private void ApplyState()
        {
            switch (_currentState)
            {
                case State.Initial:
                    if (triggerArea != null) triggerArea.SetActive(true);
                    if (imageB != null) imageB.SetActive(false);
                    if (imageC != null) imageC.SetActive(false);
                    break;

                case State.Stage2:
                    if (triggerArea != null) triggerArea.SetActive(false);
                    if (imageB != null) imageB.SetActive(true);
                    if (imageC != null) imageC.SetActive(false);
                    break;

                case State.Found:
                    if (triggerArea != null) triggerArea.SetActive(false);
                    if (imageB != null) imageB.SetActive(false);
                    if (imageC != null) imageC.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// Load state from PlayerPrefs.
        /// Reset is applied once per save key using GameProgressResetService reset version.
        /// </summary>
        private void LoadState()
        {
            string key = GetSaveKey();
            
            // Apply reset-on-load for this key if the latest reset version has not been processed yet.
            if (Core.GameProgressResetService.ShouldApplyResetForKey(key))
            {
                _currentState = State.Initial;
                ApplyState(); // Apply the reset state visually
                // Clear the key if it exists
                PlayerPrefs.DeleteKey(key);
                Core.GameProgressResetService.MarkResetAppliedForKey(key);
                
                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenCatInteractable] Reset version detected, using initial state: {gameObject.name}");
                }
                return;
            }
            
            // Normal load: restore state from PlayerPrefs (scene switch should preserve state)
            int savedState = PlayerPrefs.GetInt(key, (int)State.Initial);
            _currentState = (State)savedState;

            if (enableDebugLog && _currentState != State.Initial)
            {
                Debug.Log($"[HiddenCatInteractable] Loaded state: {_currentState}");
            }
        }

        /// <summary>
        /// Save state to PlayerPrefs.
        /// </summary>
        private void SaveState()
        {
            string key = GetSaveKey();
            PlayerPrefs.SetInt(key, (int)_currentState);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Get the PlayerPrefs key for saving this hidden cat's state.
        /// Uses "SR_" prefix when speedrun mode is active so normal and speedrun
        /// save data stay independent.
        /// </summary>
        private string GetSaveKey()
        {
            return $"{SpeedrunService.GetSaveKeyPrefix()}HiddenCat_{_uniqueId}_State";
        }

        /// <summary>
        /// Generate a unique ID for this hidden cat.
        /// </summary>
        private string GenerateUniqueId()
        {
            Vector3 position = transform.position;
            return $"{sceneName}_HiddenCat_{position.x:F2}_{position.y:F2}_{position.z:F2}";
        }

        /// <summary>
        /// Check if the hidden cat has been found.
        /// </summary>
        public bool IsFound => _currentState == State.Found;

        /// <summary>
        /// The trigger area transform — the region the player must find and click.
        /// Used by HintMagnifierService to position the PromptBox correctly.
        /// </summary>
        public Transform TriggerAreaTransform => triggerArea != null ? triggerArea.transform : transform;

        /// <summary>
        /// Get the scene name where this hidden cat is located.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Get the unique ID of this hidden cat.
        /// </summary>
        public string UniqueId => _uniqueId;

        /// <summary>
        /// 该猫在 Prefab 里是否默认活跃（实例化时的 activeSelf）。
        /// </summary>
        public bool InitiallyActiveInPrefab => _initiallyActiveInPrefab;

        /// <summary>
        /// Reload visual / interaction state from the current mode's save key.
        /// Called by SpeedrunService when the mode toggles on/off.
        /// </summary>
        public void ReloadState()
        {
            LoadState();
            ApplyState();
        }

        /// <summary>
        /// Reset this hidden cat to initial state.
        /// </summary>
        public void ResetCollection()
        {
            _currentState = State.Initial;
            ApplyState();

            string key = GetSaveKey();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (enableDebugLog)
            {
                Debug.Log($"[HiddenCatInteractable] Reset: {gameObject.name}");
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

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = HiddenCats.Core.SceneName.RoomWnd;
            }

            if (!Application.isPlaying)
            {
                UpdateEditorPreview();
            }
        }

        private void UpdateEditorPreview()
        {
            switch (editorPreviewState)
            {
                case EditorPreviewState.TriggerArea:
                    if (triggerArea != null) triggerArea.SetActive(true);
                    if (imageB != null) imageB.SetActive(false);
                    if (imageC != null) imageC.SetActive(false);
                    break;

                case EditorPreviewState.ImageB:
                    if (triggerArea != null) triggerArea.SetActive(false);
                    if (imageB != null) imageB.SetActive(true);
                    if (imageC != null) imageC.SetActive(false);
                    break;

                case EditorPreviewState.ImageC:
                    if (triggerArea != null) triggerArea.SetActive(false);
                    if (imageB != null) imageB.SetActive(false);
                    if (imageC != null) imageC.SetActive(true);
                    break;
            }
        }
        #endif
    }
}
