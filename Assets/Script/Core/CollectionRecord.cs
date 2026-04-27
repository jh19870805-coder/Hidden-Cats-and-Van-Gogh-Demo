using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Serializable data structure for collection records.
    /// Stores both per-scene counts and global counts.
    /// </summary>
    [Serializable]
    public class CollectionRecord
    {
        /// <summary>
        /// Per-scene collection counts.
        /// Key: Scene name (e.g., "RoomWnd"), Value: Dictionary of CollectibleType -> count
        /// </summary>
        [SerializeField]
        private Dictionary<string, Dictionary<int, int>> sceneCounts = new Dictionary<string, Dictionary<int, int>>();

        /// <summary>
        /// Global collection counts across all scenes.
        /// Key: CollectibleType (as int), Value: total count
        /// </summary>
        [SerializeField]
        private Dictionary<int, int> globalCounts = new Dictionary<int, int>();

        /// <summary>
        /// Serialization helper: Convert Dictionary to List for JSON serialization.
        /// Unity's JsonUtility doesn't support Dictionary directly.
        /// </summary>
        [Serializable]
        private class SceneCountsData
        {
            public List<SceneData> scenes = new List<SceneData>();
        }

        [Serializable]
        private class SceneData
        {
            public string sceneName;
            public List<ItemCount> items = new List<ItemCount>();
        }

        [Serializable]
        private class ItemCount
        {
            public int type;
            public int count;
        }

        [Serializable]
        private class GlobalCountsData
        {
            public List<ItemCount> items = new List<ItemCount>();
        }

        [Serializable]
        private class CombinedData
        {
            public List<SceneData> scenes;
            public List<ItemCount> globalItems;
        }

        /// <summary>
        /// Get count for a specific item type in a specific scene.
        /// </summary>
        public int GetSceneCount(string sceneName, CollectibleType type)
        {
            if (sceneCounts == null)
            {
                sceneCounts = new Dictionary<string, Dictionary<int, int>>();
            }

            if (!sceneCounts.ContainsKey(sceneName))
            {
                return 0;
            }

            var sceneDict = sceneCounts[sceneName];
            if (sceneDict == null)
            {
                return 0;
            }

            int typeInt = (int)type;
            return sceneDict.ContainsKey(typeInt) ? sceneDict[typeInt] : 0;
        }

        /// <summary>
        /// Set count for a specific item type in a specific scene.
        /// </summary>
        public void SetSceneCount(string sceneName, CollectibleType type, int count)
        {
            if (sceneCounts == null)
            {
                sceneCounts = new Dictionary<string, Dictionary<int, int>>();
            }

            if (!sceneCounts.ContainsKey(sceneName))
            {
                sceneCounts[sceneName] = new Dictionary<int, int>();
            }

            int typeInt = (int)type;
            sceneCounts[sceneName][typeInt] = count;
        }

        /// <summary>
        /// Increment count for a specific item type in a specific scene.
        /// </summary>
        public int IncrementSceneCount(string sceneName, CollectibleType type)
        {
            int currentCount = GetSceneCount(sceneName, type);
            Debug.Log($"[CollectionRecord] IncrementSceneCount: sceneName='{sceneName}', type={type}, currentCount={currentCount}");
            SetSceneCount(sceneName, type, currentCount + 1);
            return currentCount + 1;
        }

        /// <summary>
        /// Get global count for a specific item type across all scenes.
        /// </summary>
        public int GetGlobalCount(CollectibleType type)
        {
            if (globalCounts == null)
            {
                globalCounts = new Dictionary<int, int>();
            }

            int typeInt = (int)type;
            return globalCounts.ContainsKey(typeInt) ? globalCounts[typeInt] : 0;
        }

        /// <summary>
        /// Set global count for a specific item type.
        /// </summary>
        public void SetGlobalCount(CollectibleType type, int count)
        {
            if (globalCounts == null)
            {
                globalCounts = new Dictionary<int, int>();
            }

            int typeInt = (int)type;
            globalCounts[typeInt] = count;
        }

        /// <summary>
        /// Increment global count for a specific item type.
        /// </summary>
        public int IncrementGlobalCount(CollectibleType type)
        {
            int currentCount = GetGlobalCount(type);
            SetGlobalCount(type, currentCount + 1);
            return currentCount + 1;
        }

        /// <summary>
        /// Serialize to JSON string (for saving to PlayerPrefs).
        /// </summary>
        public string ToJson()
        {
            var data = new SceneCountsData();
            foreach (var scenePair in sceneCounts)
            {
                var sceneData = new SceneData
                {
                    sceneName = scenePair.Key,
                    items = new List<ItemCount>()
                };

                foreach (var itemPair in scenePair.Value)
                {
                    sceneData.items.Add(new ItemCount
                    {
                        type = itemPair.Key,
                        count = itemPair.Value
                    });
                }

                data.scenes.Add(sceneData);
            }

            var globalData = new GlobalCountsData();
            foreach (var itemPair in globalCounts)
            {
                globalData.items.Add(new ItemCount
                {
                    type = itemPair.Key,
                    count = itemPair.Value
                });
            }

            // Combine both into a single JSON structure
            var combinedData = new CombinedData
            {
                scenes = data.scenes,
                globalItems = globalData.items
            };

            return JsonUtility.ToJson(combinedData);
        }

        /// <summary>
        /// Deserialize from JSON string (for loading from PlayerPrefs).
        /// </summary>
        public static CollectionRecord FromJson(string json)
        {
            var record = new CollectionRecord();

            if (string.IsNullOrEmpty(json))
            {
                return record;
            }

            try
            {
                var data = JsonUtility.FromJson<CombinedData>(json);

                // Deserialize scene counts
                if (data.scenes != null)
                {
                    foreach (var sceneData in data.scenes)
                    {
                        var sceneDict = new Dictionary<int, int>();
                        if (sceneData.items != null)
                        {
                            foreach (var item in sceneData.items)
                            {
                                sceneDict[item.type] = item.count;
                            }
                        }
                        record.sceneCounts[sceneData.sceneName] = sceneDict;
                    }
                }

                // Deserialize global counts
                if (data.globalItems != null)
                {
                    foreach (var item in data.globalItems)
                    {
                        record.globalCounts[item.type] = item.count;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CollectionRecord] Failed to deserialize JSON: {e.Message}");
            }

            return record;
        }

        /// <summary>
        /// Create a deep copy of this record.
        /// </summary>
        public CollectionRecord Clone()
        {
            var clone = new CollectionRecord();
            string json = ToJson();
            return FromJson(json);
        }

        /// <summary>
        /// Reset all collection data.
        /// </summary>
        public void Reset()
        {
            sceneCounts.Clear();
            globalCounts.Clear();
        }
    }
}
