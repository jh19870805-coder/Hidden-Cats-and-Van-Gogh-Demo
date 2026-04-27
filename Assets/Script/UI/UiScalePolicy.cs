using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// Canonical UI scale for Steam / desktop: single reference resolution and Canvas Scaler rules
    /// so all <see cref="CanvasScaler"/> instances stay aligned (design baseline 2560×1440, height match).
    /// </summary>
    public static class UiScalePolicy
    {
        public const float ReferenceWidth = 2560f;
        public const float ReferenceHeight = 1440f;

        /// <summary>
        /// Applies policy to every <see cref="CanvasScaler"/> in loaded objects (including inactive).
        /// Skips world-space canvases (scaler usage differs).
        /// </summary>
        public static void ApplyToAllScreenSpaceCanvases()
        {
            CanvasScaler[] scalers = Object.FindObjectsOfType<CanvasScaler>(true);
            for (int i = 0; i < scalers.Length; i++)
            {
                ApplyCanonicalSettings(scalers[i]);
            }
        }

        public static void ApplyCanonicalSettings(CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }

            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
    }
}
