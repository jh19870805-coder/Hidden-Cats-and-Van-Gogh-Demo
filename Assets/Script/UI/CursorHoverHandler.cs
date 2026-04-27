using UnityEngine;
using UnityEngine.EventSystems;
using HiddenCats.Core;

/// <summary>
/// 自动处理鼠标悬停时的光标切换。
/// 将此脚本添加到需要光标切换效果的 UI 元素上（如按钮）。
/// </summary>
public sealed class CursorHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Cursor Settings")]
    [Tooltip("是否在悬停时切换到 MouseX2 光标")]
    [SerializeField] private bool useHoverCursor = true;

    [Tooltip("是否在按下时切换到 MouseX2 光标")]
    [SerializeField] private bool useClickCursor = true;

    private bool _isHovering = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        if (useHoverCursor && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetLargeCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetNormalCursor();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (useClickCursor && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetLargeCursor();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 如果鼠标还在按钮上，保持悬停光标；否则恢复普通光标
        if (CursorManager.Instance != null)
        {
            if (_isHovering && useHoverCursor)
            {
                CursorManager.Instance.SetLargeCursor();
            }
            else
            {
                CursorManager.Instance.SetNormalCursor();
            }
        }
    }

    private void OnDisable()
    {
        // 当对象被禁用时，恢复普通光标
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetNormalCursor();
        }
    }
}
