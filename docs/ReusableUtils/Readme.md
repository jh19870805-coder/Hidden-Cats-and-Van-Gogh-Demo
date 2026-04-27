## 通用封装功能归档（Reusable Utils）

这里用来记录一些在项目中**已经封装好、可以在其它地方直接复用**的小功能，同时把对应的脚本原件也备份一份，方便以后查阅和复制粘贴。

目前收录：

- 竞速模式通用开关视图 `SpeedrunToggleView`
- 单一气泡提示组件 `SimpleBubbleHint`

---

## 1. 通用开关视图：`SpeedrunToggleView`

**脚本路径（原始位置）**：`Assets/Script/UI/SpeedrunToggleView.cs`  
**脚本备份位置**：`docs/ReusableUtils/SpeedrunToggleView.cs`

**适用场景**：  
任何只需要**切两张图片表示开/关状态**的 UI 开关（不直接管玩法逻辑，只负责表现层）。

### 1.1 使用步骤

1. 在界面层级中创建一个根节点（例如：`SpeedrunToggleRoot`）；
2. 在根节点下再创建一个子节点（例如：`ImagesRoot`）；
3. 在 `ImagesRoot` 下创建两个子节点：
   - 第一个子节点：挂 `Image`，显示“开启”状态图（On）；
   - 第二个子节点：挂 `Image`，显示“关闭”状态图（Off）；
4. 把脚本 `SpeedrunToggleView` 挂在根节点（如 `SpeedrunToggleRoot`）上；
5. 在 Inspector 中，把 `ImagesRoot` 拖到 `imagesRoot` 字段；
6. 在根节点上添加 `Button` 组件，把 `onClick` 事件绑定到 `SpeedrunToggleView.OnClick_Toggle`。

### 1.2 在其它界面复用的流程

如果你在别的界面想做一个类似的开关：

1. 在新界面层级里，按上面「使用步骤」搭一套 `Root + ImagesRoot + On/Off` 结构；
2. 把 `SpeedrunToggleView` 这个脚本：
   - 要么直接从 `Assets/Script/UI/` 拖过去用；
   - 要么从本目录 `docs/ReusableUtils/SpeedrunToggleView.cs` 复制一份到合适的位置；
3. 同样给根节点挂 `Button`，`onClick` 绑 `OnClick_Toggle`；
4. 如果需要让它真正控制某个玩法逻辑，就在 `OnClick_Toggle` 里的 `TODO` 区域里，调用你自己的服务 / 单例：

   ```csharp
   // 例：在 OnClick_Toggle 末尾增加
   MyFeatureService.Instance.SetEnabled(_isOn);
   ```

### 1.3 从其它脚本直接控制开关状态

`SpeedrunToggleView` 暴露了一个 `SetState(bool isOn)` 接口，可以在别的脚本里用代码方式直接设置视觉状态：

```csharp
public class ExampleUsage : MonoBehaviour
{
    [SerializeField] private SpeedrunToggleView speedrunToggle;

    private void Start()
    {
        // 强制把开关显示为“开启”
        speedrunToggle.SetState(true);
    }
}
```

注意：`SetState` 目前只负责**视觉表现**，如果你需要同时更新玩法逻辑，可以在调用前后自己去同步逻辑层状态。

---

如果你之后又封装了新的通用小组件（比如：通用提示气泡、通用进度条、通用收集进度展示等），
可以照着这个文档结构继续往下追加：

- 先在 `docs/ReusableUtils/` 里存一份脚本备份；
- 然后在本 `Readme.md` 里增加一个小节「功能说明 + 使用步骤 + 示例代码」。

---

## 2. 单一气泡提示组件：`SimpleBubbleHint`

**脚本建议放置位置**：`Assets/Script/UI/SimpleBubbleHint.cs`  
（如需备份，可复制一份到 `docs/ReusableUtils/SimpleBubbleHint.cs`）

**适用场景**：  
需要在某个 UI 元素上做**“单一提示气泡”**效果：点击后在附近弹出一个气泡（图片 + 文字），**停留若干秒后自动消失**，无需复杂状态切换。

### 2.1 核心思路（从主界面猫咪点击气泡中提炼）

- **单一气泡根节点**：准备一个 `bubbleRoot`（`GameObject`），内部放你的提示图标和文字，默认 `SetActive(false)`；
- **点击触发一次显示**：点击目标节点时，打开 `bubbleRoot`，并启动一个协程计时；
- **自动隐藏**：计时结束后把 `bubbleRoot.SetActive(false)`，并清理协程引用，防止重复启动。

伪代码结构大致如下（真实实现见脚本）：

```csharp
public sealed class SimpleBubbleHint : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private float duration = 1.5f;
    private Coroutine activeRoutine;

    public void OnPointerClick(PointerEventData eventData)
    {
        ShowBubbleOnce();
    }

    public void ShowBubbleOnce()
    {
        bubbleRoot.SetActive(true);
        // 如已有协程先停止，再重新启动一个
        ...
    }
}
```

### 2.2 使用步骤

1. **在层级中准备气泡节点**
   - 在界面中创建一个气泡根节点，例如：`MyHintBubbleRoot`；
   - 在其下放入一个背景图 + 文本（可以是现有的提示样式）；
   - 确保默认状态下 `MyHintBubbleRoot` 为未勾选（`SetActive(false)`）。

2. **给点击节点挂脚本**
   - 在你希望被点击的节点上（按钮、图片等）挂上 `SimpleBubbleHint`；
   - 在 Inspector 中把 `MyHintBubbleRoot` 拖到 `bubbleRoot` 字段；
   - 确保该节点上有 `Graphic` 或 `Button` 组件，并启用 `Raycast Target`，以便接收点击事件。

3. **（可选）从代码主动触发**
   - 如果不是点击，而是代码主动触发（例如：新手引导时自动提示），可以在其它脚本中持有 `SimpleBubbleHint` 引用，并直接调用：

   ```csharp
   [SerializeField] private SimpleBubbleHint hint;

   void ShowGuideHint()
   {
       hint.ShowBubbleOnce();
   }
   ```

### 2.3 与主界面猫咪点击逻辑的关系

- 主界面的猫咪点击气泡（鱼 / 心）逻辑中，已经有一套类似的**“显示一个气泡若干秒后自动隐藏”的封装**；
- `SimpleBubbleHint` 是在此基础上针对**单一气泡场景**抽出来的独立组件：
  - 不关心收集条件判断；
  - 只负责**显示 / 隐藏一个气泡节点 + 自动计时收回**；
  - 可以在任意界面、任意按钮上复用。
