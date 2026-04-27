using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单一气泡提示组件（可复用）。
/// 挂在「可点击节点」上，点击后在附近显示一个气泡节点若干秒，然后自动隐藏。
/// </summary>
public sealed class SimpleBubbleHint : MonoBehaviour, IPointerClickHandler
{
    [Header("气泡节点（通常是子节点）")]
    [SerializeField] private GameObject bubbleRoot;

    [Header("气泡显示时长（秒）")]
    [SerializeField] private float duration = 1.5f;

    private Coroutine _activeRoutine;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bubbleRoot == gameObject)
        {
            Debug.LogWarning(
                $"{nameof(SimpleBubbleHint)} on '{name}': bubbleRoot 指向了自己。通常应把脚本挂在“可点击节点”上，bubbleRoot 指向“气泡节点”。",
                this);
        }

        // UGUI 点击依赖 Graphic + RaycastTarget（或 Button/Selectable 依赖的 targetGraphic）
        var g = GetComponent<Graphic>();
        if (g == null)
        {
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

    private void Awake()
    {
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
        ShowBubbleOnce();
    }

    /// <summary>
    /// 主动触发一次气泡显示（可从其它脚本调用）。
    /// </summary>
    public void ShowBubbleOnce()
    {
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

