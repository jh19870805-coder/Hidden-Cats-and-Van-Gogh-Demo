using HiddenCats.Core;
using TMPro;
using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Binds a <see cref="TMP_Text"/> to <see cref="LocalizationManager"/> using a string key.
    /// Refreshes on enable and on <see cref="LocalizationManager.OnLanguageChanged"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Key in LocalizationTable")]
        [SerializeField]
        private string textKey;

        [SerializeField]
        private TMP_Text target;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChangedHandler;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChangedHandler;
            }
        }

        private void OnLanguageChangedHandler(string _)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (target == null)
            {
                target = GetComponent<TMP_Text>();
            }

            if (target == null)
            {
                return;
            }

            if (LocalizationManager.Instance == null)
            {
                target.text = string.IsNullOrEmpty(textKey) ? string.Empty : textKey;
                return;
            }

            target.text = LocalizationManager.Instance.GetText(textKey);
        }

        /// <summary>Runtime key change (optional).</summary>
        public void SetKey(string key)
        {
            textKey = key;
            Refresh();
        }
    }
}
