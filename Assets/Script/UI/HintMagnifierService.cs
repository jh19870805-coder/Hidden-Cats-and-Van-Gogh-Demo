using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HiddenCats.Core;
using HiddenCats.Interactable;

namespace HiddenCats.UI
{
    /// <summary>
    /// Handles the magnifier hint functionality:
    /// 1. Click magnifier button to show a prompt box with random position
    /// 2. If prompt box is off-screen, show hand pointing to it
    /// 3. Cooldown starts only after player finds the cat in the prompt box area
    /// </summary>
    public class HintMagnifierService : MonoBehaviour
    {
        [Header("References (set in prefab)")]
        [SerializeField] private Button searchButton;
        [SerializeField] private Image searchButtonImage;
        [SerializeField] private RectTransform promptBox;
        [SerializeField] private RectTransform catHand;
        [SerializeField] private GameObject promptBoxHighlight;

        [Header("Settings")]
        [SerializeField] private float cooldownSeconds = 60f;
        [SerializeField] private float catSafeMargin = 50f;

        [Header("Fade Settings")]
        [SerializeField] private float promptBoxFadeDuration = 0.5f;
        [SerializeField] private float catHandFadeDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool enableVerboseLog = false;

        /// <summary>
        /// Priority order: NormalCat > HiddenCat > Fish > Firework
        /// </summary>
        private enum HintTargetType
        {
            NormalCat,
            HiddenCat,
            Fish,
            Firework,
            None
        }

        private HintTargetType _currentTargetType = HintTargetType.None;
        private bool _isOnCooldown;
        private float _cooldownRemaining;
        private bool _isPromptBoxActive;
        private NormalCatInteractable _targetCat;
        private HiddenCatInteractable _targetHiddenCat;
        private FishInteractable _targetFish;
        private FireworkInteractable _targetFirework;

        private Coroutine _promptBoxFadeCoroutine;
        private Coroutine _catHandFadeCoroutine;
        private bool _catHandVisible;

        public static HintMagnifierService Instance { get; private set; }

        private void VerboseLog(string message)
        {
            if (enableVerboseLog)
            {
                Debug.Log(message);
            }
        }

        private void VerboseWarn(string message)
        {
            if (enableVerboseLog)
            {
                Debug.LogWarning(message);
            }
        }

        private void Awake()
        {
            VerboseLog($"[HintMagnifierService] Awake called on {gameObject.name}, existing Instance={Instance}");

            if (Instance != null && Instance != this)
            {
                VerboseWarn("[HintMagnifierService] Duplicate instance detected, disabling.");
                enabled = false;
                return;
            }
            Instance = this;
            VerboseLog($"[HintMagnifierService] Instance set to {this}, transform.parent={transform.parent?.name}");
        }

        private void Start()
        {
            // Force re-wire references to ensure we get the correct UI elements from the current window
            ForceReWireReferences();
            HidePromptBox();
            HideHand();
            UpdateSearchButtonState();
        }

        private void OnEnable()
        {
            // Re-wire references when enabled (e.g., after window switch)
            ForceReWireReferences();
        }

