# PlugHub Ribbon 高级布局控件设计

日期：2026-05-31

## 背景

当前 PlugHub 设置页已经支持用户调整插件包、功能、分组、显示名、图标、顺序和按钮大小。运行时 Ribbon 的生成模型仍是：

```text
ViewGroupConfiguration -> Revit RibbonPanel
FeatureConfiguration -> Revit PushButton
FeatureConfiguration.ButtonSize -> large / small
```

这个模型能满足大按钮、小按钮和小按钮自动堆叠，但不能表达 Revit Ribbon 中更常用的组合控件：

- `PushButton`
- `SplitButton`
- `PulldownButton`
- `AddStackedItems` 形成的 2 到 3 个小控件组合

如果继续只在 `FeatureConfiguration` 上追加字段，例如 `buttonType`、`parentId`、`containerId`，配置会变成扁平的父子引用表，难以校验和编辑。SplitButton 的默认功能、PulldownButton 的子项顺序、Stack 的 2 到 3 项限制都会散落在功能字段中，设置页也很难给用户清晰反馈。

因此下一阶段需要把“功能定义”和“Ribbon 布局定义”分离：功能仍描述可执行命令，Ribbon layout 描述这些功能如何被组合成 Revit UI。

## 目标

本设计的目标是：

- 支持用户在设置页中组合使用 `PushButton`、`SplitButton`、`PulldownButton` 和小控件堆叠。
- 保留现有插件包和配置的兼容性，旧配置仍能按分组和按钮大小渲染。
- 让 Ribbon 布局成为显式、可验证、可迁移的配置，而不是散落在 feature 字段中的隐式规则。
- 保持业务命令路由不变，所有可点击功能仍通过稳定框架 slot 命令进入 `FeatureCommandDispatcher`。
- 明确 Revit 2020 限制：Ribbon 结构变化保存后需要重启 Revit 才能可靠生效。
- 为后续更强的可视化布局设计器保留数据结构，但首期只实现可维护的树形布局编辑。

## 非目标

本阶段不做以下内容：

- 不引入 `AssemblyLoadContext`、AppDomain 沙箱或新的热重载承诺。
- 不改变业务插件 DLL 的 shadow-copy 加载策略。
- 不让设置页实时重绘当前 Revit 会话中的 Ribbon。
- 不实现像 Revit 原生 Ribbon 一样的像素级预览设计器。
- 不允许布局配置直接绑定外部业务 DLL。业务命令仍只能来自已发现的 feature。
- 不在 Framework 层引用 Revit API。Revit 控件实例化仍留在 `PlugHub.Revit2020` 适配层。

## 推荐方案

采用显式 Ribbon layout 模型。

旧模型：

```text
Group -> RibbonPanel
Feature -> PushButton
ButtonSize -> large / small
```

新模型：

```text
RibbonTab
  Panel
    PushButton
    SplitButton
      PushButton children
    PulldownButton
      PushButton children
    Stack
      PushButton / PulldownButton children
```

Feature 继续是“可执行功能”的权威来源。Ribbon layout 只引用 feature，不复制 `commandAssembly`、`commandType` 或插件业务信息。

## 配置模型

在 `RibbonConfiguration` 下增加高级布局字段：

```json
{
  "ribbon": {
    "tabName": "PlugHub",
    "fallbackPanelName": "其他工具",
    "layoutVersion": "1.0",
    "panels": [
      {
        "id": "model-tools",
        "name": "模型工具",
        "order": 100,
        "items": [
          {
            "type": "pushButton",
            "featureId": "level-visibility.toggle",
            "size": "large"
          },
          {
            "type": "pulldownButton",
            "id": "visibility-more",
            "text": "可见性",
            "iconPath": "builtin:visibility",
            "items": [
              { "type": "pushButton", "featureId": "view.show-levels" },
              { "type": "pushButton", "featureId": "view.hide-levels" }
            ]
          },
          {
            "type": "splitButton",
            "id": "batch-tools",
            "text": "批处理",
            "defaultFeatureId": "batch.apply",
            "items": [
              { "type": "pushButton", "featureId": "batch.apply" },
              { "type": "pushButton", "featureId": "batch.preview" }
            ]
          },
          {
            "type": "stack",
            "id": "quick-stack",
            "items": [
              { "type": "pushButton", "featureId": "quick.a" },
              { "type": "pulldownButton", "id": "quick.more", "text": "更多", "items": [
                { "type": "pushButton", "featureId": "quick.b" },
                { "type": "pushButton", "featureId": "quick.c" }
              ]}
            ]
          }
        ]
      }
    ]
  }
}
```

