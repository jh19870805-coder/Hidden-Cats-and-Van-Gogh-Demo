using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using HiddenCats.UI;
using HiddenCats.Core;

namespace HiddenCats.UI
{
    /// <summary>
    /// Single-scene window controller.
    /// Responsible for instantiating and switching between top-level windows (MainWnd, etc.).
    /// </summary>
    public sealed class WindowManager : MonoBehaviour
    {
        public static WindowManager Instance { get; private set; }

        /// <summary>
        /// 标记是否正在Loading流程中，Loading期间不自动显示MainWnd
        /// 默认值为true，确保启动时自动进入Loading状态
        /// </summary>
        internal static bool IsInLoadingPhase { get; set; } = true;

        /// <summary>
        /// Currently active top-level window instance (e.g. MainWnd, RoomWnd).
        /// Exposed for services that need to find UI nodes inside the active window (e.g. WinPop, toast popups).
        /// </summary>
        public GameObject CurrentWindow => _currentWindow;

        public GameObject GetMainWndPrefab() => mainWndPrefab;

        /// <summary>
        /// 获取首次进入游戏的过渡配置
        /// </summary>
        public TransitionConfig GetFirstEntryTransitionConfig() => firstEntryTransitionConfig;

        /// <summary>
        /// 公开的切换窗口方法，供 LoadingUI 等外部脚本调用
        /// </summary>
        public void PublicSwitchToWindow(GameObject prefab)
        {
            SwitchToWindow(prefab);
        }

        [Header("Window Prefabs")]
        [SerializeField] private GameObject mainWndPrefab;
        [SerializeField] private GameObject settingPopPrefab;
        [SerializeField] private GameObject rankPopPrefab;
        [SerializeField] private GameObject roomWndPrefab;

        [Header("Spawn Root")]
        [Tooltip("Parent transform for spawned windows. If null, uses this GameObject's transform.")]
        [SerializeField] private Transform windowRoot;

        [Header("Window Lifetime")]
        [Tooltip("If enabled, cache each window instance and switch via SetActive(true/false) instead of Destroy+Instantiate. " +
                 "This allows global scanners (e.g., total Fish count) to find interactables in inactive windows.")]
        [SerializeField] private bool keepWindowsAlive = true;

        [Tooltip("If enabled and KeepWindowsAlive is true, pre-instantiate Room/Flower/Cafe windows (inactive) on startup " +
                 "so MainWnd can auto-detect totals by scanning inactive interactables.")]
        [SerializeField] private bool prewarmContentWindowsOnStartup = true;

        [Header("Popup Root (Optional)")]
        [Tooltip("Parent transform for popups (SettingPop, RankPop). If null, uses windowRoot.")]
        [SerializeField] private Transform popupRoot;

        private GameObject _currentWindow;
        private GameObject _currentPopup;
        private readonly Dictionary<GameObject, GameObject> _windowInstances = new Dictionary<GameObject, GameObject>();
        private bool _isSwitchingWindow;
        private int _switchDepth;

        [Header("Debug")]
        [Tooltip("Logs window switch / NumUI wiring details (noisy during normal play).")]
        [SerializeField] private bool enableVerboseWindowLogs = false;

        [Header("Transition Effect")]
        [Tooltip("启用后，窗口切换时播放波点溶解过渡效果")]
        [SerializeField] private bool enableTransitionEffect = true;

        [Header("=== 特殊过渡配置 ===")]
        [Tooltip("MainWnd 去其他界面时使用的过渡配置")]
        [SerializeField] private TransitionConfig mainWndTransitionConfig;
        [Tooltip("首次进入游戏时 Loading → MainWnd 使用的过渡配置")]
        [SerializeField] private TransitionConfig firstEntryTransitionConfig;

        private bool _isTransitioning;

