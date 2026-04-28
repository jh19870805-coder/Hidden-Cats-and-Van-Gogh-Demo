using System;
using System.Collections;
using HiddenCats.UI;
using HiddenCats.Core;
using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to a GameObject in the RoomWnd prefab.
/// Handles navigation from RoomWnd back to MainWnd.
/// </summary>
public sealed class RoomWndUI : MonoBehaviour
{
    [Header("Small Game Entry (Legacy)")]
    [Tooltip("Image for the small game entry button - kept for display purposes")]
    [SerializeField] private UnityEngine.UI.Image smallGameEntryImage;

    [Header("Flower Button")]
    [Tooltip("Flower bubble root - shows when FlowerBtn is clicked")]
    [SerializeField] private GameObject flowerBubbleRoot;

    [Tooltip("Duration in seconds before the flower bubble auto-hides")]
    [SerializeField] private float flowerBubbleDuration = 1.5f;

    private Coroutine _flowerBubbleRoutine;

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

        // 强制停止定时器并隐藏气泡，和 SmallGameHintBubbleRoot 一样
        if (_flowerBubbleRoutine != null)
        {
            StopCoroutine(_flowerBubbleRoutine);
            _flowerBubbleRoutine = null;
        }

        if (flowerBubbleRoot != null)
        {
            flowerBubbleRoot.SetActive(false);
        }
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

    public void OnClick_EnterFlower()
    {
        if (flowerBubbleRoot == null)
        {
            return;
        }

        // 如果已有定时器在运行，先停止它
        if (_flowerBubbleRoutine != null)
        {
            StopCoroutine(_flowerBubbleRoutine);
            _flowerBubbleRoutine = null;
        }

        // 显示气泡
        flowerBubbleRoot.SetActive(true);

        // 启动新的定时器协程
        _flowerBubbleRoutine = StartCoroutine(FlowerBubbleLifeRoutine());
    }

    private IEnumerator FlowerBubbleLifeRoutine()
    {
        // 等待指定时间
        float waitTime = flowerBubbleDuration > 0f ? flowerBubbleDuration : 0.01f;
        yield return new WaitForSeconds(waitTime);

        // 自动隐藏气泡
        if (flowerBubbleRoot != null)
        {
            flowerBubbleRoot.SetActive(false);
        }

        _flowerBubbleRoutine = null;
    }
}
