using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HiddenCats.UI
{
    /// <summary>
    /// ????
    /// </summary>
    [Serializable]
    public class TransitionConfig
    {
        public Color dotColor = Color.white;
        public Color solidColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public float solidDuration = 0.3f;
    }

    public sealed class WindowTransitionEffect : MonoBehaviour
    {
        public static WindowTransitionEffect Instance { get; private set; }

        [Header("Shader Material")]
        [SerializeField] private Material transitionMaterial;
        [SerializeField] private string shaderName = "HiddenCats/CircularDissolveTransition";

        [Header("=== ???? ===")]
        [Tooltip("?????")]
        [SerializeField] private float totalDuration = 1.0f;
        [SerializeField] private float phase1EndRatio = 0.35f;
        [SerializeField] private float phase2EndRatio = 0.65f;

        [Header("=== ???????? ===")]
        [Tooltip("?????????????????????")]
        [Range(5, 80)]
        [SerializeField] private int dotCount = 24;

        [Tooltip("???????????????")]
        [Range(0.01f, 2f)]
        [SerializeField] private float dotSize = 0.5f;

        [Tooltip("?????????????????")]
        [Range(-0.5f, 1f)]
        [SerializeField] private float dotSpacing = 0f;

        [Tooltip("????")]
        [SerializeField] private Color dotColor = Color.white;

        [Header("=== ?????? ===")]
        [SerializeField] private float edgeWidth = 0.06f;
        [SerializeField] private float softness = 0.02f;

        [Header("=== ???????? ===")]
        [SerializeField] private Color solidColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        private Canvas _transitionCanvas;
        private RawImage _transitionImage;
        private Material _runtimeMaterial;
        private bool _isTransitioning;
        private Coroutine _transitionCoroutine;

        public bool IsTransitioning => _isTransitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Log("WindowTransitionEffect Awake() called");
            CreateTransitionOverlay();
            Log($"Awake complete - Image: {_transitionImage != null}, Material: {_runtimeMaterial != null}");

            if (transform.parent == null)
            {
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
            }
        }

        private void CreateTransitionOverlay()
        {
            Log("=== CreateTransitionOverlay() BEGIN ===");

            GameObject canvasObj = new GameObject("TransitionCanvas");
            _transitionCanvas = canvasObj.AddComponent<Canvas>();
            _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _transitionCanvas.sortingOrder = 1000;
            _transitionCanvas.overrideSorting = true;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Screen.width, Screen.height);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imageObj = new GameObject("TransitionImage");
            imageObj.transform.SetParent(canvasObj.transform);

            RectTransform rt = imageObj.AddComponent<RectTransform>();
            rt.StretchFull();

            _transitionImage = imageObj.AddComponent<RawImage>();
            _transitionImage.raycastTarget = false;
            _transitionImage.enabled = false;

            Texture2D whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            _transitionImage.texture = whiteTexture;

            if (transitionMaterial != null && transitionMaterial.shader != null)
            {
                _transitionImage.material = transitionMaterial;
                _runtimeMaterial = _transitionImage.material;
                Log($"Material from preset - Shader: '{_runtimeMaterial.shader?.name ?? "NULL"}'");
                Log($"[CreateOverlay] runtimeMaterial.GetInstanceID()={_runtimeMaterial?.GetInstanceID()}, texMaterial.GetInstanceID()={_transitionImage?.material?.GetInstanceID()}, SAME={(_runtimeMaterial != null && _transitionImage?.material != null && _runtimeMaterial.GetInstanceID() == _transitionImage.material.GetInstanceID())}");
            }
            else
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader);
                    _transitionImage.material = _runtimeMaterial;
                }
                else
                {
                    LogError($"Shader.Find('{shaderName}') failed!");
                }
            }

            ApplyMaterialProperties(dotColor, solidColor);
            Log("=== CreateTransitionOverlay() END ===");

            // Debug: log ALL canvases in scene
            Canvas[] allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            Log($"=== All Canvases in scene ({allCanvases.Length}):");
            for (int i = 0; i < allCanvases.Length; i++)
            {
                var c = allCanvases[i];
                Log($"  [{i}] '{c.name}' sortOrder={c.sortingOrder} renderMode={c.renderMode} enabled={c.enabled} overrideSorting={c.overrideSorting}");
            }
            Log("===");
        }

        private void ApplyMaterialProperties(Color dotColor, Color solidColor)
        {
            if (_runtimeMaterial == null)
            {
                LogWarning("[Apply] runtimeMaterial is null!");
                return;
            }

            _runtimeMaterial.SetFloat("_DissolveProgress", 0f);
            _runtimeMaterial.SetFloat("_DotSize", dotSize);
            _runtimeMaterial.SetFloat("_DotCount", dotCount);
            _runtimeMaterial.SetFloat("_DotSpacing", dotSpacing);
            _runtimeMaterial.SetFloat("_Softness", softness);
            _runtimeMaterial.SetFloat("_EdgeWidth", edgeWidth);
            _runtimeMaterial.SetColor("_EdgeColor", dotColor);
            _runtimeMaterial.SetColor("_SolidColor", solidColor);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Cleanup white texture to prevent memory leak
            if (_transitionImage != null && _transitionImage.texture != null)
            {
                Destroy(_transitionImage.texture);
                _transitionImage.texture = null;
            }

            // Cleanup runtime material
            if (_runtimeMaterial != null && _runtimeMaterial != transitionMaterial)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        /// <summary>
        /// ?????????
        /// </summary>
        public void PerformTransition(Action onMidPoint, Action onComplete)
        {
            var defaultConfig = new TransitionConfig
            {
                dotColor = this.dotColor,
                solidColor = this.solidColor
            };
            PerformTransition(defaultConfig, onMidPoint, onComplete);
        }

        /// <summary>
        /// ??????????
        /// </summary>
        public void PerformTransition(TransitionConfig config, Action onMidPoint, Action onComplete)
        {
            Log($"[Perform] isTransitioning: {_isTransitioning}");

            if (_isTransitioning)
            {
                Log("[Perform] Already transitioning, skipping");
                onMidPoint?.Invoke();
                onComplete?.Invoke();
                return;
            }

            if (_runtimeMaterial == null)
            {
                LogError("[Perform] Material is null!");
                onMidPoint?.Invoke();
                onComplete?.Invoke();
                return;
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(TransitionCoroutine(config, onMidPoint, onComplete));
        }

        private IEnumerator TransitionCoroutine(TransitionConfig config, Action onMidPoint, Action onComplete)
        {
            _isTransitioning = true;

            // Apply material properties for this transition
            ApplyMaterialProperties(config.dotColor, config.solidColor);

            _transitionImage.enabled = true;
            _runtimeMaterial.SetFloat("_DissolveProgress", 0f);

            Log($"[Transition] DotColor: {config.dotColor}, SolidColor: {config.solidColor}, SolidDuration: {config.solidDuration}");
            Log($"[Transition] Image enabled={_transitionImage.enabled}, tex={_transitionImage?.texture != null}, mat={_runtimeMaterial != null}, prog=0.0");

            // Calculate phase durations
            float phase1 = totalDuration * phase1EndRatio;
            float phase2 = config.solidDuration; // Middle solid color duration
            float phase3 = totalDuration * (1f - phase2EndRatio);

            // Phase 1: dots dissolve and shrink to nothing
            float elapsed = 0f;
            while (elapsed < phase1)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / phase1);
                float progress = Mathf.Lerp(0f, phase1EndRatio, t);
                _runtimeMaterial.SetFloat("_DissolveProgress", progress);
                yield return null;
            }
            _runtimeMaterial.SetFloat("_DissolveProgress", phase1EndRatio);
            Log("[Phase1] Done - solid color phase starting");

            // Phase 2: hold solid color (window switch happens here)
            elapsed = 0f;
            float phase2End = phase1EndRatio + 0.01f; // Avoid boundary issues
            Log($"[Phase2] START - progress={phase2End}, solidColor={config.solidColor}, tex={_transitionImage?.texture != null}, mat={_runtimeMaterial != null}, imgEnabled={_transitionImage?.enabled}");
            while (elapsed < phase2)
            {
                if (!_isTransitioning) { LogError($"[Phase2] LOOP BREAK - isTransitioning became FALSE at elapsed={elapsed}"); break; }
                if (gameObject == null || !gameObject.activeInHierarchy) { LogError($"[Phase2] LOOP BREAK - gameObject inactive! active={gameObject?.activeInHierarchy}"); break; }
                elapsed += Time.deltaTime;
                _runtimeMaterial.SetFloat("_DissolveProgress", phase2End);
                yield return null;
                // Immediately after yield, check if we should still be running
                if (!_isTransitioning || gameObject == null || !gameObject.activeInHierarchy) { LogError($"[Phase2] POST-YIELD check failed! isTrans={_isTransitioning}, active={gameObject?.activeInHierarchy}"); break; }
            }
            Log($"[Phase2] Done - solid color held for {phase2}s, final elapsed={elapsed}");

            // Switch window at the end of phase2 (before phase3 starts)
            Log($"[Transition] BEFORE onMidPoint - isTransitioning={_isTransitioning}, imageEnabled={_transitionImage?.enabled}");
            Log($"[Transition] >>>>>>>> onMidPoint CALLING (Phase2 ended, window should switch now) <<<<<<<");
            onMidPoint?.Invoke();
            Log($"[Transition] >>>>>>>> onMidPoint CALLED (Phase2 ended) <<<<<<<");
            Log($"[Transition] AFTER onMidPoint - isTransitioning={_isTransitioning}, imageEnabled={_transitionImage?.enabled}, this={GetInstanceID()}");

            Log("[Transition] About to WaitForEndOfFrame...");
            yield return new WaitForEndOfFrame();
            Log($"[Transition] AFTER WaitForEndOfFrame - isTransitioning={_isTransitioning}, imageEnabled={_transitionImage?.enabled}, gameObjectActive={gameObject != null && gameObject.activeInHierarchy}");

            // Phase 3: dots grow and appear
            elapsed = 0f;
            Log($"[Phase3] START - progress will go from {phase2End} to 1.0, dotColor={config.dotColor}");
            while (elapsed < phase3)
            {
                if (!_isTransitioning) { LogError($"[Phase3] LOOP BREAK - isTransitioning became FALSE at elapsed={elapsed}"); break; }
                if (gameObject == null || !gameObject.activeInHierarchy) { LogError($"[Phase3] LOOP BREAK - gameObject inactive! active={gameObject?.activeInHierarchy}"); break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / phase3);
                float progress = Mathf.Lerp(phase2End, 1f, t);
                _runtimeMaterial.SetFloat("_DissolveProgress", progress);
                yield return null;
                // Immediately after yield, check if we should still be running
                if (!_isTransitioning || gameObject == null || !gameObject.activeInHierarchy) { LogError($"[Phase3] POST-YIELD check failed! isTrans={_isTransitioning}, active={gameObject?.activeInHierarchy}"); break; }
            }
            _runtimeMaterial.SetFloat("_DissolveProgress", 1f);
            Log($"[Phase3] Done - final elapsed={elapsed}, isTransitioning={_isTransitioning}, gameObjActive={gameObject?.activeInHierarchy}");

            // Cleanup
            Log($"[Cleanup] BEFORE - image.enabled={_transitionImage?.enabled}, isTransitioning={_isTransitioning}");
            _transitionImage.enabled = false;
            _runtimeMaterial.SetFloat("_DissolveProgress", 0f);
            _isTransitioning = false;
            _transitionCoroutine = null;
            Log($"[Cleanup] AFTER - image.enabled={_transitionImage?.enabled}, isTransitioning={_isTransitioning}");

            Log("[Transition] Complete!");
            onComplete?.Invoke();
        }

        private void Log(string msg)
        {
            if (enableDebugLog) Debug.Log($"[WT] {msg}");
        }

        private void LogWarning(string msg)
        {
            if (enableDebugLog) Debug.LogWarning($"[WT] {msg}");
        }

        private void LogError(string msg)
        {
            if (enableDebugLog) Debug.LogError($"[WT] {msg}");
        }

        [ContextMenu("Test Transition")]
        public void TestTransition()
        {
            PerformTransition(
                () => Debug.Log("[WT] MidPoint!"),
                () => Debug.Log("[WT] Done!")
            );
        }

        /// <summary>
        /// ????????
        /// </summary>
        [ContextMenu("Test Transition With Config")]
        public void TestTransitionWithConfig()
        {
            var config = new TransitionConfig
            {
                dotColor = Color.red,
                solidColor = Color.blue,
                solidDuration = 1f
            };
            PerformTransition(config,
                () => Debug.Log("[WT] MidPoint!"),
                () => Debug.Log("[WT] Done!")
            );
        }
    }

    public static class RectTransformExtensions
    {
        public static void StretchFull(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
