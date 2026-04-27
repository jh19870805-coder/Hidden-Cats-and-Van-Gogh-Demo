using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenCats.UI;
using HiddenCats.Core;

namespace HiddenCats.Core
{
    /// <summary>
    /// Initializes core game systems on startup.
    /// Attach this to a GameObject in the StartUp scene (or let it auto-create).
    /// </summary>
    public sealed class GameInitializer : MonoBehaviour
    {
        [Header("Loading 界面")]
        [Tooltip("Loading 界面预制体")]
        [SerializeField] private GameObject loadingUIPrefab;

        private static bool _isInitialized = false;
        private bool _hasStartedLoading = false;

        private void Awake()
        {
            if (_isInitialized)
            {
                return;
            }

            // 立即标记Loading阶段，阻止WindowManager自动显示MainWnd
            // 注意：这必须在其他Start()之前执行
            WindowManager.IsInLoadingPhase = true;

            // 启动Loading流程
            if (!_hasStartedLoading)
            {
                _hasStartedLoading = true;
                StartCoroutine(StartupSequenceCoroutine());
            }

            InitializeCoreSystems();
            _isInitialized = true;
        }

        private IEnumerator StartupSequenceCoroutine()
        {
            // 等待一帧确保所有Awake完成
            yield return null;

            // 1. 确保并显示Loading界面
            GameObject loadingInstance = EnsureLoadingUI();
            
            if (loadingInstance == null)
            {
                WindowManager.IsInLoadingPhase = false;
                yield break;
            }

            // 获取LoadingUI脚本并开始加载
            LoadingUI loadingUI = loadingInstance.GetComponent<LoadingUI>();
            if (loadingUI != null)
            {
                // LoadingUI 会自己处理加载逻辑和进入MainWnd
                // LoadingUI完成时会将IsInLoadingPhase设为false
                yield break;
            }
            else
            {
                // 降级方案：至少等待2秒后进入MainWnd
                yield return new WaitForSeconds(2f);
                WindowManager.IsInLoadingPhase = false;
                EnterMainWindow();
            }
        }

        private GameObject EnsureLoadingUI()
        {
            // 检查场景中是否已有Loading
            GameObject existingLoading = GameObject.Find("Loading");
            if (existingLoading != null)
            {
                existingLoading.SetActive(true);
                return existingLoading;
            }

            // 没有则实例化（作为独立根对象，不挂载到 MainWndCanvas 下）
            if (loadingUIPrefab != null)
            {
                GameObject instance = Instantiate(loadingUIPrefab);
                return instance;
            }

            // 尝试从 Resources 加载
            GameObject loadingFromResources = Resources.Load<GameObject>("Pop/Loading");
            if (loadingFromResources != null)
            {
                return Instantiate(loadingFromResources);
            }

            return null;
        }

        private void EnterMainWindow()
        {
            if (WindowManager.Instance == null)
            {
                Debug.LogError("[GameInitializer] WindowManager not found!");
                return;
            }

            GameObject mainWndPrefab = WindowManager.Instance.GetMainWndPrefab();
            if (mainWndPrefab != null)
            {
                WindowManager.Instance.PublicSwitchToWindow(mainWndPrefab);
            }
        }

        private void InitializeCoreSystems()
        {
            // Align every CanvasScaler with the desktop / Steam UI baseline (see UiScalePolicy).
            UiScalePolicy.ApplyToAllScreenSpaceCanvases();

            // Ensure LetterboxController exists (for aspect ratio letterboxing)
            if (LetterboxController.Instance == null)
            {
                LetterboxController existingLetterbox = FindFirstObjectByType<LetterboxController>();
                if (existingLetterbox == null)
                {
                    GameObject letterboxObj = new GameObject("LetterboxController");
                    letterboxObj.AddComponent<LetterboxController>();
                    DontDestroyOnLoad(letterboxObj);
                }
            }

            // Ensure SettingsManager exists
            if (SettingsManager.Instance == null)
            {
                // Try to find existing SettingsManager in scene first
                SettingsManager existingSettings = FindFirstObjectByType<SettingsManager>();
                if (existingSettings == null)
                {
                GameObject settingsManagerObj = new GameObject("SettingsManager");
                settingsManagerObj.AddComponent<SettingsManager>();
                DontDestroyOnLoad(settingsManagerObj);
                }
            }

            // Ensure AudioManager exists
            // IMPORTANT: Check for existing AudioManager in scene first to preserve configured sfxEntries
            if (AudioManager.Instance == null)
            {
                // Try to find existing AudioManager in scene first
                AudioManager existingAudio = FindFirstObjectByType<AudioManager>();
                if (existingAudio == null)
                {
                    // Only create new one if none exists in scene
                GameObject audioManagerObj = new GameObject("AudioManager");
                audioManagerObj.AddComponent<AudioManager>();
                DontDestroyOnLoad(audioManagerObj);
                }
            }

            // Ensure CursorManager exists
            if (CursorManager.Instance == null)
            {
                // Try to find existing CursorManager in scene first
                CursorManager existingCursor = FindFirstObjectByType<CursorManager>();
                if (existingCursor == null)
                {
                    GameObject cursorManagerObj = new GameObject("CursorManager");
                    cursorManagerObj.AddComponent<CursorManager>();
                    DontDestroyOnLoad(cursorManagerObj);
                }
            }

            // Ensure CollectionService exists
            if (CollectionService.Instance == null)
            {
                // Try to find existing CollectionService in scene first
                CollectionService existingCollection = FindFirstObjectByType<CollectionService>();
                if (existingCollection == null)
                {
                    GameObject collectionServiceObj = new GameObject("CollectionService");
                    collectionServiceObj.AddComponent<CollectionService>();
                    DontDestroyOnLoad(collectionServiceObj);
                }
            }

            // Ensure SpeedrunService exists
            if (SpeedrunService.Instance == null)
            {
                SpeedrunService existingSpeedrun = FindFirstObjectByType<SpeedrunService>();
                if (existingSpeedrun == null)
                {
                    GameObject speedrunServiceObj = new GameObject("SpeedrunService");
                    speedrunServiceObj.AddComponent<SpeedrunService>();
                    DontDestroyOnLoad(speedrunServiceObj);
                }
            }

            // Check LocalizationManager (must be manually created in scene with LanguageConfig assigned)
            if (LocalizationManager.Instance == null)
            {
                LocalizationManager existingLocalization = FindFirstObjectByType<LocalizationManager>();
                if (existingLocalization == null)
                {
                    Debug.LogWarning("[GameInitializer] LocalizationManager not found. Please create a LocalizationManager GameObject in the StartUp scene and assign a LanguageConfig asset to it.");
                }
            }

            // Re-apply settings once both managers exist so that background music
            // starts playing with the correct volume on first launch.
            if (SettingsManager.Instance != null)
            {
                var currentSettings = SettingsManager.Instance.GetSettings();
                SettingsManager.Instance.ApplySettings(currentSettings);
            }

            // Ensure WindowManager exists (if not already created)
            // Use StartCoroutine to check after all Awake() calls complete
            StartCoroutine(CheckWindowManagerDelayed());
        }

        private System.Collections.IEnumerator CheckWindowManagerDelayed()
        {
            // Wait one frame to ensure all Awake() methods have completed
            yield return null;
            
            if (WindowManager.Instance == null)
            {
                Debug.LogWarning("[GameInitializer] WindowManager not found. Make sure WindowManager is set up in the StartUp scene.");
            }
        }
    }
}
