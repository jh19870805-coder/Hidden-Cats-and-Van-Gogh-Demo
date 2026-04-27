using System;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// Simple modal dialog service.
    /// Intended to work with a generic dialog prefab that has:
    /// - optional title Text
    /// - body Text
    /// - confirm Button (with Text or icon)
    /// - cancel Button (with Text or icon)
    ///
    /// A HelpTipsPanel 可以作为一个 Info 类型 Dialog 的默认皮肤，
    /// 后续其他窗口（门上的小贴士、设置重置二次确认等）也可以复用该服务。
    /// </summary>
    public sealed class DialogService : MonoBehaviour
    {
        public static DialogService Instance { get; private set; }

        [Header("Dialog Prefab")]
        [Tooltip("Generic dialog prefab with title/body/buttons. Root must be a RectTransform.")]
        [SerializeField] private RectTransform dialogPrefab;

        [Header("Optional Overlay Mask")]
        [Tooltip("Optional full-screen mask behind dialog. If null, no extra mask is created.")]
        [SerializeField] private GameObject overlayMaskPrefab;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = false;

        private GameObject _activeMask;
        private RectTransform _activeDialog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[DialogService] Duplicate instance detected, destroying this one.");
                }
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        #region Public API

        public enum DialogType
        {
            Info,
            ConfirmCancel
        }

        public struct DialogRequest
        {
            public DialogType type;
            public string title;
            public string body;
            public string confirmText;
            public string cancelText;
            public Action onConfirm;
            public Action onCancel;
        }

        public static void Show(DialogRequest request)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[DialogService] Instance is null. Please ensure one exists in the scene.");
                return;
            }

            Instance.InternalShow(request);
        }

        public static void ShowInfo(string title, string body)
        {
            Show(new DialogRequest
            {
                type = DialogType.Info,
                title = title,
                body = body
            });
        }

        public static void ShowConfirmCancel(string title, string body, string confirmText, string cancelText, Action onConfirm, Action onCancel = null)
        {
            Show(new DialogRequest
            {
                type = DialogType.ConfirmCancel,
                title = title,
                body = body,
                confirmText = confirmText,
                cancelText = cancelText,
                onConfirm = onConfirm,
                onCancel = onCancel
            });
        }

        #endregion

        #region Internal

        private void InternalShow(DialogRequest request)
        {
            if (dialogPrefab == null)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[DialogService] Dialog prefab is not assigned.");
                }
                return;
            }

            // Close previous dialog if any
            InternalHide();

            if (overlayMaskPrefab != null)
            {
                _activeMask = Instantiate(overlayMaskPrefab, transform);
                _activeMask.SetActive(true);
            }

            _activeDialog = Instantiate(dialogPrefab, transform);
            _activeDialog.gameObject.SetActive(true);

            WireDialogUI(_activeDialog, request);

            if (enableDebugLog)
            {
                Debug.Log($"[DialogService] Show dialog type={request.type}, title={request.title}");
            }
        }

        private void WireDialogUI(RectTransform dialogRoot, DialogRequest request)
        {
            // 约定：对话框预制体上可以有可选的 Title 文本、Body 文本、Confirm 按钮、Cancel 按钮。
            Text[] texts = dialogRoot.GetComponentsInChildren<Text>(true);
            Button[] buttons = dialogRoot.GetComponentsInChildren<Button>(true);

            Text titleText = null;
            Text bodyText = null;
            Button confirmButton = null;
            Button cancelButton = null;

            // 通过名字做一个宽松的匹配，方便在不同预制体中复用
            foreach (var t in texts)
            {
                string lower = t.name.ToLowerInvariant();
                if (titleText == null && lower.Contains("title"))
                {
                    titleText = t;
                }
                else if (bodyText == null && (lower.Contains("body") || lower.Contains("content") || lower.Contains("text")))
                {
                    bodyText = t;
                }
            }

            foreach (var b in buttons)
            {
                string lower = b.name.ToLowerInvariant();
                if (confirmButton == null && (lower.Contains("ok") || lower.Contains("confirm") || lower.Contains("yes")))
                {
                    confirmButton = b;
                }
                else if (cancelButton == null && (lower.Contains("cancel") || lower.Contains("close") || lower.Contains("no")))
                {
                    cancelButton = b;
                }
            }

            if (titleText != null)
            {
                titleText.text = request.title ?? string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = request.body ?? string.Empty;
            }

            // Info: 只需要一个关闭按钮（可以用确认按钮或整个对话点击关闭）
            if (request.type == DialogType.Info)
            {
                if (confirmButton != null)
                {
                    confirmButton.onClick.RemoveAllListeners();
                    confirmButton.onClick.AddListener(() =>
                    {
                        request.onConfirm?.Invoke();
                        InternalHide();
                    });

                    if (confirmButton.GetComponentInChildren<Text>() != null && !string.IsNullOrEmpty(request.confirmText))
                    {
                        confirmButton.GetComponentInChildren<Text>().text = request.confirmText;
                    }
                }

                if (cancelButton != null)
                {
                    cancelButton.onClick.RemoveAllListeners();
                    cancelButton.onClick.AddListener(() =>
                    {
                        request.onCancel?.Invoke();
                        InternalHide();
                    });

                    if (cancelButton.GetComponentInChildren<Text>() != null && !string.IsNullOrEmpty(request.cancelText))
                    {
                        cancelButton.GetComponentInChildren<Text>().text = request.cancelText;
                    }
                }
            }
            else // ConfirmCancel
            {
                if (confirmButton != null)
                {
                    confirmButton.onClick.RemoveAllListeners();
                    confirmButton.onClick.AddListener(() =>
                    {
                        request.onConfirm?.Invoke();
                        InternalHide();
                    });

                    var label = confirmButton.GetComponentInChildren<Text>();
                    if (label != null && !string.IsNullOrEmpty(request.confirmText))
                    {
                        label.text = request.confirmText;
                    }
                }

                if (cancelButton != null)
                {
                    cancelButton.onClick.RemoveAllListeners();
                    cancelButton.onClick.AddListener(() =>
                    {
                        request.onCancel?.Invoke();
                        InternalHide();
                    });

                    var label = cancelButton.GetComponentInChildren<Text>();
                    if (label != null && !string.IsNullOrEmpty(request.cancelText))
                    {
                        label.text = request.cancelText;
                    }
                }
            }
        }

        private void InternalHide()
        {
            if (_activeDialog != null)
            {
                Destroy(_activeDialog.gameObject);
                _activeDialog = null;
            }

            if (_activeMask != null)
            {
                Destroy(_activeMask);
                _activeMask = null;
            }
        }

        #endregion
    }
}

