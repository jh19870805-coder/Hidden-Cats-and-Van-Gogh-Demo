# 功能说明（功能策划案 / 系统拆分）（文档3）

> **目标**：把游戏按“系统”拆分，写清楚每个系统：做什么、不做什么、数据口径、对外接口、配置方式、与当前实现状态的差距。  
> **使用方式**：实现任何需求前，先定位该需求属于哪个系统；实现完成且你验收通过后，回写本文件对应章节。

---

## 0. 游戏概览（当前实现形态）

- **窗口形态**：项目目前采用 **单 Unity 场景 + 窗口 Prefab 切换**（不是用 Unity Scene Load 来切换 Room）。
- **核心循环（玩家）**：
  - 进入房间（RoomWnd）
  - 点击交互物（普通猫/隐藏猫/鱼/拼图块/烟花）
  - UI 实时显示进度（NumUI）
  - 进度写入全局存档（CollectionService + PlayerPrefs）
  - 主界面根据全局进度做反馈（狗盆鱼数量、猫气泡）
  - 收集齐拼图块后可进入拼图小游戏（SmallGameWnd）
- **多语言**：见 **第 9 章《多语言与本地化（Localization）》**（文本表、`LocalizationManager` API、`LocalizedText`、策划配置流程）。

---

## 1. 启动与框架系统（StartUp / Managers / Window Flow）

### 1.1 职责

- **GameInitializer（StartUp 必挂）**
  - 确保关键单例存在：`SettingsManager`、`AudioManager`、`CursorManager`、`CollectionService`
  - 对 `LocalizationManager` 仅做“缺失警告”；场景中需挂载并配置 `LanguageConfig`（及可选 `LocalizationTable`），详见 **第 9 章**
  - 在所有管理器就绪后，重新 `ApplySettings`，保证首帧音量/语言正确

- **WindowManager（StartUp 必挂）**
  - 在一个 Unity 场景内实例化并切换顶层窗口 Prefab：
    - `MainWnd`、`RoomWnd`、`SmallGameWnd`
  - 弹窗覆盖：
    - `SettingPop`、`RankPop`

### 1.2 关键约束（非常重要）

- 如果出现 `CollectionService.Instance == null` / `SettingsManager.Instance == null`，优先检查：
  - StartUp 场景里是否存在且启用 `GameInitializer`
  - 是否有重复实例在 Awake 被销毁（多套 Prefab 重复挂管理器）

### 1.3 当前实现（代码依据）

- `Assets/Script/Core/GameInitializer.cs`
- `Assets/Script/UI/WindowManager.cs`
- `docs/UIMode.md`（包含较完整的窗口配置步骤）

---

## 2. 收集与统计系统（CollectionService / CollectionRecord）

### 2.1 职责

- 统一管理“已收集”的统计数据：
  - **单场景计数**：每个 `sceneName` 下，各 `CollectibleType` 的计数
  - **全局计数**：跨所有 sceneName 汇总的计数
  - **拼图块 ID**：PuzzlePiece 的唯一 ID 列表（避免重复收集）
  - **持久化**：写入 PlayerPrefs（JSON）

### 2.2 对外接口（当前已有）

- `CollectItem(sceneName, type, puzzlePieceId?) -> bool`
- `GetSceneCount(sceneName, type) -> int`
- `GetGlobalCount(type) -> int`
- Puzzle 相关：
  - `HasPuzzlePiece(id)`
  - `GetCollectedPuzzlePieceIds()`
  - `GetPuzzlePieceCount()`
- 重置：
  - `ResetCollectionData()`

### 2.3 事件（用于 UI/逻辑联动）

- `OnSceneCountChanged(sceneName, type, newCount)`
- `OnGlobalCountChanged(type, newCount)`
- `OnPuzzlePieceCollected(puzzlePieceId)`

### 2.4 当前实现（代码依据）

- `Assets/Script/Core/CollectionService.cs`
- `Assets/Script/Core/CollectionRecord.cs`
- `Assets/Script/Core/CollectibleType.cs`

---

## 3. 点击检测与交互事件系统（ClickDetector / EventConfiguration）

### 3.1 职责

- 点击检测：
  - 基础点击：`SimpleClickDetector`
  - 像素级点击（透明通道）：`PixelPerfectClickDetector`
- 交互事件配置：
  - 交互物在不同阶段（点击、收集开始、收集完成、流程完成）触发可配置事件

### 3.2 当前实现（代码依据）

- `Assets/Script/Core/SimpleClickDetector.cs`
- `Assets/Script/Core/PixelPerfectClickDetector.cs`
- `Assets/Script/Core/InteractionEventConfig.cs`
- `Assets/Script/Core/ClickEventConfig.cs` / `ClickEventType.cs`

### 3.3 约定

- 交互物脚本应优先通过 Inspector 配置事件（而不是写死引用与层级路径）。
- 事件触发不应该破坏动画流程；如事件可能会 `SetActive(false)`，需要在协程内做防御（项目内已有这种处理模式）。

---

## 4. 场景目标与进度系统（HiddenObjectManager / NumUI / GameSceneUI）

### 4.1 HiddenObjectManager（每个窗口/关卡一个）

- **职责**：
  - 自动或手动注册交互物（按 `SceneName` 过滤）
  - 缓存各类型最大数量（maxCount）
  - 从 `CollectionService` 读取当前数量（currentCount）
  - 发事件：目标找到、进度更新、关卡完成

- **当前实现**：
  - `Assets/Script/Core/HiddenObjectManager.cs`

### 4.2 NumUIController（进度展示）

- **职责**：显示 “current / max”
- **max 的来源**：扫描所有 interactables，按 `SceneName` 过滤后计数
- **当前实现**：
  - `Assets/Script/UI/NumUIController.cs`

### 4.3 GameSceneUI（房间/花海/咖啡厅通用 UI）

- **职责**：
  - 暂停菜单 / 返回按钮 / 提示按钮（Hint）
  - 目前 **Hint 与 Quit** 仍是 TODO（占位日志）
- **当前实现**：
  - `Assets/Script/UI/GameSceneUI.cs`

---

## 5. 交互物系统（Interactables）

### 5.1 类型与现状

- **普通猫**：点击切换图片 + 写入收集
- **隐藏猫**：多阶段流程（触发区 → 图 B → 图 C）+ 写入收集
- **鱼**：淡出（可缩放）+ 关闭 GameObject + 写入收集（同时有 PlayerPrefs 的单体状态保存）
- **烟花**：收集动画 + 状态持久化 + 写入收集
- **拼图块**：唯一 ID 收集 + 动画 + 写入收集（并与 Puzzle 解锁相关）

### 5.2 持久化策略（当前是“双写”）

项目内部分交互物（例如 Fish）既：
- 写入 `CollectionService`（用于全局计数/联动）
- 也写入 PlayerPrefs（用于“单个物体是否消失”的状态）

这套策略可工作，但需要明确口径：  
- **计数**以 `CollectionService` 为准  
- **物体显示/隐藏**以该交互物自己的 Key 为准（并受 `GameProgressResetService` 的 resetVersion 影响）

### 5.3 音效集成（现状）

- `AudioManager` 已存在并支持按 ID 播放 SFX  
- 但部分交互物脚本仍保留 `TODO: Integrate with AudioManager`（需要统一接入与配置）

---

## 6. 主界面联动系统（MainWnd）

### 6.1 狗盆鱼视觉反馈（DogBowlVisualFeedback）

- **职责**：根据全局 Fish 收集进度显示 4 条鱼（Fish01–Fish04），按总鱼数等分为 4 档：收集数达到 1/4、1/2、3/4、4/4 时依次点亮 Fish01–Fish04。
- **总数解析**：
  - 支持自动扫描所有 `FishInteractable`（含 inactive），可按 SceneName 白名单过滤
  - 或使用 Inspector 手填 `totalFishCount`
- **事件驱动**：监听 `CollectionService.OnGlobalCountChanged`

代码：`Assets/Script/UI/DogBowlVisualFeedback.cs`

### 6.2 首页猫提示气泡（MainCatClickHandler）

- **职责**：
  - 点击猫：若“全局鱼已找齐”显示爱心气泡；否则显示鱼气泡
  - 全局鱼总数通过扫描所有 `FishInteractable` 去重计算（与狗盆逻辑对齐）

代码：`Assets/Script/UI/MainCatClickHandler.cs`

### 6.4 门口小贴士（MainWnd / Room 入口门）