        private void ForceReWireReferences()
        {
            VerboseLog($"[HintMagnifierService] ForceReWireReferences called on {gameObject.name}, current window={WindowManager.Instance?.CurrentWindow?.name}");

            // Get the current active window
            GameObject currentWindow = WindowManager.Instance?.CurrentWindow;
            if (currentWindow == null)
            {
                VerboseWarn("[HintMagnifierService] Current window is null!");
                return;
            }

            // Find UI elements in the current window, not in our parent
            var hintServiceInWindow = currentWindow.GetComponentInChildren<HintMagnifierService>();
            if (hintServiceInWindow != null && hintServiceInWindow != this)
            {
                VerboseLog("[HintMagnifierService] Another HintMagnifierService found in current window, skipping wire");
                return;
            }

            // Find Search button in current window (NumUI may be under Ui/ after EnsureNumUIController)
            var searchTransform = currentWindow.transform.Find("NumUI/Search");
            if (searchTransform == null)
                searchTransform = currentWindow.transform.Find("Ui/NumUI/Search");
            if (searchTransform == null)
                searchTransform = currentWindow.transform.FindDeepChild("Search");

            if (searchTransform != null)
            {
                searchButton = searchTransform.GetComponent<Button>();
                if (searchButton == null)
                    searchButton = searchTransform.gameObject.AddComponent<Button>();
                searchButtonImage = searchTransform.GetComponent<Image>();
                VerboseLog($"[HintMagnifierService] Found Search: {searchTransform.name}, hasButton={searchButton != null}");
            }

            // Find PromptBox in current window (may be under __ContentRoot after GameSceneUI moves it)
            var promptBoxTransform = currentWindow.transform.Find("PromptBox");
            if (promptBoxTransform == null)
                promptBoxTransform = currentWindow.transform.FindDeepChild("PromptBox");

            if (promptBoxTransform != null)
            {
                promptBox = promptBoxTransform as RectTransform;
                VerboseLog($"[HintMagnifierService] Found PromptBox: {promptBoxTransform.name}, parent={promptBoxTransform.parent?.name}");
            }

            // Find CatHand in current window
            var catHandTransform = currentWindow.transform.Find("CatHand");
            if (catHandTransform != null)
            {
                catHand = catHandTransform as RectTransform;
                VerboseLog($"[HintMagnifierService] Found CatHand: {catHandTransform.name}");
            }
            else
            {
                catHandTransform = currentWindow.transform.FindDeepChild("CatHand");
                if (catHandTransform != null)
                {
                    catHand = catHandTransform as RectTransform;
                    VerboseLog($"[HintMagnifierService] Found CatHand via FindDeepChild: {catHandTransform.name}");
                }
            }

            // Setup button listener
            if (searchButton != null)
            {
                searchButton.onClick.RemoveAllListeners();
                searchButton.onClick.AddListener(OnClick_Search);
                VerboseLog("[HintMagnifierService] Button listener registered");
            }
            else
            {
                VerboseWarn("[HintMagnifierService] Search button not found!");
            }
        }

        /// <summary>
        /// Called every frame by WindowManager.Update() so the service keeps ticking
        /// even when its own GameObject is on an inactive window.
        /// </summary>
        public void ServiceUpdate()
        {
            if (_isOnCooldown)
            {
                _cooldownRemaining -= Time.deltaTime;
                if (_cooldownRemaining <= 0)
                {
                    _isOnCooldown = false;
                    _cooldownRemaining = 0;
                }
                UpdateSearchButtonState();
            }

            if (_isPromptBoxActive && catHand != null && promptBox != null)
            {
                UpdateCatHandState();
            }
        }

        private void AutoWireReferences()
        {
            // Auto-find Search button
            if (searchButton == null)
            {
                var search = transform.Find("Search");
                if (search != null)
                {
                    searchButton = search.GetComponent<Button>();
                    if (searchButtonImage == null)
                    {
                        searchButtonImage = search.GetComponent<Image>();
                    }
                }
            }

            // Auto-find PromptBox
            if (promptBox == null)
            {
                var box = transform.Find("PromptBox");
                if (box != null)
                {
                    promptBox = box as RectTransform;
                }
            }

            // Auto-find CatHand
            if (catHand == null)
            {
                var hand = transform.Find("CatHand");
                if (hand != null)
                {
                    catHand = hand as RectTransform;
                }
            }

            // Setup button listener
            if (searchButton != null)
            {
                searchButton.onClick.RemoveAllListeners();
                searchButton.onClick.AddListener(OnClick_Search);
            }
        }

        private void OnClick_Search()
        {
            VerboseLog($"[HintMagnifierService] OnClick_Search called, cooldown={_isOnCooldown}, promptActive={_isPromptBoxActive}");

            if (_isOnCooldown || _isPromptBoxActive)
            {
                VerboseLog("[HintMagnifierService] Search clicked but on cooldown or prompt active");
                return;
            }

            ShowPromptBox();
        }

