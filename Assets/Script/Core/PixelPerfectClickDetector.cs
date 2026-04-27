using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;

namespace HiddenCats.Core
{
    /// <summary>
    /// Component that provides pixel-perfect click detection for images with transparency.
    /// Only triggers events when clicking on non-transparent pixels of the image.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Hidden Cats/Pixel Perfect Click Detector")]
    public class PixelPerfectClickDetector : MonoBehaviour, 
        IPointerClickHandler, 
        IPointerEnterHandler, 
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [Header("Click Detection Settings")]
        [Tooltip("Enable pixel-perfect transparency detection")]
        [SerializeField] private bool enablePixelPerfectDetection = true;
        
        [Tooltip("Alpha threshold for considering a pixel as opaque (0-1). Pixels with alpha below this value are considered transparent.")]
        [Range(0f, 1f)]
        [SerializeField] private float alphaThreshold = 0.1f;
        
        [Tooltip("Enable debug logging for click detection")]
        [SerializeField] private bool enableDebugLog = false;

        [Header("Event Configuration")]
        [SerializeField] private ClickEventConfig eventConfig = new ClickEventConfig();

        [Header("Advanced Settings")]
        [Tooltip("Cache the texture data for better performance. Enable if the sprite doesn't change at runtime.")]
        [SerializeField] private bool cacheTextureData = true;
        
        [Tooltip("Enable fallback click detection when object is blocked by other UI elements. This will check clicks directly even if EventSystem doesn't call OnPointerClick.")]
        [SerializeField] private bool enableFallbackDetection = true;

        // Cached components
        private Image _image;
        private RectTransform _rectTransform;
        private Canvas _canvas;
        
        // Cached texture data for performance
        private Texture2D _cachedTexture;
        private Sprite _cachedSprite;
        private bool _isTextureReadable;

        // Events
        public event Action OnClickDetected;
        public event Action OnPointerEnterDetected;
        public event Action OnPointerExitDetected;
        public event Action OnPointerDownDetected;
        public event Action OnPointerUpDetected;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            
            Debug.Log($"[PixelPerfectClickDetector] Awake called on {gameObject.name}, Image: {(_image != null ? "found" : "null")}, Canvas: {(_canvas != null ? "found" : "null")}");
            
            if (_image == null)
            {
                Debug.LogError($"[PixelPerfectClickDetector] Image component not found on {gameObject.name}");
                enabled = false;
                return;
            }

            if (_canvas == null)
            {
                Debug.LogError($"[PixelPerfectClickDetector] Canvas not found in parent hierarchy of {gameObject.name}");
                enabled = false;
                return;
            }

            // Check EventSystem
            bool hasEventSystem = EventSystem.current != null;
            var graphicRaycaster = _canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            bool hasGraphicRaycaster = graphicRaycaster != null;
            
            // Get Canvas details
            string canvasInfo = $"Canvas: {_canvas.name}, RenderMode: {_canvas.renderMode}, SortingOrder: {_canvas.sortingOrder}, Enabled: {_canvas.enabled}, ReceivesEvents: {_canvas.enabled}";
            string rectInfo = $"Rect: {_rectTransform.rect}, Position: {_rectTransform.position}, AnchoredPosition: {_rectTransform.anchoredPosition}";
            string imageInfo = $"Image Enabled: {_image.enabled}, Color: {_image.color}, Material: {(_image.material != null ? _image.material.name : "null")}";
            
            // Check hierarchy and sibling index
            int siblingIndex = transform.GetSiblingIndex();
            Transform parent = transform.parent;
            int parentChildCount = parent != null ? parent.childCount : 0;
            
            Debug.Log($"[PixelPerfectClickDetector] Initialized on {gameObject.name}, Sprite: {(_image.sprite != null ? _image.sprite.name : "null")}, RaycastTarget: {_image.raycastTarget}, EventSystem: {hasEventSystem}, GraphicRaycaster: {hasGraphicRaycaster}");
            Debug.Log($"[PixelPerfectClickDetector] {canvasInfo}");
            Debug.Log($"[PixelPerfectClickDetector] {rectInfo}");
            Debug.Log($"[PixelPerfectClickDetector] {imageInfo}");
            Debug.Log($"[PixelPerfectClickDetector] Hierarchy: SiblingIndex: {siblingIndex}/{parentChildCount}, Parent: {(parent != null ? parent.name : "null")}");
            
