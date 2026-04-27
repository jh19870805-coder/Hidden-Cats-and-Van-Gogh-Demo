using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using HiddenCats.Interactable;
using HiddenCats.UI;
using UnityEngine.UI;

namespace HiddenCats.Core
{
    /// <summary>
    /// Serializable record for one speedrun completion.
    /// </summary>
    [Serializable]
    public sealed class SpeedrunRecord
    {
        public float timeSeconds;
        /// <summary>Local time string for display (e.g. "2026-03-06 14:30").</summary>
        public string completedAtLocal;
    }

    /// <summary>
    /// Global speedrun mode service (singleton, DontDestroyOnLoad).
    /// Manages:
    /// - Speedrun enabled/disabled toggle (persisted)
    /// - Per-run timer (accumulated across scenes, persisted for mid-run restarts)
    /// - Ranking records (persisted, sorted by time ascending)
    /// - Mode-aware save key prefix for interactable states
    /// </summary>
    public sealed class SpeedrunService : MonoBehaviour
    {
        public static SpeedrunService Instance { get; private set; }

        // --- PlayerPrefs keys ---
        private const string KEY_ENABLED  = "Speedrun_Enabled";
        private const string KEY_RECORDS  = "Speedrun_Records";
        private const string KEY_RUN_ACTIVE = "Speedrun_RunActive";
        private const string KEY_RUN_TIME   = "Speedrun_RunTime";
        private const string KEY_LATEST_RECORD_SIG = "Speedrun_LatestRecordSig";

        // --- State ---
        [SerializeField] private List<SpeedrunRecord> _records = new List<SpeedrunRecord>();
        private bool _isSpeedrunEnabled;
        private bool _runActive;
        private float _runTimeSeconds;
        private bool _hasPendingCompletion;
        private float _pendingCompletionTimeSeconds;

        // Index of the most-recently added record in the sorted list (-1 = none).
        private int _latestRecordIndex = -1;
        private string _latestRecordSig = string.Empty;

        // --- Public Properties ---
        public bool  IsSpeedrunEnabled    => _isSpeedrunEnabled;
        public bool  IsRunActive          => _runActive;
        public float CurrentRunTimeSeconds => _runTimeSeconds;
        public IReadOnlyList<SpeedrunRecord> Records => _records;
        public int   LatestRecordIndex    => _latestRecordIndex;

        // --- Events ---
        /// <summary>Fired after speedrun mode is toggled on/off. Param = new enabled state.</summary>
        public event Action<bool> OnModeChanged;

        /// <summary>Fired when a run is completed (record created).</summary>
        public event Action<SpeedrunRecord> OnRunCompleted;

        // =========================================================================
        // Lifecycle
        // =========================================================================

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

            LoadAll();
        }