        private void VerboseWindowLog(string message)
        {
            if (enableVerboseWindowLogs)
            {
                Debug.Log(message);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Ensure we call DontDestroyOnLoad on a root GameObject to avoid Unity warnings.
            var root = transform.root;
            if (root != transform)
            {
                // This is expected if WindowManager is a child of Canvas, so we only log in verbose mode
                // The code handles it correctly by using the root, so this is just informational
                // Debug.LogWarning("[WindowManager] WindowManager is not on a root GameObject. Using root for DontDestroyOnLoad.");
            }
            DontDestroyOnLoad(root.gameObject);

            if (windowRoot == null)
            {
                // IMPORTANT:
                // WindowManager is often placed under a Canvas as a 100x100 helper RectTransform in the scene.
                // If we spawn windows under that, all window layout/zoom math will be in a tiny coordinate space,
                // causing "corner leakage" and zoom/pan feeling broken.
                //
                // Prefer using the parent (typically the full-screen Canvas root) as the spawn root.
                windowRoot = transform.parent != null ? transform.parent : transform;
            }

            if (popupRoot == null)
            {
                popupRoot = windowRoot;
            }
        }

        private void Update()
        {
            HintMagnifierService.Instance?.ServiceUpdate();
        }

        private void Start()
        {
            Debug.Log("[WindowManager] Start() called, IsInLoadingPhase=" + IsInLoadingPhase);
            
            // 创建过渡效果管理器
            if (enableTransitionEffect && WindowTransitionEffect.Instance == null)
            {
                // 关键：不设置 parent！让 WindowTransitionEffect 自己管理 hierarchy
                GameObject transitionGO = new GameObject("WindowTransitionEffect", typeof(WindowTransitionEffect));
                // 不要添加：transitionGO.transform.SetParent(transform);
            }

            // Prewarm content windows so MainWnd can scan totals (Fish/etc.) even when those windows are inactive.
            if (keepWindowsAlive && prewarmContentWindowsOnStartup)
            {
                PrewarmContentWindows();
            }

            // 如果不在Loading流程中，默认显示主界面
            if (!IsInLoadingPhase)
            {
                Debug.Log("[WindowManager] 不在Loading流程中，显示主界面");
                ShowMainWindow();
            }
            else
            {
                Debug.Log("[WindowManager] 当前处于Loading流程，延迟显示MainWnd");
            }
        }

        public void ShowMainWindow()
        {
            SwitchToWindow(mainWndPrefab);
        }

        public void ShowRoomWindow()
        {
            SwitchToWindow(roomWndPrefab);
        }

        // Popup methods (overlay on top of current window)
        public void ShowSettingPopup()
        {
            ShowPopup(settingPopPrefab);
        }

        public void ShowRankPopup()
        {
            ShowPopup(rankPopPrefab);
        }

        public void HideCurrentPopup()
        {
            if (_currentPopup != null)
            {
                Destroy(_currentPopup);
                _currentPopup = null;
            }
        }

        private void SwitchToWindow(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[WindowManager] Target window prefab is not assigned.");
                return;
            }

            if (_isSwitchingWindow)
            {
                Debug.LogWarning($"[WindowManager] SwitchToWindow re-entry ignored. target={prefab.name}, current={(_currentWindow != null ? _currentWindow.name : "null")}, depth={_switchDepth}");
                return;
            }

            float t0 = Time.realtimeSinceStartup;
            _isSwitchingWindow = true;
            _switchDepth++;

            VerboseWindowLog($"[WindowManager] SwitchToWindow BEGIN targetPrefab={prefab.name} keepAlive={keepWindowsAlive} frame={Time.frameCount} t={t0:0.000} current={(_currentWindow != null ? _currentWindow.name : "null")}");

            // Clear transient hint bubbles when switching windows
            HintBubbleService.ClearAll();

            GameObject previousWindow = _currentWindow;

            // 根据当前窗口判断使用哪个特殊过渡配置
            // MainWnd 去其他界面时使用特殊配置
            // 其他窗口使用默认配置
            TransitionConfig transitionConfig = null;
            if (previousWindow != null)
            {
                if (previousWindow.name.Contains("Main") && mainWndTransitionConfig != null)
                    transitionConfig = mainWndTransitionConfig;
            }

            try
            {
                if (enableTransitionEffect && WindowTransitionEffect.Instance != null)
                {
                    if (transitionConfig != null)
                    {
                        // 使用特殊过渡配置
                        LetterboxController.Instance?.EnableLetterboxBackground();
                        WindowTransitionEffect.Instance.PerformTransition(
                            transitionConfig,
                            () => // onMidPoint: 在 Phase2（纯色阶段）结束时切换窗口
                            {
                                SwitchWindowImmediate(prefab, previousWindow);
                            },
                            () => // onComplete: 在 Phase3 结束后清理
                            {
                                LetterboxController.Instance?.DisableLetterboxBackground();
                                _isSwitchingWindow = false;
                                _switchDepth = Mathf.Max(0, _switchDepth - 1);
                            }
                        );
                    }
                    else
                    {
                        // 使用默认过渡配置
                        LetterboxController.Instance?.EnableLetterboxBackground();
                        WindowTransitionEffect.Instance.PerformTransition(
                            () => // onMidPoint: 在 Phase2（纯色阶段）结束时切换窗口
                            {
                                SwitchWindowImmediate(prefab, previousWindow);
                            },
                            () => // onComplete: 在 Phase3 结束后清理
                            {
                                LetterboxController.Instance?.DisableLetterboxBackground();
                                _isSwitchingWindow = false;
                                _switchDepth = Mathf.Max(0, _switchDepth - 1);
                            }
                        );
                    }
                    return;
                }

                // 直接切换（无过渡效果）
                SwitchWindowImmediate(prefab, previousWindow);
                _isSwitchingWindow = false;
                _switchDepth = Mathf.Max(0, _switchDepth - 1);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _isSwitchingWindow = false;
                _switchDepth = Mathf.Max(0, _switchDepth - 1);
            }
        }