        private void ShowPromptBox()
        {
            if (promptBox == null)
            {
                VerboseWarn("[HintMagnifierService] PromptBox not found!");
                return;
            }

            if (!FindTargetInPriorityOrder())
            {
                VerboseLog("[HintMagnifierService] No uncollected items found");
                return;
            }

            AudioManager.PlaySpinGet();

            RectTransform targetRt = GetTargetRectTransform();
            if (targetRt == null)
            {
                VerboseWarn("[HintMagnifierService] Target RectTransform is null");
                return;
            }

            RectTransform promptBoxParent = promptBox.parent as RectTransform;
            if (promptBoxParent == null)
            {
                VerboseWarn("[HintMagnifierService] PromptBox has no RectTransform parent");
                return;
            }

            // Convert target world position → screen → PromptBox parent local.
            // This is more robust than InverseTransformPoint because
            // ScreenPointToLocalPointInRectangle properly accounts for
            // anchors, pivots, and canvas render mode.
            Camera uiCam = GetUICamera();
            Vector3 targetWorldPos = targetRt.position;
            Vector2 screenPos = uiCam != null
                ? (Vector2)uiCam.WorldToScreenPoint(targetWorldPos)
                : (Vector2)targetWorldPos;

            Vector2 targetLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                promptBoxParent, screenPos, uiCam, out targetLocal);

            VerboseLog($"[HintMagnifierService] ShowPromptBox: type={_currentTargetType}, " +
                      $"target={targetRt.name}, worldPos={targetWorldPos}, screenPos={screenPos}, " +
                      $"localInParent={targetLocal}, parent={promptBoxParent.name}, " +
                      $"parentPivot={promptBoxParent.pivot}, parentAnchors=[{promptBoxParent.anchorMin},{promptBoxParent.anchorMax}], " +
                      $"promptBoxAnchors=[{promptBox.anchorMin},{promptBox.anchorMax}], " +
                      $"uiCam={(uiCam != null ? uiCam.name : "null")}");

            StopPersistentCoroutine(ref _promptBoxFadeCoroutine);

            promptBox.anchoredPosition = targetLocal;
            promptBox.gameObject.SetActive(true);
            promptBox.SetAsLastSibling();
            _isPromptBoxActive = true;

            EnsurePromptBoxPassthrough();

            var pbCg = promptBox.GetComponent<CanvasGroup>();
            if (pbCg != null) pbCg.alpha = 1f;

            UpdateCatHandState();
            UpdateSearchButtonState();

            VerboseLog($"[HintMagnifierService] PromptBox final anchoredPos={promptBox.anchoredPosition}, " +
                      $"worldPos={promptBox.position}");
        }

