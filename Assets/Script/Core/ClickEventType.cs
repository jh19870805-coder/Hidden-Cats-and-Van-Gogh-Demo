namespace HiddenCats.Core
{
    /// <summary>
    /// Types of click events that can be triggered.
    /// </summary>
    public enum ClickEventType
    {
        /// <summary>
        /// Triggered when the click is detected (on pointer down).
        /// </summary>
        OnClick,
        
        /// <summary>
        /// Triggered when pointer enters the object.
        /// </summary>
        OnPointerEnter,
        
        /// <summary>
        /// Triggered when pointer exits the object.
        /// </summary>
        OnPointerExit,
        
        /// <summary>
        /// Triggered when pointer is pressed down.
        /// </summary>
        OnPointerDown,
        
        /// <summary>
        /// Triggered when pointer is released.
        /// </summary>
        OnPointerUp
    }
}
