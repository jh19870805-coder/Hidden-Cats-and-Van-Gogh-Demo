using UnityEngine;
using System;
using System.Collections.Generic;

namespace HiddenCats.Core
{
    /// <summary>
    /// Language configuration data for localization system.
    /// Create instances of this ScriptableObject in the project to configure available languages.
    /// </summary>
    [CreateAssetMenu(fileName = "LanguageConfig", menuName = "Hidden Cats/Language Config", order = 1)]
    public class LanguageConfig : ScriptableObject
    {
        [System.Serializable]
        public class LanguageOption
        {
            [Tooltip("Language code (e.g., 'zh-CN', 'en-US')")]
            public string languageCode;

            [Tooltip("Display name in native language (e.g., '简体中文', 'English')")]
            public string displayName;

            [Tooltip("Display name in English (e.g., 'Simplified Chinese', 'English')")]
            public string displayNameEnglish;
        }

        [Header("Available Languages")]
        [Tooltip("List of available languages. Configure in the Inspector.")]
        [SerializeField] private List<LanguageOption> languages = new List<LanguageOption>
        {
            new LanguageOption { languageCode = "en-US", displayName = "English", displayNameEnglish = "English" },
            new LanguageOption { languageCode = "zh-CN", displayName = "简体中文", displayNameEnglish = "Simplified Chinese" }
        };

        [Header("Localization fallback")]
        [Tooltip("When a string key is missing in the current language, try this code next. Empty = use first entry in the list above.")]
        [SerializeField]
        private string fallbackLanguageCode = "";

        /// <summary>
        /// Language code used when a key is missing for the active language (must exist in <see cref="languages"/> unless list is empty).
        /// </summary>
        public string GetFallbackLanguageCode()
        {
            if (!string.IsNullOrWhiteSpace(fallbackLanguageCode))
            {
                string trimmed = fallbackLanguageCode.Trim();
                if (GetLanguageByCode(trimmed) != null)
                {
                    return trimmed;
                }
            }

            return languages.Count > 0 ? languages[0].languageCode : "en-US";
        }

        /// <summary>
        /// Get all available language options.
        /// </summary>
        public List<LanguageOption> GetLanguages()
        {
            return new List<LanguageOption>(languages);
        }

        /// <summary>
        /// Get language option by language code.
        /// </summary>
        public LanguageOption GetLanguageByCode(string languageCode)
        {
            return languages.Find(lang => lang.languageCode == languageCode);
        }

        /// <summary>
        /// Get index of language by language code.
        /// </summary>
        public int GetLanguageIndex(string languageCode)
        {
            return languages.FindIndex(lang => lang.languageCode == languageCode);
        }

        /// <summary>
        /// Get language code by index.
        /// </summary>
        public string GetLanguageCode(int index)
        {
            if (index >= 0 && index < languages.Count)
            {
                return languages[index].languageCode;
            }
            return languages.Count > 0 ? languages[0].languageCode : "en-US";
        }

        /// <summary>
        /// Get display name for language at index (in current language if available, otherwise in English).
        /// </summary>
        public string GetDisplayName(int index, string currentLanguageCode = "en-US")
        {
            if (index >= 0 && index < languages.Count)
            {
                var lang = languages[index];
                // If current language is Chinese, show native name; otherwise show English name
                if (currentLanguageCode.StartsWith("zh"))
                {
                    return lang.displayName;
                }
                else
                {
                    return lang.displayNameEnglish;
                }
            }
            return "Unknown";
        }
    }
}
