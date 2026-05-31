# PlugHub Ribbon 高级布局控件实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 让用户在设置页中用 PushButton、PulldownButton、SplitButton 和 Stack 组合配置 Revit Ribbon 布局，同时保持旧分组/按钮大小配置可用。

**架构：** 把“功能定义”和“Ribbon 布局定义”分离：`FeatureConfiguration` 仍描述可执行功能，`RibbonConfiguration.Panels` 描述 UI 组合。`PlugHub.Framework` 只生成中立 `RibbonLayoutViewModel`，`PlugHub.Revit2020` 负责把布局树渲染成 Revit Ribbon 控件并继续通过稳定 slot 调度业务命令。

**技术栈：** C# net48、WPF、Autodesk Revit 2020 UI API、JavaScriptSerializer、PlugHub.StaticValidation、PowerShell、MSBuild。

---

## 文件结构

### 配置模型

- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
  - 在 `RibbonConfiguration` 中增加 `LayoutVersion` 和 `Panels`。
  - 新增 `RibbonPanelLayoutConfiguration` 和 `RibbonItemLayoutConfiguration`。

### Framework 组合层

- 创建：`src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs`
  - 定义中立 Ribbon 布局 view model，不引用 Revit API。
- 创建：`src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs`
  - 把 `ViewConfiguration` 和 `FeatureViewModel` 列表组合成布局树。
  - 兼容旧配置：没有 `ribbon.panels` 时按当前 group/size 生成布局。
  - 高级布局中未放置的可见功能进入 fallback panel。

### Revit 2020 渲染层

- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`
  - 改为消费 `RibbonLayoutComposer` 产出的布局树。
  - 渲染 `PushButtonData`、`PulldownButtonData`、`SplitButtonData` 和 `AddStackedItems`。
  - 继续使用 `FeatureSlotRegistry.Replace` 和 `FrameworkFeatureCommandSlots.CommandTypeFor(slotId)`。

### 设置页

- 创建：`src/PlugHub.Revit2020/Settings/Rows/RibbonLayoutNodeRow.cs`
  - 表示设置页 TreeView 中的 panel、pushButton、pulldownButton、splitButton、stack 节点。
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RibbonFeaturePoolRow.cs`
  - 表示功能池中的可放置 feature。
- 修改：`src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs`
  - 增加 `RibbonLayoutNodes` 和 `RibbonFeaturePool` 集合。
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
  - 增加 `Ribbon 布局` 页签。
  - 增加迁移为高级布局、恢复基础布局、添加容器、删除布局项、保存布局的交互。

### 静态验证和文档

- 修改：`src/PlugHub.StaticValidation/Program.cs`
  - 增加 Ribbon layout 结构和实现形状验证。
- 修改：`docs/architecture.md`
- 修改：`docs/development.md`
- 修改：`docs/plugin-development.md`
  - 说明功能与布局分离、布局变更需要重启 Revit、插件包不直接声明高级 Ribbon 容器。

---

### 任务 1：增加 Ribbon layout 配置模型

**文件：**
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `Program.Main()` 的验证调用列表中，在 `ValidateRevitRibbonAdapter();` 前加入：

```csharp
ValidateRibbonLayoutConfigurationModels();
```

在 `Program.cs` 中加入方法：

```csharp
private static void ValidateRibbonLayoutConfigurationModels()
{
    var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
    Require(configurationModels.Contains("public string LayoutVersion { get; set; }"), "RibbonConfiguration must expose LayoutVersion.");
    Require(configurationModels.Contains("public List<RibbonPanelLayoutConfiguration> Panels { get; set; }"), "RibbonConfiguration must expose Panels.");
    Require(configurationModels.Contains("public sealed class RibbonPanelLayoutConfiguration"), "Ribbon panel layout configuration must exist.");
    Require(configurationModels.Contains("public sealed class RibbonItemLayoutConfiguration"), "Ribbon item layout configuration must exist.");
    Require(configurationModels.Contains("public string Type { get; set; }"), "Ribbon item layout configuration must expose Type.");
    Require(configurationModels.Contains("public string FeatureId { get; set; }"), "Ribbon item layout configuration must expose FeatureId.");
    Require(configurationModels.Contains("public string DefaultFeatureId { get; set; }"), "Ribbon item layout configuration must expose DefaultFeatureId.");
}
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `RibbonConfiguration must expose LayoutVersion`。

- [ ] **步骤 3：增加配置模型**

在 `RibbonConfiguration` 中改成：

```csharp
public sealed class RibbonConfiguration
{
    public string TabName { get; set; } = "PlugHub";
    public string FallbackPanelName { get; set; } = "Framework";
    public string LayoutVersion { get; set; } = string.Empty;
    public List<RibbonPanelLayoutConfiguration> Panels { get; set; } = new List<RibbonPanelLayoutConfiguration>();
}
```

在 `RibbonConfiguration` 后新增：

```csharp
public sealed class RibbonPanelLayoutConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<RibbonItemLayoutConfiguration> Items { get; set; } = new List<RibbonItemLayoutConfiguration>();
}

