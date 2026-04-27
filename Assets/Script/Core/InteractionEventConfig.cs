using System;
using UnityEngine;

namespace HiddenCats.Core
{
    /// <summary>
    /// 高层级的交互事件类型（与具体输入无关）。
    /// 例如：点击、收集、完成、特殊事件等。
    /// </summary>
    public enum InteractionEventType
    {
        /// <summary>
        /// 普通点击（未必完成收集）。
        /// </summary>
        Click = 0,
        
        /// <summary>
        /// 发生收集行为时（如点击后开始收集）。
        /// </summary>
        Collect = 1,
        
        /// <summary>
        /// 完成整个交互流程时（如隐藏猫三段交互的最后一步）。
        /// </summary>
        Complete = 2,
        
        /// <summary>
        /// 用于特殊剧情、开窗、弹窗等自定义行为。
        /// </summary>
        Special = 3
    }

    /// <summary>
    /// 交互事件触发时机。
    /// 通常与内部逻辑阶段对应，例如：点击瞬间、开始收集、收集完成、延迟触发等。
    /// </summary>
    public enum EventTriggerTiming
    {
        /// <summary>
        /// 立即触发（调用方一进入对应阶段就触发）。
        /// </summary>
        Immediate = 0,
        
        /// <summary>
        /// 在收集开始时触发。
        /// </summary>
        OnCollectStart = 1,
        
        /// <summary>
        /// 在收集完成时触发。
        /// </summary>
        OnCollectComplete = 2,
        
        /// <summary>
        /// 在动画或流程全部完成后触发。
        /// </summary>
        OnFlowComplete = 3,
        
        /// <summary>
        /// 延迟一定时间后触发。
        /// </summary>
        Delayed = 4
    }

    /// <summary>
    /// 通用交互事件配置：
    /// - 事件类型（高层语义：点击、收集、完成等）
    /// - 触发时机（点击时、收集时、完成时、延迟触发等）
    /// - 目标 GameObject（可用于激活/关闭、播放动画等）
    /// - 自定义参数（字符串形式，方便在 UnityEvent 中使用）
    /// </summary>
    [Serializable]
    public class EventConfiguration
    {
        [Header("Basic")]
        [Tooltip("该交互对应的事件类型（点击、收集、完成、特殊事件等）")]
        public InteractionEventType eventType = InteractionEventType.Click;

        [Tooltip("事件触发的时间点（点击时、收集时、完成时、延迟触发等）")]
        public EventTriggerTiming triggerTiming = EventTriggerTiming.Immediate;

        [Header("Target")]
        [Tooltip("事件相关的目标 GameObject（可选），例如需要激活/关闭的窗口、特效等")]
        public GameObject targetObject;

        [Tooltip("是否在触发时切换目标物体的激活状态（active = !active）")]
        public bool toggleTargetActive;

        [Tooltip("触发时是否强制设为 Active（优先级高于 toggleTargetActive）")]
        public bool setTargetActive = true;

        [Tooltip("如果使用延迟触发（EventTriggerTiming.Delayed），在触发前等待的秒数")]
        public float delaySeconds = 0f;

        [Header("Parameters")]
        [Tooltip("可选的字符串参数，例如用于 AudioManager 的音效名、MessagePopup 的文本 Key 等")]
        public string stringParameter;

        [Header("Callbacks")]
        [Tooltip("最终触发时调用的 UnityEvent，可在 Inspector 中配置任何自定义行为")]
        public UnityEngine.Events.UnityEvent onEventTriggered;

        /// <summary>
        /// 由外部交互组件在合适的阶段调用。
        /// </summary>
        public void Trigger(MonoBehaviour owner)
        {
            if (owner == null)
            {
                // 缺少 MonoBehaviour 时也可以同步触发，但无法使用协程与延迟。
                ExecuteImmediate();
                return;
            }

            if (triggerTiming == EventTriggerTiming.Delayed && delaySeconds > 0f)
            {
                owner.StartCoroutine(TriggerDelayed(delaySeconds));
            }
            else
            {
                ExecuteImmediate();
            }
        }

        private System.Collections.IEnumerator TriggerDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            ExecuteImmediate();
        }

        private void ExecuteImmediate()
        {
            // 先处理目标物体激活逻辑
            if (targetObject != null)
            {
                if (toggleTargetActive)
                {
                    targetObject.SetActive(!targetObject.activeSelf);
                }
                else
                {
                    targetObject.SetActive(setTargetActive);
                }
            }

            // 再触发 UnityEvent，方便在 Inspector 中追加任意行为
            onEventTriggered?.Invoke();
        }
    }
}