- **使用场景**：主界面或场景中的“大门”交互，带有“小贴士”弹窗与缩略图。
- **逻辑规则**：
  1. **第一次点击大门**：
     - 必定弹出“小贴士弹窗”。
     - 弹窗关闭后，可根据设计：
       - 仅关闭弹窗，不进入房间；或
       - 关闭弹窗后自动进入房间（具体由关卡 Flow 决定）。
     - 同时记录“已看过小贴士”的状态（持久化，避免重置后重复出现）。
  2. **第二次及之后点击大门（已看过小贴士）**：
     - 不再自动弹出小贴士弹窗。
     - 直接进入对应房间/场景窗口（如 `RoomWnd`）。
     - 是否被点击过第一需要被记录，重启游戏后依然生效。
  3. **点击门上方的小贴士缩略图**：
     - 无论是否已进入过房间，只要有缩略图显示，点击缩略图都会弹出“小贴士弹窗”。
     - 缩略图相当于一个“帮助按钮”，用于随时回看提示内容。
  4. **文件位置**
     - MainWnd预制体下
     - SmallTipsPaper是门上的小贴士，可被点击
     - BigTipBg是点击后展开的弹窗
     - `Text (TMP)` 文案建议改为 **第 9 章** 文本表 key + `LocalizedText`（或代码 `GetText`），避免按语言复制 Prefab


---

## 7. 提示与对话框系统（HintBubbleService / DialogService / 传统 Popup）

### 7.1 统一服务（已存在，基础版）

- `HintBubbleService`
  - 轻量气泡/Toast：显示 icon/text，持续一段时间后销毁
- `DialogService`
  - 简单模态对话：Info / ConfirmCancel

代码：
- `Assets/Script/UI/HintBubbleService.cs`
- `Assets/Script/UI/DialogService.cs`

### 7.2 传统 Popup（当前仍在使用）

项目里仍存在并被部分 UI 脚本直接使用：
- `MessagePopup`（提示文字）
- `ConfirmationPopup`（Yes/No）

规划建议（后续迭代）：
- 逐步让业务脚本改用 `HintBubbleService` / `DialogService`，减少重复弹窗逻辑与 Prefab 分裂。

---

## 8. 设置 / 音频 / 光标系统

### 8.1 SettingsManager（已实现）

- 保存/读取 `SettingsData`（PlayerPrefs JSON）
- 触发 `OnSettingsChanged`
- 应用：音量、**语言**（`SettingsData.language`，语言码字符串）、全屏、光标大小等

代码：`Assets/Script/Core/SettingsManager.cs`

### 8.2 AudioManager（已实现，待全面接入）

- BGM：自动播放背景音乐（可从 Resources fallback）
- SFX：按字符串 ID 查表播放

代码：`Assets/Script/Core/AudioManager.cs`

### 8.3 CursorManager（已实现）

- 支持 MouseX1/MouseX2（并做 DPI/缩放处理）
- 订阅 `SettingsManager` 来切换大小

代码：`Assets/Script/Core/CursorManager.cs`

### 8.4 与多语言的关系

- **语言项**在设置里由 `SettingPopUI` 写入 `SettingsData.language`；`SettingsManager.ApplySettings` 会调用 `LocalizationManager.SetLanguage`。
- 文本翻译表、`LocalizedText`、键名约定、策划配置步骤等均在 **第 9 章** 说明。

---

## 9. 多语言与本地化（Localization）

### 9.1 目标与范围

- **静态 UI**：按钮、标题、说明等，优先 **文本表 key + `LocalizedText`（TMP）**，避免按语言复制 Prefab。
- **动态文案**：含 `{0}`、`{1}` 等占位符的格式串放在文本表，代码只传参数：`GetFormattedText(key, args…)`。
- **与设置的关系**：当前语言 = `SettingsData.language`（如 `zh-CN`、`en-US`、`zh-TW`），与 `LanguageConfig` 中列出的 `languageCode` **必须一致**。

### 9.2 当前实现状态（与代码对齐）

| 能力 | 状态 | 说明 |
|------|------|------|
| 语言列表与展示名 | 已有 | `LanguageConfig`（`Assets/Resources/LanguageConfig.asset`） |
| 设置里切换语言并存档 | 已有 | `SettingPopUI` + `SettingsManager` |
| 启动时应用语言 | 已有 | `LocalizationManager.Awake` 读设置；`GameInitializer` 后 `ApplySettings` |
| 文本表 | 已有 | `LocalizationTable`（`Assets/Resources/LocalizationTable.asset`） |
| `GetText` / `GetLocalizedString` / `GetFormattedText` | 已有 | `LocalizationManager`；`GetFormattedText` 使用 `InvariantCulture` 格式化 |
| 缺失译文 fallback | 已有 | 当前语言 → `LanguageConfig.fallbackLanguageCode`（空则用列表第一项）→ 同 key 下任意非空语言 → 回退 key 并 `LogWarning` |
| UI 自动刷新 | 已有 | `LocalizedText` 订阅 `OnLanguageChanged(string languageCode)` |
| 全项目文案已迁入表 | 未要求一次完成 | 新增/改版界面时逐步替换硬编码即可 |

核心代码：

- `Assets/Script/Core/LocalizationManager.cs`
- `Assets/Script/Core/LanguageConfig.cs`
- `Assets/Script/Core/LocalizationTable.cs`
- `Assets/Script/UI/LocalizedText.cs`
- StartUp 场景：`LocalizationManager` 组件上指派 `languageConfig`、`localizationTable`

### 9.3 API 约定（实现已对齐）

- **`SetLanguage(string languageCode)`**：`languageCode` 须为 `LanguageConfig` 中已有项；未知则回退到 `GetFallbackLanguageCode()`。
- **`OnLanguageChanged`**：`Action<string>`，参数为当前 **语言码**（不是枚举）。
- **`GetText(string key)`**：等价于 `GetLocalizedString(key)`，取当前语言字符串。
- **`GetFormattedText(string key, params object[] args)`**：对表中模板做 `string.Format`（不变文化，避免数字格式随系统区域变乱）。
- 表中 **无 key** 或全部为空：返回 `key` 本身并打警告（便于发现漏配）。

### 9.4 键名约定（建议）

- 使用 **小写 + 点分段**：`模块.界面.用途`，例如：`smallgame.unlock_hint`、`ui.main.door_tip`。
- **动态提示**与 **静态 TMP** 共用同一套 key 空间，避免重复含义。
- 已在表中预置示例 key：**`smallgame.unlock_hint`**（拼图未集齐时的解锁提示模板，三语占位已填，可与 `GetFormattedText` 对接）。

### 9.5 分阶段与可选方案

- **阶段 1（已完成）**：`LocalizationTable` + 字典加载 + `GetText` / `GetFormattedText` + fallback + `LocalizedText`。
- **阶段 2（可选）**：Editor 工具：导出/导入 CSV、扫描缺失 key；按语言切换 TMP `fontAsset`（中日英混排，设置下拉已有中文字体处理，见 `SettingPopUI`）。
- **多 Prefab 分语言**：仅当某界面布局随语言差异极大时采用；默认不推荐。
- **Unity Localization Package**：长周期、多译员与 LQA 时可评估迁移；成本高于当前 ScriptableObject 方案。

### 9.6 与各系统的对接约定

- **大门小贴士（§6）**、**拼图解锁提示（§13.2.1）**、**小游戏玩法说明（§13.3）**、`DialogService` / `HintBubbleService` 的文案：优先走 **文本表 + key**；动态句用 `GetFormattedText`。
- **Small Game Unlock Hint**：推荐 key **`smallgame.unlock_hint`**；逻辑层示例：`LocalizationManager.Instance.GetFormattedText("smallgame.unlock_hint", collected, total);`

### 9.7 策划 / 程序配置流程

1. **语言列表**：在 Unity 中编辑 `Assets/Resources/LanguageConfig.asset` → `languages`：每条 `languageCode`（如 `zh-CN`）须与设置存档、文本表 `cells[].languageCode` **完全一致**。
2. **回退语言**：同一资源中 `fallbackLanguageCode`：某 key 在当前语言下无译文时尝试该码（留空则使用 `languages` **第一项**）。
3. **翻译表**：编辑 `Assets/Resources/LocalizationTable.asset` → `entries`：每条 `key` + 多条 `cells`（`languageCode` + `text`）。新增语言 = 在每条 entry 上增加对应 cell。
4. **静态界面**：在 `TextMeshProUGUI` 所在物体上添加 **`LocalizedText`**，Inspector 填写 **`textKey`**（与表中 key 一致）。
5. **动态界面**：代码只拼参数，调用 `GetFormattedText`；禁止在逻辑里写死某一语言的整句（除临时调试）。
6. **验证**：运行游戏 → 设置切换语言 → 确认文案与气泡/弹窗更新；关注 Console 中 missing key 警告。
7. **存档**：语言已含在 `SettingsData`（PlayerPrefs）中，无需额外步骤。

