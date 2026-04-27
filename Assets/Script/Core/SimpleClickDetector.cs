using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace HiddenCats.Core
{
    /// <summary>
    /// Simplified and reliable click detector for UI elements.
    /// Uses Unity's built-in EventSystem for reliable click detection.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Hidden Cats/Simple Click Detector")]
    public class SimpleClickDetector : MonoBehaviour, 
        IPointerClickHandler, 
        IPointerDownHandler,
        IPointerUpHandler
    {
        [Header("Click Detection Settings")]
        [Tooltip("Enable pixel-perfect transparency detection (checks if clicked pixel is opaque)")]
        [SerializeField] private bool enablePixelPerfectDetection = false;
        
        [Tooltip("Alpha threshold for considering a pixel as opaque (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float alphaThreshold = 0.1f;
        
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        // Cached components
        private Image _image;
        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Camera _uiCamera;

        // Events
        public event Action OnClickDetected;
        public event Action OnPointerDownDetected;
        public event Action OnPointerUpDetected;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            
            if (_image == null)
            {
                Debug.LogError($"[SimpleClickDetector] Image component not found on {gameObject.name}");
                enabled = false;
                return;
            }

            if (_canvas == null)
            {
                Debug.LogError($"[SimpleClickDetector] Canvas not found in parent hierarchy of {gameObject.name}");
                enabled = false;
                return;
            }

            // Get UI camera based on canvas render mode
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera || _canvas.renderMode == RenderMode.WorldSpace)
            {
                _uiCamera = _canvas.worldCamera ?? Camera.main;
            }
            else
            {
                _uiCamera = null; // ScreenSpaceOverlay doesn't need camera
            }

            // Ensure raycastTarget is enabled
            _image.raycastTarget = true;

            if (enableDebugLog)
            {
                Debug.Log($"[SimpleClickDetector] Initialized on {gameObject.name}, Canvas: {_canvas.name}, RenderMode: {_canvas.renderMode}, Camera: {(_uiCamera != null ? _uiCamera.name : "null")}");
            }
        }

        /// <summary>
        /// Check if the click position hits a non-transparent pixel.
        /// </summary>
        private bool IsClickOnOpaquePixel(Vector2 screenPosition)
        {
            if (!enablePixelPerfectDetection)
            {
                return true; // If pixel-perfect detection is disabled, always return true
            }

            if (_image.sprite == null)
            {
                return true; // No sprite, allow click
            }

            Texture2D texture = _image.sprite.texture;
            if (texture == null || !texture.isReadable)
            {
                return true; // Texture not readable, allow click
            }

            // Convert screen position to local position in rect transform
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, 
                screenPosition, 
                _uiCamera, 
                out localPoint))
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning($"[SimpleClickDetector] Failed to convert screen point to local point for {gameObject.name}");
                }
                return false;
            }

            // Check if point is within rect bounds
            Rect rect = _rectTransform.rect;
            if (!rect.Contains(localPoint))
            {
                return false;
            }

            // Normalize local point to 0-1 range
            float normalizedX = (localPoint.x - rect.x) / rect.width;
            float normalizedY = (localPoint.y - rect.y) / rect.height;
            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedY = Mathf.Clamp01(normalizedY);

            // Get sprite rect and calculate pixel coordinates
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
                Color pixelColor = texture.GetPixel(textureX, textureY);
                bool isOpaque = pixelColor.a >= alphaThreshold;
                
                if (enableDebugLog)
                {
                    Debug.Log($"[SimpleClickDetector] Pixel at ({textureX}, {textureY}), Alpha: {pixelColor.a:F3}, IsOpaque: {isOpaque}");
                }
                
                return isOpaque;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleClickDetector] Error reading pixel: {e.Message}");
                return true; // Fallback to allowing click
            }
        }

        #region IPointerClickHandler Implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[SimpleClickDetector] OnPointerClick called on {gameObject.name} at {eventData.position}");
            }

            if (enablePixelPerfectDetection && !IsClickOnOpaquePixel(eventData.position))
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[SimpleClickDetector] Click detected but pixel is transparent. Ignoring click on {gameObject.name}");
                }
                return;
            }

            if (enableDebugLog)
            {
                Debug.Log($"[SimpleClickDetector] ✓ Click detected on {gameObject.name}");
            }

            OnClickDetected?.Invoke();
        }

        #endregion

        #region IPointerDownHandler Implementation

        public void OnPointerDown(PointerEventData eventData)
        {
            if (enablePixelPerfectDetection && !IsClickOnOpaquePixel(eventData.position))
            {
                return;
            }

            OnPointerDownDetected?.Invoke();
        }

        #endregion

        #region IPointerUpHandler Implementation

        public void OnPointerUp(PointerEventData eventData)
        {
            if (enablePixelPerfectDetection && !IsClickOnOpaquePixel(eventData.position))
            {
                return;
            }

            OnPointerUpDetected?.Invoke();
        }

        #endregion

        #region Public Configuration Methods

        /// <summary>
        /// Enable or disable pixel-perfect detection at runtime.
        /// </summary>
        /// <param name="enabled">True to enable pixel-perfect detection, false to disable</param>
        public void SetPixelPerfectDetection(bool enabled)
        {
            enablePixelPerfectDetection = enabled;
        }

        /// <summary>
        /// Set the alpha threshold for pixel-perfect detection.
        /// </summary>
        /// <param name="threshold">Alpha threshold (0-1). Pixels with alpha below this value are considered transparent.</param>
        public void SetAlphaThreshold(float threshold)
        {
            alphaThreshold = Mathf.Clamp01(threshold);
        }

        #endregion
    }
}