        private void SwitchWindowImmediate(GameObject prefab, GameObject previousWindow)
        {
            if (keepWindowsAlive)
            {
                if (_currentWindow != null)
                {
                    _currentWindow.SetActive(false);
                }

                GameObject instance = GetOrCreateWindowInstance(prefab);
                if (instance == null)
                {
                    Debug.LogError($"[WindowManager] GetOrCreateWindowInstance returned null for prefab={prefab.name}");
                    return;
                }

                instance.SetActive(true);
                _currentWindow = instance;

                HintMagnifierService.Instance?.OnWindowSwitched();
                EnsureNumUIController(instance);
                MaybeTryStartSpeedrunOnGameplayEntry(prefab, previousWindow);
                AudioManager.Instance?.ApplyBgmForWindowPrefab(prefab.name);

                Canvas canvas = instance.GetComponentInChildren<Canvas>();
                VerboseWindowLog($"[WindowManager] Window activated: {instance.name}, active={instance.activeSelf}, activeInHierarchy={instance.activeInHierarchy}, canvas={(canvas != null ? canvas.name : "null")}, canvasEnabled={(canvas != null ? canvas.enabled : false)}");
                return;
            }

            // Legacy behavior: Destroy and re-instantiate each time.
            if (_currentWindow != null)
            {
                Destroy(_currentWindow);
                _currentWindow = null;
            }

            _currentWindow = Instantiate(prefab, windowRoot);
            EnsureWindowRootStretchesToParent(_currentWindow);
            HintMagnifierService.Instance?.OnWindowSwitched();
            MaybeTryStartSpeedrunOnGameplayEntry(prefab, previousWindow);
            AudioManager.Instance?.ApplyBgmForWindowPrefab(prefab.name);
        }

        private void PrewarmContentWindows()
        {
            // Only prewarm windows that contain scene content (fish/cats/etc.).
            // MainWnd is instantiated on demand by ShowMainWindow().
            GetOrCreateWindowInstance(roomWndPrefab, setInactive: true);
        }

