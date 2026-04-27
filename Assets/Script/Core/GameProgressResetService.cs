using UnityEngine;
using System.Collections.Generic;
using HiddenCats.Interactable;
using System;

namespace HiddenCats.Core
{
    /// <summary>
    /// Service for resetting game progress data.
    /// Resets collection data and interactable states, but does NOT reset settings.
    /// </summary>
    public static class GameProgressResetService
    {
        private const string RESET_VERSION_KEY = "GameProgressResetVersion";
        private const string RESET_APPLIED_SUFFIX = "_ResetAppliedVersion";

        /// <summary>
        /// Fired after a reset operation completes (after PlayerPrefs.Save).
        /// UI and runtime services can subscribe to immediately refresh visuals/state.
        /// </summary>
        public static event Action OnGameProgressReset;

        /// <summary>
        /// Reset all game progress data (collection data, hidden cats, normal cats).
        /// Does NOT reset settings.
        /// </summary>
        public static void ResetGameProgress()
        {
            Debug.Log("[GameProgressResetService] Resetting game progress...");

            Exception resetException = null;

            // Wipe BOTH collection slots (normal + speedrun). UnlockChecker reads both keys; clearing only
            // the active slot leaves the other slot with stale "full" progress so trophy stays unlocked
            // until a second reset. SpeedrunService.ResetAll also switches modes and would re-save normal
            // before clearing speedrun if we only used ResetCollectionData().
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.WipeAllCollectionSlotsForFullGameReset();
                Debug.Log("[GameProgressResetService] All collection slots wiped (normal + speedrun).");
            }
            else
            {
                PlayerPrefs.DeleteKey(CollectionService.NormalCollectionPlayerPrefsKey);
                PlayerPrefs.DeleteKey(CollectionService.SpeedrunCollectionPlayerPrefsKey);
                Debug.Log("[GameProgressResetService] Collection slot keys deleted (CollectionService not initialized).");
            }

            // Reset all active HiddenCat instances in the current scene
            HiddenCatInteractable[] hiddenCats = UnityEngine.Object.FindObjectsOfType<HiddenCatInteractable>(true);
            int hiddenCatCount = 0;
            foreach (HiddenCatInteractable hiddenCat in hiddenCats)
            {
                try
                {
                    if (hiddenCat != null)
                    {
                        hiddenCat.ResetCollection();
                        hiddenCatCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameProgressResetService] HiddenCat ResetCollection failed for '{hiddenCat?.gameObject?.name}': {e.Message}");
                    resetException = e;
                }
            }
            Debug.Log($"[GameProgressResetService] Reset {hiddenCatCount} HiddenCat instances.");

            // Reset all active NormalCat instances in the current scene
            NormalCatInteractable[] normalCats = UnityEngine.Object.FindObjectsOfType<NormalCatInteractable>(true);
            int normalCatCount = 0;
            foreach (NormalCatInteractable normalCat in normalCats)
            {
                try
                {
                    if (normalCat != null)
                    {
                        normalCat.ResetCollection();
                        normalCatCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameProgressResetService] NormalCat ResetCollection failed for '{normalCat?.gameObject?.name}': {e.Message}");
                    resetException = e;
                }
            }
            Debug.Log($"[GameProgressResetService] Reset {normalCatCount} NormalCat instances.");

            // Reset all active Fish instances in the current scene
            FishInteractable[] fishes = UnityEngine.Object.FindObjectsOfType<FishInteractable>(true);
            int fishCount = 0;
            foreach (FishInteractable fish in fishes)
            {
                try
                {
                    if (fish != null)
                    {
                        fish.ResetCollection();
                        fishCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameProgressResetService] Fish ResetCollection failed for '{fish?.gameObject?.name}': {e.Message}");
                    resetException = e;
                }
            }
            Debug.Log($"[GameProgressResetService] Reset {fishCount} Fish instances.");

            // Reset all active Firework instances in the current scene
            FireworkInteractable[] fireworks = UnityEngine.Object.FindObjectsOfType<FireworkInteractable>(true);
            int fireworkCount = 0;
            foreach (FireworkInteractable firework in fireworks)
            {
                try
                {
                    if (firework != null)
                    {
                        firework.ResetCollection();
                        fireworkCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameProgressResetService] Firework ResetCollection failed for '{firework?.gameObject?.name}': {e.Message}");
                    resetException = e;
                }
            }
            Debug.Log($"[GameProgressResetService] Reset {fireworkCount} Firework instances.");

