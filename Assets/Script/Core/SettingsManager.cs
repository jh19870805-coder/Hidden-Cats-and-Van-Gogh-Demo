using UnityEngine;
using System;

namespace HiddenCats.Core
{
    /// <summary>
    /// Singleton manager for game settings (volume, language, hints, etc.).
    /// Persists settings data and provides events for UI updates.
    /// </summary>
    public sealed class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string SETTINGS_SAVE_KEY = "GameSettings";

        [Header("Default Settings")]
        [SerializeField] private SettingsData defaultSettings = new SettingsData();

        private SettingsData _currentSettings;

        // Events for UI updates
        public event Action<SettingsData> OnSettingsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        /// <summary>
        /// Get current settings (returns a copy to prevent external modification).
        /// </summary>
        public SettingsData GetSettings()
        {
            return _currentSettings.Clone();
        }

        /// <summary>
        /// Apply new settings.
        /// </summary>
        public void ApplySettings(SettingsData newSettings)
        {
            if (newSettings == null)
            {
                Debug.LogError("[SettingsManager] Cannot apply null settings.");
                return;
            }

            _currentSettings = newSettings.Clone();
            ApplySettingsToGame();
            SaveSettings();
            OnSettingsChanged?.Invoke(_currentSettings);
        }

        /// <summary>
        /// Update a single setting value.
        /// </summary>
        public void UpdateSetting<T>(string settingName, T value)
        {
            var newSettings = _currentSettings.Clone();

            switch (settingName)
            {
                case "masterVolume":
                    if (value is float f1) newSettings.masterVolume = f1;
                    break;
                case "musicVolume":
                    if (value is float f2) newSettings.musicVolume = f2;
                    break;
                case "sfxVolume":
                    if (value is float f3) newSettings.sfxVolume = f3;
                    break;
                case "language":
                    if (value is string s) newSettings.language = s;
                    break;
                case "hintsEnabled":
                    if (value is bool b1) newSettings.hintsEnabled = b1;
                    break;
                case "maxHintsPerLevel":
                    if (value is int i) newSettings.maxHintsPerLevel = i;
                    break;
                case "isFullscreen":
                    if (value is bool b2) newSettings.isFullscreen = b2;
                    break;
                case "isCursorLarge":
                    if (value is bool b3) newSettings.isCursorLarge = b3;
                    break;
                default:
                    Debug.LogWarning($"[SettingsManager] Unknown setting name: {settingName}");
                    return;
            }

            ApplySettings(newSettings);
        }

        /// <summary>
        /// Reset settings to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            ApplySettings(defaultSettings.Clone());
        }

        private void ApplySettingsToGame()
        {
            // Apply volume settings to AudioListener or AudioMixer
            AudioListener.volume = _currentSettings.masterVolume;

            // Apply music / SFX volumes via AudioManager if available
            try
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.ApplySettings(_currentSettings);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] Failed to apply audio settings: {e.Message}");
            }

            // SFX volume is applied via AudioManager's OnSettingsChanged handler

            // Apply language setting to localization system
            try
            {
                if (LocalizationManager.Instance != null)
                {
                    LocalizationManager.Instance.SetLanguage(_currentSettings.language);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] Failed to apply language setting: {e.Message}");
            }

            // Apply fullscreen / windowed mode
            try
            {
                if (_currentSettings.isFullscreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Screen.fullScreen = true;
                }
                else
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    Screen.fullScreen = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] Failed to apply screen mode: {e.Message}");
            }

            // Apply cursor size
            // Use a coroutine to delay cursor application if CursorManager hasn't initialized yet
            try
            {
                if (CursorManager.Instance != null)
                {
                    CursorManager.Instance.SetCursorSize(_currentSettings.isCursorLarge);
                }
                else
                {
                    // If CursorManager hasn't initialized yet, delay the application
                    StartCoroutine(ApplyCursorSizeDelayed());
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] Failed to apply cursor size: {e.Message}");
            }
        }

        private void LoadSettings()
        {
            string json = PlayerPrefs.GetString(SETTINGS_SAVE_KEY, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                _currentSettings = defaultSettings.Clone();
                SaveSettings(); // Save defaults
            }
            else
            {
                try
                {
                    _currentSettings = JsonUtility.FromJson<SettingsData>(json);
                    if (_currentSettings == null)
                    {
                        _currentSettings = defaultSettings.Clone();
                    }
                    else
                    {
                        // Force cursor to default (MouseX1) on game start
                        // This ensures the game always starts with MouseX1, regardless of saved settings
                        _currentSettings.isCursorLarge = false;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SettingsManager] Failed to load settings: {e.Message}");
                    _currentSettings = defaultSettings.Clone();
                }
            }

            ApplySettingsToGame();
        }

        private System.Collections.IEnumerator ApplyCursorSizeDelayed()
        {
            // Wait up to 10 frames for CursorManager to initialize
            int maxWait = 10;
            while (CursorManager.Instance == null && maxWait > 0)
            {
                yield return null;
                maxWait--;
            }
            
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.SetCursorSize(_currentSettings.isCursorLarge);
            }
            else
            {
                Debug.LogWarning("[SettingsManager] CursorManager.Instance is still null after waiting, cursor size will be applied when CursorManager initializes.");
            }
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(_currentSettings);
                PlayerPrefs.SetString(SETTINGS_SAVE_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] Failed to save settings: {e.Message}");
            }
        }
    }
}
