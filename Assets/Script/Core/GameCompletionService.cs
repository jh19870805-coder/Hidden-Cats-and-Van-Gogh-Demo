using System;
using UnityEngine;
using HiddenCats.Core;
using HiddenCats.UI;
using UnityEngine.UI;

namespace HiddenCats.Core
{
    /// <summary>
    /// Handles game completion detection and WinPop display for normal (non-speedrun) mode.
    /// Monitors collection progress and shows WinPop when all normal and hidden cats are found.
    /// </summary>
    public sealed class GameCompletionService : MonoBehaviour
    {
        public static GameCompletionService Instance { get; private set; }

        // PlayerPrefs key for tracking if game has been completed at least once (for unlock purposes)
        private const string KEY_GAME_COMPLETED = "GameCompleted";

        // Events
        /// <summary>Fired when the game is completed (all cats found).</summary>
        public event Action OnGameCompleted;

        private bool _isCompleted = false;
        private bool _hasPendingCompletion = false;

        public bool IsGameCompleted => _isCompleted;
        public bool HasPendingCompletion => _hasPendingCompletion;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            LoadCompletionState();
        }

        private void Start()
        {
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged += HandleGlobalCountChanged;
            }

            // Check completion on startup in case player returned with a save that already has all cats collected
            // Delay by a few frames to ensure WindowManager is ready
            StartCoroutine(CheckCompletionAfterDelay());
        }

        private System.Collections.IEnumerator CheckCompletionAfterDelay()
        {
            // Wait a few frames for other services to initialize
            yield return null;
            yield return null;
            yield return null;

            // Only check if we haven't already triggered completion for this save
            if (!_hasPendingCompletion)
            {
                CheckGameCompletion();
            }
        }

        private void OnDestroy()
        {
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged -= HandleGlobalCountChanged;
            }
        }

        private void HandleGlobalCountChanged(CollectibleType type, int newCount)
        {
            // Only check completion in non-speedrun mode
            if (SpeedrunService.Instance != null && SpeedrunService.Instance.IsSpeedrunEnabled)
            {
                return;
            }

            if (type == CollectibleType.NormalCat || type == CollectibleType.HiddenCat)
            {
                CheckGameCompletion();
            }
        }

        /// <summary>
        /// Check if all normal and hidden cats have been collected.
        /// </summary>
        public void CheckGameCompletion()
        {
            // Not in speedrun mode
            if (SpeedrunService.Instance != null && SpeedrunService.Instance.IsSpeedrunEnabled)
            {
                return;
            }

            if (CollectionService.Instance == null)
            {
                return;
            }

            // Get totals from interactable objects
            if (!UnlockChecker.TryGetTotalCatsForSpeedrun(out int totalNormal, out int totalHidden))
            {
                return;
            }

            int collectedNormal = CollectionService.Instance.GetGlobalCount(CollectibleType.NormalCat);
            int collectedHidden = CollectionService.Instance.GetGlobalCount(CollectibleType.HiddenCat);

            Debug.Log($"[GameCompletionService] CheckGameCompletion: {collectedNormal}/{totalNormal} NormalCat, {collectedHidden}/{totalHidden} HiddenCat");

            if (collectedNormal >= totalNormal && collectedHidden >= totalHidden)
            {
                CompleteGameIfActive();
            }
        }

        /// <summary>
        /// Called when all cats are found. Shows WinPop and records completion.
        /// </summary>
        public void CompleteGameIfActive()
        {
            // Guard: prevent double-invocation within the same frame or during scene transitions
            if (_isCompleted || _hasPendingCompletion)
            {
                return;
            }

            // Not in speedrun mode
            if (SpeedrunService.Instance != null && SpeedrunService.Instance.IsSpeedrunEnabled)
            {
                return;
            }

            _hasPendingCompletion = true;
            _isCompleted = true;

            Debug.Log("[GameCompletionService] Game completed! Showing WinPop.");

            // Show WinPop immediately
            ShowWinPopAndBindButton();

            // Persist completion state
            SaveCompletionState();

            OnGameCompleted?.Invoke();
        }

        private void LoadCompletionState()
        {
            _isCompleted = PlayerPrefs.GetInt(KEY_GAME_COMPLETED, 0) == 1;
        }

        private void SaveCompletionState()
        {
            PlayerPrefs.SetInt(KEY_GAME_COMPLETED, _isCompleted ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reset completion state (called from GameProgressResetService).
        /// </summary>
        public void ResetCompletionState()
        {
            _isCompleted = false;
            _hasPendingCompletion = false;
            PlayerPrefs.DeleteKey(KEY_GAME_COMPLETED);
            PlayerPrefs.Save();
        }

        private void ShowWinPopAndBindButton()
        {
            if (WindowManager.Instance == null || WindowManager.Instance.CurrentWindow == null)
            {
                Debug.LogWarning("[GameCompletionService] Cannot show WinPop: WindowManager or CurrentWindow is null.");
                return;
            }

            Transform root = WindowManager.Instance.CurrentWindow.transform;
            Transform winPopTr = FindDescendantByName(root, "WinPop");
            if (winPopTr == null)
            {
                Debug.LogWarning("[GameCompletionService] WinPop not found under current window.");
                return;
            }

            GameObject winPop = winPopTr.gameObject;

            // Ensure WinPopCelebrationParticles is attached for particle effects
            EnsureWinPopCelebrationParticles(winPopTr);

            winPop.SetActive(true);

            AudioManager.Instance?.PlaySfx("overNewRecord");

            Transform buttonTr = FindDescendantByName(winPopTr, "ButtonWin");
            if (buttonTr == null)
            {
                Debug.LogWarning("[GameCompletionService] ButtonWin not found under WinPop.");
                return;
            }

            Button btn = buttonTr.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("[GameCompletionService] ButtonWin has no Button component.");
                return;
            }

            // Ensure we don't register duplicates
            btn.onClick.RemoveListener(OnClick_WinPopReturnToMain);
            btn.onClick.AddListener(OnClick_WinPopReturnToMain);

            // Apply button localization if available
            WinPopButtonLocalization.Apply(winPopTr);
        }

        /// <summary>
        /// Ensure WinPopCelebrationParticles component is attached for particle effects.
        /// If WinPop.prefab doesn't have the component, this adds it at runtime.
        /// </summary>
        private void EnsureWinPopCelebrationParticles(Transform winPopTr)
        {
            if (winPopTr == null)
            {
                return;
            }

            var celebrationParticles = winPopTr.GetComponent<UI.WinPopCelebrationParticles>();
            if (celebrationParticles == null)
            {
                celebrationParticles = winPopTr.gameObject.AddComponent<UI.WinPopCelebrationParticles>();
                Debug.Log("[GameCompletionService] Added WinPopCelebrationParticles to WinPop.");
            }
        }

        private void OnClick_WinPopReturnToMain()
        {
            // Hide WinPop if visible
            try
            {
                if (WindowManager.Instance != null && WindowManager.Instance.CurrentWindow != null)
                {
                    Transform winPopTr = FindDescendantByName(WindowManager.Instance.CurrentWindow.transform, "WinPop");
                    if (winPopTr != null)
                    {
                        winPopTr.gameObject.SetActive(false);
                    }
                }
            }
            catch
            {
                // Ignore
            }

            // Clear pending completion flag
            _hasPendingCompletion = false;

            // Return to MainWnd
            if (WindowManager.Instance != null)
            {
                WindowManager.Instance.ShowMainWindow();
            }
        }

        private static Transform FindDescendantByName(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