### 9.8 目标语言列表（策划 / 商店口径）

下列语言为产品侧计划支持或展示在语言选择中的条目；在 `LanguageConfig`、`LocalizationTable` 中新增语言时，**`languageCode` 须与下表一致**（与常见 Steam / BCP-47 习惯对齐；若与发行平台最终要求冲突，以平台为准并同步改表）。

| 展示名（用户可见） | `languageCode` | 备注 |
|-------------------|----------------|------|
| English | `en-US` | 拉丁 |
| Deutsch | `de-DE` | 拉丁扩展 |
| Français | `fr-FR` | 拉丁扩展 |
| Italiano | `it-IT` | 拉丁 |
| 한국어 | `ko-KR` | 谚文 |
| Español - España | `es-ES` | 拉丁 |
| 简体中文 | `zh-CN` | 汉字（简体） |
| 繁体中文 | `zh-TW` | 汉字（繁体，港澳台常用 `zh-TW`） |
| Русский | `ru-RU` | 西里尔 |
| ไทย | `th-TH` | 泰文 |
| 日本語 | `ja-JP` | 汉字 + 假名 |
| Português - Portugal | `pt-PT` | 拉丁 |
| Polski | `pl-PL` | 拉丁扩展 |
| Dansk | `da-DK` | 拉丁 |
| Nederlands | `nl-NL` | 拉丁 |
| Suomi | `fi-FI` | 拉丁 |
| Norsk | `nb-NO` | 拉丁（书面挪威语；若需区分可再拆） |
| Svenska | `sv-SE` | 拉丁 |
| Magyar | `hu-HU` | 拉丁扩展 |
| Čeština | `cs-CZ` | 拉丁扩展 |
| Română | `ro-RO` | 拉丁扩展 |
| Türkçe | `tr-TR` | 拉丁扩展 |
| Português - Brasil | `pt-BR` | 拉丁 |
| Български | `bg-BG` | 西里尔 |
| Ελληνικά | `el-GR` | 希腊文 |
| Українська | `uk-UA` | 西里尔 |
| Español - Latinoamérica | `es-419` | 拉丁（拉美西班牙语常用 `es-419`） |
| Tiếng Việt | `vi-VN` | 拉丁扩展 + 越南文声调字符 |
| Bahasa Indonesia | `id-ID` | 拉丁 |

### 9.9 TMP 字体、方块字与获取方式