建议新增配置类：

```text
RibbonPanelLayoutConfiguration
RibbonItemLayoutConfiguration
RibbonFeatureReferenceConfiguration
```

`RibbonItemLayoutConfiguration` 使用 `Type` 区分控件：

- `pushButton`
- `splitButton`
- `pulldownButton`
- `stack`

通用字段：

- `id`：容器控件 ID。`pushButton` 可以省略，由 `featureId` 派生。
- `text`：容器显示名。`pushButton` 默认使用 feature 显示名。
- `iconPath`：容器图标。`pushButton` 默认使用 feature 图标。
- `order`：同级排序。设置页拖拽后会重写顺序。
- `items`：容器子项。

`pushButton` 专用字段：

- `featureId`：引用已发现功能。
- `size`：`large` 或 `small`。在 `stack` 内默认按小控件处理。
- `textOverride`：可选显示名覆盖。为空时使用 feature 显示名。
- `iconPathOverride`：可选图标覆盖。为空时使用 feature 图标。

`splitButton` 专用字段：

- `defaultFeatureId`：默认点击功能。必须引用 `items` 中的一个 feature。
- `items`：至少 2 个 `pushButton` 子项。

`pulldownButton` 专用字段：

- `items`：至少 1 个 `pushButton` 子项。

`stack` 专用字段：

- `items`：2 到 3 个子项。
- 首期允许子项类型为 `pushButton` 或 `pulldownButton`。
- 不允许嵌套 `stack` 或 `splitButton`，避免 Revit API 支持边界不清。

## 兼容与迁移

### 旧配置继续工作

如果 `RibbonConfiguration.Panels` 为空，运行时继续使用当前兼容路径：

1. 根据 `ViewGroupConfiguration` 创建 Ribbon panel。
2. 根据 `FeatureConfiguration.Group` 把 feature 放入 panel。
3. 根据 `FeatureConfiguration.ButtonSize` 渲染大按钮或小按钮。
4. 小按钮仍按现有规则最多 3 个一组调用 `AddStackedItems`。

### 迁移为高级布局

设置页提供“迁移为高级布局”动作：

1. 读取当前 view groups 和 feature rows。
2. 每个 group 生成一个 `RibbonPanelLayoutConfiguration`。
3. 每个 feature 生成一个 `pushButton` item。
4. 保留现有顺序、显示名、图标和按钮大小。
5. 保存后以 `ribbon.panels` 为权威布局。

迁移是显式动作，不在普通保存时自动执行，避免用户未理解高级布局前改变配置结构。

### 回退为基础布局

设置页可以提供“恢复基础布局”动作：

1. 清空 `ribbon.panels`。
2. 保留 feature 的 `Group`、`Order`、`ButtonSize`。
3. 下次启动回到旧兼容渲染路径。

这个动作需要确认，因为 PulldownButton、SplitButton 和 Stack 容器信息会被丢弃。

## 运行时组合

新增 `RibbonLayoutComposer`，输入为：

- `ViewConfiguration`
- `FeatureViewCompositionResult`
- 当前可用 `FeatureViewModel` 列表

输出为：

```text
RibbonLayoutViewModel
  Panels
    Items
      PushButtonViewModel
      SplitButtonViewModel
      PulldownButtonViewModel
      StackViewModel
```

组合规则：

- layout 引用的 feature 必须存在于当前可见 feature 列表中。
- layout 没引用的可见 feature 放入 fallback panel，或在设置中标记为“未放置功能”。首期推荐放入 fallback panel，避免功能因为布局遗漏而不可见。
- 同一个 feature 被重复引用时，默认保留第一处，后续引用产生 warning 并跳过。未来可增加允许重复的显式开关。
- 容器控件本身不占命令 slot。每个可点击 feature 占一个 slot。
- `FeatureSlotRegistry.Replace` 仍按最终可点击 feature 顺序写入映射。

## Revit 2020 渲染适配

`FeatureRibbonBuilder` 改为消费 `RibbonLayoutViewModel`，不再直接按 group 遍历 feature。

渲染规则：

- `pushButton`：创建 `PushButtonData`，使用当前 slot 对应的 `FrameworkFeatureCommandSlots.CommandTypeFor(slotId)`。
- `pulldownButton`：创建 `PulldownButtonData`，再把子 `PushButtonData` 加入 PulldownButton。
- `splitButton`：创建 `SplitButtonData`，再把子 `PushButtonData` 加入 SplitButton。默认项优先使用 `defaultFeatureId`；如果 Revit 2020 API 对默认项设置不可用，则把默认项排在第一位。
- `stack`：把 2 到 3 个子控件传给 `RibbonPanel.AddStackedItems`。如果子项数量不合法，静态验证报错，运行时跳过该 stack 并写 warning。
- 框架固定“设置”入口继续保留在 `框架` panel，不进入用户高级布局首期编辑范围。

