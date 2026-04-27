using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// Unity UI <see cref="Image"/> with no <see cref="Image.sprite"/> does not generate mesh geometry,
    /// so <see cref="UnityEngine.UI.GraphicRaycaster"/> never hits it — <see cref="IPointerClickHandler"/> will not run.
    /// Use a 1×1 white sprite so the image stays effectively invisible but receives raycasts.
    /// </summary>
    public static class UiInvisibleRaycastSprite
    {
        private static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            Texture2D tex = Texture2D.whiteTexture;
            _sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _sprite;
        }

        public static void ApplyTo(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = Get();
            image.type = Image.Type.Simple;
        }
    }
}