            // Reset speedrun data (records + run state + speedrun collection data)
            if (SpeedrunService.Instance != null)
            {
                SpeedrunService.Instance.ResetAll();
                Debug.Log("[GameProgressResetService] Speedrun data reset.");
            }

            // Increase reset version so all interactables (including other scenes loaded later)
            // can detect and apply this reset exactly once per object key.
            int newResetVersion = PlayerPrefs.GetInt(RESET_VERSION_KEY, 0) + 1;
            PlayerPrefs.SetInt(RESET_VERSION_KEY, newResetVersion);

            // Delete PlayerPrefs keys with known patterns
            // Note: Unity's PlayerPrefs doesn't support enumerating all keys,
            // so we can only delete keys we know about. The reset flag ensures
            // that interactables loaded later will also reset themselves.
            DeleteKnownPlayerPrefsKeys();

            PlayerPrefs.Save();
            Debug.Log("[GameProgressResetService] Game progress reset complete.");
            Debug.Log($"[GameProgressResetService] Reset version updated to {newResetVersion}. Interactables loaded later will apply this reset once.");

            UnlockChecker.InvalidateTotalsCache();

            try
            {
                OnGameProgressReset?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameProgressResetService] OnGameProgressReset listener threw exception: {e.Message}");
                resetException = e;
            }

            if (resetException != null)
            {
                throw resetException;
            }
        }

        /// <summary>
        /// Check if this save key should apply the latest reset operation.
        /// Each interactable key applies a reset version only once.
        /// </summary>
        public static bool ShouldApplyResetForKey(string saveKey)
        {
            if (string.IsNullOrEmpty(saveKey))
            {
                return false;
            }

            int resetVersion = PlayerPrefs.GetInt(RESET_VERSION_KEY, 0);
            if (resetVersion <= 0)
            {
                return false;
            }

            int appliedVersion = PlayerPrefs.GetInt(GetResetAppliedKey(saveKey), 0);
            return appliedVersion < resetVersion;
        }

        /// <summary>
        /// Mark reset as applied for this save key in the current reset version.
        /// </summary>
        public static void MarkResetAppliedForKey(string saveKey)
        {
            if (string.IsNullOrEmpty(saveKey))
            {
                return;
            }

            int resetVersion = PlayerPrefs.GetInt(RESET_VERSION_KEY, 0);
            if (resetVersion <= 0)
            {
                return;
            }

            PlayerPrefs.SetInt(GetResetAppliedKey(saveKey), resetVersion);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Build key used to track whether a save key already applied a reset version.
        /// </summary>
        private static string GetResetAppliedKey(string saveKey)
        {
            return $"{saveKey}{RESET_APPLIED_SUFFIX}";
        }

        /// <summary>
        /// Delete known PlayerPrefs keys related to cats.
        /// Since Unity's PlayerPrefs doesn't support enumerating all keys,
        /// we can only delete keys we know about. The reset flag ensures
        /// that interactables loaded later will also reset themselves.
        /// </summary>
        private static void DeleteKnownPlayerPrefsKeys()
        {
            // Try to delete keys with common patterns
            // Note: This is limited because we can't enumerate all keys,
            // but the reset flag mechanism ensures interactables will reset
            // when they load their state.
            
            // We'll try to delete keys for common positions/scenes
            // This is a best-effort approach. The reset flag is the primary mechanism.
        }

        /// <summary>
        /// Get a list of all PlayerPrefs keys (for debugging purposes).
        /// Note: This is a workaround since Unity doesn't provide this functionality directly.
        /// </summary>
        public static List<string> GetAllPlayerPrefsKeys()
        {
            // Unity doesn't provide a way to enumerate all PlayerPrefs keys.
            // This would require platform-specific code or maintaining a registry.
            // For now, return known keys.
            List<string> knownKeys = new List<string>
            {
                "GameCollectionData",
                "GameSettings",
                RESET_VERSION_KEY
            };

            return knownKeys;
        }
    }
}