所有 Revit API 类型只出现在 `PlugHub.Revit2020` 项目中。`PlugHub.Framework` 只产生中立 view model。

## 设置页交互

新增或重构为 `Ribbon 布局` 页签。

页面区域：

- 左侧：功能池。展示当前可用但未放入布局的 feature，包含功能名、插件包、分类和图标。
- 中间：布局树。展示 Tab、Panel、PushButton、SplitButton、PulldownButton、Stack 和子项。
- 右侧：属性面板。根据当前选中节点显示可编辑属性。

布局树支持的操作：

- 新增 panel。
- 新增 pushButton、splitButton、pulldownButton、stack。
- 从功能池把 feature 放入 panel、container 或 stack。
- 调整同级顺序。
- 移动 pushButton 到其他 panel 或 container。
- 删除布局项。删除只移除布局引用，不删除插件功能。
- 把 pushButton 转为 pulldownButton 子项。
- 把多个 pushButton 组合为 stack。
- 把多个同类功能组合为 pulldownButton 或 splitButton。

属性面板：

- Panel：显示名、顺序。
- PushButton：引用功能、显示名覆盖、图标覆盖、大小。
- PulldownButton：显示名、图标、子项顺序。
- SplitButton：显示名、图标、默认功能、子项顺序。
- Stack：子项顺序。

用户保存后提示：

```text
Ribbon 布局已保存。Ribbon 结构、容器控件、图标和按钮大小需要重启 Revit 后生效。
```

## 静态验证

`PlugHub.StaticValidation` 增加 Ribbon layout 验证。

错误级别：

- layout schema 字段类型错误。
- panel id 重复。
- 容器 id 在同一 panel 内重复。
- layout 引用不存在的 featureId。
- `splitButton` 子项少于 2。
- `splitButton.defaultFeatureId` 不在子项中。
- `pulldownButton` 子项少于 1。
- `stack` 子项不是 2 到 3 个。
- `stack` 包含不支持的子项类型。
- 可点击 feature 数超过 `FeatureSlotRegistry.MaxSlots`。

警告级别：

- 同一个 feature 被多个 layout item 引用。
- 可见 feature 没有被 layout 引用，将进入 fallback panel。
- 容器缺少 `text` 且无法从默认 feature 推导显示名。
- 容器缺少图标，将使用默认图标。

验证仍通过现有命令运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

## 文档与用户说明

需要更新：

- `docs/architecture.md`：说明 Ribbon layout 新模型和 Framework/Revit 适配边界。
- `docs/development.md`：说明布局变更需要重启 Revit。
- `docs/plugin-development.md`：说明插件包只声明功能，不直接声明高级 Ribbon 容器；高级布局由用户设置保存。
- `config/schemas`：增加 layout schema 或扩展现有 package/view schema。

用户文案重点：

- “功能”是可执行命令。
- “布局项”是 Ribbon 上的显示方式。
- 删除布局项不会卸载插件。
- Ribbon 结构变更需要重启 Revit。

## 实施顺序建议

1. 增加配置模型和 JSON 序列化兼容。
2. 增加 `RibbonLayoutComposer` 和 view model。
3. 改造 `FeatureRibbonBuilder`，让它从布局树渲染 Revit 控件。
4. 增加静态验证规则。
5. 增加设置页 `Ribbon 布局` 页签，先采用 TreeView + 属性面板。
6. 增加从基础布局迁移到高级布局的显式动作。
7. 更新文档和验收说明。

## 验收标准

本阶段完成后应满足：

- 旧配置不包含 `ribbon.panels` 时，Ribbon 渲染结果与当前版本保持一致。
- 高级布局能渲染 PushButton、PulldownButton、SplitButton 和 Stack。
- SplitButton 默认功能能按配置优先显示；若 Revit 2020 API 限制默认项设置，则默认功能排在第一位。
- PulldownButton 和 SplitButton 内部功能点击后仍走框架 slot 调度。
- 设置页可以创建、编辑、排序和删除布局项。
- 删除布局项不会删除插件包或 feature。
- 静态验证能阻止非法布局进入发布版本。
- 本地构建通过，静态验证通过。
- 不声称 Revit 实机通过，除非后续在 Revit 2020 中实际启动并点击验证。
