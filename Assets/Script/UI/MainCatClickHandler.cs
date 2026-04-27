using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HiddenCats.Core;
using HiddenCats.Interactable;

/// <summary>
/// 挂在 MainWnd 里猫咪 Image 上，处理「点击猫 → 弹气泡」的逻辑。
/// 未来可以接入 CollectionService / UnlockChecker，根据鱼是否找齐切换不同图标。
/// </summary>
public sealed class MainCatClickHandler : MonoBehaviour, IPointerClickHandler
{
    [Header("Hint Bubble Roots (use existing UI hierarchy)")]
    [Tooltip("未找完所有鱼时显示的气泡根节点（例如 CatHintBubble/FishIcon）")]
    [SerializeField] private GameObject fishBubbleRoot;

    [Tooltip("找完所有鱼后显示的气泡根节点（例如 CatHintBubble/HeartIcon）")]
    [SerializeField] private GameObject heartBubbleRoot;

    [Header("Bubble Settings")]
    [Tooltip("气泡停留时间（秒）")]
    [SerializeField] private float bubbleDuration = 1.5f;

    [Header("CatPop Animator (appear)")]
    [Tooltip("挂在 CatPop 上的 Animator。Controller 里应为两条出现动画各建一个 State，名称与下面两个字段一致（默认 CatHintBubble01 / CatHintBubble02），loopTime 关闭。")]
    [SerializeField] private Animator catPopAnimator;

    [Tooltip("未找齐鱼时点击猫，播放此 State（对应 CatHintBubble01 出现动画）。")]
    [SerializeField] private string fishBubbleAnimatorState = "CatHintBubble01";

    [Tooltip("找齐鱼后点击猫，播放此 State（对应 CatHintBubble02 出现动画）。")]
    [SerializeField] private string heartBubbleAnimatorState = "CatHintBubble02";

    [SerializeField] private int catPopAnimatorLayer = 0;

    [Header("Debug")]
    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool enableDebugLog = true;

    private Coroutine activeBubbleRoutine;

    private void OnEnable()
    {
        // 窗口重新显隐时，确保不会带着上一次的气泡状态回来。
        HideAllBubblesAndStopRoutine();
    }

    private void OnDisable()
    {
        // MainWnd 被 WindowManager 隐藏时，协程会被停掉，但气泡的 active 状态会被“记住”；
        // 这里手动清理，避免从 RoomWnd 回来后气泡还挂在那。
        HideAllBubblesAndStopRoutine();
    }

    private void Awake()
    {
        // Inspector 没配的话，尝试从层级里约定的节点自动取，优先使用现有的 CatHintBubble 子物体。
        if (fishBubbleRoot == null || heartBubbleRoot == null)
        {
            ResolveBubbleRootsFromHierarchy();
        }

        // 初始时都关掉，避免一开始就显示在场景里。
        if (fishBubbleRoot != null)
        {
            fishBubbleRoot.SetActive(false);
        }
        if (heartBubbleRoot != null)
        {
            heartBubbleRoot.SetActive(false);
        }

        if (catPopAnimator == null)
        {
            catPopAnimator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// 关闭所有气泡并停止当前计时协程。
    /// 供 OnEnable/OnDisable 等生命周期调用，避免跨窗口残留 UI。
    /// </summary>
    private void HideAllBubblesAndStopRoutine()
    {
        if (fishBubbleRoot != null)
        {
            fishBubbleRoot.SetActive(false);
        }

        if (heartBubbleRoot != null)
        {
            heartBubbleRoot.SetActive(false);
        }

        if (activeBubbleRoutine != null)
        {
            StopCoroutine(activeBubbleRoutine);
            activeBubbleRoutine = null;
        }
    }

    private void ResolveBubbleRootsFromHierarchy()
    {
        // 优先按常见路径找（MainWnd/Cat/CatHintBubble/FishIcon & HeartIcon）
        Transform bubbleRoot = transform.Find("CatHintBubble");
        if (bubbleRoot == null)
        {
            // 再兜底：在所有子节点里按名字找
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] == null)
                {
                    continue;
                }

                if (allChildren[i].name == "CatHintBubble")
                {
                    bubbleRoot = allChildren[i];
                    break;
                }
            }
        }

