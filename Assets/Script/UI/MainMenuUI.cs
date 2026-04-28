using HiddenCats.UI;
using HiddenCats.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach this to a GameObject in the MainWnd prefab (e.g. a root controller object).
/// Hook its public methods to Button OnClick events in the MainWnd UI.
/// </summary>
public sealed class MainMenuUI : MonoBehaviour
{
    [Header("Door Button SFX Suppressor")]
    [Tooltip("Door 按钮上的 ButtonSfxPlayer 组件引用（用于首次点击时静音）。找不到时会自动按名字查找。")]
    [SerializeField] private ButtonSfxPlayer doorButtonSfxPlayer;
    [Header("Debug")]
    [Tooltip("临时调试：勾选后不检查解锁条件，直接打开排行榜。等收集系统完成后再关闭。")]
    [SerializeField] private bool bypassRankUnlockCheck = false;

    [Header("Unlock Hint Popup (Optional)")]
    [Tooltip("If assigned, will show unlock hint when trying to access locked features.")]
    [SerializeField] private MessagePopup unlockHintPopup;

    [Header("Unlock Hint Messages")]
    [TextArea(2, 4)]
    [Tooltip("Fallback when LocalizationManager is missing. Prefer key ui.main.trophy_lock_hint.")]
    [SerializeField] private string rankUnlockHint = "Unlocked after clearing the puzzle game once.";

    private const string KeyTrophyLockHint = "ui.main.trophy_lock_hint";
    private const string KeyExitConfirm = "ui.main.exit_confirm";

    [Header("Exit game confirmation (CloseButtonBg / CloseButtonTips)")]
    [SerializeField] private GameObject exitConfirmMask;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private TextMeshProUGUI exitConfirmMessageText;
    [SerializeField] private Button exitConfirmYesButton;
    [SerializeField] private Button exitConfirmNoButton;

    [Header("Trophy Visual")]
    [Tooltip("Image component for the trophy / leaderboard button on MainWnd.")]
    [SerializeField] private Image trophyButtonImage;

    [Tooltip("Sprite used when the trophy / leaderboard is still locked.")]
    [SerializeField] private Sprite lockedTrophySprite;

    [Tooltip("Sprite used when the trophy / leaderboard has been unlocked (after first clear).")]
    [SerializeField] private Sprite unlockedTrophySprite;

    [Header("Speedrun Toggle Root (Optional)")]
    [Tooltip("Root GameObject for the 'enable speedrun mode' toggle shown after the leaderboard is unlocked.")]
    [SerializeField] private GameObject speedrunToggleRoot;

    [Header("Speedrun / Leaderboard First-Unlock Reward (FeatureSpec)")]
    [Tooltip("Separate prefs: trophy RewardStar clears when clicking the trophy button; speedrun RewardStar clears when clicking the speedrun toggle.")]
    private const string PrefRewardDismissedTrophy = "MainWnd_SpeedrunUnlockRewardDismissed_Trophy";
    private const string PrefRewardDismissedSpeedrun = "MainWnd_SpeedrunUnlockRewardDismissed_Speedrun";
    private const string PrefRewardDismissedLegacy = "MainWnd_SpeedrunUnlockRewardDismissed";

    [Header("Door Tips (门口小贴士)")]
    [Tooltip("BigTipBg: 小贴士弹窗面板（点击后展开的弹窗）")]
    [SerializeField] private GameObject bigTipBg;

    [Tooltip("SmallTipsPaper: 门上的小贴士缩略图（可被点击）")]
    [SerializeField] private GameObject smallTipsPaper;

    [Tooltip("BackgroundMaskPopup 组件（如果 BigTipBg 上已挂载，会自动获取；否则需要手动指定）")]
    [SerializeField] private BackgroundMaskPopup tipPopup;

    [Tooltip("关闭弹窗后是否自动进入房间（根据关卡 Flow 决定）")]
    [SerializeField] private bool enterRoomAfterTipClosed = false;

    private const string DOOR_TIP_SEEN_KEY = "DoorTipSeen";
    private bool _hasSeenDoorTip = false;
    // When BackgroundMaskPopup is missing, we fallback to directly toggling bigTipBg.
    private bool _tipShownUsingLegacyPanel = false;
    private bool _pendingEnterRoomAfterTipClose = false;
    private bool _isEnteringRoom = false; // 防止连续点击 Door

