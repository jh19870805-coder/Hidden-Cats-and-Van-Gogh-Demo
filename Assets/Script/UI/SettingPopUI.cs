using HiddenCats.UI;
using HiddenCats.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Attach this to a GameObject in the SettingPop prefab.
/// Handles settings UI controls and closing the settings popup.
/// </summary>
public sealed class SettingPopUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Screen & Display")]
    [SerializeField] private Toggle screenToggle;
    [SerializeField] private GameObject screenOnRoot;
    [SerializeField] private GameObject screenOffRoot;

    [Header("Cursor Size")]
    [SerializeField] private Toggle mouseToggle;
    [SerializeField] private GameObject mouseOnRoot;
    [SerializeField] private GameObject mouseOffRoot;

    [Header("Language Dropdown")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    [Tooltip("Optional: Font asset that supports Chinese characters. If not set, will try to load from Resources.")]
    [SerializeField] private TMPro.TMP_FontAsset chineseFontAsset;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    [Header("Confirmation Popup (Optional)")]
    [Tooltip("Legacy. Prefer ResetButtonBg / ResetButtonTips wiring below.")]
    [SerializeField] private ConfirmationPopup confirmationPopup;

    [Header("Reset confirmation (ResetButtonBg / ResetButtonTips)")]
    [SerializeField] private GameObject resetConfirmMask;
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private TextMeshProUGUI resetConfirmMessageText;
    [SerializeField] private Button resetConfirmYesButton;
    [SerializeField] private Button resetConfirmNoButton;

    [Header("Reset Confirmation Message (fallback)")]
    [TextArea(2, 4)]
    [SerializeField] private string resetConfirmationMessage = "确定要重置游戏数据吗？";

    private const string KeyResetConfirm = "settings.reset_confirm";

    private SettingsData _pendingSettings;
    private bool _isInitializing = false;

    private void Awake()
    {
        // Ensure SettingsManager exists before using it.
        if (SettingsManager.Instance == null)
        {
            var existing = FindObjectOfType<SettingsManager>();
            if (existing == null)
            {
                var go = new GameObject("SettingsManager");
                existing = go.AddComponent<SettingsManager>();
            }
        }

        _pendingSettings = SettingsManager.Instance.GetSettings();
        InitializeUI();
        SetupEventListeners();
        ResolveResetConfirmUi();
        WireResetConfirmButtons();
        HideResetConfirmUi();
    }

    private void InitializeUI()
    {
        _isInitializing = true;

        // Initialize sliders
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = _pendingSettings.musicVolume;
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = _pendingSettings.sfxVolume;
        }

        // Initialize language dropdown
        if (languageDropdown != null)
        {
            InitializeLanguageDropdown();
        }

        // Initialize screen toggle
        if (screenToggle != null)
        {
            screenToggle.isOn = _pendingSettings.isFullscreen;
            UpdateScreenToggleVisuals(screenToggle.isOn);
        }

        // Initialize mouse toggle
        if (mouseToggle != null)
        {
            // First update visuals based on settings, then set toggle state
            // This ensures visual state is correct before toggle is set
            bool isLarge = _pendingSettings.isCursorLarge;
            UpdateMouseToggleVisuals(isLarge);
            mouseToggle.isOn = isLarge;
            // Force update visuals again after setting toggle to ensure consistency
            UpdateMouseToggleVisuals(isLarge);
        }

        _isInitializing = false;
    }

    private void SetupEventListeners()
    {
        // Volume sliders
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        // Screen toggle
        if (screenToggle != null)
        {
            screenToggle.onValueChanged.AddListener(OnScreenToggleChanged);
        }

        // Mouse toggle
        if (mouseToggle != null)
        {
            mouseToggle.onValueChanged.AddListener(OnMouseToggleChanged);
        }

        // Language dropdown
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        // Buttons
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnClick_Reset);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnClick_Close);
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChangedHandler;
        }
    }

    private void OnLanguageChangedHandler(string _)
    {
        if (resetConfirmPanel != null && resetConfirmPanel.activeSelf)
        {
            ApplyResetConfirmLocalizedText();
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioManager.PlayCommon02();
        if (_isInitializing) return;
        _pendingSettings.musicVolume = value;
        ApplySettingsImmediately();
    }

    private void OnSfxVolumeChanged(float value)
    {
        AudioManager.PlayCommon02();
        if (_isInitializing) return;
        _pendingSettings.sfxVolume = value;
        ApplySettingsImmediately();
    }

    private void OnScreenToggleChanged(bool isOn)
    {
        AudioManager.PlayCommon02();
        if (_isInitializing) return;
        _pendingSettings.isFullscreen = isOn;
        UpdateScreenToggleVisuals(isOn);
        ApplySettingsImmediately();
    }

    private void UpdateScreenToggleVisuals(bool isOn)
    {
        if (screenOnRoot != null)
        {
            screenOnRoot.SetActive(isOn);
        }

        if (screenOffRoot != null)
        {
            screenOffRoot.SetActive(!isOn);
        }
    }

    private void OnMouseToggleChanged(bool isOn)
    {
        AudioManager.PlayCommon02();
        if (_isInitializing) return;
        _pendingSettings.isCursorLarge = isOn;
        UpdateMouseToggleVisuals(isOn);
        ApplySettingsImmediately();
    }

    private void UpdateMouseToggleVisuals(bool isOn)
    {
        // isOn = true means MouseX2 (large cursor), isOn = false means MouseX1 (normal cursor)
        if (mouseOnRoot != null)
        {
            mouseOnRoot.SetActive(isOn);
        }

        if (mouseOffRoot != null)
        {
            mouseOffRoot.SetActive(!isOn);
        }
        
        Debug.Log($"[SettingPopUI] UpdateMouseToggleVisuals: isOn={isOn}, mouseOnRoot.active={mouseOnRoot?.activeSelf}, mouseOffRoot.active={mouseOffRoot?.activeSelf}");
    }

    private void OnLanguageChanged(int index)
    {
        AudioManager.PlayCommon02();
        if (_isInitializing) return;
        
        // Get language code from LocalizationManager
        if (LocalizationManager.Instance != null)
        {
            string languageCode = LocalizationManager.Instance.GetLanguageCodeByIndex(index);
            _pendingSettings.language = languageCode;
            ApplySettingsImmediately();
            Debug.Log($"[SettingPopUI] Language changed to: {languageCode} (index: {index})");
        }
        else
        {
            Debug.LogWarning("[SettingPopUI] LocalizationManager.Instance is null. Cannot change language.");
        }
    }

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null) return;

        // Try to set font that supports Chinese characters
        SetupChineseFont();

        // Clear existing options
        languageDropdown.ClearOptions();

        // Get available languages from LocalizationManager
        if (LocalizationManager.Instance != null)
        {
            var languages = LocalizationManager.Instance.GetAvailableLanguages();
            var options = new List<TMP_Dropdown.OptionData>();

            for (int i = 0; i < languages.Count; i++)
            {
                var lang = languages[i];
                // Always show each language in its native name (LanguageConfig.displayName).
                string displayName = string.IsNullOrEmpty(lang.displayName)
                    ? lang.displayNameEnglish
                    : lang.displayName;
                options.Add(new TMP_Dropdown.OptionData(displayName));
            }

            languageDropdown.AddOptions(options);

            // Set current language index
            int currentIndex = LocalizationManager.Instance.GetCurrentLanguageIndex();
            if (currentIndex >= 0 && currentIndex < languages.Count)
            {
                languageDropdown.value = currentIndex;
            }
            else
            {
                languageDropdown.value = 0;
            }
            
            Debug.Log($"[SettingPopUI] Language dropdown initialized with {languages.Count} languages. Current: {LocalizationManager.Instance.GetCurrentLanguage()} (index: {currentIndex})");
        }
        else
        {
            Debug.LogWarning("[SettingPopUI] LocalizationManager.Instance is null. Cannot initialize language dropdown.");
            // Fallback: add default options
            languageDropdown.AddOptions(new List<string> { "简体中文", "English" });
            languageDropdown.value = 0;
        }
    }

    /// <summary>
    /// Setup font that supports Chinese characters for the language dropdown.
    /// This prevents Chinese characters from displaying as black boxes.
    /// </summary>
    private void SetupChineseFont()
    {
        if (languageDropdown == null) return;

        TMPro.TMP_FontAsset fontToUse = null;

        // First, try to use the font assigned in Inspector
        if (chineseFontAsset != null)
        {
            fontToUse = chineseFontAsset;
            Debug.Log("[SettingPopUI] Using Chinese font asset assigned in Inspector.");
        }
        else
        {
            // Try to load from Resources
            fontToUse = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Chinese Font SDF");
            if (fontToUse != null)
            {
                Debug.Log("[SettingPopUI] Loaded Chinese font from Resources.");
            }
        }

        // Apply font if found
        if (fontToUse != null)
        {
            // Verify font contains Chinese characters
            bool hasChineseChars = VerifyFontHasChineseCharacters(fontToUse);
            if (!hasChineseChars)
            {
                Debug.LogWarning($"[SettingPopUI] Font '{fontToUse.name}' does not contain Chinese characters. " +
                               "The font asset needs to be regenerated with Chinese character set. " +
                               "Please use Font Asset Creator with 'Custom Characters' containing: 简体中文繁體中文English");
            }

            // Set font for the caption (selected item text)
            if (languageDropdown.captionText != null)
            {
                languageDropdown.captionText.font = fontToUse;
                // Force update to apply font immediately
                languageDropdown.captionText.ForceMeshUpdate();
            }

            // Set font for the item text (dropdown options)
            if (languageDropdown.itemText != null)
            {
                languageDropdown.itemText.font = fontToUse;
                // Force update to apply font immediately
                languageDropdown.itemText.ForceMeshUpdate();
            }

            // Also set font on the dropdown template if it exists
            if (languageDropdown.template != null)
            {
                var templateTextComponents = languageDropdown.template.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var textComp in templateTextComponents)
                {
                    if (textComp != languageDropdown.captionText && textComp != languageDropdown.itemText)
                    {
                        textComp.font = fontToUse;
                        textComp.ForceMeshUpdate();
                    }
                }
            }

            // Try to add font to TMP Settings fallback fonts (if not already there)
            AddFontToTMPFallbacks(fontToUse);

            Debug.Log($"[SettingPopUI] Chinese font applied to language dropdown: {fontToUse.name} " +
                    $"(Has Chinese chars: {hasChineseChars})");
        }
        else
        {
            Debug.LogWarning("[SettingPopUI] No Chinese font asset found. Chinese characters may display as black boxes. " +
                           "Please create a TMP Font Asset that supports Chinese characters and assign it in the Inspector, " +
                           "or place it at 'Resources/Fonts & Materials/Chinese Font SDF'.");
        }
    }

    /// <summary>
    /// Verify if the font asset contains Chinese characters.
    /// </summary>
    private bool VerifyFontHasChineseCharacters(TMPro.TMP_FontAsset font)
    {
        if (font == null) return false;

        // Test characters: 简(0x7B80), 体(0x4F53), 中(0x4E2D), 文(0x6587), 繁(0x7E41), 體(0x9AD4)
        char[] testChars = { '\u7B80', '\u4F53', '\u4E2D', '\u6587', '\u7E41', '\u9AD4' };
        
        foreach (char testChar in testChars)
        {
            if (!font.HasCharacter(testChar))
            {
                Debug.LogWarning($"[SettingPopUI] Font '{font.name}' missing character U+{(uint)testChar:X4}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Add font to TMP Settings fallback fonts if not already present.
    /// </summary>
    private void AddFontToTMPFallbacks(TMPro.TMP_FontAsset font)
    {
        if (font == null) return;

        try
        {
            var fallbacks = TMPro.TMP_Settings.fallbackFontAssets;
            if (fallbacks != null && !fallbacks.Contains(font))
            {
                // Note: We can't modify TMP Settings at runtime, but we can log a suggestion
                Debug.Log($"[SettingPopUI] To ensure Chinese characters work everywhere, add '{font.name}' to TMP Settings > Fallback Font Assets in the Unity Editor.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SettingPopUI] Could not check TMP Settings fallback fonts: {ex.Message}");
        }
    }

    private void ApplySettingsImmediately()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ApplySettings(_pendingSettings);
        }
    }

    public void OnClick_Reset()
    {
        ResolveResetConfirmUi();
        if (resetConfirmMask != null && resetConfirmPanel != null)
        {
            AudioManager.PlayCommon02();
            resetConfirmMask.SetActive(true);
            resetConfirmMask.transform.SetAsLastSibling();
            resetConfirmPanel.SetActive(true);
            resetConfirmPanel.transform.SetAsLastSibling();
            ApplyResetConfirmLocalizedText();
            return;
        }

        if (confirmationPopup != null)
        {
            AudioManager.PlayCommon02();
            string msg = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText(KeyResetConfirm)
                : resetConfirmationMessage;
            confirmationPopup.Show(
                msg,
                onConfirm: ResetGameProgress,
                onCancel: () => Debug.Log("[SettingPopUI] Reset cancelled by user."));
        }
        else
        {
            Debug.LogWarning("[SettingPopUI] No reset confirmation UI. Resetting progress directly.");
            ResetGameProgress();
        }
    }

    private void ResolveResetConfirmUi()
    {
        if (resetConfirmMask == null)
        {
            resetConfirmMask = FindChildByName(transform, "ResetButtonBg");
        }

        if (resetConfirmPanel == null)
        {
            resetConfirmPanel = FindChildByName(transform, "ResetButtonTips");
        }

        if (resetConfirmMessageText == null && resetConfirmPanel != null)
        {
            Transform bg = resetConfirmPanel.transform.Find("Bg");
            if (bg != null)
            {
                Transform t = bg.Find("Text (TMP)");
                if (t != null)
                {
                    resetConfirmMessageText = t.GetComponent<TextMeshProUGUI>();
                }
            }

            if (resetConfirmMessageText == null)
            {
                resetConfirmMessageText = resetConfirmPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (resetConfirmYesButton == null && resetConfirmPanel != null)
        {
            Transform yesTr = resetConfirmPanel.transform.Find("Yes");
            if (yesTr != null)
            {
                resetConfirmYesButton = yesTr.GetComponent<Button>();
            }
        }

        if (resetConfirmNoButton == null && resetConfirmPanel != null)
        {
            Transform noTr = resetConfirmPanel.transform.Find("No");
            if (noTr != null)
            {
                resetConfirmNoButton = noTr.GetComponent<Button>();
            }
        }
    }

    private static GameObject FindChildByName(Transform root, string exactName)
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

    private void WireResetConfirmButtons()
    {
        if (resetConfirmYesButton != null)
        {
            resetConfirmYesButton.onClick.RemoveListener(OnResetConfirmYes);
            resetConfirmYesButton.onClick.AddListener(OnResetConfirmYes);
        }

        if (resetConfirmNoButton != null)
        {
            resetConfirmNoButton.onClick.RemoveListener(OnResetConfirmNo);
            resetConfirmNoButton.onClick.AddListener(OnResetConfirmNo);
        }

        if (resetConfirmMask != null)
        {
            Button maskBtn = resetConfirmMask.GetComponent<Button>();
            if (maskBtn == null)
            {
                maskBtn = resetConfirmMask.AddComponent<Button>();
                maskBtn.transition = Selectable.Transition.None;
            }

            Image maskImg = resetConfirmMask.GetComponent<Image>();
            if (maskImg != null)
            {
                UiInvisibleRaycastSprite.ApplyTo(maskImg);
                maskBtn.targetGraphic = maskImg;
            }

            maskBtn.onClick.RemoveListener(OnResetConfirmNo);
            maskBtn.onClick.AddListener(OnResetConfirmNo);
        }
    }

    private void ApplyResetConfirmLocalizedText()
    {
        if (resetConfirmMessageText == null)
        {
            return;
        }

        string text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetText(KeyResetConfirm)
            : resetConfirmationMessage;

        resetConfirmMessageText.text = text;

        // Ensure the text displays correctly if it contains Chinese characters
        ApplyChineseFontToText(resetConfirmMessageText);
    }

    /// <summary>
    /// Apply Chinese font to a TextMeshProUGUI component if the text contains Chinese characters.
    /// </summary>
    private void ApplyChineseFontToText(TMPro.TextMeshProUGUI textComponent)
    {
        if (textComponent == null) return;

        // Check if text contains Chinese characters
        bool hasChinese = false;
        foreach (char c in textComponent.text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF) // CJK Unified Ideographs range
            {
                hasChinese = true;
                break;
            }
        }

        if (!hasChinese) return;

        TMPro.TMP_FontAsset fontToUse = null;

        // First, try to use the font assigned in Inspector
        if (chineseFontAsset != null)
        {
            fontToUse = chineseFontAsset;
        }
        else
        {
            // Try to load from Resources
            fontToUse = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/NotoSans SC SDF");
        }

        if (fontToUse != null)
        {
            textComponent.font = fontToUse;
            textComponent.ForceMeshUpdate();
        }
    }

    private void OnResetConfirmYes()
    {
        AudioManager.PlayCommon02();
        HideResetConfirmUi();
        ResetGameProgress();
    }

    private void OnResetConfirmNo()
    {
        AudioManager.PlayCommon02();
        HideResetConfirmUi();
    }

    private void HideResetConfirmUi()
    {
        if (resetConfirmPanel != null)
        {
            resetConfirmPanel.SetActive(false);
        }

        if (resetConfirmMask != null)
        {
            resetConfirmMask.SetActive(false);
        }
    }

    /// <summary>
    /// Reset game progress (collection data, hidden cats, normal cats).
    /// Does NOT reset settings.
    /// </summary>
    private void ResetGameProgress()
    {
        try
        {
            GameProgressResetService.ResetGameProgress();
            Debug.Log("[SettingPopUI] Game progress reset successfully.");

            // After resetting progress, refresh main menu unlock visuals (trophy + speedrun toggle)
            // so that they immediately reflect the new locked state.
            var mainMenu = Object.FindObjectOfType<MainMenuUI>();
            if (mainMenu != null)
            {
                mainMenu.UpdateTrophyVisual();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SettingPopUI] Failed to reset game progress: {e.Message}");
        }
    }

    public void OnClick_Close()
    {
        AudioManager.PlayCommon02();
        if (WindowManager.Instance == null)
        {
            Debug.LogError("[SettingPopUI] WindowManager.Instance is null.");
            return;
        }

        // Language changes are already applied immediately when dropdown value changes
        // No need to do anything here

        WindowManager.Instance.HideCurrentPopup();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChangedHandler;
        }

        // Clean up event listeners
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        }

        if (screenToggle != null)
        {
            screenToggle.onValueChanged.RemoveListener(OnScreenToggleChanged);
        }

        if (mouseToggle != null)
        {
            mouseToggle.onValueChanged.RemoveListener(OnMouseToggleChanged);
        }
    }
}
