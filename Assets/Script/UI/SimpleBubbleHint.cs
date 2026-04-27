using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using HiddenCats.Core;

/// <summary>
/// 单一气泡提示组件（可复用）。
/// 挂在「可点击节点」上，点击后在附近显示一个气泡节点若干秒，然后自动隐藏。
/// </summary>
public sealed class SimpleBubbleHint : MonoBehaviour, IPointerClickHandler
{
    [Header("气泡节点（通常是子节点）")]
    [SerializeField] private GameObject bubbleRoot;

    [Header("点击触发（可选）")]
    [Tooltip("如果该组件需要响应 UI 点击（IPointerClickHandler），则需要同节点上有 Graphic 且勾选 Raycast Target，或有 Button。若仅通过代码调用 ShowTextOnce()/ShowBubbleOnce() 触发，可关闭以避免 OnValidate 警告。")]
    [SerializeField] private bool requiresUIClickTarget = true;

    [Header("气泡显示时长（秒）")]
    [SerializeField] private float duration = 1.5f;

    private Coroutine _activeRoutine;

    /// <summary>
    /// 如果没在 Inspector 里显式指定 bubbleRoot，则在运行时做一次“最佳猜测”：
    /// - 若当前节点只有一个子节点，则自动把该子节点当作气泡根节点。
    /// 这样像 CompletingTheRacingModePop 这类简单结构，就不需要额外手动配置也能正常显示气泡。
    /// </summary>
    private void EnsureBubbleRootAssignedIfPossible()
    {
        if (bubbleRoot != null)
        {
            return;
        }

        // 简单兜底规则：只有一个子节点时，自动使用该子节点作为气泡节点。
        if (transform.childCount == 1)
        {
            bubbleRoot = transform.GetChild(0)?.gameObject;
#if UNITY_EDITOR
            Debug.LogWarning(
                $"{nameof(SimpleBubbleHint)} on '{name}': bubbleRoot 未指定，已自动使用唯一子节点 '{bubbleRoot?.name}' 作为气泡节点。如需更精细控制，请在 Inspector 中手动指定 bubbleRoot。",
                this);
#endif
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bubbleRoot == gameObject)
        {
            Debug.LogWarning(
                $"{nameof(SimpleBubbleHint)} on '{name}': bubbleRoot 指向了自己。通常应把脚本挂在“可点击节点”上，bubbleRoot 指向“气泡节点”。",
                this);
        }

        if (!requiresUIClickTarget)
        {
            return;
        }

        // UGUI 点击依赖 Graphic + RaycastTarget（或 Button/Selectable 依赖的 targetGraphic）
        var g = GetComponent<Graphic>();
        if (g == null)
        {
            // 允许使用 Button 且 targetGraphic 在子节点上，但依然提醒一下常见踩坑
            var btn = GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning(
                    $"{nameof(SimpleBubbleHint)} on '{name}': 未发现 Graphic/Button。若这是 UI 点击，请给该节点加 Image(可透明) 并勾选 Raycast Target，或加 Button。",
                    this);
            }
        }
        else if (!g.raycastTarget)
        {
            Debug.LogWarning(
                $"{nameof(SimpleBubbleHint)} on '{name}': Graphic 的 Raycast Target 未勾选，可能收不到点击。",
                this);
        }
    }
#endif


    private void EnsureUIClickTargetExists()
    {
        if (!requiresUIClickTarget)
        {
            return;
        }

        if (GetComponent<Graphic>() != null || GetComponent<Button>() != null)
        {
            return;
        }

        var image = gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }

    private void Awake()
    {
        EnsureBubbleRootAssignedIfPossible();
        EnsureUIClickTargetExists();

        // 兜底：如果忘记在场景里关掉，启动时也强制关闭
        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 当所属界面/节点被隐藏或切换时，强制收起气泡并停止计时协程
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }
    }

    /// <summary>
    /// IPointerClickHandler 回调：点击当前节点时触发一次气泡。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 如果组件被禁用，不显示气泡
        if (!enabled)
        {
            return;
        }

        ShowBubbleOnce();
    }

    /// <summary>
    /// Show bubble once and (best-effort) set its text content (TMP preferred, fallback to UGUI Text).
    /// Useful for unlock/toast messages configured in code.
    /// </summary>
    public void ShowTextOnce(string message)
    {
        EnsureBubbleRootAssignedIfPossible();

        // 允许在对象默认 inactive 的情况下被外部显式调用显示（例如 CompletingTheRacingModePop）
        // 否则 StartCoroutine 会报：Coroutine couldn't be started because the game object is inactive
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        if (!enabled)
        {
            enabled = true;
        }

        if (bubbleRoot == null)
        {
            Debug.LogWarning($"{nameof(SimpleBubbleHint)} on {name} has no bubbleRoot assigned.");
            return;
        }

        // Try TMP first
        TMP_Text tmp = bubbleRoot.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = message ?? string.Empty;
        }
        else
        {
            // Fallback to UGUI Text
            Text uiText = bubbleRoot.GetComponentInChildren<Text>(true);
            if (uiText != null)
            {
                uiText.text = message ?? string.Empty;
            }
        }

        ShowBubbleOnce();
    }

    /// <summary>
    /// 主动触发一次气泡显示（可从其它脚本调用）。
    /// </summary>
    public void ShowBubbleOnce()
    {
        EnsureBubbleRootAssignedIfPossible();

        // 允许在对象默认 inactive 的情况下被外部显式调用显示（例如 CompletingTheRacingModePop）
        // 否则 StartCoroutine 会报：Coroutine couldn't be started because the game object is inactive
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        if (!enabled)
        {
            enabled = true;
        }

        if (bubbleRoot == null)
        {
            Debug.LogWarning($"{nameof(SimpleBubbleHint)} on {name} has no bubbleRoot assigned.");
            return;
        }

        bubbleRoot.SetActive(true);

        // 如已有计时协程，先停掉，再重新开始一轮
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
        }

        _activeRoutine = StartCoroutine(BubbleLifeRoutine());
    }

    /// <summary>
    /// 立即隐藏气泡并停止计时协程（可从其它脚本调用）。
    /// </summary>
    public void HideBubbleImmediately()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }
    }

    private IEnumerator BubbleLifeRoutine()
    {
        // 最小兜底，防止 duration 配成 0 或负数导致协程立刻结束又被频繁开启
        var waitTime = duration > 0f ? duration : 0.01f;
        yield return new WaitForSeconds(waitTime);

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }

        _activeRoutine = null;
    }
}