public sealed class RibbonItemLayoutConfiguration
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;
    public string DefaultFeatureId { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string TextOverride { get; set; } = string.Empty;
    public string IconPathOverride { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<RibbonItemLayoutConfiguration> Items { get; set; } = new List<RibbonItemLayoutConfiguration>();
}
```

- [ ] **步骤 4：运行验证确认通过**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过，输出包含：

```text
passed: modules=0, features=0, views=1, presets=0
```

- [ ] **步骤 5：Commit**

```powershell
git add src/PlugHub.Framework/Configuration/ConfigurationModels.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: add ribbon layout configuration models"
```

---

### 任务 2：增加中立 RibbonLayoutComposer

**文件：**
- 创建：`src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs`
- 创建：`src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `ValidateRibbonLayoutConfigurationModels();` 后加入：

```csharp
ValidateRibbonLayoutComposerShape();
```

新增方法：

```csharp
private static void ValidateRibbonLayoutComposerShape()
{
    var composerPath = "src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs";
    var viewModelPath = "src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs";
    Require(File.Exists(FullPath(composerPath)), "RibbonLayoutComposer must exist.");
    Require(File.Exists(FullPath(viewModelPath)), "RibbonLayoutViewModel must exist.");

    var composer = ReadText(composerPath);
    var viewModel = ReadText(viewModelPath);
    Require(composer.Contains("class RibbonLayoutComposer"), "RibbonLayoutComposer class must exist.");
    Require(composer.Contains("Compose(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)"), "RibbonLayoutComposer must expose Compose(ViewConfiguration, features).");
    Require(composer.Contains("BuildLegacyLayout"), "RibbonLayoutComposer must preserve legacy group-based layout.");
    Require(composer.Contains("BuildConfiguredLayout"), "RibbonLayoutComposer must support configured ribbon panels.");
    Require(composer.Contains("AppendUnplacedFeatures"), "RibbonLayoutComposer must keep visible unplaced features reachable.");
    Require(!composer.Contains("Autodesk.Revit"), "RibbonLayoutComposer must not reference Revit API.");
    Require(viewModel.Contains("public sealed class RibbonLayoutViewModel"), "RibbonLayoutViewModel type must exist.");
    Require(viewModel.Contains("public const string PushButton = \"pushButton\""), "Ribbon layout item type constants must include pushButton.");
    Require(viewModel.Contains("public const string PulldownButton = \"pulldownButton\""), "Ribbon layout item type constants must include pulldownButton.");
    Require(viewModel.Contains("public const string SplitButton = \"splitButton\""), "Ribbon layout item type constants must include splitButton.");
    Require(viewModel.Contains("public const string Stack = \"stack\""), "Ribbon layout item type constants must include stack.");
}
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `RibbonLayoutComposer must exist`。

- [ ] **步骤 3：创建 RibbonLayoutViewModel**

创建 `src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Framework.Composition
{
    public sealed class RibbonLayoutViewModel
    {
        public RibbonLayoutViewModel(IReadOnlyList<RibbonPanelViewModel> panels, IReadOnlyList<FeatureViewModel> clickableFeatures)
        {
            Panels = panels ?? new List<RibbonPanelViewModel>();
            ClickableFeatures = clickableFeatures ?? new List<FeatureViewModel>();
        }

        public IReadOnlyList<RibbonPanelViewModel> Panels { get; }
        public IReadOnlyList<FeatureViewModel> ClickableFeatures { get; }
    }

    public sealed class RibbonPanelViewModel
    {
        public RibbonPanelViewModel(string id, string name, int order, IReadOnlyList<RibbonItemViewModel> items)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Order = order;
            Items = items ?? new List<RibbonItemViewModel>();
        }

        public string Id { get; }
        public string Name { get; }
        public int Order { get; }
        public IReadOnlyList<RibbonItemViewModel> Items { get; }
    }

    public sealed class RibbonItemViewModel
    {
        public const string PushButton = "pushButton";
        public const string PulldownButton = "pulldownButton";
        public const string SplitButton = "splitButton";
        public const string Stack = "stack";

        public RibbonItemViewModel(
            string type,
            string id,
            string text,
            string iconPath,
            string size,
            FeatureViewModel feature,
            string defaultFeatureId,
            IReadOnlyList<RibbonItemViewModel> items)
        {
            Type = string.IsNullOrWhiteSpace(type) ? PushButton : type.Trim();
            Id = id ?? string.Empty;
            Text = text ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            Size = size ?? string.Empty;
            Feature = feature;
            DefaultFeatureId = defaultFeatureId ?? string.Empty;
            Items = items ?? new List<RibbonItemViewModel>();
        }

        public string Type { get; }
        public string Id { get; }
        public string Text { get; }
        public string IconPath { get; }
        public string Size { get; }
        public FeatureViewModel Feature { get; }
        public string FeatureId => Feature == null ? string.Empty : Feature.FeatureId;
        public string DefaultFeatureId { get; }
        public IReadOnlyList<RibbonItemViewModel> Items { get; }

        public IReadOnlyList<FeatureViewModel> ClickableFeatures()
        {
            var result = new List<FeatureViewModel>();
            CollectClickableFeatures(this, result);
            return result;
        }

        private static void CollectClickableFeatures(RibbonItemViewModel item, List<FeatureViewModel> result)
        {
            if (item == null) return;
            if (string.Equals(item.Type, PushButton, StringComparison.OrdinalIgnoreCase) && item.Feature != null)
            {
                result.Add(item.Feature);
            }

            foreach (var child in item.Items ?? new List<RibbonItemViewModel>())
            {
                CollectClickableFeatures(child, result);
            }
        }
    }
}
```

- [ ] **步骤 4：创建 RibbonLayoutComposer**

创建 `src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Composition
{
    public sealed class RibbonLayoutComposer
    {
        public RibbonLayoutViewModel Compose(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            var visibleFeatures = features ?? new List<FeatureViewModel>();
            var ribbon = view.Ribbon ?? new RibbonConfiguration();
            var panels = ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>();
            return panels.Any()
                ? BuildConfiguredLayout(ribbon, visibleFeatures)
                : BuildLegacyLayout(view, visibleFeatures);
        }

        private static RibbonLayoutViewModel BuildLegacyLayout(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)
        {
            var panels = features
                .GroupBy(feature => new { feature.GroupId, feature.GroupName, feature.GroupOrder })
                .OrderBy(group => group.Key.GroupOrder)
                .ThenBy(group => group.Key.GroupName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new RibbonPanelViewModel(
                    SafeId(group.Key.GroupId, group.Key.GroupName),
                    SafeText(group.Key.GroupName, view.Ribbon != null ? view.Ribbon.FallbackPanelName : "Framework"),
                    group.Key.GroupOrder,
                    group.OrderBy(feature => feature.DisplayOrder)
                        .ThenBy(feature => feature.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                        .Select(feature => PushItem(feature, feature.ButtonSize, string.Empty, string.Empty))
                        .ToList()))
                .ToList();

            return new RibbonLayoutViewModel(panels, features.ToList());
        }

        private static RibbonLayoutViewModel BuildConfiguredLayout(RibbonConfiguration ribbon, IReadOnlyList<FeatureViewModel> features)
        {
            var featuresById = features
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var placedFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var panels = (ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>())
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                .Select(panel => new RibbonPanelViewModel(
                    SafeId(panel.Id, panel.Name),
                    SafeText(panel.Name, panel.Id),
                    panel.Order,
                    BuildConfiguredItems(panel.Items, featuresById, placedFeatureIds)))
                .ToList();

            AppendUnplacedFeatures(ribbon, panels, features, placedFeatureIds);
            var clickable = panels.SelectMany(panel => panel.Items).SelectMany(item => item.ClickableFeatures()).ToList();
            return new RibbonLayoutViewModel(panels, clickable);
        }

        private static List<RibbonItemViewModel> BuildConfiguredItems(
            IEnumerable<RibbonItemLayoutConfiguration> items,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            return (items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(item => item.Order)
                .ThenBy(item => SafeText(item.Text, item.Id), StringComparer.OrdinalIgnoreCase)
                .Select(item => BuildConfiguredItem(item, featuresById, placedFeatureIds))
                .Where(item => item != null)
                .Cast<RibbonItemViewModel>()
                .ToList();
        }

        private static RibbonItemViewModel BuildConfiguredItem(
            RibbonItemLayoutConfiguration item,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            var type = string.IsNullOrWhiteSpace(item.Type) ? RibbonItemViewModel.PushButton : item.Type.Trim();
            if (string.Equals(type, RibbonItemViewModel.PushButton, StringComparison.OrdinalIgnoreCase))
            {
                FeatureViewModel feature;
                if (!featuresById.TryGetValue(item.FeatureId ?? string.Empty, out feature)) return null;
                if (!placedFeatureIds.Add(feature.FeatureId)) return null;
                return PushItem(feature, item.Size, item.TextOverride, item.IconPathOverride);
            }

            var children = BuildConfiguredItems(item.Items, featuresById, placedFeatureIds);
            return new RibbonItemViewModel(
                type,
                SafeId(item.Id, item.Text),
                SafeText(item.Text, item.Id),
                item.IconPath ?? string.Empty,
                item.Size ?? string.Empty,
                null,
                item.DefaultFeatureId ?? string.Empty,
                OrderDefaultFeatureFirst(type, item.DefaultFeatureId, children));
        }

        private static List<RibbonItemViewModel> OrderDefaultFeatureFirst(string type, string defaultFeatureId, List<RibbonItemViewModel> children)
        {
            if (!string.Equals(type, RibbonItemViewModel.SplitButton, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(defaultFeatureId))
            {
                return children;
            }

            return children
                .OrderBy(child => string.Equals(child.FeatureId, defaultFeatureId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(child => child.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AppendUnplacedFeatures(
            RibbonConfiguration ribbon,
            List<RibbonPanelViewModel> panels,
            IReadOnlyList<FeatureViewModel> features,
            ISet<string> placedFeatureIds)
        {
            var unplaced = features
                .Where(feature => !placedFeatureIds.Contains(feature.FeatureId))
                .OrderBy(feature => feature.GroupOrder)
                .ThenBy(feature => feature.DisplayOrder)
                .ThenBy(feature => feature.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!unplaced.Any()) return;

            panels.Add(new RibbonPanelViewModel(
                "fallback",
                SafeText(ribbon.FallbackPanelName, "其他工具"),
                int.MaxValue,
                unplaced.Select(feature => PushItem(feature, feature.ButtonSize, string.Empty, string.Empty)).ToList()));
        }

        private static RibbonItemViewModel PushItem(FeatureViewModel feature, string size, string textOverride, string iconPathOverride)
        {
            return new RibbonItemViewModel(
                RibbonItemViewModel.PushButton,
                SafeId(feature.FeatureId, feature.DisplayName),
                SafeText(textOverride, feature.DisplayName),
                string.IsNullOrWhiteSpace(iconPathOverride) ? feature.IconPath : iconPathOverride,
                string.IsNullOrWhiteSpace(size) ? feature.ButtonSize : size,
                feature,
                string.Empty,
                new List<RibbonItemViewModel>());
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty).Trim() : value.Trim();
        }

        private static string SafeId(string value, string fallback)
        {
            var source = SafeText(value, fallback);
            return string.IsNullOrWhiteSpace(source) ? "ribbon-item" : source;
        }
    }
}
```

- [ ] **步骤 5：运行验证确认通过**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过。

- [ ] **步骤 6：Commit**

```powershell
git add src/PlugHub.Framework/Composition src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: compose advanced ribbon layouts"
```

---

### 任务 3：让 FeatureRibbonBuilder 渲染高级布局控件

**文件：**
- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在 `ValidateRevitRibbonAdapter()` 中把 token 列表扩展为：

```csharp
foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "PulldownButtonData", "SplitButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "FeatureCommandDispatcher", "FeatureSlotRegistry" })
{
    Require(adapterText.Contains(token), "missing Revit adapter token: " + token);
}
```

在 `ValidateRuntimeRoutingSpecification()` 中加入：

```csharp
Require(ribbonBuilder.Contains("new RibbonLayoutComposer().Compose"), "Ribbon builder must consume RibbonLayoutComposer.");
Require(ribbonBuilder.Contains("AddPulldownButton"), "Ribbon builder must render pulldown buttons.");
Require(ribbonBuilder.Contains("AddSplitButton"), "Ribbon builder must render split buttons.");
Require(ribbonBuilder.Contains("AddStackItemData"), "Ribbon builder must render stacked layout item data.");
Require(ribbonBuilder.Contains("RibbonItemData"), "Ribbon builder must pass generic RibbonItemData into AddStackedItems.");
Require(ribbonBuilder.Contains("layout.ClickableFeatures"), "Ribbon slot assignment must use clickable features from the layout tree.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `Ribbon builder must consume RibbonLayoutComposer`。

- [ ] **步骤 3：接入布局组合器和 slot 分配**

在 `FeatureRibbonBuilder.Build()` 中替换 ordered groups 逻辑：

```csharp
var layout = new RibbonLayoutComposer().Compose(view, composition.Features);
var slotAssignments = BuildSlotAssignments(layout.ClickableFeatures);
FeatureSlotRegistry.Replace(slotAssignments.SlotToFeatureId, slotAssignments.SkippedFeatureIds);

foreach (var skippedFeatureId in slotAssignments.SkippedFeatureIds)
{
    Trace.TraceWarning("PH-FEATURE-SLOT-LIMIT: Feature was not assigned a Revit command slot: " + skippedFeatureId);
}

foreach (var panelModel in layout.Panels)
{
    var panelName = SafeDisplayName(panelModel.Name, fallbackPanelName);
    var panel = GetOrCreatePanel(application, tabName, panelName);
    AddRibbonLayoutItems(panel, panelModel.Items, slotAssignments.FeatureIdToSlot);
}
```

保留 `AddFrameworkButtons(GetOrCreatePanel(application, tabName, "框架"));`，并保留没有 feature 时清空 slot registry 的分支。

- [ ] **步骤 4：新增布局渲染方法**

在 `FeatureRibbonBuilder` 中新增：

```csharp
private void AddRibbonLayoutItems(RibbonPanel panel, IEnumerable<RibbonItemViewModel> items, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    foreach (var item in items ?? new List<RibbonItemViewModel>())
    {
        if (string.Equals(item.Type, RibbonItemViewModel.Stack, StringComparison.OrdinalIgnoreCase))
        {
            AddStackLayout(panel, item, featureIdToSlot);
            continue;
        }

        var data = CreateRibbonItemData(item, featureIdToSlot);
        if (data == null) continue;
        if (panel.GetItems().Any(existing => string.Equals(existing.Name, data.Name, StringComparison.OrdinalIgnoreCase))) continue;
        var added = panel.AddItem(data);
        PopulateContainer(added, item, featureIdToSlot);
    }
}

private RibbonItemData CreateRibbonItemData(RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    if (string.Equals(item.Type, RibbonItemViewModel.PushButton, StringComparison.OrdinalIgnoreCase))
    {
        return CreateFeatureButtonData(item, featureIdToSlot);
    }

    if (string.Equals(item.Type, RibbonItemViewModel.PulldownButton, StringComparison.OrdinalIgnoreCase))
    {
        var data = new PulldownButtonData(SafeInternalName(item.Id), SafeDisplayName(item.Text, "更多"));
        data.Image = LoadFeatureIcon(item.IconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
        data.LargeImage = LoadFeatureIcon(item.IconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();
        return data;
    }

    if (string.Equals(item.Type, RibbonItemViewModel.SplitButton, StringComparison.OrdinalIgnoreCase))
    {
        var data = new SplitButtonData(SafeInternalName(item.Id), SafeDisplayName(item.Text, "工具"));
        data.Image = LoadFeatureIcon(item.IconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
        data.LargeImage = LoadFeatureIcon(item.IconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();
        return data;
    }

    return null;
}
```

- [ ] **步骤 5：新增容器填充和 stack 渲染**

在 `FeatureRibbonBuilder` 中新增：

```csharp
private void PopulateContainer(RibbonItem added, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    var pulldown = added as PulldownButton;
    if (pulldown != null)
    {
        foreach (var child in item.Items)
        {
            var buttonData = CreateFeatureButtonData(child, featureIdToSlot);
            if (buttonData != null) pulldown.AddPushButton(buttonData);
        }
        return;
    }

    var split = added as SplitButton;
    if (split != null)
    {
        foreach (var child in item.Items)
        {
            var buttonData = CreateFeatureButtonData(child, featureIdToSlot);
            if (buttonData != null) split.AddPushButton(buttonData);
        }
    }
}

private void AddStackLayout(RibbonPanel panel, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    var data = (item.Items ?? new List<RibbonItemViewModel>())
        .Select(child => CreateRibbonItemData(child, featureIdToSlot))
        .Where(child => child != null)
        .Cast<RibbonItemData>()
        .ToList();
    if (data.Count < 2 || data.Count > 3)
    {
        Trace.TraceWarning("PH-RIBBON-STACK-SKIPPED: Stack item count must be 2 or 3: " + item.Id);
        return;
    }

    AddStackItemData(panel, data);
}

private static void AddStackItemData(RibbonPanel panel, IReadOnlyList<RibbonItemData> data)
{
    if (data.Count == 2)
    {
        panel.AddStackedItems(data[0], data[1]);
        return;
    }

    panel.AddStackedItems(data[0], data[1], data[2]);
}
```

- [ ] **步骤 6：改造 PushButtonData 创建方法**

把 `CreateFeatureButtonData(FeatureViewModel feature, ...)` 改成接收 `RibbonItemViewModel item`：

```csharp
private PushButtonData CreateFeatureButtonData(RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    if (item == null || item.Feature == null || string.IsNullOrWhiteSpace(item.Feature.FeatureId)) return null;
    var feature = item.Feature;
    if (!featureIdToSlot.ContainsKey(feature.FeatureId)) return null;

    var buttonName = SafeInternalName(feature.FeatureId);
    var slotId = featureIdToSlot[feature.FeatureId];
    var commandType = FrameworkFeatureCommandSlots.CommandTypeFor(slotId);
    var data = new PushButtonData(
        buttonName,
        SafeDisplayName(item.Text, SafeDisplayName(feature.DisplayName, "Feature")),
        _assemblyPath,
        commandType.FullName);

    data.ToolTip = BuildToolTip(feature);
    data.LongDescription = feature.Description;
    data.Image = LoadFeatureIcon(item.IconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
    data.LargeImage = LoadFeatureIcon(item.IconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();
    return data;
}
```

如果 C# 编译器要求 nullable 兼容，把返回类型保持为 `PushButtonData` 并在调用处先确认 feature 和 slot；不要引入 `#nullable`。

- [ ] **步骤 7：运行静态验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过。

- [ ] **步骤 8：运行本地构建**

运行：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir 'D:\Program Files\Autodesk\Revit 2020' -UseRelativeAddinAssembly
```

预期：`0 个警告`、`0 个错误`，输出 `dist\Revit2020\PlugHub.Revit2020.dll`。

- [ ] **步骤 9：Commit**

```powershell
git add src/PlugHub.Revit2020/FeatureRibbonBuilder.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: render advanced ribbon layout controls"
```

---

### 任务 4：增加 Ribbon layout 静态验证规则

**文件：**
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加验证入口**

在 `ValidateRibbonLayoutComposerShape();` 后加入：

```csharp
ValidateRibbonLayoutRules();
```

- [ ] **步骤 2：新增规则验证方法**

在 `Program.cs` 中加入：

```csharp
private static void ValidateRibbonLayoutRules()
{
    var views = ReadObject("config/views.example.json");
    var modules = AllModules().ToList();
    var featureIds = new HashSet<string>(
        modules.SelectMany(Features).Select(feature => Convert.ToString(Prop(feature, "id")) ?? string.Empty),
        StringComparer.OrdinalIgnoreCase);

    foreach (var view in Views(views))
    {
        var ribbon = Prop(view, "ribbon") as IDictionary<string, object>;
        if (ribbon == null) continue;
        var panels = Prop(ribbon, "panels") as IEnumerable;
        if (panels == null) continue;

        var panelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var panelObject in panels.Cast<object>())
        {
            var panel = panelObject as IDictionary<string, object>;
            Require(panel != null, "ribbon panel layout entries must be objects.");
            var panelId = Convert.ToString(Prop(panel, "id")) ?? string.Empty;
            Require(!string.IsNullOrWhiteSpace(panelId), "ribbon panel layout id is required.");
            Require(panelIds.Add(panelId), "duplicate ribbon panel layout id: " + panelId);
            ValidateRibbonLayoutItems(Prop(panel, "items") as IEnumerable, featureIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase), panelId);
        }
    }
}

private static void ValidateRibbonLayoutItems(IEnumerable items, ISet<string> featureIds, ISet<string> containerIds, string location)
{
    if (items == null) return;
    foreach (var itemObject in items.Cast<object>())
    {
        var item = itemObject as IDictionary<string, object>;
        Require(item != null, "ribbon layout item must be an object at " + location);
        var type = (Convert.ToString(Prop(item, "type")) ?? string.Empty).Trim();
        Require(!string.IsNullOrWhiteSpace(type), "ribbon layout item type is required at " + location);

        if (string.Equals(type, "pushButton", StringComparison.OrdinalIgnoreCase))
        {
            var featureId = Convert.ToString(Prop(item, "featureId")) ?? string.Empty;
            Require(featureIds.Contains(featureId), "ribbon layout references missing featureId: " + featureId);
            continue;
        }

        var id = Convert.ToString(Prop(item, "id")) ?? string.Empty;
        Require(!string.IsNullOrWhiteSpace(id), "ribbon container id is required at " + location);
        Require(containerIds.Add(id), "duplicate ribbon container id in panel " + location + ": " + id);

        var children = (Prop(item, "items") as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
        if (string.Equals(type, "pulldownButton", StringComparison.OrdinalIgnoreCase))
        {
            Require(children.Count >= 1, "pulldownButton must contain at least one child: " + id);
            ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
            continue;
        }

        if (string.Equals(type, "splitButton", StringComparison.OrdinalIgnoreCase))
        {
            Require(children.Count >= 2, "splitButton must contain at least two children: " + id);
            ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
            var defaultFeatureId = Convert.ToString(Prop(item, "defaultFeatureId")) ?? string.Empty;
            Require(string.IsNullOrWhiteSpace(defaultFeatureId) || children.Any(child =>
            {
                var childMap = child as IDictionary<string, object>;
                return string.Equals(Convert.ToString(Prop(childMap, "featureId")), defaultFeatureId, StringComparison.OrdinalIgnoreCase);
            }), "splitButton defaultFeatureId must reference one child feature: " + id);
            continue;
        }

        if (string.Equals(type, "stack", StringComparison.OrdinalIgnoreCase))
        {
            Require(children.Count >= 2 && children.Count <= 3, "stack must contain two or three children: " + id);
            foreach (var child in children)
            {
                var childMap = child as IDictionary<string, object>;
                var childType = Convert.ToString(Prop(childMap, "type")) ?? string.Empty;
                Require(string.Equals(childType, "pushButton", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(childType, "pulldownButton", StringComparison.OrdinalIgnoreCase), "stack supports pushButton and pulldownButton children only: " + id);
            }
            ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
            continue;
        }

        Require(false, "unsupported ribbon layout item type: " + type);
    }
}
```

If the current helper `Prop` does not accept nullable dictionaries, call it only after checking `childMap != null`.

- [ ] **步骤 3：运行验证确认通过**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过。

- [ ] **步骤 4：Commit**

```powershell
git add src/PlugHub.StaticValidation/Program.cs
git commit -m "test: validate ribbon layout configuration"
```

---

### 任务 5：增加设置页 Ribbon 布局行模型和迁移方法

**文件：**
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RibbonLayoutNodeRow.cs`
- 创建：`src/PlugHub.Revit2020/Settings/Rows/RibbonFeaturePoolRow.cs`
- 修改：`src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

新增验证方法：

```csharp
private static void ValidateRibbonLayoutSettingsRows()
{
    var viewModel = ReadText("src/PlugHub.Revit2020/Settings/FrameworkSettingsViewModel.cs");
    var nodeRowPath = "src/PlugHub.Revit2020/Settings/Rows/RibbonLayoutNodeRow.cs";
    var poolRowPath = "src/PlugHub.Revit2020/Settings/Rows/RibbonFeaturePoolRow.cs";
    Require(File.Exists(FullPath(nodeRowPath)), "RibbonLayoutNodeRow must exist.");
    Require(File.Exists(FullPath(poolRowPath)), "RibbonFeaturePoolRow must exist.");
    var nodeRow = ReadText(nodeRowPath);
    var poolRow = ReadText(poolRowPath);
    Require(viewModel.Contains("RibbonLayoutNodes"), "settings view model must expose RibbonLayoutNodes.");
    Require(viewModel.Contains("RibbonFeaturePool"), "settings view model must expose RibbonFeaturePool.");
    Require(nodeRow.Contains("ObservableCollection<RibbonLayoutNodeRow> Children"), "RibbonLayoutNodeRow must expose child nodes.");
    Require(nodeRow.Contains("ToPanelConfiguration"), "RibbonLayoutNodeRow must convert panel nodes to configuration.");
    Require(nodeRow.Contains("ToItemConfiguration"), "RibbonLayoutNodeRow must convert item nodes to configuration.");
    Require(poolRow.Contains("FeatureId") && poolRow.Contains("ModuleName"), "RibbonFeaturePoolRow must identify feature and module.");
}
```

在验证调用列表中加入：

```csharp
ValidateRibbonLayoutSettingsRows();
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `RibbonLayoutNodeRow must exist`。

- [ ] **步骤 3：创建 RibbonLayoutNodeRow**

创建 `src/PlugHub.Revit2020/Settings/Rows/RibbonLayoutNodeRow.cs`：

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class RibbonLayoutNodeRow
    {
        public string NodeType { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string DefaultFeatureId { get; set; } = string.Empty;
        public string Size { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
        public int Order { get; set; }
        public ObservableCollection<RibbonLayoutNodeRow> Children { get; } = new ObservableCollection<RibbonLayoutNodeRow>();

        public RibbonPanelLayoutConfiguration ToPanelConfiguration()
        {
            return new RibbonPanelLayoutConfiguration
            {
                Id = Id ?? string.Empty,
                Name = Text ?? string.Empty,
                Order = Order,
                Items = Children.Select(child => child.ToItemConfiguration()).ToList()
            };
        }

        public RibbonItemLayoutConfiguration ToItemConfiguration()
        {
            return new RibbonItemLayoutConfiguration
            {
                Type = NodeType ?? string.Empty,
                Id = Id ?? string.Empty,
                Text = Text ?? string.Empty,
                FeatureId = FeatureId ?? string.Empty,
                DefaultFeatureId = DefaultFeatureId ?? string.Empty,
                Size = Size ?? string.Empty,
                IconPath = IconPath ?? string.Empty,
                Order = Order,
                Items = Children.Select(child => child.ToItemConfiguration()).ToList()
            };
        }

        public static RibbonLayoutNodeRow FromPanel(RibbonPanelLayoutConfiguration panel)
        {
            var row = new RibbonLayoutNodeRow
            {
                NodeType = "panel",
                Id = panel.Id ?? string.Empty,
                Text = panel.Name ?? string.Empty,
                Order = panel.Order
            };
            foreach (var item in panel.Items ?? new List<RibbonItemLayoutConfiguration>())
            {
                row.Children.Add(FromItem(item));
            }
            return row;
        }

        public static RibbonLayoutNodeRow FromItem(RibbonItemLayoutConfiguration item)
        {
            var row = new RibbonLayoutNodeRow
            {
                NodeType = item.Type ?? string.Empty,
                Id = item.Id ?? string.Empty,
                Text = string.IsNullOrWhiteSpace(item.TextOverride) ? item.Text ?? string.Empty : item.TextOverride,
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = string.IsNullOrWhiteSpace(item.Size) ? "large" : item.Size,
                IconPath = string.IsNullOrWhiteSpace(item.IconPathOverride) ? item.IconPath ?? string.Empty : item.IconPathOverride,
                Order = item.Order
            };
            foreach (var child in item.Items ?? new List<RibbonItemLayoutConfiguration>())
            {
                row.Children.Add(FromItem(child));
            }
            return row;
        }
    }
}
```

- [ ] **步骤 4：创建 RibbonFeaturePoolRow**

创建 `src/PlugHub.Revit2020/Settings/Rows/RibbonFeaturePoolRow.cs`：

```csharp
namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class RibbonFeaturePoolRow
    {
        public string ModuleId { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string FeatureName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }
}
```

- [ ] **步骤 5：扩展 FrameworkSettingsViewModel**

在 `FrameworkSettingsViewModel` 中加入：

```csharp
public ObservableCollection<RibbonLayoutNodeRow> RibbonLayoutNodes { get; } = new ObservableCollection<RibbonLayoutNodeRow>();
public ObservableCollection<RibbonFeaturePoolRow> RibbonFeaturePool { get; } = new ObservableCollection<RibbonFeaturePoolRow>();
```

并在文件顶部确认存在：

```csharp
using PlugHub.Revit2020.Settings.Rows;
```

- [ ] **步骤 6：运行验证确认通过**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：通过。

- [ ] **步骤 7：Commit**

```powershell
git add src/PlugHub.Revit2020/Settings src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: add ribbon layout settings rows"
```

---

### 任务 6：增加设置页 Ribbon 布局页签

**文件：**
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsWindow.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加失败验证**

在现有 settings UI 验证方法中加入：

```csharp
Require(settingsWindow.Contains("BuildRibbonLayoutTab"), "settings window must expose a Ribbon layout tab.");
Require(settingsWindow.Contains("LoadRibbonLayoutRows"), "settings window must load ribbon layout rows.");
Require(settingsWindow.Contains("ApplyRibbonLayoutRows"), "settings window must save ribbon layout rows.");
Require(settingsWindow.Contains("MigrateBasicRibbonLayout"), "settings window must migrate basic layout into advanced ribbon layout.");
Require(settingsWindow.Contains("RestoreBasicRibbonLayout"), "settings window must restore basic group layout.");
Require(settingsWindow.Contains("TreeView"), "settings window must use TreeView for ribbon layout editing.");
Require(settingsWindow.Contains("Ribbon 布局"), "settings window must label the ribbon layout tab.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `settings window must expose a Ribbon layout tab`。

- [ ] **步骤 3：新增字段和页签注册**

在 `FrameworkSettingsWindow` 字段区域加入：

```csharp
private readonly TreeView _ribbonLayoutTree = new TreeView();
private readonly ListBox _ribbonFeaturePoolList = new ListBox();
private readonly TextBox _selectedRibbonNodeText = new TextBox();
private readonly ComboBox _selectedRibbonNodeTypeCombo = new ComboBox();
```

在构造 tab 的位置加入：

```csharp
tabs.Items.Add(BuildRibbonLayoutTab());
```

- [ ] **步骤 4：实现 BuildRibbonLayoutTab**

在 `FrameworkSettingsWindow` 中加入：

```csharp
private TabItem BuildRibbonLayoutTab()
{
    _selectedRibbonNodeTypeCombo.ItemsSource = new[] { "panel", "pushButton", "pulldownButton", "splitButton", "stack" };
    _ribbonLayoutTree.ItemsSource = _viewModel.RibbonLayoutNodes;
    _ribbonFeaturePoolList.ItemsSource = _viewModel.RibbonFeaturePool;

    var root = new DockPanel();
    var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    actions.Children.Add(Button("迁移为高级布局", (sender, args) => MigrateBasicRibbonLayout()));
    actions.Children.Add(Button("恢复基础布局", (sender, args) => RestoreBasicRibbonLayout()));
    actions.Children.Add(Button("新增面板", (sender, args) => AddRibbonPanelNode()));
    actions.Children.Add(Button("新增下拉", (sender, args) => AddRibbonContainerNode("pulldownButton")));
    actions.Children.Add(Button("新增拆分", (sender, args) => AddRibbonContainerNode("splitButton")));
    actions.Children.Add(Button("新增堆叠", (sender, args) => AddRibbonContainerNode("stack")));
    actions.Children.Add(Button("删除布局项", (sender, args) => RemoveSelectedRibbonLayoutNode()));
    DockPanel.SetDock(actions, Dock.Top);
    root.Children.Add(actions);

    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

    Grid.SetColumn(_ribbonFeaturePoolList, 0);
    Grid.SetColumn(_ribbonLayoutTree, 1);
    Grid.SetColumn(BuildRibbonNodePropertyPanel(), 2);
    grid.Children.Add(_ribbonFeaturePoolList);
    grid.Children.Add(_ribbonLayoutTree);
    grid.Children.Add(BuildRibbonNodePropertyPanel());
    root.Children.Add(grid);

    return new TabItem { Header = "Ribbon 布局", Content = root };
}
```

如果 `Button(...)` helper 不存在，使用当前窗口中已有的按钮创建模式；不要引入新 UI 框架。

- [ ] **步骤 5：实现加载和保存方法**

在加载流程 `LoadFromConfiguration()` 中调用：

```csharp
LoadRibbonLayoutRows();
```

在保存流程 `ApplyRowsToConfiguration()` 中调用：

```csharp
ApplyRibbonLayoutRows();
```

新增方法：

```csharp
private void LoadRibbonLayoutRows()
{
    _viewModel.RibbonLayoutNodes.Clear();
    _viewModel.RibbonFeaturePool.Clear();

    var ribbon = WorkspaceView().Ribbon ?? new RibbonConfiguration();
    foreach (var panel in ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>())
    {
        _viewModel.RibbonLayoutNodes.Add(RibbonLayoutNodeRow.FromPanel(panel));
    }

    foreach (var row in _viewModel.Features.OrderBy(feature => feature.ModuleName).ThenBy(feature => feature.Name))
    {
        _viewModel.RibbonFeaturePool.Add(new RibbonFeaturePoolRow
        {
            ModuleId = row.ModuleId,
            ModuleName = row.ModuleName,
            FeatureId = row.FeatureId,
            FeatureName = row.Name,
            Group = row.Group,
            IconPath = row.IconPath
        });
    }
}

private void ApplyRibbonLayoutRows()
{
    var view = WorkspaceView();
    if (view.Ribbon == null)
    {
        view.Ribbon = new RibbonConfiguration { TabName = "PlugHub", FallbackPanelName = "其他工具" };
    }

    view.Ribbon.LayoutVersion = _viewModel.RibbonLayoutNodes.Any() ? "1.0" : string.Empty;
    view.Ribbon.Panels = _viewModel.RibbonLayoutNodes
        .Select((row, index) =>
        {
            row.Order = (index + 1) * 100;
            return row.ToPanelConfiguration();
        })
        .ToList();
}
```

- [ ] **步骤 6：实现迁移和回退**

新增方法：

```csharp
private void MigrateBasicRibbonLayout()
{
    _viewModel.RibbonLayoutNodes.Clear();
    foreach (var group in _viewModel.Groups.OrderBy(group => group.Order))
    {
        var panel = new RibbonLayoutNodeRow
        {
            NodeType = "panel",
            Id = group.Id,
            Text = group.Name,
            Order = group.Order
        };

        foreach (var feature in _viewModel.Features
            .Where(feature => string.Equals(feature.Group, group.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(feature => feature.Order))
        {
            panel.Children.Add(new RibbonLayoutNodeRow
            {
                NodeType = "pushButton",
                FeatureId = feature.FeatureId,
                Text = DisplayName(feature.DisplayName, feature.Name, feature.FeatureId),
                Size = feature.ButtonSize,
                IconPath = feature.IconPath,
                Order = feature.Order
            });
        }

        _viewModel.RibbonLayoutNodes.Add(panel);
    }

    RefreshStatus("已从当前分组生成高级 Ribbon 布局，保存并重启 Revit 后生效。");
}

private void RestoreBasicRibbonLayout()
{
    var result = MessageBox.Show("恢复基础布局会删除高级 Ribbon 容器配置，但不会卸载插件功能。是否继续？", "恢复基础布局", MessageBoxButton.YesNo, MessageBoxImage.Warning);
    if (result != MessageBoxResult.Yes) return;
    _viewModel.RibbonLayoutNodes.Clear();
    RefreshStatus("已恢复为基础分组布局，保存并重启 Revit 后生效。");
}
```

- [ ] **步骤 7：实现基本编辑动作**

新增方法：

```csharp
private void AddRibbonPanelNode()
{
    var index = _viewModel.RibbonLayoutNodes.Count + 1;
    _viewModel.RibbonLayoutNodes.Add(new RibbonLayoutNodeRow
    {
        NodeType = "panel",
        Id = "custom-panel-" + index,
        Text = "自定义面板 " + index,
        Order = index * 100
    });
}

private void AddRibbonContainerNode(string type)
{
    var panel = _ribbonLayoutTree.SelectedItem as RibbonLayoutNodeRow;
    while (panel != null && !string.Equals(panel.NodeType, "panel", StringComparison.OrdinalIgnoreCase))
    {
        panel = FindParentRibbonNode(panel);
    }
    if (panel == null) return;

    var index = panel.Children.Count + 1;
    panel.Children.Add(new RibbonLayoutNodeRow
    {
        NodeType = type,
        Id = type + "-" + index,
        Text = type,
        Order = index * 100
    });
}

private void RemoveSelectedRibbonLayoutNode()
{
    var row = _ribbonLayoutTree.SelectedItem as RibbonLayoutNodeRow;
    if (row == null) return;
    if (_viewModel.RibbonLayoutNodes.Remove(row)) return;
    foreach (var panel in _viewModel.RibbonLayoutNodes)
    {
        if (RemoveRibbonNode(panel, row)) return;
    }
}
```

Add helper methods `FindParentRibbonNode` and `RemoveRibbonNode` by recursively walking `_viewModel.RibbonLayoutNodes`; keep them private in `FrameworkSettingsWindow`.

- [ ] **步骤 8：运行验证和构建**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
.\scripts\build-revit2020.ps1 -RevitApiDir 'D:\Program Files\Autodesk\Revit 2020' -UseRelativeAddinAssembly
```

预期：静态验证通过；本地构建 `0 个警告`、`0 个错误`。

- [ ] **步骤 9：Commit**

```powershell
git add src/PlugHub.Revit2020/FrameworkSettingsWindow.cs src/PlugHub.StaticValidation/Program.cs
git commit -m "feat: add ribbon layout settings tab"
```

---

### 任务 7：更新文档和最终验证

**文件：**
- 修改：`docs/architecture.md`
- 修改：`docs/development.md`
- 修改：`docs/plugin-development.md`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：增加文档验证**

在 `ValidateDocumentationStructure()` 或相邻文档验证方法中加入：

```csharp
var architecture = ReadText("docs/architecture.md");
var development = ReadText("docs/development.md");
var pluginDevelopment = ReadText("docs/plugin-development.md");
Require(architecture.Contains("Ribbon layout") || architecture.Contains("Ribbon 布局"), "architecture docs must describe advanced Ribbon layout.");
Require(architecture.Contains("PulldownButton") && architecture.Contains("SplitButton"), "architecture docs must mention advanced Revit Ribbon controls.");
Require(development.Contains("Ribbon 结构") && development.Contains("重启 Revit"), "development docs must state Ribbon layout changes require Revit restart.");
Require(pluginDevelopment.Contains("插件包只声明功能") || pluginDevelopment.Contains("插件包清单只声明功能"), "plugin development docs must keep package manifests separate from user Ribbon layout.");
```

- [ ] **步骤 2：运行验证确认失败**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

预期：失败，错误包含 `architecture docs must describe advanced Ribbon layout`。

- [ ] **步骤 3：更新 architecture 文档**

在 `docs/architecture.md` 的 Ribbon 章节补充：

```markdown
### Ribbon layout

PlugHub 将功能定义和 Ribbon 布局分离。插件包清单中的 feature 仍是可执行命令的权威来源；用户设置中的 Ribbon layout 决定这些 feature 以 PushButton、PulldownButton、SplitButton 或 Stack 的方式显示。

高级布局配置保存在当前 workspace 的 `ribbon.panels` 下。没有 `ribbon.panels` 时，框架继续按 `ViewGroupConfiguration` 和 `FeatureConfiguration.ButtonSize` 使用旧的分组布局。

Framework 层只组合中立布局模型，不引用 Revit API。`PlugHub.Revit2020` 负责把中立布局渲染成 Revit Ribbon 控件，并继续通过稳定 slot 命令路由到业务功能。
```

- [ ] **步骤 4：更新 development 文档**

在 `docs/development.md` 的 Revit 验收说明中补充：

```markdown
Ribbon 结构、容器控件、图标和按钮大小保存后需要重启 Revit 才能可靠重绘。设置页不会尝试在当前 Revit 会话中实时替换已有 Ribbon 控件。
```

- [ ] **步骤 5：更新 plugin-development 文档**

在 `docs/plugin-development.md` 的 package 清单说明中补充：

```markdown
插件包清单只声明模块和功能，不直接声明用户的高级 Ribbon 容器。高级 Ribbon 布局由用户设置保存，引用已安装 featureId。删除布局项只会移除 Ribbon 引用，不会卸载插件包或删除 feature。
```

- [ ] **步骤 6：运行完整验证**

运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
.\scripts\build-revit2020.ps1 -RevitApiDir 'D:\Program Files\Autodesk\Revit 2020' -UseRelativeAddinAssembly
git diff --check
```

预期：

```text
passed: modules=0, features=0, views=1, presets=0
```

构建输出包含：

```text
已成功生成。
    0 个警告
    0 个错误
```

`git diff --check` 无输出。

- [ ] **步骤 7：Commit**

```powershell
git add docs/architecture.md docs/development.md docs/plugin-development.md src/PlugHub.StaticValidation/Program.cs
git commit -m "docs: document advanced ribbon layout controls"
```

---

## 最终验收

完成所有任务后运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
.\scripts\build-revit2020.ps1 -RevitApiDir 'D:\Program Files\Autodesk\Revit 2020' -UseRelativeAddinAssembly
git status --short --branch
git log --oneline --decorate -8
```

验收标准：

- 静态验证通过，输出 `passed: modules=0, features=0, views=1, presets=0`。
- 本地 Revit2020 构建通过，`0 个警告`、`0 个错误`。
- 旧配置没有 `ribbon.panels` 时仍按分组和按钮大小渲染。
- 高级布局配置能渲染 PushButton、PulldownButton、SplitButton 和 Stack。
- 每个可点击 feature 仍通过 `FeatureSlotRegistry` 和稳定 slot 命令调度。
- 设置页能生成、编辑、保存和清空高级布局。
- 文档明确说明 Ribbon 结构变更需要重启 Revit。
- 不声称 Revit 实机启动测试通过，除非单独在 Revit 2020 中启动并点击验证。
