using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace HiddenCats.UI
{
    /// <summary>
    /// Confirmation popup dialog with Yes/No buttons.
    /// Can be used for any confirmation dialog in the game.
    /// </summary>
    public sealed class ConfirmationPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Default Message")]
        [TextArea(2, 4)]
        [SerializeField] private string defaultMessage = "Are you sure?";

        private Action _onConfirm;
        private Action _onCancel;

        private void Awake()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            if (yesButton != null)
            {
                yesButton.onClick.AddListener(OnYesClicked);
            }

            if (noButton != null)
            {
                noButton.onClick.AddListener(OnNoClicked);
            }
        }

        /// <summary>
        /// Show the confirmation popup with default message.
        /// </summary>
        /// <param name="onConfirm">Callback when user clicks Yes</param>
        /// <param name="onCancel">Optional callback when user clicks No</param>
        public void Show(Action onConfirm, Action onCancel = null)
        {
            Show(defaultMessage, onConfirm, onCancel);
        }

        /// <summary>
        /// Show the confirmation popup with custom message.
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="onConfirm">Callback when user clicks Yes</param>
        /// <param name="onCancel">Optional callback when user clicks No</param>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Hide the popup.
        /// </summary>
        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            _onConfirm = null;
            _onCancel = null;
        }

        private void OnYesClicked()
        {
            _onConfirm?.Invoke();
            Hide();
        }

        private void OnNoClicked()
        {
            _onCancel?.Invoke();
            Hide();
        }
    }
}