- **界面出现「方块 / 豆腐块」**：在 TextMeshPro 里通常表示 **当前 `TMP_FontAsset` 的字形贴图（Atlas）里没有该字符**，或 **Fallback 字体链** 未能提供该 Unicode 区的字形——**不是**「翻译没加载」的独有现象；拉丁-only 的字体无法显示中文、韩文、泰文等，都会成方块。
- **一种字体能否包全**：开源里 **Noto**（Google）按文种拆分为多个字体文件（拉丁 + 西里尔 + 希腊可合并进同一 Noto Sans；**中日韩 CJK** 体积大，一般为 **Noto Sans CJK** 独立包；阿拉伯、泰文等也有独立子族）。Unity TMP 需在 **Font Asset Creator** 里为每个源字体生成 **SDF 资源**；若单张 Atlas 过大，可拆成多个 `TMP_FontAsset` 并在 **Project Settings → TextMeshPro → Fallback Font Assets** 里串成链，或按语言在代码里切换主字体（见 §9.5 阶段 2）。
- **推荐下载入口（免费可商用，以各字体 LICENSE 为准）**：
  - [Google Fonts：Noto 系列](https://fonts.google.com/noto) — 按语言/文种筛选并下载 TTF/OTF。
  - [Noto 官方仓库（GitHub：notofonts）](https://github.com/notofonts) — 各文种子仓库与发布包。
- **项目内现状**：`SettingPopUI` 对语言下拉有 **中文字体** 分支（Inspector 或 `Resources/Fonts & Materials/Chinese Font SDF`）；扩展到上表全部语种时，需保证 **下拉展示名** 与 **游戏正文 TMP** 使用的字体 / Fallback 覆盖对应脚本（至少：拉丁扩展、西里尔、希腊、CJK、泰、谚文等）。

### 9.10 推荐字体包方案（与 §9.8 语言表对齐）

**结论：源字体下载按「3 个包」准备即可**（对应 3 套文种/体积划分）；导入 Unity 后通常生成 **3 个 `TMP_FontAsset`（SDF）**，再在 **TMP Settings** 里串成 **Fallback 链**（主字体 → CJK → 泰文）。若 CJK 单包 Atlas 过大，可再拆成 SC/TC 两个 TMP 资源，但**源字体仍算同一 CJK 包**。

| # | 源字体包（名称） | 覆盖 §9.8 中的脚本 / 语言 | 下载入口 |
|---|------------------|---------------------------|----------|
| **1** | **Noto Sans**（Latin / Greek / Cyrillic） | 全部拉丁系（含越南文声调）、**希腊文**、**西里尔文**（俄 / 保 / 乌 等） | [Google Fonts：Noto Sans](https://fonts.google.com/noto/specimen/Noto+Sans) 点 **Download family**；或源码与发布包：[notofonts / latin-greek-cyrillic](https://github.com/notofonts/latin-greek-cyrillic) |
| **2** | **Noto Sans CJK** | **简体中文、繁体中文、日语、韩语**（同一 CJK 大包，按地区子集 OTF 选一款即可用于 TMP） | [notofonts / noto-cjk Releases](https://github.com/notofonts/noto-cjk/releases)（取 **Sans** 子目录下 OTF，如 `NotoSansCJKsc-*` / `tc` / `jp` / `kr`）；或 [Google Fonts：Noto Sans SC / TC / JP / KR](https://fonts.google.com/noto/fonts?noto.query=noto+sans+cjk) 按需各下一款 |
| **3** | **Noto Sans Thai** | **泰文** | [Google Fonts：Noto Sans Thai](https://fonts.google.com/noto/specimen/Noto+Sans+Thai) |

**为何是 3 个包**：包 1 负责「欧陆 + 越南 + 西里尔 + 希腊」；包 2 单独承担超大汉字与谚文、假名；包 3 承担泰文（与拉丁主字体分开是常见做法，避免单张 Atlas 策略混乱）。**商用与修改条款**以各包内 **LICENSE / OFL** 为准。

**解压 / 放置位置（建议，便于版本管理）**：

- 原始 TTF/OTF（不要改扩展名）放在 **`Assets/Fonts/Source/`** 下按包分子文件夹即可，例如：
  - `Assets/Fonts/Source/NotoSans/` — 包 1
  - `Assets/Fonts/Source/NotoSansCJK/` — 包 2（可只保留 Regular + Bold 等实际用到的字重）
  - `Assets/Fonts/Source/NotoSansThai/` — 包 3  
- 由 **Font Asset Creator** 生成的 **`TMP_FontAsset`**（`* SDF.asset` 及贴图）放在 **`Assets/TextMesh Pro/Resources/Fonts & Materials/`**（与现有 `LiberationSans SDF` 同层），或继续沿用 **`Assets/Resources/Fonts & Materials/`** 供 `Resources.Load`（如语言下拉的「中文字体」路径）；**两者择一规范即可，避免重复**。

**下载后的处理步骤（程序 / 美术）**：

1. **生成 TMP 资源**：菜单 **Window → TextMeshPro → Font Asset Creator**，分别对 **包 1 / 2 / 3** 的 **Regular**（及需要的 Bold）源字体生成 SDF，命名建议：`Noto Sans LGC SDF`、`Noto Sans CJK SDF`、`Noto Sans Thai SDF`。
2. **字符集**：首版可用 **Unicode Range** 或 **Character List** 包含项目译文与语言下拉里出现的全部字符；CJK 建议 **Dynamic** 或分阶段扩充 Atlas，避免一次性全字表爆内存。
3. **Fallback 链**：**Edit → Project Settings → TextMeshPro**，将 **`Noto Sans LGC SDF`** 设为全局默认或 UI 主字体，在其 **Fallback Font Assets** 中依次加入 **`Noto Sans CJK SDF`**、**`Noto Sans Thai SDF`**（顺序：主文 → CJK → 泰）。
4. **与设置界面一致**：在 `SettingPopUI` 里将语言下拉的 `captionText` / `itemText` 指向 **同一主 TMP 字体**（或专门做一个「语言列表用」字体资源，但 Fallback 仍建议与全局一致）。
5. **验证**：切换 §9.8 各语言码，检查下拉名与正文无方块；关注 Console 的 TMP 缺字提示。

---

## 10. 存档重置系统（GameProgressResetService）

- **职责**：重置游戏进度（收集、交互物状态），**不重置设置**
- **机制**：通过 `resetVersion` 让每个交互物在下次加载时“只重置一次”

代码：`Assets/Script/Core/GameProgressResetService.cs`

---

## 11. 排行榜与竞速模式系统（Leaderboard / Speedrun）

### 11.1 职责

- **排行榜入口（主界面奖杯按钮）**：
  - 管理“未解锁/已解锁”的视觉状态。
  - 提供点击行为：未解锁时给出提示；已解锁时打开排行榜弹窗 `RankPop`。


- **排行榜的视觉效果需求**
  - Scroll 的 **Content** 上仍可有原型行 **RankBar01–RankBar04**（仅作模板，运行时保持隐藏）。
  - 运行时由 `RankPopUI` 在 Content 下创建 **`RankRows`** 容器，用 **VerticalLayoutGroup** 纵向排列实例化的行，避免与 Content 自带布局「抢」子节点坐标。
  - 名次与模板：第 1–3 名分别 **RankBar01 / RankBar02 / RankBar03**，第 4 名及以后 **RankBar04**。可在 Inspector 为各名次指定 `rankRowPrefab*` 覆盖；未指定则从对应原型实例化。默认**不**用脚本改 Num01 的颜色/描边，以保持预制体上的 TMP 材质（如 Outline）与顶点色。



- **竞速模式开关（Speedrun Toggle）**：
  - 管理主界面竞速开关的显示与点击行为。
  - 当前阶段只做 UI 层的开/关切换，暂不接入真实竞速玩法与计时上传逻辑。

### 11.2 解锁条件（口径）

- **奖杯入口（Leaderboard）解锁条件**：
  - 默认：未解锁奖杯图片；点击后出现提示（Toast/气泡）。
  - 解锁后：替换为“已解锁奖杯”图片，成为 `RankPop` 的入口。
- **竞速开关显示条件**：
  - “首次通关游戏后”写入某个持久化标记（可通过 `UnlockChecker` 或单独的通关 Flag）。
  - 满足条件后：
    - 主界面刷新时切换为“已解锁奖杯”图片；
    - 同时显示竞速开关 `SpeedrunToggleRoot`。

> 注：奖杯入口的“收集类解锁条件（如：找到所有场景普通猫 + 隐藏猫）”与“首次通关解锁”两套口径目前在文档中都有描述。  
> 后续建议统一到 `UnlockChecker` 的同一套规则，并明确：到底是“通关解锁”还是“全收集解锁”（或两者叠加）。

### 11.3 点击行为（对外接口口径）

- **奖杯入口点击（`MainMenuUI.OnClick_OpenRank`）**：
  - **未解锁**：
    - 不进入排行榜；
    - 弹出一条提示文案（提示内容由策划在 Inspector 或配置中可配置，对应 `rankUnlockHint`）；
    - 提示以轻量气泡/Toast 形式出现，一段时间后自动消失（建议复用 `HintBubbleService`）。
  - **已解锁**：
    - 打开排行榜弹窗 `RankPop`。
- **竞速模式开关点击（`SpeedrunToggleRoot` + `SpeedrunToggleView`）**：
  - 当前阶段：只做 UI 显示层的开/关图标切换，暂不真正开启/关闭竞速玩法逻辑。
  - 后续阶段：接入“是否记录通关时间、是否上传排行榜”的具体竞速规则，再由该开关控制开启/关闭。

### 11.4 当前实现（代码依据）

- `Assets/Script/UI/MainMenuUI.cs`（奖杯入口点击 / 解锁提示文案）
- `Assets/Script/UI/RankPopUI.cs`（排行榜弹窗 `RankPop` 列表与关闭）
- `Assets/Script/Core/UnlockChecker.cs`（解锁规则承载点，当前为占位逻辑）

#### 排行榜内容逻辑
- 每一次竞速通关后生成一条记录（一行 RankBar 样式 UI）
- 排列顺序按用时从短到长
- 最新一条显示 **NewFlag**、**HighlightBg**（名称匹配容忍末尾空格，如 `NewFlag `）
- 列表挂在 Content 子节点 **`RankRows`** 下，由布局组件排布；原型 RankBar01–04 不参与显示
- 行内：**Num01(TMP)** 序号，**DateText (TMP)** 日期，**TimeText (TMP)** 时间
- 数据持久化，可重置

##### 竞速模式切换对照表

| 项目 | 普通模式 | 竞速模式 |
|------|---------|----------|
| **收集物品** | 普通猫 + 隐藏猫 + 鱼 + 拼图块 + 烟花 | 仅普通猫 + 隐藏猫 |
| **NumUI 显示** | 全部显示（猫/鱼/拼图/烟花） | 仅猫（其他隐藏） |
| **TimeBg 显示** | 隐藏 | 显示（计时累加） |
| **通关条件** | 收集所有物品 | 收集所有普通猫 + 隐藏猫 |
| **数据存储** | 普通存档（无前缀） | 竞速存档（SR_ 前缀） |
| **重置行为** | 重置普通存档 + 竞速存档 | 重置竞速存档（不关闭开关） |

> **说明**：
> - 竞速模式切换时，`NumUIController` 自动重新计算 HUD 布局可见元素并居中（`ApplyCenteredHudLayout`）
> - 普通模式切换时，当某场景无烟花节点时也会自动居中（`layoutSpacingNormal`）
> - 所有场景（RoomWnd / FlowerWnd / CafeWnd）均适用同一套居中逻辑

##### 竞速模式的逻辑
- 开关speedrunToggleRoot功能已经实现，方法没有调用完成，当开启时，竞速模式开启
- 竞速模式有单独的数据存储，可以重置，重启游戏后数据不变。普通模式的数据和竞速模式的不相关，各自保存各自的，重置则一起重置
- 竞速模式下：游戏内没有拼图块收集，烟花收集和鱼的收集，只有普通猫咪+隐藏猫咪的收集，并且NumUI也只展示普通猫咪+隐藏猫咪数量
- 竞速模式下：三个场景内的TimeBg节点显示，该节点下TimeText (TMP)代表时间，计时累加
- 1、找到所有场景内的普通猫咪+隐藏猫咪，弹出结算界面WinPop预制体
  2、结算界面中有返回主界面的按钮ButtonWin，点击返回按钮返回主界面，排行榜RankPop增加一条记录，重置竞速模式的猫咪。
  3、每一次完成竞速模式，弹出提示CompletingTheRacingModePop，提示内容RacingModePopText (TMP)：竞速关卡已经重置，通关时间已经记录到排行榜界面，

- 竞速模式的ui布局
当前状态：竞速模式三个场景中NumUI，Jigsaws、Fish、Fire被隐藏了（已经完成）
修改：隐藏后，放大镜Search距离剩余的ui距离太大，希望调整到合适距离，NumUI再整体居中（已完成）

- 界面放大功能
1、鼠标的滚轮能放大或缩小画面RoomWnd，ui的大小不变。最大和最小的缩放值，我希望我能配置
2、鼠标可以拖动画面

- 放大镜功能
1、点击放大镜，放大镜置灰，当我找到框体内的要找的物品时，再开始冷却（Search图标需要有从上到下的恢复原状的动画），默认冷却时间1分钟（冷却时间我可以配置），
2、当我切换场景时，Search需要重新开始1分钟冷却，比如从RoomWnd返回到MainWnd时，Search需要重新冷却，当我MainWnd进入RoomWnd时也需要重新冷却，切换场景后Search需立即开始冷却的
3、普通猫咪被全部找到后，如果玩家继续点击，则未被找到的物品继续提示，先后顺序如下，普通猫咪>隐藏猫咪>拼图块>鱼>烟花


2、画面中出现提示框，提示框大小固定，如果提示框不在屏幕显示的范围内，出现提示的手的图片，指向提示框
3、出先提示框后，放大镜还未开始冷却，只有当玩家找到提示框内的指定猫咪后，才开始冷却倒计时
4、提示指定的猫咪，不一定出现在提示框的正中间，只要出现在提示框内即可，位置需要在提示框内随机，不要被提示框遮挡（所以需要被提示猫咪距离提示边框有一定安全距离，这个值可以设置）
5、以RoomWnd为例，手的图片是CatHand，提示框是PromptBox ，放大镜是Search



7、提示框PromptBox，出现的时候，它下层的被提示的物品应当都能被点击
8、提示框PromptBox的消失是渐隐（需要可以设置渐隐时长）
9、手CatHand沿用以前的逻辑，不需要做放大缩小动画，大小为原图的0.4倍，三个场景内统一使用；永远是处于屏幕中央的，如果画面中能看到PromptBox，那么CatHand不出现；如果画面中看不到PromptBox，那么出现手CatHand，实时指向PromptBox（就是我移动画面依然是指向PromptBox的），当PromptBox出现的大于1/4时，手CatHand就消失（渐隐，需要可以设置渐隐时长）

---

---

## 12. 解锁系统（UnlockChecker）

- **现状**：占位逻辑（用“是否至少收集过一些”来替代“是否找齐”）
- **目标**：接入“每个场景每种物品的上限/目标数”，以真正实现：
  - 排行榜/竞速解锁
  - 小游戏解锁
  - 场景入口解锁（例如房间→花海）

代码：`Assets/Script/Core/UnlockChecker.cs`

---

## 13. 游戏完成系统（GameCompletionService）

### 13.1 职责

- 在普通模式（非竞速）下，检测玩家是否找到了所有普通猫和隐藏猫
- 当满足完成条件时显示 `WinPop` 结算界面
- 记录游戏完成状态（用于排行榜解锁等）

### 13.2 与竞速模式的区分

| 项目 | 普通模式 | 竞速模式 |
|------|---------|----------|
| **完成检测** | `GameCompletionService` | `SpeedrunService` |
| **完成条件** | 找到所有普通猫 + 隐藏猫 | 找到所有普通猫 + 隐藏猫（独立存档） |
| **结算界面** | `WinPop` | `WinPop` |
| **完成后操作** | 返回主界面 | 返回主界面 + 记录排行榜 |

### 13.3 当前实现

- `Assets/Script/Core/GameCompletionService.cs`
- 通过 `CollectionService.OnGlobalCountChanged` 事件监听收集进度
- 使用 `UnlockChecker.TryGetTotalCatsForSpeedrun` 获取目标总数
- 在 `WindowManager.CurrentWindow` 下查找 `WinPop` 节点并显示
- `GameInitializer` 负责初始化单例
- `GameProgressResetService` 负责重置完成状态

---

## 14. 拼图小游戏系统（Puzzle）

### 13.1 系统规则（当前实现）

- 入口窗口：`SmallGameWnd`
- 核心控制器：`PuzzleController`
- 支持：
  - 初始化、打乱
  - 点击选择两块交换
  - 完成检测
  - **保存布局**：
    - 运行期静态保存（退出小窗口再进入可恢复）
    - 持久化保存（跨重启）

代码：
- `Assets/Script/UI/PuzzleController.cs`
- `Assets/Script/UI/PuzzlePieceController.cs`
- `Assets/Script/UI/SmallGameWndUI.cs`
- `Assets/Script/Core/PuzzleData.cs`

### 13.2 仍需补齐（后续）

- 未解锁/未收集齐时的“玩家可见提示”（目前多处为 TODO）
- 与通用 `DialogService` / `HintBubbleService` 的统一提示接入

#### 13.2.1 Small Game Unlock Hint（解锁提示文案格式）

- **Unlock Hint Messages 分组**：
  - Small Game Unlock Hint Format（拼图小游戏解锁提示）
- **默认文案格式（中文示例）**：
  - `还需要收集 {0}/{1} 块拼图解锁`
  - 其中：
    - `{0}`：当前已收集的拼图块总数（3 个场景之和，从 `CollectionService.GetPuzzlePieceCount()` 或类似接口获取）
    - `{1}`：拼图块总数（来自 `PuzzleData.TotalPieces`）
- **展示场景**：
  - 玩家在 `MainWnd` 或 `RoomWnd` 中点击拼图小游戏入口，但还未收集齐全部拼图块时：
    - 不进入 `SmallGameWnd`
    - 通过 `HintBubbleService` 或轻量气泡在入口附近显示该提示
- **多语言支持方式**：
  - 方案 A（过渡）：每种语言一份预制体，仅当布局差异极大时采用。
  - 方案 B（推荐）：**第 9 章** 文本表 + `LocalizationManager.GetFormattedText`；表中 key 建议使用 **`smallgame.unlock_hint`**（已在 `LocalizationTable` 预置三语模板）。
  - 代码示例：`LocalizationManager.Instance.GetFormattedText("smallgame.unlock_hint", collected, total);`

### 13.3 小游戏玩法提示（Puzzle Help）

- **展示方式**：
  - 在首次进入 `SmallGameWnd` 时，弹出一次性“玩法说明弹窗”（可由策划开关）。
  - 也可通过一个“帮助 / 玩法说明”按钮手动再次查看（建议放在 `SmallGameWndUI` 中）。
- **玩法文案（示例，可在配置中编辑）**：
  1. “用鼠标点击第一块您想移动的拼图，它会高亮显示。接着点击第二块不同的拼图，这两块拼图的位置会立即互相交换。”
  2. “您可以不断重复点击交换的操作，当构成一幅完整、连贯的画面时游戏即可通关。”
- **实现要点**：
  - 文案和是否开启“首次自动弹出”在配置中可调整；多语言见 **第 9 章**（文本表 key + `LocalizedText` 或 `GetText`）。
  - 建议依托 `DialogService` 或 `HintBubbleService`：
    - 若用对话框：点击“我知道了”关闭。
    - 若用气泡/Toast：支持一定时间后自动消失（但推荐首次进入使用可确认的对话框）。


## 14. 动效与杂项需求

- 动效需求
1. 点击猫咪和物品特效
当猫咪和物品被找到时播放Click粒子特效，特效位置在被找到猫咪或者物品的位置，Click是个预制体

2. 竞速模式开关和奖杯解锁粒子逻辑
Mainwnd中，当玩家第一次解锁竞速模式时，播放trophyButtonImage和speedrunToggleRoot节点下的RewardStar粒子，切换界面再回来，不消失，当玩家点击一次后才消失（点击后粒子消失是点击竞速模式开关或者奖杯按钮后对应的粒子消失，点击了哪个粒子消失哪个，不是点击后同时消失），重置游戏后也会消失。

3. SmallGameWnd结算动画逻辑表现
当拼图完成后
1、拼图块的描边缓慢消失（0.5s），视觉上看上去是一幅完整的画了，
2、调用结算界面WinPop，阶段界面下层的按钮、交互不能点击
3、播放粒子动画WinEffect和WinEffect02，
4、再次进入界面拼图画面是完整的状态，拼图不可再点击移动（重置后需要可以点击，再次打乱拼图顺序），点击拼图区域（就是GameImg的区域）出现提示弹窗，提示弹窗为 `CompleteMiniGamePrompt`。



第一步：把源字体放进工程
在工程里建目录（示例，可自定）
Assets/Fonts/Source/Noto/
把已选好的 .ttf 拷进去，例如：
NotoSans_SemiCondensed-Regular.ttf
NotoSansSC-Regular.ttf
NotoSansTC-Regular.ttf
NotoSansJP-Regular.ttf
NotoSansKR-Regular.ttf
NotoSansThai_SemiCondensed-Regular.ttf
不需要进包的多余字体（印地语、阿拉伯等）不要拷进 Assets，可减小工程与误用风险。
回到 Unity，等 Import 完成（底部进度条）。




第二步：用 Font Asset Creator 生成 TMP 字体（每个源字体一个 SDF）
对 每一个 要用的 .ttf 各生成 一个 TMP_FontAsset（* SDF.asset）。

菜单：Window → TextMeshPro → Font Asset Creator。
Source Font File：选当前要处理的 .ttf。
Sampling Point Size：常用 90（默认即可，可按需要微调）。
Padding：默认即可。
Packing Method：Fast 即可先跑通。
Atlas Resolution：先 2048 或 4096；若后面报 atlas 满再加大或改字符策略。
Character Set（重要，小游戏可先简单、再收紧）：

TMP 的静态图集只能装「你指定的一批字形」。下面三种方式任选其一，本质都是在回答：**哪些字符要打进这张 SDF 纹理里**。

1. **Unicode Range（按码位区间）**  
   不单独维护「字符列表」文件，而是按 Unicode 标准区间批量包含，例如：Basic Latin、Latin-1 Supplement、某段 CJK 等。  
   **优点**：省事、不用收集具体字。  
   **缺点**：区间一大，图集很容易爆（体积、生成时间、内存）；小游戏往往只需要其中一小部分字。

2. **Characters from File（从文件读入字符）**  
   这里的 **「字符列表」** 指：**你自己准备的一个纯文本文件**（如 `.txt`），里面写上**所有希望打进图集的字符**——可以一行一个、也可以多行、或一整段连续字符串，只要文件里出现的**不重复字符**都会被当作要烘焙的字形来源（以 Unity/TMP 版本说明为准，常见用法是「文件里出现的每个字符各算一个」）。  
   **适用**：文案已冻结、或能从脚本/表格导出「全游戏用到的字」时，用文件批量维护，比手贴在 Inspector 里更稳、可版本管理。  
   **文件放哪**：Unity **没有强制路径**。建议放在 **工程 `Assets/` 下**任意固定目录并纳入版本库，例如与源字体并列：`Assets/Fonts/Source/Noto/CharSets/game_chars_sc.txt`，或单独：`Assets/Fonts/CharSets/`。在 Font Asset Creator 里点选该 txt 即可；它只在 **编辑器里生成 SDF 时**读一次，**不会**因为放在 `Assets` 里就自动进包——真正进包的是生成出来的 `* SDF.asset`。勿放在工程外磁盘路径，否则其他人克隆仓库后容易丢引用。

3. **Custom Characters（自定义字符，直接贴在窗口里）**  
   **不需要**再单独准备一个「字符列表」文件。你在 Font Asset Creator 的 **Custom Characters** 大文本框里 **直接粘贴** 一段字即可，这段粘贴内容本身就是你的「字符集合」。  
   **更省事的做法**：把下面几类字一次性粘进去（去重即可）：游戏里会出现的正文/按钮文案用字、**语言下拉里每一种语言的展示名**（避免下拉项缺字）、常用标点与数字 `0–9`。  
   若与第 2 种对比：**File = 列表在磁盘上的 `.txt`；Custom = 列表在编辑器粘贴框里**，二者都是「显式列出要哪些字」，只是载体不同。

**先跑通怎么选**：想最快看到效果 → 常用 **Unicode Range**（先选较小、够用的区间）或 **Custom Characters**（随便粘一小段测试字）；已有整理好的全文用字表 → 用 **Characters from File**。  
字特别多时，可再考虑 Dynamic（见文末说明）。
Font Style：Regular 即可（你打算用引擎里加粗，见第五步）。
Font Render Mode：SDFAA（默认）。
Generate Font Atlas → 成功后 Save 或 Save as…。
建议保存到（与现有工程一致）：
Assets/TextMesh Pro/Resources/Fonts & Materials/
命名示例：
NotoSans LGC SDF（来自 NotoSans_SemiCondensed-Regular）
NotoSans SC SDF
NotoSans TC SDF
NotoSans JP SDF
NotoSans KR SDF
NotoSans Thai SDF
说明：CJK 四个分开时，就要生成 四个 SDF；泰文、西文各一个。




第三步：主字体 + Fallback 链（避免混排缺字）

**目的**：同一段 TMP 文本里可能混用多种脚本（例如英文 UI + 中文句子 + 韩文专名）。**主字体**（NotoSans LGC SDF）的 Atlas 里不可能包含所有汉字/谚文/泰文，因此要在 **主 `TMP_FontAsset` 上挂一串 Fallback**：某个字符在主字体里找不到时，TMP **按列表顺序**依次去后面的 SDF 里找，直到找到或用尽。

**前置条件**（第二步已完成）：
- 工程中已有独立的 `TMP_FontAsset` 资源，例如：  
  `NotoSans LGC SDF`、`NotoSans SC SDF`、`NotoSans TC SDF`、`NotoSans JP SDF`、`NotoSans KR SDF`、`NotoSans Thai SDF`（名称以你实际生成为准）。

**在 Unity 里操作（主字体上配置）**：

1. 在 **Project** 窗口定位到主字体资源（例如 `Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSans LGC SDF.asset`），**单击选中**。
2. 看 **Inspector**。在 **TextMeshPro Font Asset** 组件上找到与 Fallback 相关的区域（不同 Unity / TMP 版本文案略有差异，常见为以下之一）：
   - **Fallback Font Assets**：`List<TMP_FontAsset>`，带 **+ / -** 可增删条目；
   - 或 **Fallback Font Asset Table**：表格形式，同样可添加多行。
3. 点击 **+** 增加条目，**每个条目拖入一个**上述已生成的 `*.asset`（必须是 **TMP Font Asset**，不要拖原始 `.ttf`）。
4. **按顺序** 填入（**从上到下 = 查找顺序**）。推荐默认顺序如下（与 §9.8 多语言、中日韩泰拆分方案一致）：
   1. `NotoSans SC SDF`（简体中文）
   2. `NotoSans TC SDF`（繁体中文）
   3. `NotoSans JP SDF`（日文）
   4. `NotoSans KR SDF`（韩文/谚文）
   5. `NotoSans Thai SDF`（泰文）  
   **微调原则**：若游戏正文以 **繁体** 为主、简体极少，可将 **TC 调到 SC 前**；若某语种几乎不出现，仍可保留在链中（略增一次查找开销），或按需删减。**不要**把同一文种的重复 SDF 加两次。
5. **保存资源**：`Ctrl+S`（Windows）或 **File → Save**，确保 `.asset` 已写入磁盘。

**运行时行为（便于排查缺字）**：
- 渲染每个字符时：**先在当前使用的 `TMP_FontAsset`（通常是主字体）的 Atlas 里查**；没有则进入 **Fallback 列表第 1 项** → 仍没有则 **第 2 项** … 直到 **最后一项** 仍没有 → 常见表现为 **方块（□）** 或 **替代字形**（视 TMP 设置而定）。
- 因此：**主字体 + Fallback 链**解决的是「**字形有没有**」，不是「**翻译有没有**」；翻译仍由本地化表负责。

**常见坑**：
- **链里顺序写反**：例如泰文段落却先查 JP，会多几次无效查找；一般不会致命，但建议按「最常用脚本靠前」排列。
- **Fallback 指向空或未生成的 SDF**：拖错对象、或子资源未导入成功 → 该步直接被跳过，缺字依旧。
- **子字体的 Atlas 里本身没有该字**：Fallback 也救不了，需在对应源字体上 **重新生成 Atlas / 扩大 Character Set / 换 Dynamic**（见前文 § 第二步与文末 Dynamic 说明）。
- **循环引用**：一般不要在子字体上再把主字体加回 Fallback，除非你很清楚 TMP 的解析规则；常规做法是 **只在主字体上** 挂一长串 Fallback。

**与第四步的关系**：第三步是 **「主 Noto LGC 这一份资源」上的链**；第四步还会在 **Project Settings → TextMeshPro** 里配 **全局 Default + 全局 Fallback**。两处建议 **与第三步同一套顺序**，避免只配一处导致部分界面仍缺字（见下一步）。

第四步：设为全局默认（新项目物体、未单独指定字体的 TMP 都会用）
Edit → Project Settings → TextMeshPro。
Default Font Asset：选 NotoSans LGC SDF。
Fallback Font Assets（全局列表）：同样把 SC / TC / JP / KR / Thai 加进去（与第三步一致即可；若第三步已挂在主字体上，有些版本以主资源上的 Fallback 为准，两处都配齐最省心）。
你当前 TMP Settings.asset 里 m_fallbackFontAssets: [] 是空的，这里一定要填，否则全局缺字时无处可退。

第五步：加粗（你选的方案：只用 Regular 源字体，在引擎里做「粗」）

**背景（为什么单独讲一步）**  
你只准备了 **Regular** 的 `.ttf`（没有 `Bold.ttf`），所以 TMP **里没有一个叫 Bold 的独立字重文件** 可用。此时若仍需要「标题更粗、按钮更醒目」，有两种常见做法：**用 TMP 自带的样式模拟粗体**，或 **以后再加一套真正的 Bold 字体 SDF**。

**5.1 术语：真粗体 vs 合成粗体**

- **真粗体**：字体设计师为 **Bold** 重画了一套笔画更粗的轮廓；需要 **Bold.ttf** → 再生成 **`NotoSans LGC Bold SDF`** 这类资源，在 **Font** 里选它。  
- **合成粗体（假粗）**：没有 Bold 文件时，TMP 在 **Regular SDF** 上通过算法把字形 **加粗/描边式** 变粗，**能省一个 Bold 包**，但大字、标题上有时 **边缘略糊、略不均匀**。

**5.2 做法 A：Font Style → Bold（最常用）**

1. 在 **Hierarchy** 里选中带 **TextMeshProUGUI** 的物体（例如标题、按钮文字）。  
2. 看 **Inspector** 最下方 **TextMeshProUGUI** 组件。  
3. 展开 **Extra Settings**（若折叠，点左侧小三角）。  
4. 找到 **Font Style**：  
   - 点 **B**（Bold），表示这段文字用 **粗体样式**；  
   - 可同时勾 **I**（Italic）等，视需求而定。  
5. **前提**：该文本的 **Font** 已指向你的 **`NotoSans LGC SDF`**（或带 Fallback 的主字体）；**Bold** 是在 **当前 SDF** 上做的样式，**不会**自动去换另一个「Bold 字体文件」。  
6. **效果**：若只有 Regular SDF，即为 **合成粗体**；若以后你为同一字体做了 **Bold SDF**，可在 **Font** 里直接换成 Bold 资源，或继续用 **B** 做叠加（一般二选一即可，避免重复加粗）。

**5.3 做法 B：Material / Outline / Underlay（强调「看起来像粗」，不是字重）**

- **Outline**：在 **TextMeshProUGUI** 上 **Material Preset** 选带 Outline 的材质，或在 **Inspector** 里 **Outline** 厚度、颜色可调，字外圈一圈线，**视觉上更醒目**，但和印刷意义上的「Bold 字重」不完全一样。  
- **Underlay**：字下方一层阴影/衬底，适合**可点击感、悬浮感**，同样不是替换字重。  
- 适用：按钮、标签需要「强调」又不想加粗整个段落时。

**5.4 什么时候该补「真正的 Bold 字重」**

- 大标题、重要 UI 在 **Font Style B** 后仍觉得 **糊、发虚、粗细不匀**；  
- 或希望与 **Noto** 官方 **Bold** 视觉完全一致。  

此时：**下载对应字重的 `.ttf`（如 `NotoSans-Bold.ttf`）** → 用 **Font Asset Creator** 再生成 **`NotoSans LGC Bold SDF`** → 需要粗体的 **TMP** 把 **Font** 直接换成 **Bold SDF**，**Font Style** 一般改回 **Normal**（避免对 Bold 再合成一次粗）。

**5.5 小结**

| 你的情况 | 建议 |
|----------|------|
| 只有 Regular SDF | 用 **Extra Settings → Font Style → B** 做合成粗体；或 **Outline** 做强调。 |
| 标题仍不满意 | 再为同一字体增加 **Bold.ttf → Bold SDF**，粗体处 **换 Font 为 Bold SDF**。 |

第六步：设置界面语言下拉（与当前 `SettingPop.prefab` 结构对齐）

**预制体路径**：`Assets/Prefabs/Pop/SettingPop.prefab`

**层级（Language 相关，自上而下）**：

```
SettingPop                    ← 根物体，挂 `SettingPopUI`
└── LanguageBtn               ← 语言区域容器
    └── Dropdown              ← `TMP_Dropdown`（组件名显示为 Dropdown）
        ├── Label             ← **Caption Text**（当前选中项显示）
        ├── Arrow             ← 箭头（无 TMP 文本）
        └── Template          ← 下拉列表模板，默认 **未激活**，Inspector 里仍可展开子节点
            └── Viewport
                └── Content
                    └── Item
                        ├── Item Background
                        └── Item Label        ← **Item Text**（列表每一项文字）
```

**当前工程里的引用（便于你对照要改什么）**：

- 根物体 **`SettingPop`** 的 **`SettingPopUI`**：
  - **`Language Dropdown`** → 子物体 **`Dropdown`** 上的 `TMP_Dropdown`。
  - **`Chinese Font Asset`** → 当前指向 **`ArialUnicodeMS-B SDF`**（`Assets/TextMesh Pro/Resources/Fonts & Materials/ArialUnicodeMS-B SDF.asset`）。运行时 **`SetupChineseFont()`** 会把它赋给 Caption / Item Text，并遍历 **`Template`** 下所有 **`TextMeshProUGUI`**（见 `SettingPopUI.cs`）。
- **`Dropdown` → `Label`**、**`Template` → … → `Item Label`** 上 **`Font Asset`** 当前为 **`LiberationSans SDF`**（与全局默认一致）；运行后会被脚本覆盖为 **`Chinese Font Asset`**。

**推荐操作（按优先级）**：

1. **双击打开** `Assets/Prefabs/Pop/SettingPop.prefab`，进入 **Prefab 编辑**（或 Scene 里已展开的实例，改完记得 **Apply** 到 Prefab）。
2. 选中 **`SettingPop`**（根），在 **Inspector** 找到 **`SettingPopUI`**：
   - 将 **`Chinese Font Asset`** 改为你的 **`NotoSans LGC SDF`**（或你希望下拉统一使用的主 `TMP_FontAsset`；需已按第二～四步配好 **Fallback**，以便多语言脚本不缺字）。
3. **（建议）同步改 Prefab 上的静态引用**，避免编辑器里预览仍是旧字、也避免未执行脚本的路径仍指向 Liberation：
   - 选中 **`LanguageBtn` → `Dropdown`**，在 **`TMP_Dropdown`** 组件上确认：
     - **Caption Text** 指向子物体 **`Label`**；
     - **Item Text** 指向 **`Template/Viewport/Content/Item/Item Label`**（一般已自动绑定，勿清空）。
   - 分别选中 **`Label`** 与 **`Item Label`**，在 **`TextMeshProUGUI`** 中将 **Font Asset** 设为 **`NotoSans LGC SDF`**（与上一步一致）。
4. **若清空 `Chinese Font Asset`**：运行时改走 **`Resources.Load("Fonts & Materials/Chinese Font SDF")`**。只有当你把 **`NotoSans LGC SDF`**（或副本）放进 **`Assets/Resources/Fonts & Materials/`** 且命名为 **`Chinese Font SDF`** 时才与旧路径兼容；否则请 **保留 Inspector 赋值** 或改代码/资源路径。

**说明**：`Template` 在层级里可能是灰色（未激活），**不必**为编辑而强行勾选激活；直接展开子级即可。若 **`SettingPopUI`** 已正确指定 **`Chinese Font Asset`**，进入 Play 后 Caption 与列表项会统一使用该字体。

第七步：已有界面上的 TMP
带 LocalizedText 的物体：一般只改 父物体/同物体上 TMP 的 Font 为 NotoSans LGC SDF（或依赖默认字体——若 Prefab 里写死了 LiberationSans，需批量改或去掉覆盖）。
可在 Hierarchy 里搜 TextMeshProUGUI，抽查是否仍引用 LiberationSans SDF，逐个替换或通过 Edit → Find References 处理。
第八步：运行验证
进 Play，打开 设置 → 语言，每种语言看 下拉项 是否无方块。
切语言后看 主界面/弹窗 译文是否正常。
看 Console：TMP 有时会提示缺字或 atlas 问题，按提示加字符或调 Dynamic / 分辨率。
可选：Dynamic 字体（字特别多、不想一次塞满表）
在 Font Asset Creator 里对某个 SDF 勾选 Dynamic（具体以你 Unity 版本界面为准）。
运行时按需扩展字形，首包可能更小，但有 运行时开销 与 内存 取舍；小游戏字数少时常用 静态表 + 合理 Character Set 即可。
小结清单
步骤	做什么
1	TTF 放进 Assets/Fonts/Source/...
2	每个 TTF 生成一个 * SDF.asset 到 TextMesh Pro/Resources/Fonts & Materials/
3	在 Project 选中 NotoSans LGC SDF → Inspector 的 Fallback Font Assets（或 Table）按顺序加入 SC、TC、JP、KR、Thai 的 TMP Font Asset，保存
4	Project Settings → TextMeshPro 设 Default + 全局 Fallback
5	加粗：见第五步（Regular 时用 Font Style **B** 合成粗体；要真粗体再另做 Bold SDF）
6	见第六步：`SettingPop.prefab` → `SettingPop` 上 `SettingPopUI.chineseFontAsset`，及 `LanguageBtn/Dropdown` 的 `Label`、`Item Label` 的 Font，统一为 `NotoSans LGC SDF`（或 Resources 同名路径）
7	旧 Prefab 上仍绑 LiberationSans 的改为新字体
8	Play 全语言测一遍
按上面做完，字体文件就真正接到 TMP + 设置 + 本地化 这条链路上了。若你希望我直接改工程里 SettingPopUI / Resources 路径或补一段「启动时设默认字体」的代码，可以说一下你 Unity 版本和 Prefab 路径，我可以按仓库结构改一版




## 15.多语言的文本 ##
1. 更新流程：先更新Char.txt的内容（我如果提供了一种语言，请补全翻译其他语言），再同步配置到LocalizationTable中ui.main.door_tip。
2. 多语言的的内容
1 点击奖杯未解锁显示的解锁提示如下（MyHintBubbleRoot节点）：
第一次通关拼图游戏后解锁

2 未解锁拼图游戏时，点击拼图游戏入口按钮显示如下（3代表已经收集的拼图块，12表示所有场景有的拼图块，提示框节点为：SmallGameHintBubbleRoot）：
还需要3/12块拼图解锁
	
3 拼图游戏玩法提示如下（节点HelpTipsPanel）：	
1、用鼠标点击第一块您想移动的拼图，它会高亮显示。接着点击第二块不同的拼图，这两块拼图的位置会立即互相交换。
2、您可以不断重复点击交换的操作,当构成一幅完整、连贯的画面时游戏即可通关

4 完成小游戏后，点击拼图区域显示提示（节点CompleteMiniGamePrompt）：
恭喜，已经完成拼图啦！

5 门上便签提示（节点是BigTipBg，已完成，可以参考）：
我亲爱的朋友：
真遗憾没能等到你，我先赶往小镇处理一件要事。房门未锁，请你就像回家一样，随意进来坐坐。
屋里有很多小捣蛋鬼，你若觉得寂寥，不妨和它们一起打发时光。
对了，窗边椅子上那幅未完成的拼图，似乎被它们弄丢了几片。如果你愿意，请帮我留意找寻。

5 设置页面点击重置按钮弹出的二次确认弹窗(还没有逻辑，要配置好东西的)：
确定要重置游戏数据吗？
	
6 按下主界面的右上角的叉号弹出二次确认弹窗：
退出游戏？

## 音乐配置文件 ##

1、新游戏背景音乐音量未50%
2、音效是100%
重置游戏后音乐、音效的音量依旧未上面的样子


### 1、背景音乐（自动切换）

| 界面 | 资源文件 | 说明 |
|------|---------|------|
| MainWnd | 01Main | 主界面 |   可以直接使用，第二款游戏
| RoomWnd | 02Room | 房间界面 |
| FlowerWnd | 03fowler | 花店界面 |
| CafeWnd | 04Cafe | 咖啡厅界面（默认，烟花未完成时） |
| CafeWnd | 05Cafe | 咖啡厅烟花全部收集后自动切换，循环播放 |

切换逻辑由 `AudioManager.ApplyBgmForWindowPrefab()` 控制。进入 CafeWnd 时根据烟花完成状态选择 `04Cafe` 或 `05Cafe`；点亮最后一个烟花时通过 `CafeWndFireworksCelebration.TriggerCelebrationAudioOnce()` 切换到 `05Cafe` 并播放 `Fire` 音效；退出 CafeWnd 时 `ApplyBgmForWindowPrefab()` 切走 `05Cafe`。`_celebrationAudioTriggered` 标志确保切换仅触发一次，避免重复。

---

### 2、音效（通过 `AudioManager.PlaySfx("id")` 调用）

| 音效 ID | 触发场景 | 代码位置 |
|---------|---------|---------|
| miao01~miao11（共11个） | 普通猫咪收集，隐藏猫咪触发（第二下点击） | `NormalCatInteractable.cs`、`HiddenCatInteractable.cs` |
| HiddenCatsFinded | 找到隐藏猫咪（第一下点击完成收集） | `HiddenCatInteractable.cs` |
| miao01 | 点击 MainWnd 中的 CatPop | `MainCatClickHandler.cs` |


| Fire | 烟花动画播放时 | `FireworkInteractable.cs` |
| FireFind | 烟花被收集时 | `FireworkInteractable.cs` |
| Fish01 | 收集鱼时 | `FishInteractable.cs` |
| clickBlock | 拼图块收集完成（从界面消失） | `PuzzlePieceInteractable.cs` |
| popup | 小游戏拼图交换碎片 | `PuzzleController.cs` |
| spin_get | 放大镜提示节点、各界面 Search 节点、 | `RoomWndUI.cs`、`MainMenuUI.cs`、`HintMagnifierService.cs`、`SmallGameWndUI.cs` |
奖杯解锁、小游戏未解锁的时候使用locked
| overNewRecord | 胜利彩带（WinPop 调用时） | `SmallGameWndUI.cs` |
| DoorOpen | 主界面门节点点击，进入房间时 | `MainMenuUI.cs` |
| WindowOpen | 普通猫咪收集后，窗户动画打开 | `NormalCatInteractable.cs` |
| Paper01 | 大提示面板 BigTipBg、帮助提示 HelpTipsPanel | `SmallGameWndUI.cs`、`MainMenuUI.cs` |
| Paper02 | 奖杯解锁提示、完成竞速模式提示、是否重置游戏提示、小游戏气泡提示、迷你游戏完成提示 | `SettingPopUI.cs`、`SpeedrunService.cs`、`CompleteMiniGamePromptFeature.cs`、`SmallGameWndUI.cs` |

Common02使用的地方：
roomwnd中的BackBtn，smallGameEntryImage解锁状态的按下声音，MainWnd中的CloseBtn、SettingBtn，设置界面的内的相关按钮。重置界面确认按钮、关闭游戏的确认按钮。
增加使用Common02的情况：
1、设置界面中MouseToggle、ScreenToggle、LanguageBtn以及子节点，Sound，Music，Yes和No的按钮
2、Mainwnd界面中CloseButtonTips节点下的Yes和No的按钮

#### 通用按钮音效配置

通用按钮（如 BackBtn、SettingBtn、CloseBtn 等）建议使用 `ButtonSfxPlayer` 组件，在 Inspector 中填写 `sfxId`（目前 Prefab 中已配置 DoorOpen）。Common 音效文件存在但尚未在 Prefab 中分配。

#### 音效播放方法参考

```csharp
// 在任何脚本中调用
AudioManager.Instance?.PlaySfx("你的音效ID");
AudioManager.Instance?.PlayRandomCatMeow();   // 随机猫叫（miao01~11）
AudioManager.Instance?.PlayMainCatPopMeow();  // 主界面 CatPop 猫叫（miao01）
AudioManager.Instance?.PlayCatHidden();        // 找到隐藏猫咪
AudioManager.Instance?.PlayCatNormal();         // 普通猫叫
AudioManager.Instance?.PlayPuzzlePiece();      // 拼图块音效（clickBlock）
AudioManager.Instance?.PlayVictory();          // 胜利音效（overNewRecord）
```





4、快速测试需求
问题：比如我需要测试结算，猫咪太多，全部找出来，再出结算会很浪费时间
需求：当我在"检查器"关闭普通猫咪或者隐藏猫咪或者鱼或者拼图块或者烟花时，界面中对应物品的数量希望不要统计关闭的对应物品数量
对应的逻辑需要同步优化
1、Mainwnd中狗盆鱼显示对应逻辑
2、Mainwnd中CatHintBubble01、CatHintBubble02的显示逻辑 目前是不对的
3、重启或者重置后，鱼的数量上限需要一致，目前是不对的

5、bug优化
1、音效修改

3、画面切换波点动画
-- 溶解分为三界阶段
--第一阶段，所在界面的溶解，时长0.5s，时长我需要可以手动设置，**圆形的纯色波点**矩阵排列，波点的数量我也希望可以手动设置，纯色波点从小到大
--第二阶段，呈现出纯色的画面，色彩的值我需要可以设置，**窗口切换在此阶段完成**
--第三阶段，出现需要进入的界面，圆形波点从大到小


4、区域限制：16：9的画面区域外，超出 16:9 区域的处理
计算当前屏幕 vs 16:9 的映射：根据 _ScreenParams 获取实际屏幕宽高比，与目标 16:9 比值比较
如果屏幕更宽（21:9 等）→ 左右生成黑边（竖直方向填满）
如果屏幕更窄（4:3 等）→ 上下生成黑边（水平方向填满）
黑边区域：在三个阶段的处理方式：

阶段1（溶解消失）：完全填纯色
阶段2（纯色停留）：完全填纯色
阶段3（波点出现）：从纯色渐隐到透明
16:9 区域内：波点动画保持原有效果不变

适用范围：
- Loading页面进入主界面的过渡效果
- 界面之间的切换过渡效果（如 MainWnd ↔ RoomWnd）
- 所有使用波点溶解过渡的场合

技术实现要点：
- Shader (CircularDissolveTransition.shader) 负责16:9区域的裁剪和黑边渲染
- WindowTransitionEffect 创建的过渡Canvas为全屏，通过Shader处理区域映射
- LetterboxController 管理的UICamera viewport限制了游戏UI的可见区域
- 黑边颜色与LetterboxController的letterboxColor保持一致



启动游戏，loading界面进入主界面的第一次波点切换，逻辑和表现都对了，
但是目前的问题：其他界面的切换都是不对的。比如当我从Mainwnd切换到RoomWnd是不正确，
需要和loading界面进入主界面的第一次波点切换一样，帮我检查修复



-Loading页面内元素的比例问题
默认的时候：16：9的画面（2560乘以1440）
如果屏幕更宽（21:9 等）→ 则Logo和LoadingText (TMP)以高适配，等比例缩放
如果屏幕更窄（4:3 等）→ 则Logo和LoadingText (TMP)以宽适配，等比例缩放
