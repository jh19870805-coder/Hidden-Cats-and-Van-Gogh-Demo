using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Singleton manager for localization system.
    /// Handles language switching and text translation.
    /// </summary>
    public sealed class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [Header("Language Configuration")]
        [Tooltip("Language configuration asset. Create one in the project and assign it here.")]
        [SerializeField]
        private LanguageConfig languageConfig;

        [Header("Strings")]
        [Tooltip("Optional. If null, GetText/GetLocalizedString return keys until a table is assigned.")]
        [SerializeField]
        private LocalizationTable localizationTable;

        private string _currentLanguageCode = "en-US";

        /// <summary>Outer: translation key. Inner: language code -> text.</summary>
        private readonly Dictionary<string, Dictionary<string, string>> _byKey =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        // Event for language change
        public event Action<string> OnLanguageChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // Ensure GameObject is root before calling DontDestroyOnLoad
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            RebuildStringTable();

            // Initialize with default language
            if (languageConfig != null)
            {
                // Try to load language from settings
                if (SettingsManager.Instance != null)
                {
                    var settings = SettingsManager.Instance.GetSettings();
                    SetLanguage(settings.language);
                }
                else
                {
                    SetLanguage(_currentLanguageCode);
                }
            }
            else
            {
                Debug.LogWarning("[LocalizationManager] LanguageConfig is not assigned. Please assign a LanguageConfig asset in the Inspector.");
            }
        }

        /// <summary>
        /// Reload entries from <see cref="localizationTable"/> (e.g. after hot-reload in editor).
        /// </summary>
        public void RebuildStringTable()
        {
            _byKey.Clear();
            if (localizationTable == null)
            {
                localizationTable = Resources.Load<LocalizationTable>("LocalizationTable");
            }

            if (localizationTable == null)
            {
                return;
            }

            foreach (LocalizationTable.Entry entry in localizationTable.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                string k = entry.key.Trim();
                if (!_byKey.TryGetValue(k, out Dictionary<string, string> perLang))
                {
                    perLang = new Dictionary<string, string>(StringComparer.Ordinal);
                    _byKey[k] = perLang;
                }

                if (entry.cells == null)
                {
                    continue;
                }

                foreach (LocalizationTable.LocalizedCell cell in entry.cells)
                {
                    if (cell == null || string.IsNullOrWhiteSpace(cell.languageCode))
                    {
                        continue;
                    }

                    perLang[cell.languageCode.Trim()] = cell.text ?? string.Empty;
                }
            }

            if (_byKey.Count == 0 && localizationTable != null)
            {
                int n = localizationTable.Entries != null ? localizationTable.Entries.Count : 0;
                if (n == 0)
                {
                    Debug.LogWarning(
                        "[LocalizationManager] LocalizationTable loaded with 0 entries. If the asset was edited as YAML, quote any string that contains {…} placeholders (e.g. \"Collect {0}/{1} ...\").");
                }
            }
        }

        /// <summary>
        /// Set the current language.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (languageConfig == null)
            {
                Debug.LogError("[LocalizationManager] LanguageConfig is not assigned.");
                return;
            }

            var langOption = languageConfig.GetLanguageByCode(languageCode);
            if (langOption == null)
            {
                string fallback = languageConfig.GetFallbackLanguageCode();
                Debug.LogWarning($"[LocalizationManager] Language code '{languageCode}' not found. Using fallback '{fallback}'.");
                languageCode = fallback;
            }

            if (_currentLanguageCode != languageCode)
            {
                _currentLanguageCode = languageCode;
                OnLanguageChanged?.Invoke(languageCode);
                Debug.Log($"[LocalizationManager] Language changed to: {languageCode}");
            }
        }

        /// <summary>
        /// Get the current language code.
        /// </summary>
        public string GetCurrentLanguage()
        {
            return _currentLanguageCode;
        }

        /// <summary>
        /// Same as <see cref="GetLocalizedString"/> — preferred name for new code.
        /// </summary>
        public string GetText(string key)
        {
            return GetLocalizedString(key);
        }

        /// <summary>
        /// Format a localized template with <see cref="CultureInfo.InvariantCulture"/> (stable numeric formatting).
        /// </summary>
        public string GetFormattedText(string key, params object[] args)
        {
            string template = GetLocalizedString(key);
            if (args == null || args.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"[LocalizationManager] Format failed for key '{key}': {ex.Message}");
                return template;
            }
        }

        /// <summary>
        /// Get localized string by key (fallback: fallback language from LanguageConfig, then any non-empty language on the same key).
        /// </summary>
        public string GetLocalizedString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (!_byKey.TryGetValue(key, out Dictionary<string, string> perLang))
            {
                if (localizationTable != null)
                {
                    Debug.LogWarning($"[LocalizationManager] Missing localization key: {key}");
                }

                return key;
            }

            if (perLang.TryGetValue(_currentLanguageCode, out string s) && !string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (languageConfig != null)
            {
                string fb = languageConfig.GetFallbackLanguageCode();
                if (perLang.TryGetValue(fb, out s) && !string.IsNullOrEmpty(s))
                {
                    return s;
                }
            }

            foreach (KeyValuePair<string, string> kv in perLang)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    return kv.Value;
                }
            }

            Debug.LogWarning($"[LocalizationManager] Empty strings for key: {key}");
            return key;
        }

        /// <summary>
        /// Get the language configuration.
        /// </summary>
        public LanguageConfig GetLanguageConfig()
        {
            return languageConfig;
        }

        /// <summary>
        /// Get all available language options.
        /// </summary>
        public List<LanguageConfig.LanguageOption> GetAvailableLanguages()
        {
            if (languageConfig == null)
            {
                return new List<LanguageConfig.LanguageOption>();
            }

            return languageConfig.GetLanguages();
        }

        /// <summary>
        /// Get index of current language.
        /// </summary>
        public int GetCurrentLanguageIndex()
        {
            if (languageConfig == null)
            {
                return 0;
            }

            int index = languageConfig.GetLanguageIndex(_currentLanguageCode);
            return index >= 0 ? index : 0;
        }

        /// <summary>
        /// Get language code by index.
        /// </summary>
        public string GetLanguageCodeByIndex(int index)
        {
            if (languageConfig == null)
            {
                return "en-US";
            }

            return languageConfig.GetLanguageCode(index);
        }

        /// <summary>
        /// Get display name for language at index.
        /// </summary>
        public string GetLanguageDisplayName(int index)
        {
            if (languageConfig == null)
            {
                return "Unknown";
            }

            return languageConfig.GetDisplayName(index, _currentLanguageCode);
        }
    }
}
