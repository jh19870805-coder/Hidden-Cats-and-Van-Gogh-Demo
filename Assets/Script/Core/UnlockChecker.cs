using System.Collections.Generic;
using HiddenCats.Interactable;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// Unlock condition checker.
    /// Checks unlock conditions based on collection statistics.
    /// </summary>
    public static class UnlockChecker
    {
        /// <summary>
        /// Editor test flag: Force unlock speedrun/leaderboard (for testing purposes).
        /// Set this to true in the Unity Inspector or via code to bypass unlock checks.
        /// </summary>
        public static bool EditorForceUnlockSpeedrun = false;

        // Cache totals to avoid expensive FindObjectsOfType scans every time MainWnd re-enables.
        // In this project the content windows are prewarmed and totals are stable within a session.
        private static bool _cachedTotalsReady;
        private static int _cachedTotalNormal;
        private static int _cachedTotalHidden;

        /// <summary>
        /// Clears cached cat totals so the next unlock check re-scans (e.g. after game progress reset).
        /// </summary>
        public static void InvalidateTotalsCache()
        {
            _cachedTotalsReady = false;
            _cachedTotalNormal = 0;
            _cachedTotalHidden = 0;
        }

        /// <summary>
        /// Check if speedrun/leaderboard is unlocked.
        /// 条件：找到所有场景中的普通猫咪和隐藏猫咪。
        /// </summary>
        public static bool IsSpeedrunUnlocked()
        {
            // Editor test override
            if (Application.isEditor && EditorForceUnlockSpeedrun)
            {
                return true;
            }

            // Leaderboard/trophy unlock: full normal+hidden cat collection in *either* save slot counts.
            // (Normal-only was insufficient: toggling speedrun off reloads the normal slot; if that snapshot
            // is empty/stale while the player completed in speedrun, the trophy incorrectly re-locked.)

            int totalNormal;
            int totalHidden;
            if (_cachedTotalsReady)
            {
                totalNormal = _cachedTotalNormal;
                totalHidden = _cachedTotalHidden;
            }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                if (!TryGetTotalCatsAcrossRequiredScenes(out totalNormal, out totalHidden))
                {
                    // Totals not ready (e.g., content windows not instantiated yet).
                    return false;
                }

                _cachedTotalsReady = true;
                _cachedTotalNormal = totalNormal;
                _cachedTotalHidden = totalHidden;

                float dt = Time.realtimeSinceStartup - t0;
                // Only log when the scan is slow enough to matter (helps diagnose "Back -> freeze").
                if (dt >= 0.05f)
                {
                    Debug.LogWarning($"[UnlockChecker] Cached total cats (Normal={totalNormal}, Hidden={totalHidden}) in {dt:0.000}s");
                }
            }

            string normalJson = PlayerPrefs.GetString(CollectionService.NormalCollectionPlayerPrefsKey, string.Empty);
            CollectionRecord normalRecord = CollectionRecord.FromJson(normalJson);

            string speedrunJson = PlayerPrefs.GetString(CollectionService.SpeedrunCollectionPlayerPrefsKey, string.Empty);
            CollectionRecord speedrunRecord = CollectionRecord.FromJson(speedrunJson);

            return HasFullCatCollectionForLeaderboard(normalRecord, totalNormal, totalHidden)
                || HasFullCatCollectionForLeaderboard(speedrunRecord, totalNormal, totalHidden);
        }

        /// <summary>
        /// True if this record reports at least as many normal and hidden cats as required (global counts).
        /// </summary>
        private static bool HasFullCatCollectionForLeaderboard(CollectionRecord record, int totalNormal, int totalHidden)
        {
            if (record == null)
            {
                return false;
            }

            int collectedNormal = record.GetGlobalCount(CollectibleType.NormalCat);
            int collectedHidden = record.GetGlobalCount(CollectibleType.HiddenCat);
            return collectedNormal >= totalNormal && collectedHidden >= totalHidden;
        }

        /// <summary>
        /// Public helper: get total normal + hidden cat counts across all required scenes.
        /// Used by SpeedrunService to detect speedrun completion.
        /// </summary>
        public static bool TryGetTotalCatsForSpeedrun(out int totalNormal, out int totalHidden)
        {
            return TryGetTotalCatsAcrossRequiredScenes(out totalNormal, out totalHidden);
        }

        private static bool TryGetTotalCatsAcrossRequiredScenes(out int totalNormal, out int totalHidden)
        {
            totalNormal = 0;
            totalHidden = 0;

            string[] requiredScenes = { SceneName.RoomWnd };
            var scenePresence = new Dictionary<string, int>(requiredScenes.Length);
            for (int i = 0; i < requiredScenes.Length; i++)
            {
                scenePresence[requiredScenes[i]] = 0;
            }

            // De-dup by UniqueId to avoid accidental double counting.
            var normalIds = new HashSet<string>();
            var hiddenIds = new HashSet<string>();

            NormalCatInteractable[] normalCats = Object.FindObjectsOfType<NormalCatInteractable>(true);
            if (normalCats != null)
            {
                foreach (var cat in normalCats)
                {
                    if (cat == null) continue;
                    // 通过 InitiallyActiveInPrefab 判断，区分「Prefab 默认 inactive」和「被收集后 inactive」。
                    if (!cat.InitiallyActiveInPrefab) continue;

                    if (scenePresence.ContainsKey(cat.SceneName))
                    {
                        scenePresence[cat.SceneName]++;
                    }

                    string id = !string.IsNullOrEmpty(cat.UniqueId) ? cat.UniqueId : cat.GetInstanceID().ToString();
                    normalIds.Add(id);
                }
            }

            HiddenCatInteractable[] hiddenCats = Object.FindObjectsOfType<HiddenCatInteractable>(true);
            if (hiddenCats != null)
            {
                foreach (var cat in hiddenCats)
                {
                    if (cat == null) continue;
                    // 通过 InitiallyActiveInPrefab 判断，区分「Prefab 默认 inactive」和「被收集后 inactive」。
                    if (!cat.InitiallyActiveInPrefab) continue;

                    if (scenePresence.ContainsKey(cat.SceneName))
                    {
                        scenePresence[cat.SceneName]++;
                    }

                    string id = !string.IsNullOrEmpty(cat.UniqueId) ? cat.UniqueId : cat.GetInstanceID().ToString();
                    hiddenIds.Add(id);
                }
            }

            // If some required scene has no cat content loaded, totals are not reliable yet.
            // This prevents false-positive "first clear" when only one window is instantiated.
            foreach (string scene in requiredScenes)
            {
                if (!scenePresence.TryGetValue(scene, out int count) || count <= 0)
                {
                    return false;
                }
            }

            totalNormal = normalIds.Count;
            totalHidden = hiddenIds.Count;
            return (totalNormal + totalHidden) > 0;
        }

        /// <summary>
        /// Check if a specific scene has found all items of a specific type.
        /// This is a helper method for more detailed unlock checks.
        /// </summary>
        /// <param name="sceneName">Scene name</param>
        /// <param name="type">Collectible type</param>
        /// <param name="requiredCount">Required count to unlock (0 means any count > 0)</param>
        /// <returns>True if the scene has found the required count</returns>
        public static bool HasSceneFoundItems(string sceneName, CollectibleType type, int requiredCount = 0)
        {
            if (CollectionService.Instance == null)
            {
                return false;
            }

            int currentCount = CollectionService.Instance.GetSceneCount(sceneName, type);
            
            if (requiredCount == 0)
            {
                return currentCount > 0;
            }

            return currentCount >= requiredCount;
        }

        /// <summary>
        /// Check if all scenes have found all items of a specific type.
        /// </summary>
        /// <param name="type">Collectible type</param>
        /// <param name="requiredCountPerScene">Required count per scene (0 means any count > 0)</param>
        /// <returns>True if all scenes have found the required count</returns>
        public static bool HaveAllScenesFoundItems(CollectibleType type, int requiredCountPerScene = 0)
        {
            string[] scenes = { SceneName.RoomWnd };
            
            foreach (string sceneName in scenes)
            {
                if (!HasSceneFoundItems(sceneName, type, requiredCountPerScene))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