        if (bubbleRoot == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[MainCatClickHandler] CatHintBubble root not found in hierarchy.");
            }
            return;
        }

        if (fishBubbleRoot == null)
        {
            Transform fish = bubbleRoot.Find("FishIcon");
            if (fish != null)
            {
                fishBubbleRoot = fish.gameObject;
            }
        }

        if (heartBubbleRoot == null)
        {
            Transform heart = bubbleRoot.Find("HeartIcon");
            if (heart != null)
            {
                heartBubbleRoot = heart.gameObject;
            }
        }
    }

    private bool IsAllFishFound()
    {
        // 没有收集系统就直接认为「还没找完」，避免报错。
        if (CollectionService.Instance == null)
        {
            Debug.LogError("[MainCatClickHandler] CollectionService.Instance is null!");
            return false;
        }

        // 计算全局已经找到的鱼数量。
        int collectedFishCount = CollectionService.Instance.GetGlobalCount(CollectibleType.Fish);

        // 计算当前场景中鱼的数量（只统计 Prefab 中默认活跃的鱼）。
        // - activeSelf：排除 Prefab 里默认就是 Inactive 的鱼（只计入本局场景中真实存在的鱼）
        // 不使用 IsCollected 判断，因为 total 代表"场景中鱼的数量"，与收集进度无关。
        int totalFishCount = ResolveTotalFishCount();

        Debug.Log($"[MainCatClickHandler] IsAllFishFound: collected={collectedFishCount}, total={totalFishCount}");

        // 如果无法解析出总数，就保守地认为「还没找完」。
        if (totalFishCount <= 0)
        {
            return false;
        }

        // 当「已收集数量 >= 总数」时，认为所有鱼已经找完。
        return collectedFishCount >= totalFishCount;
    }

    private int ResolveTotalFishCount()
    {
        try
        {
            FishInteractable[] fishList = Object.FindObjectsOfType<FishInteractable>(true);
            if (fishList == null || fishList.Length == 0)
            {
                return 0;
            }

            var uniqueIds = new HashSet<string>();
            foreach (var fish in fishList)
            {
                if (fish == null)
                {
                    continue;
                }

                // 排除在 Prefab 里就被设置为 Inactive 的鱼（只计入本局场景中真实存在的鱼）。
                // 通过 InitiallyActiveInPrefab 判断，可以正确区分「Prefabrication 里默认就是 Inactive」和「被收集后 SetActive(false)」。
                if (!fish.InitiallyActiveInPrefab)
                {
                    continue;
                }

                // 优先使用 UniqueId；为空时退回到 InstanceID。
                string key = !string.IsNullOrEmpty(fish.UniqueId)
                    ? fish.UniqueId
                    : fish.GetInstanceID().ToString();

                uniqueIds.Add(key);
            }

            int total = uniqueIds.Count;

            Debug.Log($"[MainCatClickHandler] ResolveTotalFishCount: found {uniqueIds.Count} fish in scene");
            foreach (var id in uniqueIds)
            {
                Debug.Log($"  - {id}");
            }

            return total;
        }
        catch (System.Exception)
        {
            // 解析失败就返回 0，上层会认为「还没找完」。
            return 0;
        }
    }

    /// <summary>
    /// UI 点击回调（需要组件实现 IPointerClickHandler 接口）
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[MainCatClickHandler] OnPointerClick on '{gameObject.name}'");
            Debug.Log("[MainCatClickHandler] Cat clicked.");
        }

        bool allFishFound = IsAllFishFound();
        GameObject bubbleToShow = allFishFound ? heartBubbleRoot : fishBubbleRoot;

        if (bubbleToShow == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[MainCatClickHandler] bubbleToShow is null, skip showing bubble.");
            }
            return;
        }

        AudioManager.Instance?.PlayMainCatPopMeow();

        ShowBubbleForDuration(bubbleToShow, bubbleDuration);
    }

    /// <summary>
    /// 在指定时间内显示一个气泡 GameObject，之后自动隐藏。
    /// 只会有一个处于激活状态的气泡。
    /// </summary>
    private void ShowBubbleForDuration(GameObject bubbleRoot, float duration)
    {
        if (bubbleRoot == null)
        {
            return;
        }

        // 先把两个都关掉，再打开目标，保证不会重叠。
        if (fishBubbleRoot != null)
        {
            fishBubbleRoot.SetActive(false);
        }
        if (heartBubbleRoot != null)
        {
            heartBubbleRoot.SetActive(false);
        }

        bubbleRoot.SetActive(true);

        PlayCatPopBubbleAppearAnimation(bubbleRoot);

        if (activeBubbleRoutine != null)
        {
            StopCoroutine(activeBubbleRoutine);
        }

        float usedDuration = duration > 0f ? duration : 1.5f;
        activeBubbleRoutine = StartCoroutine(HideBubbleAfterDelay(bubbleRoot, usedDuration));
    }

    /// <summary>
    /// 在 CatPop 的 Animator 上从第 0 帧播放对应气泡的出现动画（与 Inspector 里两条 State 名一致）。
    /// </summary>
    private void PlayCatPopBubbleAppearAnimation(GameObject bubbleRoot)
    {
        if (catPopAnimator == null)
        {
            catPopAnimator = GetComponent<Animator>();
        }

        if (catPopAnimator == null || !catPopAnimator.isActiveAndEnabled)
        {
            return;
        }

        if (catPopAnimator.runtimeAnimatorController == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[MainCatClickHandler] CatPop Animator has no RuntimeAnimatorController; assign Cat.controller (with CatHintBubble01/02 states).");
            }

            return;
        }

        bool isHeart = heartBubbleRoot != null && bubbleRoot == heartBubbleRoot;
        string stateName = isHeart ? heartBubbleAnimatorState : fishBubbleAnimatorState;
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int layer = catPopAnimatorLayer;
        if (layer < 0 || layer >= catPopAnimator.layerCount)
        {
            layer = 0;
        }

        catPopAnimator.Play(stateName, layer, 0f);
        catPopAnimator.Update(0f);

        if (enableDebugLog)
        {
            Debug.Log($"[MainCatClickHandler] Animator.Play('{stateName}', layer={layer})");
        }
    }

    private System.Collections.IEnumerator HideBubbleAfterDelay(GameObject bubbleRoot, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bubbleRoot != null)
        {
            bubbleRoot.SetActive(false);
        }

        activeBubbleRoutine = null;
    }
}

