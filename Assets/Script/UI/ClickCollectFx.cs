using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Spawns the Click particle prefab (FeatureSpec: 点击猫咪和物品特效) at a collectable's UI position.
    /// Prefab is loaded from <c>Resources/Effects/Click</c> (copy of <c>Assets/Prefabs/Effect/Click.prefab</c>).
    /// </summary>
    public static class ClickCollectFx
    {
        private const string ResourcePath = "Effects/Click";
        private static GameObject _prefab;

        /// <summary>
        /// Spawns under the owning <see cref="Canvas"/> at the anchor's world position.
        /// Must not parent to the collectable: fish/puzzle/firework deactivate themselves after collect; children would vanish.
        /// </summary>
        public static void PlayAt(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            if (_prefab == null)
            {
                _prefab = Resources.Load<GameObject>(ResourcePath);
                if (_prefab == null)
                {
                    Debug.LogWarning("[ClickCollectFx] Missing Resources/Effects/Click.prefab (copy from Prefabs/Effect/Click).");
                    return;
                }
            }

            Vector3 worldPos = GetAnchorWorldPosition(anchor);

            Canvas canvas = anchor.GetComponentInParent<Canvas>();
            // Parent to root canvas so nested canvases (e.g. RoomWnd) don't sort FX under background layers.
            Transform parent = canvas != null
                ? (canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform)
                : anchor.root;

            GameObject instance = Object.Instantiate(_prefab, parent);
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localScale = Vector3.one;
                rt.SetAsLastSibling();
                rt.position = worldPos;
            }
            else
            {
                instance.transform.position = worldPos;
            }

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Play(true);
            }

            Object.Destroy(instance, 2.5f);
        }

        private static Vector3 GetAnchorWorldPosition(Transform anchor)
        {
            if (anchor is RectTransform rect)
            {
                return rect.TransformPoint(rect.rect.center);
            }

            return anchor.position;
        }
    }
}