        /// <summary>
        /// Ensures the PromptBox does not block raycasts so items underneath remain clickable.
        /// </summary>
        private void EnsurePromptBoxPassthrough()
        {
            if (promptBox == null) return;

            var cg = EnsureCanvasGroup(promptBox);
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        private CanvasGroup EnsureCanvasGroup(RectTransform rt)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = rt.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private Coroutine StartPersistentCoroutine(IEnumerator routine)
        {
            if (WindowManager.Instance != null)
                return WindowManager.Instance.StartCoroutine(routine);
            if (gameObject.activeInHierarchy)
                return StartCoroutine(routine);
            return null;
        }

        private void StopPersistentCoroutine(ref Coroutine coroutine)
        {
            if (coroutine == null) return;
            if (WindowManager.Instance != null)
                WindowManager.Instance.StopCoroutine(coroutine);
            else if (gameObject.activeInHierarchy)
                StopCoroutine(coroutine);
            coroutine = null;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete = null)
        {
            float startAlpha = cg.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            cg.alpha = targetAlpha;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Find target item in priority order: NormalCat > HiddenCat > Fish > Firework
        /// </summary>
        private bool FindTargetInPriorityOrder()
        {
            string windowName = WindowManager.Instance?.CurrentWindow?.name ?? "null";

            _targetCat = FindRandomUncollectedNormalCat();
            if (_targetCat != null)
            {
                _currentTargetType = HintTargetType.NormalCat;
                VerboseLog($"[HintMagnifierService] FindTarget: NormalCat found: {_targetCat.name}, window={windowName}");
                return true;
            }

            _targetHiddenCat = FindRandomUncollectedHiddenCat();
            if (_targetHiddenCat != null)
            {
                _currentTargetType = HintTargetType.HiddenCat;
                VerboseLog($"[HintMagnifierService] FindTarget: HiddenCat found: {_targetHiddenCat.name}, window={windowName}");
                return true;
            }

            _targetFish = FindRandomUncollectedFish();
            if (_targetFish != null)
            {
                _currentTargetType = HintTargetType.Fish;
                VerboseLog($"[HintMagnifierService] FindTarget: Fish found: {_targetFish.name}, window={windowName}");
                return true;
            }

            _targetFirework = FindRandomUncollectedFirework();
            if (_targetFirework != null)
            {
                _currentTargetType = HintTargetType.Firework;
                VerboseLog($"[HintMagnifierService] FindTarget: Firework found: {_targetFirework.name}, window={windowName}");
                return true;
            }

            _currentTargetType = HintTargetType.None;
            VerboseLog($"[HintMagnifierService] FindTarget: Nothing found in window={windowName}");
            return false;
        }

        private RectTransform GetTargetRectTransform()
        {
            switch (_currentTargetType)
            {
                case HintTargetType.NormalCat:
                    return _targetCat != null ? _targetCat.GetComponent<RectTransform>() : null;
                case HintTargetType.HiddenCat:
                    if (_targetHiddenCat == null) return null;
                    return _targetHiddenCat.TriggerAreaTransform.GetComponent<RectTransform>();
                case HintTargetType.Fish:
                    return _targetFish != null ? _targetFish.GetComponent<RectTransform>() : null;
                case HintTargetType.Firework:
                    return _targetFirework != null ? _targetFirework.GetComponent<RectTransform>() : null;
                default:
                    return null;
            }
        }

        #region Find Methods for Each Item Type

        private NormalCatInteractable FindRandomUncollectedNormalCat()
        {
            var cats = FindObjectsOfType<NormalCatInteractable>();
            var uncollected = new List<NormalCatInteractable>();

            foreach (var cat in cats)
            {
                if (!cat.IsCollected && IsItemInThisScene(cat.transform))
                {
                    uncollected.Add(cat);
                }
            }

            if (uncollected.Count == 0)
            {
                return null;
            }

            // Deterministic: same scene always picks the same target (first by name order)
            uncollected.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return uncollected[0];
        }

        private HiddenCatInteractable FindRandomUncollectedHiddenCat()
        {
            var cats = FindObjectsOfType<HiddenCatInteractable>();
            var uncollected = new List<HiddenCatInteractable>();

            foreach (var cat in cats)
            {
                if (!cat.IsFound && IsItemInThisScene(cat.transform))
                {
                    uncollected.Add(cat);
                }
            }

            if (uncollected.Count == 0)
            {
                return null;
            }

            uncollected.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return uncollected[0];
        }

        private FishInteractable FindRandomUncollectedFish()
        {
            var fishes = FindObjectsOfType<FishInteractable>();
            var uncollected = new List<FishInteractable>();

            foreach (var fish in fishes)
            {
                if (!fish.IsCollected && IsItemInThisScene(fish.transform))
                {
                    uncollected.Add(fish);
                }
            }

            if (uncollected.Count == 0)
            {
                return null;
            }

            uncollected.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return uncollected[0];
        }

        private FireworkInteractable FindRandomUncollectedFirework()
        {
            var fireworks = FindObjectsOfType<FireworkInteractable>();
            var uncollected = new List<FireworkInteractable>();

            foreach (var firework in fireworks)
            {
                if (!firework.IsCollected && IsItemInThisScene(firework.transform))
                {
                    uncollected.Add(firework);
                }
            }

            if (uncollected.Count == 0)
            {
                return null;
            }

            uncollected.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return uncollected[0];
        }

        private bool IsItemInThisScene(Transform itemTransform)
        {
            GameObject currentWindow = WindowManager.Instance?.CurrentWindow;
            if (currentWindow == null) return false;

            return itemTransform.IsChildOf(currentWindow.transform);
        }

        #endregion

        private Vector2 ClampPromptBoxPosition(Vector2 position, RectTransform windowRoot)
        {
            if (windowRoot == null || promptBox == null)
            {
                return position;
            }

            VerboseLog($"[HintMagnifierService] ClampPromptBoxPosition: input position={position}");

            // Get window bounds (assuming window root is stretched to fill canvas)
            Vector2 windowSize = windowRoot.rect.size;
            Vector2 halfWindowSize = windowSize * 0.5f;

            VerboseLog($"[HintMagnifierService] ClampPromptBoxPosition: windowSize={windowSize}, halfWindowSize={halfWindowSize}");

            // Get prompt box half size (accounting for scale)
            Vector3 lossyScale = promptBox.lossyScale;
            
            // Use rect.size directly since lossyScale can be unreliable (sometimes shows 0 but has tiny non-zero value)
            // Only use lossyScale if it's meaningfully large (> 0.1)
            Vector2 actualSize;
            if (Mathf.Abs(lossyScale.x) > 0.1f || Mathf.Abs(lossyScale.y) > 0.1f)
            {
                actualSize = new Vector2(
                    promptBox.rect.width * lossyScale.x,
                    promptBox.rect.height * lossyScale.y
                );
            }
            else
            {
                // lossyScale is effectively zero or very small, use rect.size directly
                actualSize = promptBox.rect.size;
            }
            
            Vector2 promptBoxHalfSize = actualSize * 0.5f;

            VerboseLog($"[HintMagnifierService] ClampPromptBoxPosition: promptBox rect.size={promptBox.rect.size}, lossyScale={lossyScale}, promptBoxHalfSize={promptBoxHalfSize}, actualSize={actualSize}");

            // Margin to ensure the prompt box stays fully visible at edges (configurable in Inspector).
            float extraMargin = catSafeMargin;

            // Calculate the valid range for the prompt box center
            float minX = -halfWindowSize.x + promptBoxHalfSize.x + extraMargin;
            float maxX = halfWindowSize.x - promptBoxHalfSize.x - extraMargin;
            float minY = -halfWindowSize.y + promptBoxHalfSize.y + extraMargin;
            float maxY = halfWindowSize.y - promptBoxHalfSize.y - extraMargin;

            VerboseLog($"[HintMagnifierService] ClampPromptBoxPosition: valid range X: [{minX}, {maxX}], Y: [{minY}, {maxY}]");

            // If the range is invalid (prompt box too big for window), center it
            if (minX > maxX)
            {
                minX = -extraMargin;
                maxX = extraMargin;
            }
            if (minY > maxY)
            {
                minY = -extraMargin;
                maxY = extraMargin;
            }

            // Clamp the position
            float clampedX = Mathf.Clamp(position.x, minX, maxX);
            float clampedY = Mathf.Clamp(position.y, minY, maxY);

            Vector2 clampedPos = new Vector2(clampedX, clampedY);

            VerboseLog($"[HintMagnifierService] ClampPromptBoxPosition: input={position}, clampedX={clampedX} (range [{minX}, {maxX}]), clampedY={clampedY} (range [{minY}, {maxY}]), result={clampedPos}");

            if (clampedPos != position)
            {
                VerboseLog($"[HintMagnifierService] Prompt box position clamped from {position} to {clampedPos}");
            }

            return clampedPos;
        }

        private RectTransform FindWindowRoot()
        {
            // First try: get window from WindowManager
            GameObject currentWindow = WindowManager.Instance?.CurrentWindow;
            if (currentWindow != null)
            {
                RectTransform windowRt = currentWindow.GetComponent<RectTransform>();
                if (windowRt != null)
                {
                    VerboseLog($"[HintMagnifierService] FindWindowRoot: using WindowManager window: {windowRt.name}, size: {windowRt.rect.size}");
                    return windowRt;
                }
            }

            // Second try: start from this transform's root and find window root
            Transform root = transform.root;
            while (root != null)
            {
                var rt = root.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Check if this is a window root (has "Wnd" in name OR is a large stretched rect)
                    if (rt.name.Contains("Wnd") || (rt.rect.width > 1000 && rt.rect.height > 1000))
                    {
                        VerboseLog($"[HintMagnifierService] FindWindowRoot: found by name/size: {rt.name}, size: {rt.rect.size}");
                        return rt;
                    }
                }
                root = root.parent;
            }

            // Fallback: find parent that has large size (likely the main canvas area)
            var parent = GetComponentInParent<RectTransform>();
            if (parent != null && parent.rect.width > 1000 && parent.rect.height > 1000)
            {
                VerboseLog($"[HintMagnifierService] FindWindowRoot: fallback to large parent: {parent.name}, size: {parent.rect.size}");
                return parent;
            }

            VerboseWarn($"[HintMagnifierService] FindWindowRoot: no suitable window found!");
            return GetComponentInParent<RectTransform>();
        }

        private void HidePromptBox()
        {
            _isPromptBoxActive = false;
            _targetCat = null;

            if (promptBox != null && promptBox.gameObject.activeSelf)
            {
                StopPersistentCoroutine(ref _promptBoxFadeCoroutine);

                var cg = EnsureCanvasGroup(promptBox);
                _promptBoxFadeCoroutine = StartPersistentCoroutine(FadeCanvasGroup(
                    cg, 0f, promptBoxFadeDuration, () =>
                    {
                        promptBox.gameObject.SetActive(false);
                        cg.alpha = 1f;
                    }));
            }
        }

        private void HideHand()
        {
            if (catHand == null) return;
            _catHandVisible = false;

            StopPersistentCoroutine(ref _catHandFadeCoroutine);

            var cg = EnsureCanvasGroup(catHand);
            _catHandFadeCoroutine = StartPersistentCoroutine(FadeCanvasGroup(
                cg, 0f, catHandFadeDuration, () =>
                {
                    catHand.gameObject.SetActive(false);
                    cg.alpha = 1f;
                }));
        }

        #region CatHand Logic

        /// <summary>
        /// Called every frame while PromptBox is active.
        /// Shows/hides CatHand based on how much of PromptBox is visible on screen.
        /// </summary>
        private void UpdateCatHandState()
        {
            if (catHand == null || promptBox == null || !promptBox.gameObject.activeSelf)
                return;

            float visibleFraction = GetPromptBoxVisibleFraction();

            if (visibleFraction >= 0.25f)
            {
                if (_catHandVisible)
                    HideHand();
            }
            else
            {
                if (!_catHandVisible)
                    ShowCatHand();

                PointCatHandToPromptBox();
            }
        }

        private void ShowCatHand()
        {
            if (catHand == null) return;

            _catHandVisible = true;

            StopPersistentCoroutine(ref _catHandFadeCoroutine);

            catHand.gameObject.SetActive(true);
            var cg = EnsureCanvasGroup(catHand);
            cg.alpha = 1f;
        }

        /// <summary>
        /// Returns 0-1 indicating what fraction of the PromptBox area is visible on screen.
        /// </summary>
        private float GetPromptBoxVisibleFraction()
        {
            Vector3[] corners = new Vector3[4];
            promptBox.GetWorldCorners(corners);

            Camera uiCamera = GetUICamera();

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 sp = uiCamera != null
                    ? uiCamera.WorldToScreenPoint(corners[i])
                    : (Vector3)corners[i];
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            float totalArea = (maxX - minX) * (maxY - minY);
            if (totalArea <= 0f) return 0f;

            float overlapMinX = Mathf.Max(minX, 0f);
            float overlapMaxX = Mathf.Min(maxX, Screen.width);
            float overlapMinY = Mathf.Max(minY, 0f);
            float overlapMaxY = Mathf.Min(maxY, Screen.height);

            if (overlapMinX >= overlapMaxX || overlapMinY >= overlapMaxY)
                return 0f;

            float overlapArea = (overlapMaxX - overlapMinX) * (overlapMaxY - overlapMinY);
            return overlapArea / totalArea;
        }

        /// <summary>
        /// Keeps CatHand at screen center and rotates it so the finger points toward PromptBox.
        /// </summary>
        private void PointCatHandToPromptBox()
        {
            if (catHand == null || promptBox == null) return;

            RectTransform catHandParent = catHand.parent as RectTransform;
            if (catHandParent == null) return;

            Camera uiCamera = GetUICamera();
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Vector2 localCenter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                catHandParent, screenCenter, uiCamera, out localCenter);
            catHand.anchoredPosition = localCenter;

            Vector2 promptBoxScreenPos = GetRectTransformScreenPosition(promptBox);
            Vector2 dir = promptBoxScreenPos - screenCenter;

            if (dir.sqrMagnitude < 1f) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Image default "finger" direction is local-up (Y+), so offset by -90
            catHand.localRotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        #endregion

        private Rect GetScreenRect()
        {
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        private Camera GetUICamera()
        {
            // Look for Canvas in the current window (not our parent, which may be inactive)
            GameObject currentWindow = WindowManager.Instance?.CurrentWindow;
            Canvas canvas = null;
            if (currentWindow != null)
                canvas = currentWindow.GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
                return canvas.worldCamera;
            return null;
        }

        private Vector2 GetRectTransformScreenPosition(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;

            Camera uiCamera = GetUICamera();
            if (uiCamera != null)
            {
                return uiCamera.WorldToScreenPoint(center);
            }
            return center;
        }

        private Vector2 GetTargetLocalPosition(Transform target)
        {
            RectTransform targetRt = target.GetComponent<RectTransform>();
            if (targetRt == null)
            {
                return Vector2.zero;
            }

            // Get the window root
            RectTransform windowRoot = FindWindowRoot();
            if (windowRoot != null)
            {
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    windowRoot, targetRt.position, GetUICamera(), out localPos);
                return localPos;
            }

            return targetRt.anchoredPosition;
        }

        private void UpdateSearchButtonState()
        {
            if (searchButton == null)
            {
                return;
            }

            bool canUse = !_isOnCooldown && !_isPromptBoxActive;
            searchButton.interactable = canUse;

            // Gray overlay drains from top to bottom during cooldown (恢复原状动画)
            bool shouldGray = _isPromptBoxActive || _isOnCooldown;
            if (searchButtonImage != null)
            {
                searchButtonImage.color = Color.white; // Keep icon colored; overlay provides gray
            }

            UpdateCooldownOverlay(shouldGray);
        }

        /// <summary>
        /// Overlay that drains from top to bottom during cooldown (恢复原状动画).
        /// </summary>
        private Image _cooldownOverlay;

        private void UpdateCooldownOverlay(bool showOverlay)
        {
            if (searchButton == null || searchButtonImage == null) return;

            if (!showOverlay)
            {
                if (_cooldownOverlay != null)
                    _cooldownOverlay.gameObject.SetActive(false);
                return;
            }

            EnsureCooldownOverlay();
            if (_cooldownOverlay == null) return;

            _cooldownOverlay.gameObject.SetActive(true);

            if (_isPromptBoxActive)
            {
                _cooldownOverlay.fillAmount = 1f;
            }
            else if (_isOnCooldown)
            {
                _cooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownRemaining / cooldownSeconds);
            }
        }