    private void Awake()
    {
        ResolveExitConfirmUi();
        EnsureCloseGameButtonWired();
        WireExitConfirmButtons();
        HideExitConfirmUi();
    }

    private void Start()
    {
        // Ensure background music starts playing when MainWnd is shown
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBackgroundMusic();
        }

        // Load door tip seen status from persistent storage
        LoadDoorTipSeenStatus();

        // Initialize tip popup reference if not assigned
        InitializeTipPopup();

        // On initial show, update trophy visual and speedrun toggle visibility
        UpdateTrophyVisual();
    }

    private void OnEnable()
    {
        Debug.Log("[MainMenuUI] OnEnable() called");
        // When returning to MainWnd (e.g., after finishing the game once),
        // refresh the trophy icon and speedrun toggle visibility.
        _isEnteringRoom = false; // 重置 Door 点击状态，允许再次点击
        Debug.Log($"[MainMenuUI] OnEnable() - reset _isEnteringRoom=false");

        // 确保背景音乐播放（Start 只在首次创建时运行，返回时 Start 不会再跑）
        // 如果 ApplyBgmForWindowPrefab 因 musicSource.clip == null 未生效，这里兜底
        if (AudioManager.Instance != null && !AudioManager.Instance.IsMusicPlaying())
        {
            AudioManager.Instance.PlayBackgroundMusic();
        }

        // 重新启用 Door 按钮
        if (doorButtonSfxPlayer != null)
        {
            var button = doorButtonSfxPlayer.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = true;
                Debug.Log("[MainMenuUI] Door button re-enabled");
            }
        }

        GameProgressResetService.OnGameProgressReset += HandleGameProgressReset;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChangedHandler;
        }

        // Always reload from PlayerPrefs to ensure latest state (handles progress resets correctly).
        LoadDoorTipSeenStatus();

        UpdateTrophyVisual();
    }

    private void OnDisable()
    {
        GameProgressResetService.OnGameProgressReset -= HandleGameProgressReset;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChangedHandler;
        }
    }

    private void OnLanguageChangedHandler(string _)
    {
        if (exitConfirmPanel != null && exitConfirmPanel.activeSelf)
        {
            ApplyExitConfirmLocalizedText();
        }
    }

    private void HandleGameProgressReset()
    {
        // Resetting progress should always re-lock leaderboard/speedrun.
        // Also guard against accidentally leaving debug bypass enabled in Inspector.
        if (bypassRankUnlockCheck)
        {
            Debug.LogWarning("[MainMenuUI] bypassRankUnlockCheck was enabled. Disabling it due to progress reset.");
            bypassRankUnlockCheck = false;
        }

        // Reset door tip seen status when game progress is reset
        _hasSeenDoorTip = false;
        PlayerPrefs.DeleteKey(DOOR_TIP_SEEN_KEY);
        PlayerPrefs.DeleteKey(PrefRewardDismissedTrophy);
        PlayerPrefs.DeleteKey(PrefRewardDismissedSpeedrun);
        PlayerPrefs.DeleteKey(PrefRewardDismissedLegacy);
        PlayerPrefs.Save();

        UpdateTrophyVisual();
    }

    public void OnClick_OpenSettings()
    {
        if (WindowManager.Instance == null)
        {
            Debug.LogError("[MainMenuUI] WindowManager.Instance is null.");
            return;
        }

        AudioManager.PlayCommon02();
        WindowManager.Instance.ShowSettingPopup();
    }

    public void OnClick_OpenDiscord()
    {
        const string discordUrl = "https://discord.gg/sfmNFEF5ec";
        Application.OpenURL(discordUrl);
        Debug.Log($"[MainMenuUI] Opening Discord: {discordUrl}");
    }

    public void OnClick_OpenQQ()
    {
        const string qqUrl = "https://qm.qq.com/cgi-bin/qm/qr?_wv=1027&k=Ke5OfLu0c2EBkNiyKug4DBbHYMlTTkWW&authKey=CXj1XfLtp7Xv4hRHsSAyuXMEHCGPz45KKD4vM%2B7nyRyudAOG45KVzBN%2BS4SJjOZw&noverify=0&group_code=1079431440";
        Application.OpenURL(qqUrl);
        Debug.Log($"[MainMenuUI] Opening QQ group: {qqUrl}");
    }

    public void OnClick_OpenRank()
    {
        if (WindowManager.Instance == null)
        {
            Debug.LogError("[MainMenuUI] WindowManager.Instance is null.");
            return;
        }

        // Demo version: always show locked hint, never open rank
        bool isDemoMode = true;
        if (isDemoMode)
        {
            string hint = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(KeyTrophyLockHint)
                : rankUnlockHint;
            ShowUnlockHint(hint);
            return;
        }

        // Check unlock condition (can be bypassed during development).
        // Spec: Before the first clear, clicking the trophy only shows a lightweight toast hint.
        bool isUnlocked = bypassRankUnlockCheck || UnlockChecker.IsSpeedrunUnlocked();
        if (!isUnlocked)
        {
            string hint = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(KeyTrophyLockHint)
                : rankUnlockHint;
            ShowUnlockHint(hint);
            return;
        }

        // FeatureSpec: first-unlock trophy RewardStar clears when the player clicks the trophy (unlocked path only).
        DismissTrophyUnlockRewardParticlesOnly();

        AudioManager.PlayCommon02();
        WindowManager.Instance.ShowRankPopup();
    }

    /// <summary>
    /// Called when clicking the door button.
    /// - First time: Shows tip popup, then enters room after closing (if configured).
    /// - After first time: Directly enters room.
    /// </summary>
    public void OnClick_EnterRoom()
    {
        Debug.Log($"[MainMenuUI] OnClick_EnterRoom() called. _isEnteringRoom={_isEnteringRoom}");

        // 防止连续点击 Door
        if (_isEnteringRoom)
        {
            Debug.Log("[MainMenuUI] OnClick_EnterRoom() BLOCKED - already entering room");
            return;
        }
        _isEnteringRoom = true;
        Debug.Log("[MainMenuUI] OnClick_EnterRoom() ACCEPTED - set _isEnteringRoom=true");

        // 禁用 Door 按钮，防止 ButtonSfxPlayer 播放重复音效
        if (doorButtonSfxPlayer != null)
        {
            var button = doorButtonSfxPlayer.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
                Debug.Log("[MainMenuUI] Door button disabled to prevent double-click sound");
            }
        }

        if (WindowManager.Instance == null)
        {
            Debug.LogError("[MainMenuUI] WindowManager.Instance is null.");
            return;
        }

        if (tipPopup == null)
        {
            InitializeTipPopup();
        }

        // First time clicking door: suppress independent DoorOpen SFX, then show tip popup.
        if (!_hasSeenDoorTip)
        {
            if (doorButtonSfxPlayer != null)
            {
                doorButtonSfxPlayer.SuppressNextSfx();
            }
            ShowDoorTipPopup(markAsSeen: true);
            _pendingEnterRoomAfterTipClose = enterRoomAfterTipClosed;
        }
        else
        {
            // Already seen tip: enter room and play DoorOpen sound
            AudioManager.Instance?.PlaySfx("DoorOpen");
            WindowManager.Instance.ShowRoomWindow();
        }
    }

    /// <summary>
    /// Called when clicking the small tips paper thumbnail.
    /// Always shows the tip popup regardless of whether it has been seen before.
    /// </summary>
    public void OnClick_SmallTipsPaper()
    {
        if (tipPopup == null)
        {
            InitializeTipPopup();
        }

        // Suppress any independent DoorOpen SFX from ButtonSfxPlayer.
        if (doorButtonSfxPlayer != null)
        {
            doorButtonSfxPlayer.SuppressNextSfx();
        }

        ShowDoorTipPopup(markAsSeen: false);
        // Don't auto-enter room when clicking the thumbnail (it's just for viewing help)
        _pendingEnterRoomAfterTipClose = false;
    }

    private void ShowUnlockHint(string message)
    {
        // IMPORTANT:
        // The trophy Image may have a legacy SimpleBubbleHint attached which implements IPointerClickHandler.
        // That means a single click can trigger BOTH:
        // - Button.onClick -> this method
        // - EventSystem pointer click -> SimpleBubbleHint.OnPointerClick()
        // If we also show a service toast here, the user will see two hints at once.
        //
        // Spec/UX: when locked, only show the MyHintBubbleRoot style bubble (SimpleBubbleHint) on trophy click.
        // So we prefer SimpleBubbleHint first and only fallback to HintBubbleService when SimpleBubbleHint is absent.

        // Prefer: if trophy has a SimpleBubbleHint (legacy setup), use it.
        if (trophyButtonImage != null && trophyButtonImage.TryGetComponent<SimpleBubbleHint>(out var simpleHint) && simpleHint != null)
        {
            // Only show when the component is enabled (we disable it when unlocked).
            if (simpleHint.enabled)
            {
                AudioManager.PlayLocked();
                simpleHint.ShowTextOnce(message);
                return;
            }
        }

        // Fallback: lightweight toast-style hint via HintBubbleService if available.
        if (HintBubbleService.Instance != null)
        {
            AudioManager.PlayLocked();
            HintBubbleService.HintBubbleRequest request = new HintBubbleService.HintBubbleRequest
            {
                anchorWorldOrUI = trophyButtonImage != null ? trophyButtonImage.transform : null,
                type = HintBubbleService.HintBubbleType.TextOnly,
                icon = null,
                text = message,
                duration = 2.0f
            };

            HintBubbleService.Show(request);
            return;
        }

        // Fallback to a generic popup if configured.
        if (unlockHintPopup != null)
        {
            AudioManager.PlayLocked();
            unlockHintPopup.Show(message);
            return;
        }

        // Last resort: log to console.
        Debug.LogWarning($"[MainMenuUI] Unlock hint: {message}");
    }

    public void UpdateTrophyVisual()
    {
        // Demo version: trophy and speedrun toggle are always locked/hidden regardless of actual completion state
        bool isUnlocked = bypassRankUnlockCheck || UnlockChecker.IsSpeedrunUnlocked();
        bool isDemoMode = true; // Demo version: always show locked state

        if (trophyButtonImage != null)
        {
            if (isUnlocked && !isDemoMode && unlockedTrophySprite != null)
            {
                trophyButtonImage.sprite = unlockedTrophySprite;
            }
            else if (lockedTrophySprite != null)
            {
                // Demo mode: always show locked sprite
                trophyButtonImage.sprite = lockedTrophySprite;
            }

            // Important: trophy button may still have a legacy SimpleBubbleHint component attached.
            // When unlocked, clicking should ONLY open RankPop and should NOT show the hint bubble.
            // Demo mode: always keep the hint enabled (locked state)
            if (trophyButtonImage.TryGetComponent<SimpleBubbleHint>(out var simpleHint) && simpleHint != null)
            {
                simpleHint.enabled = !isUnlocked || isDemoMode;
            }
        }

        // Demo version: speedrun toggle is always hidden
        if (speedrunToggleRoot != null)
        {
            speedrunToggleRoot.SetActive(false);
        }

        // Reward particles are always hidden in demo mode
        MigrateLegacyRewardDismissPrefs();
        ApplySpeedrunUnlockRewardState(isUnlocked && !isDemoMode);
    }

    /// <summary>
    /// FeatureSpec: 首次解锁时播放 trophy 与 speedrun 下的 RewardStar；切界面回来仍播放；
    /// 点击奖杯按钮只关奖杯旁粒子，点击竞速开关只关竞速旁粒子；重置进度后清除。
    /// </summary>
    private void ApplySpeedrunUnlockRewardState(bool isUnlocked)
    {
        bool showTrophy = isUnlocked && !IsTrophyUnlockRewardDismissed();
        bool showSpeedrun = isUnlocked && !IsSpeedrunUnlockRewardDismissed();

        GameObject trophyFx = trophyButtonImage != null
            ? FindDescendantByName(trophyButtonImage.transform, "RewardStar")
            : null;
        GameObject speedrunFx = speedrunToggleRoot != null
            ? FindDescendantByName(speedrunToggleRoot.transform, "RewardStar")
            : null;

        SetRewardStarPlaying(trophyFx, showTrophy);
        SetRewardStarPlaying(speedrunFx, showSpeedrun);
    }

    /// <summary>
    /// One-time migration from older single PlayerPrefs key (both dismissed together).
    /// </summary>
    private static void MigrateLegacyRewardDismissPrefs()
    {
        if (PlayerPrefs.GetInt(PrefRewardDismissedLegacy, 0) != 1)
        {
            return;
        }

        PlayerPrefs.SetInt(PrefRewardDismissedTrophy, 1);
        PlayerPrefs.SetInt(PrefRewardDismissedSpeedrun, 1);
        PlayerPrefs.DeleteKey(PrefRewardDismissedLegacy);
        PlayerPrefs.Save();
    }

    private static bool IsTrophyUnlockRewardDismissed()
    {
        return PlayerPrefs.GetInt(PrefRewardDismissedTrophy, 0) == 1;
    }

    private static bool IsSpeedrunUnlockRewardDismissed()
    {
        return PlayerPrefs.GetInt(PrefRewardDismissedSpeedrun, 0) == 1;
    }

    private static void SetTrophyUnlockRewardDismissed(bool dismissed)
    {
        PlayerPrefs.SetInt(PrefRewardDismissedTrophy, dismissed ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void SetSpeedrunUnlockRewardDismissed(bool dismissed)
    {
        PlayerPrefs.SetInt(PrefRewardDismissedSpeedrun, dismissed ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Called from trophy Button — clears only the trophy RewardStar (FeatureSpec).
    /// </summary>
    private void DismissTrophyUnlockRewardParticlesOnly()
    {
        if (IsTrophyUnlockRewardDismissed())
        {
            return;
        }

        SetTrophyUnlockRewardDismissed(true);
        GameObject trophyFx = trophyButtonImage != null
            ? FindDescendantByName(trophyButtonImage.transform, "RewardStar")
            : null;
        SetRewardStarPlaying(trophyFx, false);
    }

    /// <summary>
    /// Called from <see cref="SpeedrunToggleView.OnClick_Toggle"/> — clears only the speedrun RewardStar (FeatureSpec).
    /// </summary>
    public void DismissSpeedrunUnlockRewardParticlesOnly()
    {
        if (IsSpeedrunUnlockRewardDismissed())
        {
            return;
        }

        SetSpeedrunUnlockRewardDismissed(true);
        GameObject speedrunFx = speedrunToggleRoot != null
            ? FindDescendantByName(speedrunToggleRoot.transform, "RewardStar")
            : null;
        SetRewardStarPlaying(speedrunFx, false);
    }

    private static void SetRewardStarPlaying(GameObject rewardStarRoot, bool playing)
    {
        if (rewardStarRoot == null)
        {
            return;
        }

        rewardStarRoot.SetActive(playing);
        if (!playing)
        {
            return;
        }

        ParticleSystem[] systems = rewardStarRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                systems[i].Play(true);
            }
        }
    }

    /// <summary>
    /// Find a descendant GameObject by exact name (includes inactive objects).
    /// </summary>
    private static GameObject FindDescendantByName(Transform root, string exactName)
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
                return all[i].gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// Initialize tip popup reference if not assigned.
    /// Tries to get BackgroundMaskPopup component from bigTipBg if available.
    /// </summary>
    private void InitializeTipPopup()
    {
        // If the serialized reference wasn't wired in the prefab, try to resolve it by name at runtime.
        // This makes the feature robust against Inspector misconfiguration.
        if (bigTipBg == null)
        {
            // First search under this controller (common setup).
            bigTipBg = FindDescendantByName(transform, "BigTipBg");

            // If still not found, search under root (WindowManager typically instantiates a window root).
            if (bigTipBg == null && transform.root != null)
            {
                bigTipBg = FindDescendantByName(transform.root, "BigTipBg");
            }
        }

        if (tipPopup == null && bigTipBg != null)
        {
            tipPopup = bigTipBg.GetComponent<BackgroundMaskPopup>();
            if (tipPopup == null)
            {
                // Try to find it in children
                tipPopup = bigTipBg.GetComponentInChildren<BackgroundMaskPopup>();
            }
        }

        // If we still don't have it, try to find any BackgroundMaskPopup under this window.
        // As a last resort, assume the popup component sits on the BigTipBg node itself.
        if (tipPopup == null)
        {
            tipPopup = GetComponentInChildren<BackgroundMaskPopup>(true);
            if (tipPopup == null && transform.root != null)
            {
                tipPopup = transform.root.GetComponentInChildren<BackgroundMaskPopup>(true);
            }

            if (tipPopup != null && bigTipBg == null)
            {
                bigTipBg = tipPopup.gameObject;
            }
        }

        // Auto-resolve door button's ButtonSfxPlayer if not assigned.
        if (doorButtonSfxPlayer == null)
        {
            GameObject doorGo = FindDescendantByName(transform, "Door");
            if (doorGo != null)
            {
                doorButtonSfxPlayer = doorGo.GetComponent<ButtonSfxPlayer>();
            }
        }
    }

    /// <summary>
    /// Show the door tip popup and mark it as seen.
    /// </summary>
    private void ShowDoorTipPopup(bool markAsSeen)
    {
        if (tipPopup == null)
        {
            InitializeTipPopup();
        }

        _tipShownUsingLegacyPanel = false;

        // Show via BackgroundMaskPopup if available, otherwise fallback to directly enabling the panel.
        if (tipPopup != null)
        {
            tipPopup.Show();
        }
        else if (bigTipBg != null)
        {
            _tipShownUsingLegacyPanel = true;
            bigTipBg.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[MainMenuUI] ShowDoorTipPopup(): tipPopup and bigTipBg are both null. Cannot show tip.");
            return;
        }

        AudioManager.Instance?.PlaySfx("Paper01");

        // Only mark as seen and persist when it's actually from the door entrance path.
        if (markAsSeen)
        {
            _hasSeenDoorTip = true;
            SaveDoorTipSeenStatus();
        }

        StartCoroutine(CheckTipPopupClosed(markAsSeen));
    }

    /// <summary>
    /// Coroutine to check when tip popup is closed and handle auto-enter room if needed.
    /// </summary>
    /// <param name="playDoorOpenAndEnter">如果是 Door 首次点击后弹窗关闭，传入 true，会先播 DoorOpen 再进房间。</param>
    private System.Collections.IEnumerator CheckTipPopupClosed(bool playDoorOpenAndEnter)
    {
        if (!playDoorOpenAndEnter)
        {
            yield break;
        }

        // Wait until popup is closed.
        // - Preferred path: BackgroundMaskPopup.IsVisible
        // - Fallback path: bigTipBg active state
        if (!_tipShownUsingLegacyPanel && tipPopup != null)
        {
            while (tipPopup.IsVisible)
            {
                yield return null;
            }
        }
        else if (_tipShownUsingLegacyPanel && bigTipBg != null)
        {
            while (bigTipBg.activeInHierarchy)
            {
                yield return null;
            }
        }

        // Popup is closed, enter room with DoorOpen sound
        if (WindowManager.Instance != null)
        {
            AudioManager.Instance?.PlaySfx("DoorOpen");
            WindowManager.Instance.ShowRoomWindow();
        }
    }

    /// <summary>
    /// Load door tip seen status from PlayerPrefs.
    /// </summary>
    private void LoadDoorTipSeenStatus()
    {
        // Check if reset service should clear this flag
        if (GameProgressResetService.ShouldApplyResetForKey(DOOR_TIP_SEEN_KEY))
        {
            _hasSeenDoorTip = false;
            PlayerPrefs.DeleteKey(DOOR_TIP_SEEN_KEY);
            GameProgressResetService.MarkResetAppliedForKey(DOOR_TIP_SEEN_KEY);
            return;
        }

        _hasSeenDoorTip = PlayerPrefs.GetInt(DOOR_TIP_SEEN_KEY, 0) == 1;
    }

    /// <summary>
    /// Save door tip seen status to PlayerPrefs.
    /// </summary>
    private void SaveDoorTipSeenStatus()
    {
        PlayerPrefs.SetInt(DOOR_TIP_SEEN_KEY, _hasSeenDoorTip ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ResolveExitConfirmUi()
    {
        if (exitConfirmMask == null)
        {
            exitConfirmMask = FindDescendantByName(transform, "CloseButtonBg");
        }

        if (exitConfirmPanel == null)
        {
            exitConfirmPanel = FindDescendantByName(transform, "CloseButtonTips");
        }

        if (exitConfirmMessageText == null && exitConfirmPanel != null)
        {
            Transform bg = exitConfirmPanel.transform.Find("Bg");
            if (bg != null)
            {
                Transform t = bg.Find("Text (TMP)");
                if (t != null)
                {
                    exitConfirmMessageText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (exitConfirmMessageText == null)
            {
                exitConfirmMessageText = exitConfirmPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (exitConfirmYesButton == null && exitConfirmPanel != null)
        {
            Transform yesTr = exitConfirmPanel.transform.Find("Yes");
            if (yesTr != null)
            {
                exitConfirmYesButton = yesTr.GetComponent<Button>();
            }
        }

        if (exitConfirmNoButton == null && exitConfirmPanel != null)
        {
            Transform noTr = exitConfirmPanel.transform.Find("No");
            if (noTr != null)
            {
                exitConfirmNoButton = noTr.GetComponent<Button>();
            }
        }
    }

    private void EnsureCloseGameButtonWired()
    {
        GameObject closeGo = FindDescendantByName(transform, "CloseBtn");
        if (closeGo == null)
        {
            return;
        }

        Button btn = closeGo.GetComponent<Button>();
        if (btn == null)
        {
            btn = closeGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Image img = closeGo.GetComponent<Image>();
            if (img != null)
            {
                btn.targetGraphic = img;
            }
        }

        btn.onClick.RemoveListener(OnClick_RequestExitPrompt);
        btn.onClick.AddListener(OnClick_RequestExitPrompt);
    }

    private void WireExitConfirmButtons()
    {
        if (exitConfirmYesButton != null)
        {
            exitConfirmYesButton.onClick.RemoveListener(OnClick_ExitConfirmYes);
            exitConfirmYesButton.onClick.AddListener(OnClick_ExitConfirmYes);
        }

        if (exitConfirmNoButton != null)
        {
            exitConfirmNoButton.onClick.RemoveListener(OnClick_ExitConfirmNo);
            exitConfirmNoButton.onClick.AddListener(OnClick_ExitConfirmNo);
        }

        if (exitConfirmMask != null)
        {
            Button maskBtn = exitConfirmMask.GetComponent<Button>();
            if (maskBtn == null)
            {
                maskBtn = exitConfirmMask.AddComponent<Button>();
                maskBtn.transition = Selectable.Transition.None;
            }

            Image maskImg = exitConfirmMask.GetComponent<Image>();
            if (maskImg != null)
            {
                UiInvisibleRaycastSprite.ApplyTo(maskImg);
                maskBtn.targetGraphic = maskImg;
            }

            maskBtn.onClick.RemoveListener(OnClick_ExitConfirmNo);
            maskBtn.onClick.AddListener(OnClick_ExitConfirmNo);
        }
    }

    /// <summary>
    /// Close (X) on main menu: show localized exit confirmation.
    /// </summary>
    public void OnClick_RequestExitPrompt()
    {
        ResolveExitConfirmUi();
        if (exitConfirmMask == null || exitConfirmPanel == null)
        {
            AudioManager.PlayCommon02();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        AudioManager.PlayCommon02();
        exitConfirmMask.SetActive(true);
        exitConfirmMask.transform.SetAsLastSibling();
        exitConfirmPanel.SetActive(true);
        exitConfirmPanel.transform.SetAsLastSibling();
        ApplyExitConfirmLocalizedText();
    }

    private void ApplyExitConfirmLocalizedText()
    {
        if (exitConfirmMessageText == null)
        {
            return;
        }

        exitConfirmMessageText.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(KeyExitConfirm)
            : "Quit the game?";
    }

    private void OnClick_ExitConfirmYes()
    {
        AudioManager.PlayCommon02();
        HideExitConfirmUi();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnClick_ExitConfirmNo()
    {
        AudioManager.PlayCommon02();
        HideExitConfirmUi();
    }

    private void HideExitConfirmUi()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(false);
        }

        if (exitConfirmMask != null)
        {
            exitConfirmMask.SetActive(false);
        }
    }
}

