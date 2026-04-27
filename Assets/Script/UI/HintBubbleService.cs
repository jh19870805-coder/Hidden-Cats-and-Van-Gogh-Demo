using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// Lightweight hint bubble / toast manager.
    /// Attach this to a UI root (e.g. a Canvas under MainWnd),
    /// assign the bubble prefab and use Show() from gameplay / UI scripts.
    ///
    /// This service intentionally只关心“怎么显示”和“显示多久”，
    /// 具体显示什么图标/文字、什么时候调用，由业务逻辑（猫、奖杯、小游戏按钮等）决定。
    /// </summary>
    public sealed class HintBubbleService : MonoBehaviour
    {
        public static HintBubbleService Instance { get; private set; }

        [Header("Bubble Prefab")]
        [Tooltip("Prefab for hint bubble / toast. Root must be a RectTransform.")]
        [SerializeField] private RectTransform bubblePrefab;

        [Header("Default Settings")]
        [Tooltip("Default display duration (seconds) if request does not specify one.")]
        [SerializeField] private float defaultDuration = 1.5f;

        [Tooltip("Optional canvas used for world-to-screen positioning. If null, will try to find in parents.")]
        [SerializeField] private Canvas targetCanvas;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = false;

        // Track spawned bubbles so we can reliably clear them when switching windows.
        // Important: WindowManager may SetActive(false) on the whole window; Unity stops coroutines on disable,
        // which would otherwise leave the bubble GameObject alive and visible again when the window re-enables.
        private readonly List<RectTransform> _liveBubbles = new List<RectTransform>();

        private void Awake()
        {
            // Handle singleton pattern: only one instance should exist at a time
            if (Instance != null && Instance != this)
            {
                // If the existing instance is inactive (its window is disabled), replace it with this one.
                // This handles the case where windows are kept alive and switched via SetActive(false/true).
                if (!Instance.isActiveAndEnabled)
                {
                    if (enableDebugLog)
                    {
                        Debug.Log($"[HintBubbleService] Replacing inactive instance (window={Instance.transform.root.name}) with new active one (window={transform.root.name}).");
                    }
                    // Clear the old instance's state before destroying it
                    var oldInstance = Instance;
                    Instance = null;
                    if (oldInstance != null)
                    {
                        oldInstance.InternalClearAll();
                        // Destroy only the component, not the entire GameObject
                        Destroy(oldInstance);
                    }
                }
                else
                {
                    // Existing instance is active, this is a duplicate
                    // This can happen if Unity calls Awake multiple times during instantiation,
                    // or if the prefab has multiple HintBubbleService components (which shouldn't happen)
                    if (enableDebugLog)
                    {
                        Debug.LogWarning($"[HintBubbleService] Duplicate instance detected during instantiation. Existing instance is in window={Instance.transform.root.name}, this one is in window={transform.root.name}. Disabling this duplicate component.");
                    }
                    // Simply disable this component instead of destroying it to avoid any potential issues
                    // with GameObject destruction (especially if this is the only component on the GameObject)
                    // DO NOT disable the GameObject itself, as it may contain other critical components
                    enabled = false;
                    return;
                }
            }

            // Set this as the instance (only if Instance is null or we just cleared it)
            Instance = this;

            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }

            if (enableDebugLog)
            {
                Debug.Log("[HintBubbleService] Awake(), canvas=" +
                          (targetCanvas != null ? targetCanvas.name : "null") +
                          ", window=" + transform.root.name);
            }
        }

        private void OnDisable()
        {
            // When parent window is deactivated, Unity stops our coroutines.
            // Clear any existing bubbles to avoid "hint still there after switching UI".
            InternalClearAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Public API

        public enum HintBubbleType
        {
            IconOnly,
            TextOnly,
            IconWithText
        }

        public struct HintBubbleRequest
        {
            public Transform anchorWorldOrUI;
            public HintBubbleType type;
            public Sprite icon;
            public string text;
            public float duration;
        }

        /// <summary>
        /// Show a hint bubble / toast.
        /// Business code is responsible for deciding icon/text and when to call this.
        /// </summary>
        public static void Show(HintBubbleRequest request)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[HintBubbleService] Instance is null. Please ensure one exists in the scene.");
                return;
            }

            if (!Instance.isActiveAndEnabled)
            {
                // Most commonly happens when its window is inactive (keepWindowsAlive switching).
                // In that case, we prefer to skip showing rather than creating lingering UI.
                return;
            }

            Instance.InternalShow(request);
        }

        /// <summary>
        /// Clear all currently spawned hint bubbles immediately.
        /// Useful when switching windows/popups or after load/reset.
        /// </summary>
        public static void ClearAll()
        {
            if (Instance == null)
            {
                return;
            }
            Instance.InternalClearAll();
        }

        #endregion

        #region Internal

        private void InternalShow(HintBubbleRequest request)
        {
            if (bubblePrefab == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[HintBubbleService] Bubble prefab is not assigned.");
                }
                return;
            }

            RectTransform bubble = Instantiate(bubblePrefab, transform);
            bubble.gameObject.SetActive(true);
            // Tag this instance so ClearAll can safely destroy only bubbles even if this service is mis-attached.
            if (bubble.GetComponent<HintBubbleTag>() == null)
            {
                bubble.gameObject.AddComponent<HintBubbleTag>();
            }
            _liveBubbles.Add(bubble);

            SetupBubbleContent(bubble, request);
            PositionBubble(bubble, request.anchorWorldOrUI);

            float duration = request.duration > 0f ? request.duration : defaultDuration;

            StartCoroutine(AutoHideCoroutine(bubble, duration));

            if (enableDebugLog)
            {
                Debug.Log($"[HintBubbleService] Show bubble type={request.type}, text={request.text}, icon={(request.icon != null ? request.icon.name : "null")}");
            }
        }

        /// <summary>
        /// Basic content wiring: try to find Image/Text on prefab and set accordingly.
        /// 约定：预制体上可以有可选的 Image（图标）和 Text（文字），命名不限。
        /// </summary>
        private void SetupBubbleContent(RectTransform bubble, HintBubbleRequest request)
        {
            Image iconImage = bubble.GetComponentInChildren<Image>();
            Text textComp = bubble.GetComponentInChildren<Text>();

            switch (request.type)
            {
                case HintBubbleType.IconOnly:
                    if (iconImage != null)
                    {
                        iconImage.sprite = request.icon;
                        iconImage.enabled = request.icon != null;
                    }
                    if (textComp != null)
                    {
                        textComp.text = string.Empty;
                        textComp.enabled = false;
                    }
                    break;

                case HintBubbleType.TextOnly:
                    if (iconImage != null)
                    {
                        iconImage.enabled = false;
                    }
                    if (textComp != null)
                    {
                        textComp.text = request.text ?? string.Empty;
                        textComp.enabled = !string.IsNullOrEmpty(request.text);
                    }
                    break;

                case HintBubbleType.IconWithText:
                    if (iconImage != null)
                    {
                        iconImage.sprite = request.icon;
                        iconImage.enabled = request.icon != null;
                    }
                    if (textComp != null)
                    {
                        textComp.text = request.text ?? string.Empty;
                        textComp.enabled = !string.IsNullOrEmpty(request.text);
                    }
                    break;
            }
        }

        private void PositionBubble(RectTransform bubble, Transform anchor)
        {
            if (anchor == null || targetCanvas == null)
            {
                // Fallback: center of canvas
                bubble.anchoredPosition = Vector2.zero;
                return;
            }

            // If anchor is in world space, convert to screen point then to canvas local point.
            Vector3 worldPos = anchor.position;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, worldPos);

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    targetCanvas.worldCamera,
                    out Vector2 localPos))
            {
                bubble.anchoredPosition = localPos;
            }
            else
            {
                bubble.anchoredPosition = Vector2.zero;
            }
        }

        private IEnumerator AutoHideCoroutine(RectTransform bubble, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (bubble == null)
            {
                yield break;
            }

            // 简单实现：直接销毁。后续如有需要可改为对象池 + 淡出动画。
            _liveBubbles.Remove(bubble);
            Destroy(bubble.gameObject);
        }

        private void InternalClearAll()
        {
            // Stop our pending auto-hide timers first (otherwise they might run on already destroyed references).
            StopAllCoroutines();

            // Destroy tracked bubbles.
            for (int i = _liveBubbles.Count - 1; i >= 0; i--)
            {
                var b = _liveBubbles[i];
                if (b != null)
                {
                    Destroy(b.gameObject);
                }
            }
            _liveBubbles.Clear();

            // Extra safety: destroy any untracked bubbles by marker component,
            // WITHOUT destroying unrelated UI children.
            HintBubbleTag[] tags = GetComponentsInChildren<HintBubbleTag>(true);
            if (tags != null)
            {
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag != null)
                    {
                        Destroy(tag.gameObject);
                    }
                }
            }
        }

        #endregion
    }
}