        private void EnsureCooldownOverlay()
        {
            if (_cooldownOverlay != null) return;
            if (searchButton == null || searchButtonImage == null) return;

            // Reuse existing overlay if one already exists on this button
            var existing = searchButton.transform.Find("CooldownOverlay");
            if (existing != null)
            {
                _cooldownOverlay = existing.GetComponent<Image>();
                if (_cooldownOverlay != null) return;
            }

            var go = new GameObject("CooldownOverlay");
            go.transform.SetParent(searchButton.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            _cooldownOverlay = go.AddComponent<Image>();
            _cooldownOverlay.sprite = searchButtonImage.sprite;
            _cooldownOverlay.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            _cooldownOverlay.raycastTarget = false;
            _cooldownOverlay.type = Image.Type.Filled;
            _cooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            _cooldownOverlay.fillOrigin = (int)Image.OriginVertical.Top;
            _cooldownOverlay.fillAmount = 1f;
        }

        /// <summary>
        /// Called by NormalCatInteractable when a cat is collected.
        /// Dismisses prompt and starts cooldown if the collected item is the hinted target.
        /// </summary>
        public void OnItemCollected(NormalCatInteractable cat)
        {
            if (!_isPromptBoxActive || _currentTargetType != HintTargetType.NormalCat) return;
            if (cat != _targetCat) return;

            StartCooldown();
            HidePromptBox();
            HideHand();
            VerboseLog($"[HintMagnifierService] Target NormalCat collected: {cat.name}, starting cooldown");
        }

        public void OnItemCollected(HiddenCatInteractable cat)
        {
            if (!_isPromptBoxActive || _currentTargetType != HintTargetType.HiddenCat) return;
            if (cat != _targetHiddenCat) return;

            StartCooldown();
            HidePromptBox();
            HideHand();
            VerboseLog($"[HintMagnifierService] Target HiddenCat collected: {cat.name}, starting cooldown");
        }

        public void OnItemCollected(FishInteractable fish)
        {
            if (!_isPromptBoxActive || _currentTargetType != HintTargetType.Fish) return;
            if (fish != _targetFish) return;

            StartCooldown();
            HidePromptBox();
            HideHand();
            VerboseLog($"[HintMagnifierService] Target Fish collected: {fish.name}, starting cooldown");
        }

        public void OnItemCollected(FireworkInteractable firework)
        {
            if (!_isPromptBoxActive || _currentTargetType != HintTargetType.Firework) return;
            if (firework != _targetFirework) return;

            StartCooldown();
            HidePromptBox();
            HideHand();
            VerboseLog($"[HintMagnifierService] Target Firework collected: {firework.name}, starting cooldown");
        }

        private void StartCooldown()
        {
            _isOnCooldown = true;
            _cooldownRemaining = cooldownSeconds;
            UpdateSearchButtonState();
        }

        /// <summary>
        /// Called by WindowManager when switching windows.
        /// Hides prompt/hand, clears targets, and starts a fresh cooldown.
        /// </summary>
        public void OnWindowSwitched()
        {
            StopPersistentCoroutine(ref _promptBoxFadeCoroutine);
            StopPersistentCoroutine(ref _catHandFadeCoroutine);

            // Hide OLD window's prompt box and hand immediately (no fade)
            if (promptBox != null)
                promptBox.gameObject.SetActive(false);
            _isPromptBoxActive = false;

            if (catHand != null)
                catHand.gameObject.SetActive(false);
            _catHandVisible = false;

            // Clear targets
            _targetCat = null;
            _targetHiddenCat = null;
            _targetFish = null;
            _targetFirework = null;
            _currentTargetType = HintTargetType.None;

            // Discard old overlay reference (belongs to previous window)
            _cooldownOverlay = null;

            // Re-bind to new window's Search/PromptBox/CatHand
            ForceReWireReferences();

            // Hide NEW window's PromptBox and CatHand (they may start active in the prefab)
            if (promptBox != null)
                promptBox.gameObject.SetActive(false);
            if (catHand != null)
                catHand.gameObject.SetActive(false);

            // Start fresh cooldown
            StartCooldown();
        }
    }

