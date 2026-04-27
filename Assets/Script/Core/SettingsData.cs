using System;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Serializable settings data structure.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        [UnityEngine.Range(0f, 1f)]
        public float masterVolume = 1f;

        [UnityEngine.Range(0f, 1f)]
        public float musicVolume = 0.5f;

        [UnityEngine.Range(0f, 1f)]
        public float sfxVolume = 1f;

        public string language = "en-US";

        public bool hintsEnabled = true;

        public int maxHintsPerLevel = 3;

        /// <summary>
        /// Whether the game is in fullscreen mode (true) or windowed mode (false).
        /// </summary>
        public bool isFullscreen = true;

        /// <summary>
        /// Whether the cursor is in large size (2x) (true) or normal size (false).
        /// </summary>
        public bool isCursorLarge = false;

        public SettingsData()
        {
            // Default values
            masterVolume = 1f;
            musicVolume = 0.5f;
            sfxVolume = 1f;
            language = "en-US";
            hintsEnabled = true;
            maxHintsPerLevel = 3;
            isFullscreen = true;
            isCursorLarge = false;
        }

        public SettingsData Clone()
        {
            return new SettingsData
            {
                masterVolume = this.masterVolume,
                musicVolume = this.musicVolume,
                sfxVolume = this.sfxVolume,
                language = this.language,
                hintsEnabled = this.hintsEnabled,
                maxHintsPerLevel = this.maxHintsPerLevel,
                isFullscreen = this.isFullscreen,
                isCursorLarge = this.isCursorLarge
            };
        }
    }
}
