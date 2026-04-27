using HiddenCats.Core;
using TMPro;
using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Controls the TimeBg node in each game scene (Room/Flower/Cafe).
    /// - Shows TimeBg when speedrun mode is active.
    /// - Hides TimeBg when not in speedrun mode.
    /// - Updates the TimeText (TMP) with the running timer from SpeedrunService.
    /// - Does not start a speedrun run here: <see cref="SpeedrunService.TryStartRun"/> is invoked from
    ///   <see cref="WindowManager"/> only when entering a gameplay window from the main menu (not when
    ///   switching Room/Flower/Cafe), so mid-run navigation does not reset collection.
    ///
    /// Attach this script to a PARENT of TimeBg (e.g. the game scene root
    /// or the NumUI root) so that its Update() keeps running even when
    /// TimeBg itself is hidden.
    ///
    /// Alternatively, you may attach it directly to TimeBg; in that case,
    /// set <see cref="timeBg"/> to null and the script will treat its own
    /// GameObject as the visual root.
    /// </summary>
    [AddComponentMenu("Hidden Cats/UI/Speedrun Timer UI")]
    public sealed class SpeedrunTimerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("TimeBg GameObject (holds the timer visuals). " +
                 "If null, the script uses its own GameObject.")]
        [SerializeField] private GameObject timeBg;

        [Tooltip("TimeText (TMP) that displays mm:ss. " +
                 "If null, the script searches children for a TMP_Text.")]
        [SerializeField] private TMP_Text timeText;

        private void Awake()
        {
            if (timeBg == null)
            {
                timeBg = gameObject;
            }

            if (timeText == null && timeBg != null)
            {
                timeText = timeBg.GetComponentInChildren<TMP_Text>(true);
            }

            if (timeText != null)
            {
                timeText.enableAutoSizing = true;
                timeText.fontSizeMin = 16f;
                timeText.fontSizeMax = 64f;
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool showTimer = SpeedrunService.Instance != null
                          && SpeedrunService.Instance.IsSpeedrunEnabled
                          && SpeedrunService.Instance.IsRunActive;

            // Show / hide TimeBg.
            if (timeBg != null && timeBg != gameObject)
            {
                if (timeBg.activeSelf != showTimer)
                    timeBg.SetActive(showTimer);
            }

            // Update time text.
            if (timeText == null)
                return;

            if (!showTimer)
            {
                timeText.text = "00 : 00";
                return;
            }

            float t = SpeedrunService.Instance.CurrentRunTimeSeconds;
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(t));
            timeText.text = FormatRunTime(totalSeconds);
        }

        /// <summary>
        /// Under 1h: "mm : ss". At 1h+ use compact "h:mm:ss" so digits stay shorter; TMP auto-sizing handles edge cases.
        /// </summary>
        private static string FormatRunTime(int totalSeconds)
        {
            if (totalSeconds < 3600)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes:00} : {seconds:00}";
            }

            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            return $"{h}:{m:00}:{s:00}";
        }
    }
}