    /// <summary>
    /// Extension methods for Transform
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Recursively finds a child by name
        /// </summary>
        public static Transform FindDeepChild(this Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = child.FindDeepChild(childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Helper methods for HintMagnifierService
    /// </summary>
    public static class HintMagnifierExtensions
    {
        public static string GetTransformHierarchy(Transform t)
        {
            if (t == null) return "null";
            string hierarchy = t.name;
            Transform parent = t.parent;
            while (parent != null)
            {
                hierarchy = parent.name + " -> " + hierarchy;
                parent = parent.parent;
            }
            return hierarchy;
        }

        /// <summary>
        /// Calculate the total offset from a child to an ancestor by converting world position
        /// anchoredPosition doesn't work correctly when parent anchors differ, so we use world position conversion
        /// </summary>
        public static Vector2 GetOffsetToAncestor(Transform start, Transform ancestor)
        {
            if (start == null || ancestor == null)
                return Vector2.zero;

            // Get the world position of the start transform
            Vector3 worldPos = start.position;

            // Convert world position to ancestor's local space
            RectTransform ancestorRt = ancestor.GetComponent<RectTransform>();
            if (ancestorRt == null)
                return Vector2.zero;

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ancestorRt, worldPos, null, out localPos);

            return localPos;
        }
    }
}
