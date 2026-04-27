using UnityEngine;
using System;

namespace HiddenCats.Core
{
    /// <summary>
    /// Configuration for click events.
    /// Allows configuring which event types to trigger and when.
    /// </summary>
    [Serializable]
    public class ClickEventConfig
    {
        [Header("Event Types")]
        [Tooltip("Enable click event (OnClick)")]
        public bool enableOnClick = true;
        
        [Tooltip("Enable pointer enter event")]
        public bool enableOnPointerEnter = false;
        
        [Tooltip("Enable pointer exit event")]
        public bool enableOnPointerExit = false;
        
        [Tooltip("Enable pointer down event")]
        public bool enableOnPointerDown = false;
        
        [Tooltip("Enable pointer up event")]
        public bool enableOnPointerUp = false;

        [Header("Event Callbacks")]
        [Tooltip("Callback triggered when click event occurs")]
        public UnityEngine.Events.UnityEvent onClickEvent;
        
        [Tooltip("Callback triggered when pointer enters")]
        public UnityEngine.Events.UnityEvent onPointerEnterEvent;
        
        [Tooltip("Callback triggered when pointer exits")]
        public UnityEngine.Events.UnityEvent onPointerExitEvent;
        
        [Tooltip("Callback triggered when pointer is pressed down")]
        public UnityEngine.Events.UnityEvent onPointerDownEvent;
        
        [Tooltip("Callback triggered when pointer is released")]
        public UnityEngine.Events.UnityEvent onPointerUpEvent;

        /// <summary>
        /// Check if a specific event type is enabled.
        /// </summary>
        public bool IsEventTypeEnabled(ClickEventType eventType)
        {
            return eventType switch
            {
                ClickEventType.OnClick => enableOnClick,
                ClickEventType.OnPointerEnter => enableOnPointerEnter,
                ClickEventType.OnPointerExit => enableOnPointerExit,
                ClickEventType.OnPointerDown => enableOnPointerDown,
                ClickEventType.OnPointerUp => enableOnPointerUp,
                _ => false
            };
        }

        /// <summary>
        /// Trigger the UnityEvent for a specific event type.
        /// </summary>
        public void TriggerEvent(ClickEventType eventType)
        {
            if (!IsEventTypeEnabled(eventType))
                return;

            switch (eventType)
            {
                case ClickEventType.OnClick:
                    onClickEvent?.Invoke();
                    break;
                case ClickEventType.OnPointerEnter:
                    onPointerEnterEvent?.Invoke();
                    break;
                case ClickEventType.OnPointerExit:
                    onPointerExitEvent?.Invoke();
                    break;
                case ClickEventType.OnPointerDown:
                    onPointerDownEvent?.Invoke();
                    break;
                case ClickEventType.OnPointerUp:
                    onPointerUpEvent?.Invoke();
                    break;
            }
        }
    }
}
