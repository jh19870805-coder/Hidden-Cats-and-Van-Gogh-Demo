using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Key-based strings per language code (codes must match <see cref="LanguageConfig"/> entries).
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Hidden Cats/Localization Table", order = 2)]
    public sealed class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public sealed class LocalizedCell
        {
            [Tooltip("e.g. zh-CN, en-US — must match LanguageConfig.languageCode")]
            public string languageCode;

            [TextArea(1, 4)]
            public string text;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable id, e.g. smallgame.unlock_hint")]
            public string key;

            public List<LocalizedCell> cells = new List<LocalizedCell>();
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}
