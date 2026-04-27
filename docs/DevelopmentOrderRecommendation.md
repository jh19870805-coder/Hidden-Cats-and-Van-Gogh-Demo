# 功能优先级列表 & 当前完成情况（文档2）

> **强制流程**：开始任何需求前，必须先读：
> - 文档1：`docs/Skill.md`
> - 文档3：`docs/FeatureSpec.md`
>
> **回写规则**：当某个需求完成并被你接受后，我必须同步更新本文件与文档3对应章节。

---

## 📌 当前项目状态快照（以现有代码为准）

### ✅ 已实现（可用）

- **启动与管理器**：`GameInitializer` 会确保 `SettingsManager` / `AudioManager` / `CursorManager` / `CollectionService` 存在（StartUp 场景必须挂）。
- **窗口切换（单场景方案）**：`WindowManager` 负责 Main/Room/Flower/Cafe/SmallGame 切换与 Setting/Rank 弹窗。
- **收集统计（核心）**：`CollectionService` + `CollectionRecord`（单场景计数 + 全局计数 + PuzzlePieceId + 存档）。
- **交互物（核心）**：普通猫 / 隐藏猫 / 鱼 / 烟花 / 拼图块（均已接入收集统计）。
- **目标与进度**：`HiddenObjectManager`（注册、max 统计、进度事件、通关判定）。
- **NumUI**：`NumUIController` 自动扫描 max，并监听 `OnSceneCountChanged` 更新。
- **主界面联动**：
  - `DogBowlVisualFeedback`：根据全局鱼数量显示 4 条鱼
  - `MainCatClickHandler`：根据“是否找齐全局鱼”切换鱼/爱心气泡
- **拼图小游戏**：`PuzzleController` + `SmallGameWndUI`（初始化/打乱/交换/完成判定 + 运行期与持久化布局保存）。
- **设置系统**：`SettingsManager`（保存/应用音量、语言、全屏、光标大小等）。
- **音频基础**：`AudioManager`（BGM + SFX id 查表播放）。
- **光标系统**：`CursorManager`（MouseX1/MouseX2 + DPI 缩放 + 订阅 Settings）。
- **存档重置**：`GameProgressResetService`（重置进度不重置设置，resetVersion 机制）。
- **通用提示/对话框服务（基础版已存在）**：`HintBubbleService`、`DialogService`（集成覆盖面仍需扩展）。

### ⚠️ 已实现但仍是“占位/不完整”

- **解锁系统**：`UnlockChecker` 仍是占位（用“是否收集过一些”代替“是否找齐”）。
- **本地化**：`LocalizationManager` 依赖 `LanguageConfig` 手动配置，翻译字典 lookup 仍为 TODO（大量 UI 仍是硬编码文本）。
- **交互音效接入**：交互物脚本多处仍保留 `TODO: Integrate with AudioManager`。
- **GameSceneUI**：Hint 与 Quit 仍为 TODO（仅日志/占位）。
- **提示/弹窗统一**：项目里同时存在 `MessagePopup` / `ConfirmationPopup` 与 `DialogService`（需要统一策略）。

### ❌ 未实现（规划中）

- **StageConfig / StageController（关卡配置系统）**：用于“真实上限/目标/解锁条件”的配置化支持。
- **排行榜/竞速真实逻辑**：目前只有 Rank 弹窗入口，缺计时、成绩存储与列表展示。
- **提示系统（HintSystem / 放大镜）**：当前仅有 UI 按钮占位。
- **故事/字条/对话/演出系统**：未落地。

---

## 🎯 优先级 Backlog（按可玩性/稳定性排序）

> 每个条目都尽量写成“可直接验收”的任务。你提新需求时，也应优先落在这些系统上。

### P0（必须优先：稳定性 / 核心链路）

1. **启动稳定性：彻底避免单例缺失导致的断链**
   - **问题表现**：`CollectionService.Instance == null` 会导致狗盆鱼/猫气泡/解锁判定等全部失效。
   - **目标**：StartUp 首帧所有关键服务都可用；重复实例可被安全处理；日志不刷屏。
   - **验收**：启动进入 MainWnd，狗盆鱼与猫气泡逻辑稳定；收集鱼后主界面联动稳定。

2. **解锁判定真实化（替换 UnlockChecker 占位逻辑）**
   - **目标**：从“是否收集过”升级为“是否找齐（对比上限）”。
   - **依赖**：需要“上限来源”（推荐 StageConfig；短期可先复用扫描 max 逻辑）。
   - **验收**：小游戏/排行榜入口在满足条件时解锁，不满足时稳定提示。

3. **提示/弹窗统一策略落地（先做关键入口）**
   - **目标**：至少把“小游戏未解锁提示 / Quit 确认”统一到 `DialogService` 或 `HintBubbleService`。
   - **验收**：玩家能看见清晰提示；UI 逻辑不在多个脚本重复实现。

### P1（高优先级：体验提升）

4. **交互音效接入**
   - **目标**：普通猫/隐藏猫/鱼/烟花/拼图块收集时播放 SFX（按 id 配置）。
   - **验收**：音量设置生效；未配置音效 id 时不会刷屏报错。

5. **GameSceneUI：Hint 与 Quit**
   - **目标**：
     - Hint：先做最小版本（例如高亮一个未收集目标或弹“暂未实现”统一提示）
     - Quit：使用确认弹窗，避免误触直接退出
   - **验收**：按钮行为与 UI 状态一致；Quit 有明确二次确认。

### P2（中优先级：内容与深度）

6. **StageConfig / StageController**
   - **目标**：把“目标/上限/通关/解锁条件”配置化，减少硬编码与扫描耦合。

7. **排行榜/竞速系统（最小可用版）**
   - **目标**：本地成绩存储 + 列表展示（先本地，后网络）。

8. **本地化完善**
   - **目标**：补齐词条表、UI 刷新机制，逐步替换硬编码字符串。

---

## 🧾 文档回写规则（你验收通过后）

- 我必须：
  - 在本文件中把对应条目从 ❌/⚠️ 更新为 ✅（或 ✅基础版 / ⚠️仍待完善）
  - 在 `docs/FeatureSpec.md` 的对应系统章节更新“当前实现事实”（接口/配置/规则）

