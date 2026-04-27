# 鼠标光标使用说明

## 概述
`CursorManager` 用于管理游戏中的自定义鼠标光标，支持两种光标状态：
- **MouseX1**: 普通状态光标
- **MouseX2**: 悬停/点击状态光标

## 准备图片资源

### 方法 1：使用 Resources 文件夹（推荐）
1. 在 `Assets/Resources` 文件夹下创建 `Cursor` 文件夹（如果不存在）
2. 将你的鼠标光标图片放入该文件夹：
   - `MouseX1.png` - 普通光标
   - `MouseX2.png` - 悬停光标
3. 确保图片格式为 PNG，且支持透明背景
4. **重要：修复图片导入设置**
   - 在 Unity 编辑器中，点击菜单 `Tools > Fix Cursor Textures Import Settings`
   - 这会自动配置图片为可读模式（Cursor.SetCursor 需要）
   - 或者手动设置：选择图片 → Inspector → 勾选 "Read/Write Enabled"

### 方法 2：在 Inspector 中直接指定
1. 在 Unity 编辑器中找到 `CursorManager` 组件（在 GameInitializer 场景中）
2. 直接将 `MouseX1` 和 `MouseX2` 的图片拖拽到对应的字段中

## 设置光标热点（Hotspot）
光标热点是鼠标点击的精确位置，通常是光标的尖端。

1. 在 `CursorManager` 的 Inspector 中设置：
   - `Normal Cursor Hotspot`: MouseX1 的热点位置（例如：如果光标尖端在左上角，设置为 (0, 0)）
   - `Hover Cursor Hotspot`: MouseX2 的热点位置

## 在代码中使用

### 基本使用
```csharp
using HiddenCats.Core;

// 设置为普通光标
CursorManager.Instance.SetNormalCursor();

// 设置为悬停光标
CursorManager.Instance.SetHoverCursor();

// 重置为普通光标
CursorManager.Instance.ResetCursor();
```

### 在 UI 按钮上使用
可以在按钮的 `OnPointerEnter` 和 `OnPointerExit` 事件中切换光标：

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using HiddenCats.Core;

public class ButtonCursorHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetHoverCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetNormalCursor();
        }
    }
}
```

## 故障排除

### 光标没有显示？
1. **检查图片导入设置**：
   - 点击 Unity 菜单 `Tools > Fix Cursor Textures Import Settings`
   - 或手动设置：选择图片 → Inspector → 勾选 "Read/Write Enabled"
   - 重新导入图片后重启游戏

2. **检查控制台日志**：
   - 查看是否有 `[CursorManager]` 相关的警告或错误信息
   - 确认图片是否成功加载

3. **检查文件路径**：
   - 确保图片在 `Assets/Resources/Cursor/` 文件夹中
   - 文件名必须是 `MouseX1.png` 和 `MouseX2.png`（区分大小写）

## 注意事项
1. 光标图片建议尺寸：32x32 或 64x64 像素
2. 确保图片有透明背景（PNG 格式）
3. 如果图片未找到，游戏会使用系统默认光标，并在控制台显示警告
4. `CursorManager` 会在游戏启动时自动初始化，无需手动创建
5. **图片必须启用 "Read/Write Enabled" 才能作为光标使用**