        private void Start()
        {
            // After all Awake()s, CollectionService.Instance is guaranteed; align save slot with prefs.
            EnsureCollectionServiceModeMatchesSpeedrunToggle();

            // Subscribe to collection events to detect speedrun completion.
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged += HandleGlobalCountChanged;
            }

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChangedRefreshWinPopButton;
            }
        }

        private void OnDestroy()
        {
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnGlobalCountChanged -= HandleGlobalCountChanged;
            }

            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChangedRefreshWinPopButton;
            }
        }

        private void OnLanguageChangedRefreshWinPopButton(string _)
        {
            if (WindowManager.Instance == null || WindowManager.Instance.CurrentWindow == null)
            {
                return;
            }

            Transform winPopTr = FindDescendantByName(WindowManager.Instance.CurrentWindow.transform, "WinPop");
            if (winPopTr == null || !winPopTr.gameObject.activeSelf)
            {
                return;
            }

            WinPopButtonLocalization.Apply(winPopTr);
        }

        private void HandleGlobalCountChanged(CollectibleType type, int newCount)
        {
            // Only care about cat types during an active speedrun.
            if (!_isSpeedrunEnabled || !_runActive)
                return;

            if (type == CollectibleType.NormalCat || type == CollectibleType.HiddenCat)
            {
                CheckSpeedrunCompletion();
            }
        }

        private void Update()
        {
            if (_runActive)
            {
                _runTimeSeconds += Time.deltaTime;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Persist timer on mobile pause / alt-tab so mid-run progress isn't lost.
            if (pauseStatus && _runActive)
            {
                SaveRunState();
            }
        }

        private void OnApplicationQuit()
        {
            if (_runActive)
            {
                SaveRunState();
            }

            // Ensure toggle state is flushed (e.g. if only in-memory paths changed in a future edit).
            PlayerPrefs.SetInt(KEY_ENABLED, _isSpeedrunEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Keeps <see cref="CollectionService"/> save slot in sync with persisted <c>Speedrun_Enabled</c>.
        /// Must run after <see cref="CollectionService"/> has initialized (e.g. from <see cref="Start"/>).
        /// </summary>
        private void EnsureCollectionServiceModeMatchesSpeedrunToggle()
        {
            if (CollectionService.Instance == null)
            {
                Debug.LogWarning("[SpeedrunService] CollectionService.Instance is null; cannot apply speedrun save slot. Will retry is not implemented — check scene bootstrap.");
                return;
            }

            if (CollectionService.Instance.IsSpeedrunMode == _isSpeedrunEnabled)
            {
                return;
            }

            CollectionService.Instance.SwitchToSpeedrunMode(_isSpeedrunEnabled);
        }

        // =========================================================================
        // Public API
        // =========================================================================

        /// <summary>
        /// Returns the save-key prefix that interactables should use for their
        /// PlayerPrefs keys.  Returns <c>"SR_"</c> in speedrun mode, <c>""</c> otherwise.
        /// </summary>
        public static string GetSaveKeyPrefix()
        {
            return (Instance != null && Instance._isSpeedrunEnabled) ? "SR_" : "";
        }

        /// <summary>
        /// Toggle speedrun mode on/off.
        /// Switches CollectionService save slot, reloads all cat states, fires event.
        /// </summary>
        public void SetSpeedrunEnabled(bool enabled)
        {
            if (_isSpeedrunEnabled == enabled)
                return;

            _isSpeedrunEnabled = enabled;
            PlayerPrefs.SetInt(KEY_ENABLED, enabled ? 1 : 0);
            PlayerPrefs.Save();

            // Switch CollectionService to the matching save slot.
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.SwitchToSpeedrunMode(enabled);
            }

            // Reload all cat visual states so they reflect the correct mode's save data.
            ReloadAllCatStates();

            // If turning off while a run is active, stop the run (don't record).
            if (!enabled && _runActive)
            {
                _runActive = false;
                ClearRunState();
            }

            OnModeChanged?.Invoke(enabled);
        }

        /// <summary>
        /// Called when entering any game scene (Room/Flower/Cafe).
        /// If speedrun is enabled and no run is active, starts a fresh run
        /// (resets speedrun collection data + cat states, starts timer).
        /// </summary>
        public void TryStartRun()
        {
            if (!_isSpeedrunEnabled || _runActive)
                return;

            Debug.Log("[SpeedrunService] Starting new speedrun run.");

            // Reset speedrun collection data (cats only – fish/firework/puzzle are blocked).
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.ResetCollectionData();
            }

            // Reset all cat visual states for speedrun save keys.
            ResetAllCatStatesForSpeedrun();

            // Reset HiddenObjectManager completion flags so they can re-fire.
            RefreshHiddenObjectManagers();

            _runActive = true;
            _runTimeSeconds = 0f;
            SaveRunState();
        }

        /// <summary>
        /// Called when we detect that all normal + hidden cats across all scenes have been found.
        /// Stops the timer and shows WinPop. The leaderboard record is created when the user clicks ButtonWin.
        /// </summary>
        public void CompleteRunIfActive()
        {
            if (!_isSpeedrunEnabled || !_runActive)
                return;

            _runActive = false;
            _hasPendingCompletion = true;
            _pendingCompletionTimeSeconds = _runTimeSeconds;
            SaveRunState();

            Debug.Log($"[SpeedrunService] Run completed (pending confirm). Time: {_pendingCompletionTimeSeconds:F1}s");

            ShowWinPopAndBindButton();
        }

        /// <summary>
        /// Clear all ranking records (called from GameProgressResetService).
        /// </summary>
        public void ResetRecords()
        {
            _records.Clear();
            _latestRecordIndex = -1;
            _latestRecordSig = string.Empty;
            PlayerPrefs.DeleteKey(KEY_LATEST_RECORD_SIG);
            SaveRecords();
        }

        /// <summary>
        /// Full reset: records + run state + speedrun collection data.
        /// </summary>
        public void ResetAll()
        {
            _runActive = false;
            _runTimeSeconds = 0f;
            ClearRunState();

            ResetRecords();

            // Reset speedrun collection data.
            bool wasSpeedrun = CollectionService.Instance != null && CollectionService.Instance.IsSpeedrunMode;
            if (CollectionService.Instance != null)
            {
                if (!wasSpeedrun)
                    CollectionService.Instance.SwitchToSpeedrunMode(true);

                CollectionService.Instance.ResetCollectionData();

                if (!wasSpeedrun)
                    CollectionService.Instance.SwitchToSpeedrunMode(false);
            }

            // Delete speedrun-prefixed interactable keys via reset version.
            // (Individual cat keys with "SR_" prefix will be picked up by reset version logic.)

            // Full progress reset must turn OFF the persisted speedrun toggle (Speedrun_Enabled).
            // Otherwise after restart, LoadAll() restores KEY_ENABLED=1 and the toggle stays "on"
            // even though leaderboard/trophy should start locked again.
            SetSpeedrunEnabled(false);
        }

        // =========================================================================
        // Speedrun completion detection
        // =========================================================================

        /// <summary>
        /// Check if all normal + hidden cats across all scenes have been collected
        /// (in the current speedrun collection data).
        /// </summary>
        public void CheckSpeedrunCompletion()
        {
            if (!_isSpeedrunEnabled || !_runActive)
                return;

            if (CollectionService.Instance == null)
                return;

            // Get totals from interactable objects (they are pre-warmed, always exist).
            if (!UnlockChecker.TryGetTotalCatsForSpeedrun(out int totalNormal, out int totalHidden))
                return;

            int collectedNormal = CollectionService.Instance.GetGlobalCount(CollectibleType.NormalCat);
            int collectedHidden = CollectionService.Instance.GetGlobalCount(CollectibleType.HiddenCat);

            if (collectedNormal >= totalNormal && collectedHidden >= totalHidden)
            {
                CompleteRunIfActive();
            }
        }

        // =========================================================================
        // Private helpers
        // =========================================================================

        private void ShowWinPopAndBindButton()
        {
            if (WindowManager.Instance == null || WindowManager.Instance.CurrentWindow == null)
            {
                Debug.LogWarning("[SpeedrunService] Cannot show WinPop: WindowManager or CurrentWindow is null.");
                return;
            }

            Transform root = WindowManager.Instance.CurrentWindow.transform;
            Transform winPopTr = FindDescendantByName(root, "WinPop");
            if (winPopTr == null)
            {
                Debug.LogWarning("[SpeedrunService] WinPop not found under current window.");
                return;
            }

            GameObject winPop = winPopTr.gameObject;
            winPop.SetActive(true);
            AudioManager.Instance?.PlaySfx("overNewRecord");

            Transform buttonTr = FindDescendantByName(winPopTr, "ButtonWin");
            if (buttonTr == null)
            {
                Debug.LogWarning("[SpeedrunService] ButtonWin not found under WinPop.");
                return;
            }

            Button btn = buttonTr.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("[SpeedrunService] ButtonWin has no Button component.");
                return;
            }

            // Ensure we don't register duplicates if ShowWinPop is called multiple times.
            btn.onClick.RemoveListener(OnClick_WinPopReturnToMain);
            btn.onClick.AddListener(OnClick_WinPopReturnToMain);

            WinPopButtonLocalization.Apply(winPopTr);
        }

        private void OnClick_WinPopReturnToMain()
        {
            // Hide WinPop if it's currently visible.
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
            catch { /* ignore */ }

            // Spec: Only create leaderboard record when clicking return button.
            FinalizePendingCompletionIfAny();

            // Spec: Reset speedrun cats after completion.
            ResetSpeedrunCatsProgress();

            // Return to MainWnd.
            if (WindowManager.Instance != null)
            {
                WindowManager.Instance.ShowMainWindow();
            }

            // Show completion hint popup on MainWnd.
            StartCoroutine(ShowCompletingTheRacingModePopNextFrame());
        }

        private void FinalizePendingCompletionIfAny()
        {
            if (!_hasPendingCompletion)
                return;

            _hasPendingCompletion = false;

            float finalTime = _pendingCompletionTimeSeconds;
            _pendingCompletionTimeSeconds = 0f;

            var record = new SpeedrunRecord
            {
                timeSeconds = finalTime,
                completedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };

            _records.Add(record);
            _records.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));
            _latestRecordIndex = _records.IndexOf(record);
            _latestRecordSig = BuildRecordSignature(record);
            SaveRecords();

            ClearRunState();

            Debug.Log($"[SpeedrunService] Run finalized! Time: {record.timeSeconds:F1}s, Date: {record.completedAtLocal}");
            OnRunCompleted?.Invoke(record);
        }

        private void ResetSpeedrunCatsProgress()
        {
            // Reset collection data + cat SR_ keys states. Keep speedrun toggle enabled.
            if (CollectionService.Instance != null)
            {
                // CollectionService is already in speedrun slot when speedrun is enabled.
                CollectionService.Instance.ResetCollectionData();
            }

            ResetAllCatStatesForSpeedrun();
            RefreshHiddenObjectManagers();

            _runActive = false;
            _runTimeSeconds = 0f;
            ClearRunState();
        }

        private System.Collections.IEnumerator ShowCompletingTheRacingModePopNextFrame()
        {
            // Wait for window switch to complete.
            yield return null;

            if (WindowManager.Instance == null || WindowManager.Instance.CurrentWindow == null)
                yield break;

            Transform root = WindowManager.Instance.CurrentWindow.transform;
            Transform popTr = FindDescendantByName(root, "CompletingTheRacingModePop");
            if (popTr == null)
                yield break;

            // IMPORTANT: Do NOT override any text here; user configures the popup content in prefab/scene.
            //
            // UX: hide behavior should match MyHintBubbleRoot (SimpleBubbleHint):
            // - show briefly, then auto-hide
            // - when MainWnd is hidden, this will also disappear because it is under the window root
            AudioManager.Instance?.PlaySfx("Paper02");

            if (popTr.TryGetComponent<SimpleBubbleHint>(out var hint) && hint != null)
            {
                hint.ShowBubbleOnce();
            }
            else
            {
                popTr.gameObject.SetActive(true);
                StartCoroutine(HideGameObjectAfterSeconds(popTr.gameObject, 1.5f));
            }
        }

        private System.Collections.IEnumerator HideGameObjectAfterSeconds(GameObject go, float seconds)
        {
            // Match SimpleBubbleHint default duration (1.5s)
            float wait = seconds > 0f ? seconds : 0.01f;
            yield return new WaitForSeconds(wait);

            if (go != null)
            {
                go.SetActive(false);
            }
        }

        private static Transform FindDescendantByName(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == exactName)
                    return all[i];
            }
            return null;
        }

        /// <summary>
        /// Reload visual states of all NormalCat and HiddenCat instances
        /// so they read from the current mode's save keys.
        /// </summary>
        private void ReloadAllCatStates()
        {
            NormalCatInteractable[] normalCats = FindObjectsOfType<NormalCatInteractable>(true);
            foreach (var cat in normalCats)
            {
                if (cat != null)
                    cat.ReloadCollectionState();
            }

            HiddenCatInteractable[] hiddenCats = FindObjectsOfType<HiddenCatInteractable>(true);
            foreach (var cat in hiddenCats)
            {
                if (cat != null)
                    cat.ReloadState();
            }
        }

        /// <summary>
        /// Reset all cat visual states for speedrun (mark as uncollected under SR_ keys).
        /// </summary>
        private void ResetAllCatStatesForSpeedrun()
        {
            NormalCatInteractable[] normalCats = FindObjectsOfType<NormalCatInteractable>(true);
            foreach (var cat in normalCats)
            {
                if (cat != null)
                    cat.ResetCollection();
            }

            HiddenCatInteractable[] hiddenCats = FindObjectsOfType<HiddenCatInteractable>(true);
            foreach (var cat in hiddenCats)
            {
                if (cat != null)
                    cat.ResetCollection();
            }
        }

        /// <summary>
        /// Refresh HiddenObjectManagers so their completion flags reset
        /// (needed when starting a new run so OnLevelComplete can fire again).
        /// </summary>
        private void RefreshHiddenObjectManagers()
        {
            HiddenObjectManager[] managers = FindObjectsOfType<HiddenObjectManager>(true);
            foreach (var mgr in managers)
            {
                if (mgr != null)
                    mgr.RefreshRegistration();
            }
        }

        // =========================================================================
        // Persistence
        // =========================================================================

        private void LoadAll()
        {
            _isSpeedrunEnabled = PlayerPrefs.GetInt(KEY_ENABLED, 0) == 1;
            _runActive = PlayerPrefs.GetInt(KEY_RUN_ACTIVE, 0) == 1;
            _runTimeSeconds = PlayerPrefs.GetFloat(KEY_RUN_TIME, 0f);

            string json = PlayerPrefs.GetString(KEY_RECORDS, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<RecordListWrapper>(json);
                    _records = (wrapper != null && wrapper.records != null)
                        ? new List<SpeedrunRecord>(wrapper.records)
                        : new List<SpeedrunRecord>();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SpeedrunService] Failed to load records: {e.Message}");
                    _records = new List<SpeedrunRecord>();
                }
            }

            // Restore which record was "latest" across app restarts.
            _latestRecordSig = PlayerPrefs.GetString(KEY_LATEST_RECORD_SIG, string.Empty);
            _latestRecordIndex = ResolveLatestRecordIndex(_records, _latestRecordSig);

            // Do NOT call CollectionService.SwitchToSpeedrunMode here: Awake order can run before
            // CollectionService.Awake, leaving Instance null so speedrun mode never applies.
            // See EnsureCollectionServiceModeMatchesSpeedrunToggle() in Start().
        }

        private void SaveRecords()
        {
            try
            {
                var wrapper = new RecordListWrapper { records = _records.ToArray() };
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(KEY_RECORDS, json);
                if (!string.IsNullOrEmpty(_latestRecordSig))
                    PlayerPrefs.SetString(KEY_LATEST_RECORD_SIG, _latestRecordSig);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeedrunService] Failed to save records: {e.Message}");
            }
        }

        private void SaveRunState()
        {
            PlayerPrefs.SetInt(KEY_RUN_ACTIVE, _runActive ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_RUN_TIME, _runTimeSeconds);
            PlayerPrefs.Save();
        }

        private void ClearRunState()
        {
            PlayerPrefs.SetInt(KEY_RUN_ACTIVE, 0);
            PlayerPrefs.SetFloat(KEY_RUN_TIME, 0f);
            PlayerPrefs.Save();
        }

        [Serializable]
        private sealed class RecordListWrapper
        {
            public SpeedrunRecord[] records;
        }

        private static string BuildRecordSignature(SpeedrunRecord r)
        {
            if (r == null) return string.Empty;
            // Use invariant formatting to keep signature stable across locales.
            // completedAtLocal is already a stable string ("yyyy-MM-dd HH:mm").
            string t = r.timeSeconds.ToString("R", CultureInfo.InvariantCulture);
            return $"{r.completedAtLocal}|{t}";
        }

        private static int ResolveLatestRecordIndex(List<SpeedrunRecord> records, string latestSig)
        {
            if (records == null || records.Count == 0)
                return -1;

            // 1) Preferred: match persisted signature exactly.
            if (!string.IsNullOrEmpty(latestSig))
            {
                for (int i = 0; i < records.Count; i++)
                {
                    if (BuildRecordSignature(records[i]) == latestSig)
                        return i;
                }
            }

            // 2) Fallback for older saves: pick the most recent by completedAtLocal (if parseable).
            int bestIndex = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < records.Count; i++)
            {
                string s = records[i]?.completedAtLocal;
                if (string.IsNullOrEmpty(s))
                    continue;

                if (DateTime.TryParseExact(
                        s,
                        "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime dt))
                {
                    if (bestIndex < 0 || dt > bestTime)
                    {
                        bestIndex = i;
                        bestTime = dt;
                    }
                }
            }

            return bestIndex;
        }
    }
}
