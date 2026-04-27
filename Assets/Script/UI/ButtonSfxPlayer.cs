using HiddenCats.Core;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂载到 Button 上，点击时自动播放指定的音效。
/// 使用方法：
/// 1. 将此组件挂载到 Button GameObject 上
/// 2. 在 Inspector 中填写 Sfx Id（比如 "DoorOpen"、"ButtonClick" 等）
/// 3. 确保 AudioManager 的 Sfx Entries 里已经配置了对应的音效
/// 
/// 这样就不需要在代码里手动调用音效了，完全在编辑器中配置即可。
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class ButtonSfxPlayer : MonoBehaviour
{
    [Tooltip("音效 ID，必须和 AudioManager.sfxEntries 里配置的 id 一致。例如：DoorOpen、ButtonClick、CatNormal 等")]
    [SerializeField] private string sfxId;

    [Tooltip("如果勾选，即使音效 ID 为空或未找到，也不会输出警告日志（静默失败）。")]
    [SerializeField] private bool silentFail = false;

    private Button _button;

    /// <summary>
    /// 下一次点击时不播放音效，用于在业务逻辑层已自行播放音效时避免重复。
    /// </summary>
    private bool _suppressNextSfx = false;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
        {
            Debug.LogError($"[ButtonSfxPlayer] {gameObject.name} 上找不到 Button 组件。");
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    /// <summary>
    /// 调用后，下一次按钮点击不会播放音效（一次性标志，自动清除）。
    /// </summary>
    public void SuppressNextSfx()
    {
        _suppressNextSfx = true;
    }

    private void OnButtonClicked()
    {
        if (_suppressNextSfx)
        {
            _suppressNextSfx = false;
            return;
        }

        if (string.IsNullOrEmpty(sfxId))
        {
            if (!silentFail)
            {
                Debug.LogWarning($"[ButtonSfxPlayer] {gameObject.name} 的 Sfx Id 未设置。");
            }
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(sfxId);
        }
        else
        {
            if (!silentFail)
            {
                Debug.LogWarning($"[ButtonSfxPlayer] AudioManager.Instance 未找到，无法播放音效：{sfxId}");
            }
        }
    }
}