            if (graphicRaycaster != null)
            {
                Debug.Log($"[PixelPerfectClickDetector] GraphicRaycaster - IgnoreReversedGraphics: {graphicRaycaster.ignoreReversedGraphics}, BlockingObjects: {graphicRaycaster.blockingObjects}");
            }

            if (!hasEventSystem)
            {
                Debug.LogWarning($"[PixelPerfectClickDetector] No EventSystem found in scene! Click events may not work on {gameObject.name}");
            }

            if (!hasGraphicRaycaster)
            {
                Debug.LogWarning($"[PixelPerfectClickDetector] No GraphicRaycaster found on Canvas! Click events may not work on {gameObject.name}");
            }

            // Cache texture data if enabled
            if (cacheTextureData && _image.sprite != null)
            {
                CacheTextureData();
            }
        }

        private void OnEnable()
        {
            // Re-cache texture data when enabled (in case sprite changed)
            if (cacheTextureData && _image.sprite != null)
            {
                CacheTextureData();
            }
        }

        private void Update()
        {
            // Fallback click detection: Check if mouse is being clicked and if this object should receive it
            // This works even when the object is blocked by other UI elements
            if (enableFallbackDetection && Input.GetMouseButtonDown(0) && gameObject.activeInHierarchy)
            {
                Vector2 mousePos = Input.mousePosition;
                
                // Check if click is within rect bounds
                if (_rectTransform != null && _canvas != null)
                {
                    // Get the correct camera for coordinate conversion
                    Camera cam = null;
                    if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
                    {
                        cam = _canvas.worldCamera ?? Camera.main;
                    }
                    // For ScreenSpaceOverlay, camera should be null
                    
                    Vector2 localPoint;
                    bool conversionSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _rectTransform, 
                        mousePos, 
                        cam, 
                        out localPoint);
                    
                    Rect rect = _rectTransform.rect;
                    bool containsPoint = conversionSuccess && rect.Contains(localPoint);
                    
                    // If click is within bounds, check if it hits an opaque pixel
                    if (containsPoint && IsClickOnOpaquePixel(mousePos))
                    {
                        // Check if EventSystem would have called OnPointerClick
                        bool eventSystemWouldCall = false;
                        if (EventSystem.current != null)
                        {
                            var pointerEventData = new PointerEventData(EventSystem.current)
                            {
                                position = mousePos
                            };
                            var results = new System.Collections.Generic.List<RaycastResult>();
                            EventSystem.current.RaycastAll(pointerEventData, results);
                            
                            // Check if this object is in the results
                            for (int i = 0; i < results.Count; i++)
                            {
                                if (results[i].gameObject == gameObject || results[i].gameObject.transform.IsChildOf(transform))
                                {
                                    eventSystemWouldCall = true;
                                    break;
                                }
                            }
                        }
                        
                        // If EventSystem didn't call OnPointerClick (object is blocked), trigger events manually
                        if (!eventSystemWouldCall)
                        {
                            Debug.Log($"[PixelPerfectClickDetector] 🔧 Fallback detection: Click detected on {gameObject.name} but object is blocked. Triggering events manually.");
                            
                            // Trigger events manually
                            OnClickDetected?.Invoke();
                            eventConfig.TriggerEvent(ClickEventType.OnClick);
                            
                            // Also trigger pointer down/up for consistency
                            OnPointerDownDetected?.Invoke();
                            eventConfig.TriggerEvent(ClickEventType.OnPointerDown);
                            
                            // Use a coroutine or delayed call for pointer up
                            StartCoroutine(TriggerPointerUpDelayed());
                        }
                    }
                }
            }
            
            // Debug: Check if mouse is being clicked and if this object should receive it
            // Only check if this GameObject is active (inactive objects can't receive events anyway)
            if (Input.GetMouseButtonDown(0) && gameObject.activeInHierarchy)
            {
                Vector2 mousePos = Input.mousePosition;
                Debug.Log($"[PixelPerfectClickDetector] Mouse clicked at {mousePos} on frame {Time.frameCount}");
                
                // Check if click is within rect bounds
                if (_rectTransform != null && _canvas != null)
                {
                    // Get the correct camera for coordinate conversion
                    Camera cam = null;
                    if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
                    {
                        cam = _canvas.worldCamera ?? Camera.main;
                    }
                    // For ScreenSpaceOverlay, camera should be null
                    
                    Vector2 localPoint;
                    bool conversionSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _rectTransform, 
                        mousePos, 
                        cam, 
                        out localPoint);
                    
                    Rect rect = _rectTransform.rect;
                    bool containsPoint = conversionSuccess && rect.Contains(localPoint);
                    
                    // Additional debug info
                    Vector3[] worldCorners = new Vector3[4];
                    _rectTransform.GetWorldCorners(worldCorners);
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[0]);
                    
                    Debug.Log($"[PixelPerfectClickDetector] {gameObject.name} - Active: {gameObject.activeInHierarchy}, Camera: {(cam != null ? cam.name : "null")}, RenderMode: {_canvas.renderMode}");
                    Debug.Log($"[PixelPerfectClickDetector] Conversion: {conversionSuccess}, ContainsPoint: {containsPoint}, LocalPoint: {localPoint}, Rect: {rect}");
                    Debug.Log($"[PixelPerfectClickDetector] WorldCorners[0]: {worldCorners[0]}, ScreenPoint: {screenPoint}, MousePos: {mousePos}");
                    Debug.Log($"[PixelPerfectClickDetector] Image raycastTarget: {_image.raycastTarget}, Enabled: {_image.enabled}, GameObject active: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
                    
                    // Check what EventSystem would hit
                    if (EventSystem.current != null)
                    {
                        var pointerEventData = new PointerEventData(EventSystem.current)
                        {
                            position = mousePos
                        };
                        var results = new System.Collections.Generic.List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointerEventData, results);
                        
                        Debug.Log($"[PixelPerfectClickDetector] Raycast results for {gameObject.name}: {results.Count} objects");
                        for (int i = 0; i < results.Count; i++)
                        {
                            var result = results[i];
                            bool isThisObject = result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform);
                            Debug.Log($"[PixelPerfectClickDetector]   [{i}] {result.gameObject.name} (Depth: {result.depth}, SortingOrder: {result.sortingOrder}, Distance: {result.distance}) {(isThisObject ? "← THIS OBJECT" : "")}");
                        }
                        
                        // Check if this object or any of its children are in the results
                        bool thisObjectHit = false;
                        int hitIndex = -1;
                        for (int i = 0; i < results.Count; i++)
                        {
                            if (results[i].gameObject == gameObject || results[i].gameObject.transform.IsChildOf(transform))
                            {
                                thisObjectHit = true;
                                hitIndex = i;
                                Debug.Log($"[PixelPerfectClickDetector] ✓ {gameObject.name} IS in raycast results at index {i}");
                                break;
                            }
                        }
                        
                        // Check if there are objects in front of this one
                        if (!thisObjectHit && results.Count > 0)
                        {
                            var firstResult = results[0];
                            Debug.LogWarning($"[PixelPerfectClickDetector] ⚠️ {gameObject.name} is NOT in raycast results. First hit: {firstResult.gameObject.name} (Depth: {firstResult.depth}, SortingOrder: {firstResult.sortingOrder})");
                            Debug.LogWarning($"[PixelPerfectClickDetector] This object may be blocked by {firstResult.gameObject.name} or not properly configured for raycasting.");
                        }
                        else if (!thisObjectHit && !containsPoint)
                        {
                            Debug.Log($"[PixelPerfectClickDetector] {gameObject.name} is not hit (expected - click is outside rect or object is inactive)");
                        }
                        else if (thisObjectHit && !containsPoint)
                        {
                            Debug.LogWarning($"[PixelPerfectClickDetector] ⚠️ {gameObject.name} is in raycast results but ContainsPoint is false. This may indicate a coordinate conversion issue.");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Coroutine to trigger pointer up event with a small delay (to simulate normal pointer up behavior)
        /// </summary>
        private IEnumerator TriggerPointerUpDelayed()
        {
            yield return new WaitForEndOfFrame();
            OnPointerUpDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnPointerUp);
        }

        /// <summary>
        /// Cache the texture data for better performance.
        /// </summary>
        private void CacheTextureData()
        {
            if (_image.sprite == null)
                return;

            _cachedSprite = _image.sprite;
            
            // Check if texture is readable
            _cachedTexture = _image.sprite.texture;
            _isTextureReadable = _cachedTexture != null && _cachedTexture.isReadable;

            if (!_isTextureReadable && enablePixelPerfectDetection)
            {
                Debug.LogWarning($"[PixelPerfectClickDetector] Texture '{_cachedTexture.name}' is not readable. " +
                    "Pixel-perfect detection will not work. Please enable 'Read/Write Enabled' in texture import settings.");
            }
        }

        /// <summary>
        /// Check if the click position hits a non-transparent pixel.
        /// </summary>
        private bool IsClickOnOpaquePixel(Vector2 screenPosition)
        {
            if (!enablePixelPerfectDetection)
            {
                Debug.Log($"[PixelPerfectClickDetector] Pixel-perfect detection disabled on {gameObject.name}, allowing click");
                return true; // If pixel-perfect detection is disabled, always return true
            }

            // Get the correct camera for coordinate conversion (used in multiple places)
            Camera cam = null;
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            {
                cam = _canvas.worldCamera ?? Camera.main;
            }
            // For ScreenSpaceOverlay, camera should be null

            if (_image.sprite == null)
            {
                Debug.LogWarning($"[PixelPerfectClickDetector] Sprite is null on {gameObject.name}, falling back to bounding box check");
                // Fallback to bounding box check if sprite is null
                // Check if click is within the RectTransform bounds
                
                Vector2 fallbackLocalPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, 
                    screenPosition, 
                    cam, 
                    out fallbackLocalPoint))
                {
                    Rect fallbackRect = _rectTransform.rect;
                    bool isInBounds = fallbackRect.Contains(fallbackLocalPoint);
                    Debug.Log($"[PixelPerfectClickDetector] Bounding box check: {isInBounds} for {gameObject.name}");
                    return isInBounds;
                }
                return false;
            }

            // Get the sprite texture
            Texture2D texture = _cachedTexture;
            if (texture == null || texture != _image.sprite.texture)
            {
                texture = _image.sprite.texture;
                _isTextureReadable = texture != null && texture.isReadable;
            }

            if (!_isTextureReadable)
            {
                // Fallback to bounding box check if texture is not readable
                return true;
            }

            // Convert screen position to local position
            Vector2 localPoint;
            bool conversionSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, 
                screenPosition, 
                cam, 
                out localPoint);

            if (!conversionSuccess)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[PixelPerfectClickDetector] Failed to convert screen point to local point for {gameObject.name}");
                }
                return false;
            }

            // Get the rect of the image
            Rect rect = _rectTransform.rect;
            
            // Check if point is within rect bounds first
            if (!rect.Contains(localPoint))
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[PixelPerfectClickDetector] Click outside rect bounds for {gameObject.name}. LocalPoint: {localPoint}, Rect: {rect}");
                }
                return false;
            }
            
            // Normalize local point to 0-1 range (relative to rect)
            float normalizedX = (localPoint.x - rect.x) / rect.width;
            float normalizedY = (localPoint.y - rect.y) / rect.height;

            // Clamp to valid range (should already be in range, but just in case)
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);

            // Get sprite rect and pixels
            Rect spriteRect = _image.sprite.rect;
            int pixelX = Mathf.FloorToInt(normalizedX * spriteRect.width);
            int pixelY = Mathf.FloorToInt(normalizedY * spriteRect.height);

            // Account for sprite pivot and texture coordinates
            int textureX = Mathf.FloorToInt(spriteRect.x + pixelX);
            int textureY = Mathf.FloorToInt(spriteRect.y + pixelY);

            // Clamp to texture bounds
            textureX = Mathf.Clamp(textureX, 0, texture.width - 1);
            textureY = Mathf.Clamp(textureY, 0, texture.height - 1);

            try
            {
                // Read pixel color
                Color pixelColor = texture.GetPixel(textureX, textureY);
                
                // Check if pixel is opaque enough
                bool isOpaque = pixelColor.a >= alphaThreshold;

                if (enableDebugLog)
                {
                    Debug.Log($"[PixelPerfectClickDetector] Click at ({textureX}, {textureY}), " +
                        $"Alpha: {pixelColor.a:F3}, Threshold: {alphaThreshold}, IsOpaque: {isOpaque}");
                }

                return isOpaque;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PixelPerfectClickDetector] Error reading pixel: {e.Message}");
                return true; // Fallback to allowing click
            }
        }

        #region IPointerClickHandler Implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            // Always log click detection for debugging (even if pixel-perfect check fails)
            Debug.Log($"[PixelPerfectClickDetector] ⚡ OnPointerClick called on {gameObject.name} at screen position: {eventData.position}");
            Debug.Log($"[PixelPerfectClickDetector] EventData - Button: {eventData.button}, ClickCount: {eventData.clickCount}, PointerId: {eventData.pointerId}");
            
            // Check what other objects are being hit
            if (EventSystem.current != null)
            {
                var pointerEventData = new PointerEventData(EventSystem.current)
                {
                    position = eventData.position
                };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerEventData, results);
                
                Debug.Log($"[PixelPerfectClickDetector] Raycast results at {eventData.position}: {results.Count} objects hit");
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Debug.Log($"[PixelPerfectClickDetector]   [{i}] {result.gameObject.name} (Distance: {result.distance}, Depth: {result.depth}, SortingLayer: {result.sortingLayer}, SortingOrder: {result.sortingOrder})");
                }
            }
            
            if (!IsClickOnOpaquePixel(eventData.position))
            {
                Debug.Log($"[PixelPerfectClickDetector] Click detected but pixel is transparent or sprite is null. Ignoring click on {gameObject.name}. Sprite: {(_image.sprite != null ? _image.sprite.name : "null")}");
                return;
            }

            Debug.Log($"[PixelPerfectClickDetector] ✓ Click detected on opaque pixel: {gameObject.name}, triggering events...");

            // Trigger events
            OnClickDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnClick);
            
            Debug.Log($"[PixelPerfectClickDetector] ✓ Events triggered for {gameObject.name}");
        }

        #endregion

        #region IPointerEnterHandler Implementation

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Log pointer enter for debugging (only if debug is enabled to avoid spam)
            if (enableDebugLog)
            {
                Debug.Log($"[PixelPerfectClickDetector] OnPointerEnter called on {gameObject.name} at screen position: {eventData.position}");
            }
            
            if (!IsClickOnOpaquePixel(eventData.position))
                return;

            if (enableDebugLog)
            {
                Debug.Log($"[PixelPerfectClickDetector] ✓ Pointer enter on opaque pixel: {gameObject.name}");
            }
            
            OnPointerEnterDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnPointerEnter);
        }

        #endregion

        #region IPointerExitHandler Implementation

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnPointerExit);
        }

        #endregion

        #region IPointerDownHandler Implementation

        public void OnPointerDown(PointerEventData eventData)
        {
            // Always log pointer down for debugging
            Debug.Log($"[PixelPerfectClickDetector] OnPointerDown called on {gameObject.name} at screen position: {eventData.position}");
            
            if (!IsClickOnOpaquePixel(eventData.position))
            {
                Debug.Log($"[PixelPerfectClickDetector] Pointer down detected but pixel is transparent. Ignoring on {gameObject.name}");
                return;
            }

            Debug.Log($"[PixelPerfectClickDetector] ✓ Pointer down on opaque pixel: {gameObject.name}");
            OnPointerDownDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnPointerDown);
        }

        #endregion

        #region IPointerUpHandler Implementation

        public void OnPointerUp(PointerEventData eventData)
        {
            // Always log pointer up for debugging
            Debug.Log($"[PixelPerfectClickDetector] OnPointerUp called on {gameObject.name} at screen position: {eventData.position}");
            
            if (!IsClickOnOpaquePixel(eventData.position))
            {
                Debug.Log($"[PixelPerfectClickDetector] Pointer up detected but pixel is transparent. Ignoring on {gameObject.name}");
                return;
            }

            Debug.Log($"[PixelPerfectClickDetector] ✓ Pointer up on opaque pixel: {gameObject.name}");
            OnPointerUpDetected?.Invoke();
            eventConfig.TriggerEvent(ClickEventType.OnPointerUp);
        }

        #endregion

        /// <summary>
        /// Manually check if a screen position hits an opaque pixel.
        /// Useful for custom input handling.
        /// </summary>
        public bool CheckPixelHit(Vector2 screenPosition)
        {
            return IsClickOnOpaquePixel(screenPosition);
        }

        /// <summary>
        /// Get the event configuration for external modification.
        /// </summary>
        public ClickEventConfig GetEventConfig()
        {
            return eventConfig;
        }

        /// <summary>
        /// Set the alpha threshold for pixel detection.
        /// </summary>
        public void SetAlphaThreshold(float threshold)
        {
            alphaThreshold = Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// Enable or disable pixel-perfect detection.
        /// </summary>
        public void SetPixelPerfectDetection(bool enabled)
        {
            enablePixelPerfectDetection = enabled;
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Draw gizmos in editor for debugging.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_image == null || _image.sprite == null)
                return;

            // Draw the rect transform bounds
            Rect rect = _rectTransform.rect;
            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);

            Gizmos.color = Color.yellow;
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }
        }
        #endif
    }
}