        /// <summary>
        /// Initialize NumUIController on a window if it doesn't exist
        /// </summary>
        private void EnsureNumUIController(GameObject windowInstance)
        {
            if (windowInstance == null) return;

            // Find NumUI in the window
            var numUI = windowInstance.transform.Find("NumUI");
            if (numUI == null)
            {
                // Try to find it deep in the hierarchy
                numUI = windowInstance.transform.FindDeepChild("NumUI");
            }

            VerboseWindowLog($"[WindowManager] EnsureNumUIController: window={windowInstance.name}, numUI found={numUI != null}");

            if (numUI != null)
            {
                // FIX: If NumUI is directly under window root (not under Ui), move it to Ui or create Ui node
                Transform numUIParent = numUI.parent;
                if (numUIParent != null && numUIParent.name != "Ui" && numUIParent.name != "UI")
                {
                    Debug.LogWarning($"[WindowManager] NumUI is under '{numUIParent.name}' instead of 'Ui'. Fixing hierarchy...");
                    
                    // Find or create Ui node
                    var uiNode = windowInstance.transform.Find("Ui");
                    if (uiNode == null)
                    {
                        uiNode = windowInstance.transform.Find("UI");
                    }
                    
                    if (uiNode == null)
                    {
                        // Create Ui node if doesn't exist
                        var uiGo = new GameObject("Ui", typeof(RectTransform));
                        uiNode = uiGo.transform;
                        uiNode.SetParent(windowInstance.transform, false);
                        
                        // Set up RectTransform to stretch to fill
                        var rt = (RectTransform)uiNode;
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = Vector2.zero;
                        rt.sizeDelta = Vector2.zero;
                        rt.localScale = Vector3.one;
                        
                        // Add CanvasRenderer to ensure UI can render
                        uiGo.AddComponent<CanvasRenderer>();
                        
                        VerboseWindowLog($"[WindowManager] Created new Ui node under {windowInstance.name}");
                    }
                    
                    // Move NumUI under Ui - use worldPositionStays=true to preserve visual position
                    // Save NumUI's original RectTransform settings before moving
                    Vector2 originalAnchorMin = Vector2.zero;
                    Vector2 originalAnchorMax = Vector2.zero;
                    Vector2 originalAnchoredPosition = Vector2.zero;
                    Vector2 originalSizeDelta = Vector2.zero;
                    Vector2 originalPivot = Vector2.zero;
                    var numUIRtBefore = numUI as RectTransform;
                    if (numUIRtBefore != null)
                    {
                        originalAnchorMin = numUIRtBefore.anchorMin;
                        originalAnchorMax = numUIRtBefore.anchorMax;
                        originalAnchoredPosition = numUIRtBefore.anchoredPosition;
                        originalSizeDelta = numUIRtBefore.sizeDelta;
                        originalPivot = numUIRtBefore.pivot;
                    }
                    
                    numUI.SetParent(uiNode, false);
                    numUI.SetAsLastSibling(); // Keep UI on top
                    
                    // Restore NumUI's original RectTransform settings after moving
                    var numUIRt = numUI as RectTransform;
                    if (numUIRt != null)
                    {
                        numUIRt.anchorMin = originalAnchorMin;
                        numUIRt.anchorMax = originalAnchorMax;
                        numUIRt.anchoredPosition = originalAnchoredPosition;
                        numUIRt.sizeDelta = originalSizeDelta;
                        numUIRt.pivot = originalPivot;
                    }
                    
                    // Force rebuild layout and canvas update
                    UnityEngine.UI.LayoutGroup[] layoutGroups = uiNode.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>();
                    foreach (var layout in layoutGroups)
                    {
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                    }
                    
                    Canvas.ForceUpdateCanvases();
                    
                    VerboseWindowLog($"[WindowManager] Moved NumUI under Ui node, forced layout rebuild");
                    
                    // Update parent reference after moving
                    numUIParent = numUI.parent;
                }

                // Debug: Check the full hierarchy path to NumUI
                VerboseWindowLog($"[WindowManager] NumUI found: activeSelf={numUI.gameObject.activeSelf}, activeInHierarchy={numUI.gameObject.activeInHierarchy}, parent={numUI.parent?.name}");

                // Debug: Check NumUI RectTransform properties
                var numUIRectCheck = numUI as RectTransform;
                if (numUIRectCheck != null)
                {
                    VerboseWindowLog($"[WindowManager] NumUI RectTransform: anchoredPosition={numUIRectCheck.anchoredPosition}, sizeDelta={numUIRectCheck.sizeDelta}, anchorMin={numUIRectCheck.anchorMin}, anchorMax={numUIRectCheck.anchorMax}, pivot={numUIRectCheck.pivot}, localScale={numUIRectCheck.localScale}");
                }
                
                // Debug: Check Ui node RectTransform if exists
                var uiNodeRectCheck = numUI.parent as RectTransform;
                if (uiNodeRectCheck != null)
                {
                    VerboseWindowLog($"[WindowManager] Ui parent RectTransform: anchoredPosition={uiNodeRectCheck.anchoredPosition}, sizeDelta={uiNodeRectCheck.sizeDelta}, anchorMin={uiNodeRectCheck.anchorMin}, anchorMax={uiNodeRectCheck.anchorMax}, localScale={uiNodeRectCheck.localScale}");
                }

                // Debug: Check all child nodes of NumUI
                var childNames = new List<string>();
                foreach (Transform child in numUI)
                {
                    childNames.Add($"{child.name}(active={child.gameObject.activeSelf})");
                }
                VerboseWindowLog($"[WindowManager] NumUI children: {string.Join(", ", childNames)}");

                // Extra debug: Check specifically for Jigsaws nodes
                var jigsawsSearch = numUI.Find("Jigsaws");
                VerboseWindowLog($"[WindowManager] Jigsaws node search result: {(jigsawsSearch != null ? jigsawsSearch.name : "null")}");

                // Debug: Check the full parent chain
                Transform checkParent = numUI.parent;
                while (checkParent != null)
                {
                    VerboseWindowLog($"[WindowManager] NumUI parent chain: {checkParent.name} (activeSelf={checkParent.gameObject.activeSelf}, activeInHierarchy={checkParent.gameObject.activeInHierarchy})");
                    checkParent = checkParent.parent;
                }

                // Force refresh canvas and layout
                var canvas = numUI.GetComponentInParent<UnityEngine.Canvas>();
                string canvasInfo = canvas != null ? $"{canvas.name}, enabled={canvas.enabled}" : "null";
                VerboseWindowLog($"[WindowManager] NumUI Canvas: {canvasInfo}");

                // Check if NumUIController exists and its state
                var controller = numUI.GetComponent<NumUIController>();
                VerboseWindowLog($"[WindowManager] NumUIController exists: {controller != null}");
                if (controller != null)
                {
                    VerboseWindowLog($"[WindowManager] NumUIController state: sceneName={controller.sceneName}, " +
                              $"normalCats={controller.normalCatsNode?.name}, hiddenCats={controller.hiddenCatsNode?.name}");
                }

                // Check if parent chain is all active
                Transform parent = numUI.parent;
                while (parent != null)
                {
                    if (!parent.gameObject.activeSelf)
                    {
                        Debug.LogWarning($"[WindowManager] NumUI parent chain has inactive object: {parent.name}, enabling...");
                        parent.gameObject.SetActive(true);
                    }
                    parent = parent.parent;
                }

                // Ensure NumUI is active
                if (!numUI.gameObject.activeSelf)
                {
                    VerboseWindowLog($"[WindowManager] Enabling NumUI in {windowInstance.name}");
                    numUI.gameObject.SetActive(true);
                }

                // Now check/add NumUIController after ensuring all parents are active
                if (controller == null)
                {
                    VerboseWindowLog($"[WindowManager] Adding NumUIController to {numUI.name} in {windowInstance.name}");
                    controller = numUI.gameObject.AddComponent<NumUIController>();

                    // Auto-detect scene name from window name
                    string windowName = windowInstance.name.Replace("(Clone)", "");
                    if (windowName.Contains("Room"))
                        controller.sceneName = SceneName.RoomWnd;
                    else if (windowName.Contains("Flower"))
                        controller.sceneName = SceneName.FlowerWnd;
                    else if (windowName.Contains("Cafe"))
                        controller.sceneName = SceneName.CafeWnd;

                    // Auto-find and set the node references
                    controller.normalCatsNode = numUI.Find("NormalCats");
                    controller.hiddenCatsNode = numUI.Find("HiddenCats");
                    // Fish and Fire nodes may not exist in all prefabs
                    Transform fishNode = numUI.Find("Fish");
                    if (fishNode != null) controller.fishNode = fishNode;
                    Transform fireworkNode = numUI.Find("Firework");
                    if (fireworkNode != null) controller.fireworkNode = fireworkNode;

                    VerboseWindowLog($"[WindowManager] NumUIController configured: sceneName={controller.sceneName}, " +
                              $"normalCats={controller.normalCatsNode?.name}, hiddenCats={controller.hiddenCatsNode?.name}");
                }
            }
        }

