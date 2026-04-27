using UnityEngine;
using System;
using System.Collections.Generic;

namespace HiddenCats.Core
{
    /// <summary>
    /// Singleton service for managing collection statistics.
    /// Handles both per-scene counting and global counting across all scenes.
    /// </summary>
    public sealed class CollectionService : MonoBehaviour
    {
        public static CollectionService Instance { get; private set; }

        /// <summary>PlayerPrefs key for normal (non-speedrun) collection JSON.</summary>
        public const string NormalCollectionPlayerPrefsKey = "GameCollectionData";

        /// <summary>PlayerPrefs key for speedrun collection JSON.</summary>
        public const string SpeedrunCollectionPlayerPrefsKey = "SpeedrunCollectionData";

        private CollectionRecord _collectionRecord;

        /// <summary>Whether CollectionService is currently operating on speedrun data.</summary>
        private bool _isSpeedrunMode = false;
        public bool IsSpeedrunMode => _isSpeedrunMode;

        /// <summary>Returns the PlayerPrefs key for the current mode.</summary>
        private string CurrentSaveKey => _isSpeedrunMode ? SpeedrunCollectionPlayerPrefsKey : NormalCollectionPlayerPrefsKey;

        // Events for UI updates and game logic
        public event Action<string, CollectibleType, int> OnSceneCountChanged;
        public event Action<CollectibleType, int> OnGlobalCountChanged;

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

            LoadCollectionData();
        }

        /// <summary>
        /// Collect an item in a specific scene.
        /// This will increment both the scene count and the global count.
        /// </summary>
        /// <param name="sceneName">Scene name (e.g., SceneName.RoomWnd)</param>
        /// <param name="type">Type of collectible</param>
        /// <returns>True if the item was successfully collected</returns>
        public bool CollectItem(string sceneName, CollectibleType type)
        {
            Debug.Log($"[CollectionService] CollectItem called: sceneName='{sceneName}', type={type}");

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[CollectionService] Scene name cannot be null or empty.");
                return false;
            }

            // Increment scene count
            int newSceneCount = _collectionRecord.IncrementSceneCount(sceneName, type);
            OnSceneCountChanged?.Invoke(sceneName, type, newSceneCount);

            // Increment global count
            int newGlobalCount = _collectionRecord.IncrementGlobalCount(type);
            OnGlobalCountChanged?.Invoke(type, newGlobalCount);

            // Save to persistent storage
            SaveCollectionData();

            return true;
        }

        /// <summary>
        /// Get the count of a specific item type in a specific scene.
        /// </summary>
        public int GetSceneCount(string sceneName, CollectibleType type)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return 0;
            }

            return _collectionRecord.GetSceneCount(sceneName, type);
        }

        /// <summary>
        /// Get the global count of a specific item type across all scenes.
        /// </summary>
        public int GetGlobalCount(CollectibleType type)
        {
            return _collectionRecord.GetGlobalCount(type);
        }

        /// <summary>
        /// Get all scene counts for a specific scene.
        /// Returns a dictionary mapping CollectibleType to count.
        /// </summary>
        public Dictionary<CollectibleType, int> GetSceneCounts(string sceneName)
        {
            var result = new Dictionary<CollectibleType, int>();

            if (string.IsNullOrEmpty(sceneName))
            {
                return result;
            }

            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                int count = GetSceneCount(sceneName, type);
                if (count > 0)
                {
                    result[type] = count;
                }
            }

            return result;
        }

        /// <summary>
        /// Get all global counts.
        /// Returns a dictionary mapping CollectibleType to total count.
        /// </summary>
        public Dictionary<CollectibleType, int> GetGlobalCounts()
        {
            var result = new Dictionary<CollectibleType, int>();

            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                int count = GetGlobalCount(type);
                if (count > 0)
                {
                    result[type] = count;
                }
            }

            return result;
        }

        /// <summary>
        /// Get the collection record (returns a copy to prevent external modification).
        /// </summary>
        public CollectionRecord GetCollectionRecord()
        {
            return _collectionRecord.Clone();
        }

        /// <summary>
        /// Reset all collection data (for testing or new game).
        /// Resets the data for the currently active mode (normal or speedrun).
        /// </summary>
        public void ResetCollectionData()
        {
            _collectionRecord.Reset();
            SaveCollectionData();

            // Notify listeners that all counts have been reset
            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                OnGlobalCountChanged?.Invoke(type, 0);
            }
        }

        /// <summary>
        /// Full progress reset: removes <b>both</b> normal and speedrun collection JSON keys and clears memory.
        /// Forces normal mode so it matches <see cref="SpeedrunService"/> after reset.
        /// Required so <see cref="UnlockChecker"/> cannot read a stale full slot while the other was cleared.
        /// </summary>
        public void WipeAllCollectionSlotsForFullGameReset()
        {
            _isSpeedrunMode = false;
            _collectionRecord = new CollectionRecord();

            PlayerPrefs.DeleteKey(NormalCollectionPlayerPrefsKey);
            PlayerPrefs.DeleteKey(SpeedrunCollectionPlayerPrefsKey);
            SaveCollectionData();

            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                OnGlobalCountChanged?.Invoke(type, 0);
            }

            string[] scenes = { SceneName.RoomWnd };
            foreach (string scene in scenes)
            {
                foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
                {
                    OnSceneCountChanged?.Invoke(scene, type, 0);
                }
            }
        }

        /// <summary>
        /// Switch between normal and speedrun save slots.
        /// Saves current data, switches key, loads the other slot's data,
        /// then fires change events so UI refreshes.
        /// </summary>
        public void SwitchToSpeedrunMode(bool speedrunMode)
        {
            if (_isSpeedrunMode == speedrunMode)
                return;

            // Persist current data under the old key.
            SaveCollectionData();

            _isSpeedrunMode = speedrunMode;

            // Load data from the new key.
            LoadCollectionData();

            // Fire global-count events so NumUI / HiddenObjectManager refresh.
            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                int globalCount = _collectionRecord.GetGlobalCount(type);
                OnGlobalCountChanged?.Invoke(type, globalCount);
            }

            // Fire scene-count events for all known scenes.
            string[] scenes = { SceneName.RoomWnd };
            foreach (string scene in scenes)
            {
                foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
                {
                    int sceneCount = _collectionRecord.GetSceneCount(scene, type);
                    OnSceneCountChanged?.Invoke(scene, type, sceneCount);
                }
            }
        }

        /// <summary>
        /// Load collection data from persistent storage.
        /// </summary>
        private void LoadCollectionData()
        {
            string json = PlayerPrefs.GetString(CurrentSaveKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                _collectionRecord = new CollectionRecord();
                SaveCollectionData(); // Save empty record
            }
            else
            {
                try
                {
                    _collectionRecord = CollectionRecord.FromJson(json);
                    if (_collectionRecord == null)
                    {
                        _collectionRecord = new CollectionRecord();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CollectionService] Failed to load collection data: {e.Message}");
                    _collectionRecord = new CollectionRecord();
                }
            }
        }

        /// <summary>
        /// Save collection data to persistent storage.
        /// </summary>
        private void SaveCollectionData()
        {
            try
            {
                string json = _collectionRecord.ToJson();
                PlayerPrefs.SetString(CurrentSaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CollectionService] Failed to save collection data: {e.Message}");
            }
        }
    }
}
