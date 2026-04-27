using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// 诊断工具：检查场景中所有 Canvas 的层级关系
    /// </summary>
    public class CanvasDiagnostics : MonoBehaviour
    {
        [ContextMenu("List All Canvases")]
        public void ListAllCanvases()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            Debug.Log($"=== Found {canvases.Length} Canvases ===");

            foreach (var canvas in canvases)
            {
                string hierarchy = GetHierarchy(canvas.transform);
                Debug.Log($"Canvas: {canvas.name} | SortOrder: {canvas.sortingOrder} | Mode: {canvas.renderMode} | Hierarchy: {hierarchy}");
            }
            Debug.Log("=============================");
        }

        [ContextMenu("List All RawImages")]
        public void ListAllRawImages()
        {
            RawImage[] images = FindObjectsOfType<RawImage>();
            Debug.Log($"=== Found {images.Length} RawImages ===");

            foreach (var img in images)
            {
                string hierarchy = GetHierarchy(img.transform);
                Material mat = img.material;
                string matInfo = mat != null ? $"Material: {mat.name}, Shader: {mat.shader?.name ?? "NULL"}" : "No Material";
                Debug.Log($"RawImage: {img.name} | Enabled: {img.enabled} | {matInfo} | Hierarchy: {hierarchy}");
            }
            Debug.Log("=============================");
        }

        [ContextMenu("Check TransitionEffect State")]
        public void CheckTransitionEffectState()
        {
            var te = WindowTransitionEffect.Instance;
            if (te == null)
            {
                Debug.LogWarning("WindowTransitionEffect.Instance is null!");
                return;
            }

            Debug.Log("=== WindowTransitionEffect State ===");
            Debug.Log($"Instance exists: {te != null}");
            Debug.Log($"Is Transitioning: {te.IsTransitioning}");

            // 检查 Canvas
            var canvasGO = te.transform.Find("TransitionCanvas");
            if (canvasGO != null)
            {
                var canvas = canvasGO.GetComponent<Canvas>();
                Debug.Log($"TransitionCanvas found: {canvasGO.name}, SortOrder: {canvas?.sortingOrder ?? -1}");
            }
            else
            {
                Debug.LogWarning("TransitionCanvas not found!");
            }

            // 检查 RawImage
            var imageGO = canvasGO?.Find("TransitionImage");
            if (imageGO != null)
            {
                var rawImage = imageGO.GetComponent<RawImage>();
                Debug.Log($"TransitionImage found: {imageGO.name}, Enabled: {rawImage?.enabled}");
                Debug.Log($"Material: {rawImage?.material?.name ?? "NULL"}");
                Debug.Log($"Shader: {rawImage?.material?.shader?.name ?? "NULL"}");
            }
            else
            {
                Debug.LogWarning("TransitionImage not found!");
            }

            Debug.Log("=================================");
        }

        private string GetHierarchy(Transform t)
        {
            string path = "";
            while (t != null)
            {
                path = t.name + (path.Length > 0 ? "/" + path : "");
                t = t.parent;
            }
            return path;
        }
    }
}
