using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在一个 Image 上只放了 1/4 的边框图时，自动生成另外 3 个镜像块，拼成完整边框。
/// 把这个脚本挂在拼图框的 FrameImg 节点上即可。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public sealed class FrameQuarterMirror : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("是否在 Awake 时自动生成 3 个镜像块")]
    [SerializeField] private bool generateOnAwake = true;

    [Tooltip("原始这 1/4 图块所在的位置（决定其它 3 块如何镜像）")]
    [SerializeField] private Corner originalCorner = Corner.TopLeft;

    public enum Placement
    {
        /// <summary>
        /// 拼成完整外框后，以父节点中心为原点，向四周拓展（整体居中）。
        /// </summary>
        CenteredOnParent = 0,

        /// <summary>
        /// 拼成完整外框后，让“完整外框”的左下角对齐到父节点（FrameImg）原本的左下角。
        /// 这样父节点那块区域会落在完整外框的左下 1/4，整体只向右、向上扩展。
        /// </summary>
        AlignFullFrameBottomLeftToParentBottomLeft = 1,

        /// <summary>
        /// 拼成完整外框后，让“完整外框”的左上角对齐到父节点（FrameImg）原本的左上角。
        /// 这样父节点那块区域会落在完整外框的左上 1/4，整体只向右、向下扩展。
        /// </summary>
        AlignFullFrameTopLeftToParentTopLeft = 2
    }

    [Tooltip("完整外框生成后的摆放方式")]
    [SerializeField] private Placement placement = Placement.AlignFullFrameTopLeftToParentTopLeft;

    /// <summary>
    /// 1/4 图块当前所在的角。
    /// </summary>
    public enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private RectTransform _rectTransform;
    private Image _sourceImage;

    private void Awake()
    {
        if (!generateOnAwake)
        {
            return;
        }

        GenerateMirrorsIfNeeded();
    }

    /// <summary>
    /// 在编辑器中右键菜单手动生成镜像块。
    /// </summary>
    [ContextMenu("Generate Frame Mirrors")]
    private void GenerateMirrorsFromContextMenu()
    {
        GenerateMirrorsIfNeeded();
    }

    private void GenerateMirrorsIfNeeded()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_sourceImage == null)
        {
            _sourceImage = GetComponent<Image>();
        }

        if (_sourceImage == null || _sourceImage.sprite == null)
        {
            Debug.LogWarning("[FrameQuarterMirror] 源 Image 或 Sprite 为空，无法生成镜像。", this);
            return;
        }

        // 防止重复生成（如果已经有 4 个子 Image 了就直接返回）
        int childImageCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).GetComponent<Image>() != null)
            {
                childImageCount++;
            }
        }

        if (childImageCount >= 3)
        {
            // 认为已经生成过了
            return;
        }

        // 以当前 Image 作为模板，生成另外 3 个子 Image
        CreateFourQuarters();
    }

    private void CreateFourQuarters()
    {
        // 关闭父节点上的 Image 显示，只作为模板使用
        _sourceImage.enabled = false;

        // 父 Rect 代表 1/4 大小，我们希望最终的外框尺寸是父 Rect 的 4 倍面积（宽高各 2 倍），
        // 并且是从当前中心向四周扩展出去。
        var size = _rectTransform.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // 默认（Centered）时：2x2 的中心 = 父节点中心
        // 对齐到左下角：整体向右上偏移 (halfW, halfH)，使“完整外框”的左下角 = 父节点左下角
        // 对齐到左上角：整体向右下偏移 (halfW, -halfH)，使“完整外框”的左上角 = 父节点左上角
        Vector2 baseOffset = placement switch
        {
            Placement.AlignFullFrameBottomLeftToParentBottomLeft => new Vector2(halfW, halfH),
            Placement.AlignFullFrameTopLeftToParentTopLeft => new Vector2(halfW, -halfH),
            _ => Vector2.zero
        };

        // 四个角分别放在以父节点为中心的 2x2 网格的四个格子里
        CreateQuarter("Frame_TL", new Vector2(-halfW, halfH) + baseOffset, GetMirrorScale(Corner.TopLeft));
        CreateQuarter("Frame_TR", new Vector2(halfW, halfH) + baseOffset, GetMirrorScale(Corner.TopRight));
        CreateQuarter("Frame_BL", new Vector2(-halfW, -halfH) + baseOffset, GetMirrorScale(Corner.BottomLeft));
        CreateQuarter("Frame_BR", new Vector2(halfW, -halfH) + baseOffset, GetMirrorScale(Corner.BottomRight));
    }

    private void CreateQuarter(string name, Vector2 localPos, Vector3 mirrorScale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        // 居中锚点，使用与父节点相同的尺寸
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = _rectTransform.rect.size;
        rt.anchoredPosition = localPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = mirrorScale;

        var img = go.AddComponent<Image>();
        img.sprite = _sourceImage.sprite;
        img.color = _sourceImage.color;
        img.type = Image.Type.Simple;
        img.preserveAspect = _sourceImage.preserveAspect;
        img.raycastTarget = false;
    }

    /// <summary>
    /// 根据原始 1/4 图块所在角，计算出目标角需要的镜像缩放。
    /// 思路：原始那一块不再单独使用，只利用 Sprite，然后对于其它角通过 X/Y 轴缩放为 -1 达到镜像效果。
    /// 为了简单，这里假设原始 Sprite 的方向是「正常」的，我们只关心最终 4 个角之间的对称关系。
    /// </summary>
    private Vector3 GetMirrorScale(Corner targetCorner)
    {
        // 先把原始角看成 TopLeft，然后根据目标角决定需要的镜像
        // 如果你是从别的角开始切的，可以在 inspector 里调整 originalCorner。

        // 将 originalCorner 映射为以 TopLeft 为基准的偏移
        int ox = 0, oy = 0;
        switch (originalCorner)
        {
            case Corner.TopLeft:     ox = 0; oy = 0; break;
            case Corner.TopRight:    ox = 1; oy = 0; break;
            case Corner.BottomLeft:  ox = 0; oy = 1; break;
            case Corner.BottomRight: ox = 1; oy = 1; break;
        }

        int tx = 0, ty = 0;
        switch (targetCorner)
        {
            case Corner.TopLeft:     tx = 0; ty = 0; break;
            case Corner.TopRight:    tx = 1; ty = 0; break;
            case Corner.BottomLeft:  tx = 0; ty = 1; break;
            case Corner.BottomRight: tx = 1; ty = 1; break;
        }

        // 如果 X 方向从 0 -> 1 或 1 -> 0，说明要做一次左右镜像；Y 同理。
        int flipX = (ox == tx) ? 1 : -1;
        int flipY = (oy == ty) ? 1 : -1;

        return new Vector3(flipX, flipY, 1f);
    }
}

