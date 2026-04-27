using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HiddenCats.UI
{
    /// <summary>
    /// Simple message popup for showing unlock hints and other messages.
    /// Supports multi-language (text key lookup) - for now uses direct text assignment.
    /// </summary>
    public sealed class MessagePopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button closeButton;

        [Header("Message Content")]
        [TextArea(3, 6)]
        [SerializeField] private string unlockHintText = "You need to find all puzzle pieces to unlock this feature.";

        private void Awake()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
        }

        /// <summary>
        /// Show the popup with default message.
        /// </summary>
        public void Show()
        {
            Show(unlockHintText);
        }

        /// <summary>
        /// Show the popup with custom message.
        /// </summary>
        public void Show(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

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
        }

        /// <summary>
        /// Set the default message text (for multi-language support, this can be replaced with key lookup later).
        /// </summary>
        public void SetMessageText(string text)
        {
            unlockHintText = text;
        }
    }
}
