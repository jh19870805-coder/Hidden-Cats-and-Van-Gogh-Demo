using System;
using HiddenCats.UI;
using HiddenCats.Core;
using UnityEngine;

/// <summary>
/// Attach this to a GameObject in the RoomWnd prefab.
/// Handles navigation from RoomWnd back to MainWnd.
/// </summary>
public sealed class RoomWndUI : MonoBehaviour
{
    [Header("Small Game Entry (Legacy)")]
    [Tooltip("Image for the small game entry button - kept for display purposes")]
    [SerializeField] private UnityEngine.UI.Image smallGameEntryImage;

    private void Start()
    {
        if (smallGameEntryImage != null && !smallGameEntryImage.gameObject.activeSelf)
        {
            smallGameEntryImage.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        GameProgressResetService.OnGameProgressReset -= HandleGameProgressReset;
    }

    private void HandleGameProgressReset()
    {
    }

    public void OnClick_BackToMain()
    {
        if (WindowManager.Instance == null)
        {
            Debug.LogError("[RoomWndUI] WindowManager.Instance is null.");
            return;
        }

        AudioManager.PlayCommon02();
        WindowManager.Instance.ShowMainWindow();
    }
}
