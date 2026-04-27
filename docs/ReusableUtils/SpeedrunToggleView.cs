using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞速模式开关视图脚本。
/// - 在 Inspector 里只需要拖入“存放两张图片的父节点”。
/// - 脚本会自动从该父节点的前两个子节点上获取 Image，并在点击时进行显隐切换。
/// 使用方式：
/// 1. 在层级中创建根节点（例如：SpeedrunToggleRoot），在其下创建一个子节点（例如：ImagesRoot）。
/// 2. 在 ImagesRoot 下再创建两个子节点：
///    - 第一个子节点：显示“开启”状态的图片（挂 Image）
///    - 第二个子节点：显示“关闭”状态的图片（挂 Image）
/// 3. 把本脚本挂到根节点（如 SpeedrunToggleRoot）上。
/// 4. 在 Inspector 中，把 ImagesRoot 拖到 imagesRoot 字段。
/// 5. 给根节点添加 Button 组件，把 onClick 事件绑定到 OnClick_Toggle 方法。
/// </summary>
public class SpeedrunToggleView : MonoBehaviour
{
    [Header("拖入：两个状态图片的父节点（其下前两个子节点为 On/Off 图片）")]
    [SerializeField] private Transform imagesRoot;

    private Image _onImage;
    private Image _offImage;

    // 当前是否为“开启”状态
    private bool _isOn;

    private void Awake()
    {
        if (imagesRoot == null)
        {
            Debug.LogError("[SpeedrunToggleView] imagesRoot 没有设置，请在 Inspector 中拖入存放图片的父节点。", this);
            return;
        }

        // 按子节点顺序获取前两个子节点上的 Image：
        // 第一个子节点 = On 图，第二个子节点 = Off 图
        if (imagesRoot.childCount >= 2)
        {
            _onImage = imagesRoot.GetChild(0).GetComponent<Image>();
            _offImage = imagesRoot.GetChild(1).GetComponent<Image>();
        }

        if (_onImage == null || _offImage == null)
        {
            Debug.LogError("[SpeedrunToggleView] 未能在 imagesRoot 的前两个子节点上找到 Image 组件，请确认层级结构和组件挂载是否正确。", this);
        }
    }

    private void Start()
    {
        RefreshVisual();
    }

    /// <summary>
    /// 按钮点击事件调用的方法。
    /// 在 Button 的 onClick 中绑定本方法即可。
    /// </summary>
    public void OnClick_Toggle()
    {
        _isOn = !_isOn;
        RefreshVisual();

        // TODO：在这里同步你自己游戏里的“竞速模式开关”逻辑（如果有需要）
        // 例如：
        // SpeedrunService.Instance.SetSpeedrunEnabled(_isOn);
    }

    /// <summary>
    /// 如果你在别的脚本里想直接设置开关状态，可以调用这个方法。
    /// </summary>
    public void SetState(bool isOn)
    {
        _isOn = isOn;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (_onImage != null)
        {
            _onImage.gameObject.SetActive(_isOn);
        }

        if (_offImage != null)
        {
            _offImage.gameObject.SetActive(!_isOn);
        }
    }
}