        /// <summary>
        /// Speedrun runs must begin only when entering Room/Flower/Cafe from a non-gameplay window (e.g. main menu).
        /// Calling <see cref="SpeedrunService.TryStartRun"/> on every gameplay window OnEnable would reset cats whenever
        /// <see cref="SpeedrunService.IsRunActive"/> is false (e.g. after a misfired completion or prefs clear).
        /// </summary>
        private void MaybeTryStartSpeedrunOnGameplayEntry(GameObject targetPrefab, GameObject previousWindowInstance)
        {
            if (!IsGameplayWindowPrefab(targetPrefab))
            {
                return;
            }

            if (InstanceIsGameplayWindow(previousWindowInstance))
            {
                return;
            }

            SpeedrunService.Instance?.TryStartRun();
        }

        private bool IsGameplayWindowPrefab(GameObject prefab)
        {
            return prefab != null && prefab == roomWndPrefab;
        }

        private bool InstanceIsGameplayWindow(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            foreach (KeyValuePair<GameObject, GameObject> kv in _windowInstances)
            {
                if (kv.Value == instance && IsGameplayWindowPrefab(kv.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject GetOrCreateWindowInstance(GameObject prefab, bool setInactive = false)
        {
            if (prefab == null)
            {
                return null;
            }

            if (_windowInstances.TryGetValue(prefab, out var existing) && existing != null)
            {
                if (setInactive)
                {
                    existing.SetActive(false);
                }
                return existing;
            }

            GameObject instance = Instantiate(prefab, windowRoot);
            EnsureWindowRootStretchesToParent(instance);
            _windowInstances[prefab] = instance;

            if (setInactive)
            {
                instance.SetActive(false);
            }

            return instance;
        }

        /// <summary>
        /// Many window prefabs were authored with a 100x100 RectTransform root (anchored center); MainWnd is
        /// stretched in the prefab so editor layout matches play mode. Zoom/pan (see GameSceneUI) uses the window
        /// root as coordinate space — roots must stretch to the parent (usually a full-screen Canvas).
        /// </summary>
        private static void EnsureWindowRootStretchesToParent(GameObject windowInstance)
        {
            if (windowInstance == null)
            {
                return;
            }

            var rt = windowInstance.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            // Only fix if it looks like a "tiny centered" root; avoid touching intentionally-sized popups.
            // Most problematic windows are exactly 100x100 with center anchors.
            bool looksLikeTinyRoot =
                rt.anchorMin == new Vector2(0.5f, 0.5f) &&
                rt.anchorMax == new Vector2(0.5f, 0.5f) &&
                Mathf.Abs(rt.sizeDelta.x) <= 200f &&
                Mathf.Abs(rt.sizeDelta.y) <= 200f;

            if (!looksLikeTinyRoot)
            {
                return;
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private void ShowPopup(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[WindowManager] Target popup prefab is not assigned.");
                return;
            }

            // Clear transient hint bubbles when opening a popup to avoid overlapping hints.
            HintBubbleService.ClearAll();

            // Hide current popup if any
            if (_currentPopup != null)
            {
                Destroy(_currentPopup);
                _currentPopup = null;
            }

            _currentPopup = Instantiate(prefab, popupRoot);
        }
    }
}

