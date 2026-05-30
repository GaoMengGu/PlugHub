# PlugHub Runtime Routing 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 将 Revit 业务功能按钮统一收口到稳定的 PlugHub 框架命令调度层，避免 Ribbon 直接绑定业务插件 DLL。

**架构：** 第一阶段保留 Revit 2020 / .NET Framework 4.8，不引入 ALC。业务按钮绑定到 `PlugHub.Revit2020.dll` 内的固定 slot 命令，slot 调用 `FeatureCommandDispatcher`，dispatcher 再通过 `ICommandAssemblyLoader` 在点击时加载业务命令。

**技术栈：** C# 8、.NET Framework 4.8、Revit 2020 API、WPF、`PlugHub.StaticValidation`。

---

## 规格来源

- 设计规格：`docs/superpowers/specs/2026-05-30-plughub-runtime-routing-design.md`
- 关键约束：Revit 2020 不能使用 `AssemblyLoadContext`；第一阶段只收口命令入口，不承诺真正热重载。
- 项目验证命令：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

如果沙盒内因 `C:\Users\Yilan\AppData\Local\Microsoft SDKs` 权限失败，使用已批准的外部执行方式重新运行同一条命令。

## 文件结构

- 创建：`src/PlugHub.Revit2020/FeatureCommandDispatcher.cs`
  - 统一处理 feature id / slot id 执行路径，承接当前 `FrameworkFeatureCommand.ExecuteFeature` 中的业务命令执行逻辑。
- 创建：`src/PlugHub.Revit2020/CommandAssemblyLoader.cs`
  - 定义 `ICommandAssemblyLoader` 和第一阶段 `Net48DirectCommandAssemblyLoader`。
- 创建：`src/PlugHub.Revit2020/FeatureSlotRegistry.cs`
  - 保存 `slot id -> feature id` 映射，提供原子替换和查询。
- 创建：`src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs`
  - 定义 slot 命令基类、`FrameworkFeatureCommandSlot001` 到 `FrameworkFeatureCommandSlot128`、以及 slot id 到命令类型的解析。
- 修改：`src/PlugHub.Revit2020/FrameworkFeatureCommand.cs`
  - 保留状态回退入口，把 feature 执行委托给 `FeatureCommandDispatcher`。
- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`
  - 为业务 feature 分配 slot，Ribbon 按钮统一绑定框架 slot 命令类型，不再绑定外部 `commandAssembly`。
- 修改：`src/PlugHub.StaticValidation/Program.cs`
  - 新增 runtime routing 静态验证，锁定 Ribbon 不直连业务 DLL、slot 类型存在、`Assembly.LoadFrom` 迁移到加载器。
- 修改：`docs/architecture.md`
  - 补充 Revit 2020 命令入口收口说明。
- 修改：`docs/development.md`
  - 补充插件命令加载边界和 Revit 2020 重启边界。

## 任务 1：写入 runtime routing 失败验证

**文件：**
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：新增验证方法并接入 Main**

在 `ValidateRevitRibbonAdapter();` 后面调用新方法：

```csharp
ValidateRuntimeRoutingSpecification();
```

同时修改 `ValidateRevitRibbonAdapter` 的 token 列表，把旧的 `ResolveCommandTarget` 替换为新的 routing token：

```csharp
foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "FeatureCommandDispatcher", "FeatureSlotRegistry" })
{
    Require(adapterText.Contains(token), "missing Revit adapter token: " + token);
}
```

在 `ValidateRevitRibbonAdapter` 附近新增：

```csharp
private static void ValidateRuntimeRoutingSpecification()
{
    var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
    var featureCommand = ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
    var revitText = ReadAllCSharp("src/PlugHub.Revit2020");

    Require(revitText.Contains("class FeatureCommandDispatcher"), "runtime routing must use FeatureCommandDispatcher.");
    Require(revitText.Contains("interface ICommandAssemblyLoader"), "runtime routing must isolate command assembly loading behind ICommandAssemblyLoader.");
    Require(revitText.Contains("class Net48DirectCommandAssemblyLoader"), "runtime routing must keep the net48 direct loader explicit.");
    Require(revitText.Contains("class FeatureSlotRegistry"), "runtime routing must use a feature slot registry.");
    Require(revitText.Contains("class FrameworkFeatureCommandSlot001"), "runtime routing must define the first feature command slot.");
    Require(revitText.Contains("class FrameworkFeatureCommandSlot128"), "runtime routing must define the last feature command slot.");
    Require(revitText.Contains("FrameworkFeatureCommandSlots.CommandTypeFor"), "runtime routing must resolve slot command types through FrameworkFeatureCommandSlots.");
    Require(revitText.Contains("PH-FEATURE-SLOT-LIMIT"), "runtime routing must diagnose visible features that exceed available slots.");

    Require(!ribbonBuilder.Contains("new CommandTarget(assemblyPath, feature.CommandType)"), "Revit feature buttons must use framework slots instead of external command assemblies.");
    Require(ribbonBuilder.Contains("FeatureSlotRegistry.Replace"), "Ribbon build must atomically replace feature slot mappings.");
    Require(!featureCommand.Contains("Assembly.LoadFrom"), "FrameworkFeatureCommand must delegate business command loading to ICommandAssemblyLoader.");
    Require(ReadText("src/PlugHub.Revit2020/CommandAssemblyLoader.cs").Contains("Assembly.LoadFrom"), "net48 command loader must keep the direct LoadFrom strategy in one file.");
}
```

- [ ] **步骤 2：运行验证并确认失败**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误中包含 `runtime routing must use FeatureCommandDispatcher` 或同一方法中的后续 runtime routing 断言。

- [ ] **步骤 3：提交失败验证**

```bash
git add src/PlugHub.StaticValidation/Program.cs
git commit -m "test: add runtime routing validation"
```

## 任务 2：抽出命令加载器和调度器

**文件：**
- 创建：`src/PlugHub.Revit2020/CommandAssemblyLoader.cs`
- 创建：`src/PlugHub.Revit2020/FeatureCommandDispatcher.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkFeatureCommand.cs`

- [ ] **步骤 1：创建 `CommandAssemblyLoader.cs`**

写入：

```csharp
using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace PlugHub.Revit2020
{
    internal interface ICommandAssemblyLoader
    {
        IExternalCommand Create(string assemblyPath, string commandTypeName);
    }

    internal sealed class Net48DirectCommandAssemblyLoader : ICommandAssemblyLoader
    {
        public IExternalCommand Create(string assemblyPath, string commandTypeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentException("Command assembly path is required.", nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(commandTypeName)) throw new ArgumentException("Command type is required.", nameof(commandTypeName));

            var commandType = Assembly.LoadFrom(assemblyPath).GetType(commandTypeName, throwOnError: false);
            if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType))
            {
                throw new InvalidOperationException("Command type was not found or does not implement IExternalCommand: " + commandTypeName);
            }

            return (IExternalCommand)Activator.CreateInstance(commandType)!;
        }
    }
}
```

- [ ] **步骤 2：创建 `FeatureCommandDispatcher.cs`**

写入：

```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal static class FeatureCommandDispatcher
    {
        private static readonly ICommandAssemblyLoader CommandAssemblyLoader = new Net48DirectCommandAssemblyLoader();

        public static Result ExecuteSlot(int slotId, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!FeatureSlotRegistry.TryGetFeatureId(slotId, out var featureId))
            {
                message = "PlugHub feature slot is not assigned: " + slotId;
                ShowFailure("PlugHub 功能执行失败", message, "PH-FEATURE-SLOT", string.Empty, DiagnosticSeverity.Error);
                return Result.Cancelled;
            }

            return ExecuteFeature(featureId, commandData, ref message, elements);
        }

        public static Result ExecuteFeature(string featureKey, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var decision = new FeatureExecutionGate().CanExecute(featureKey);
            if (!decision.Allowed)
            {
                message = decision.Message;
                ShowFailure("PlugHub 功能已禁用", decision.Message, "PH-FEATURE-GATE", decision.FeatureId, DiagnosticSeverity.Warning);
                return Result.Cancelled;
            }

            var snapshot = FrameworkRuntimeState.Current;
            var feature = snapshot?.Features.FirstOrDefault(item =>
                string.Equals(item.Id, decision.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.CommandType))
            {
                FrameworkStatusWindow.ShowRuntimeStatus(snapshot);
                return Result.Succeeded;
            }

            var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
            if (!File.Exists(assemblyPath))
            {
                message = "Command assembly was not found: " + assemblyPath;
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-ASSEMBLY", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }

            try
            {
                var command = CommandAssemblyLoader.Create(assemblyPath, feature.CommandType);
                return command.Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-LOAD", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }
        }

        private static string ResolveAssemblyPath(string commandAssembly)
        {
            if (string.IsNullOrWhiteSpace(commandAssembly)) return typeof(FrameworkFeatureCommand).Assembly.Location;
            return Path.IsPathRooted(commandAssembly)
                ? commandAssembly
                : Path.GetFullPath(Path.Combine(FrameworkRuntimeState.BaseDirectory, commandAssembly));
        }

        private static void ShowFailure(string title, string failureMessage, string code, string moduleId, DiagnosticSeverity severity)
        {
            FrameworkStatusWindow.ShowLogs(
                title,
                failureMessage,
                new[]
                {
                    new DiagnosticMessage
                    {
                        Severity = severity,
                        Code = code,
                        ModuleId = moduleId ?? string.Empty,
                        Message = failureMessage ?? string.Empty
                    }
                });
        }
    }
}
```

- [ ] **步骤 3：修改 `FrameworkFeatureCommand.cs`**

删除 `System.IO`、`System.Linq`、`System.Reflection`、`PlugHub.Contracts.Modules` using。保留 `System`、Revit using 和 `PlugHub.Framework.Runtime`。

将 `ExecuteFeature` 的调用改为：

```csharp
return FeatureCommandDispatcher.ExecuteFeature(featureKey, commandData, ref message, elements);
```

删除 `FrameworkFeatureCommand` 内原有的私有 `ExecuteFeature` 和 `ResolveAssemblyPath` 方法。保留 `FeatureKeyFromJournal` 和无 feature key 时显示运行时状态的回退行为。

- [ ] **步骤 4：运行验证并确认仍失败在 slot/Ribbon 项**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误指向 `FeatureSlotRegistry`、slot class 或 Ribbon slot 映射相关断言。

- [ ] **步骤 5：提交调度器和加载器**

```bash
git add src/PlugHub.Revit2020/CommandAssemblyLoader.cs src/PlugHub.Revit2020/FeatureCommandDispatcher.cs src/PlugHub.Revit2020/FrameworkFeatureCommand.cs
git commit -m "refactor: route feature execution through dispatcher"
```

## 任务 3：新增 slot registry 和 slot 命令类型

**文件：**
- 创建：`src/PlugHub.Revit2020/FeatureSlotRegistry.cs`
- 创建：`src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs`

- [ ] **步骤 1：创建 `FeatureSlotRegistry.cs`**

写入：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Revit2020
{
    internal static class FeatureSlotRegistry
    {
        public const int MaxSlots = 128;
        private static readonly object Sync = new object();
        private static Dictionary<int, string> _slotToFeatureId = new Dictionary<int, string>();
        private static IReadOnlyList<string> _skippedFeatureIds = new List<string>();

        public static IReadOnlyList<string> SkippedFeatureIds
        {
            get
            {
                lock (Sync)
                {
                    return _skippedFeatureIds.ToList();
                }
            }
        }

        public static void Replace(IReadOnlyDictionary<int, string> slotToFeatureId, IReadOnlyList<string> skippedFeatureIds)
        {
            lock (Sync)
            {
                _slotToFeatureId = new Dictionary<int, string>(slotToFeatureId ?? new Dictionary<int, string>());
                _skippedFeatureIds = new List<string>(skippedFeatureIds ?? new List<string>());
            }
        }

        public static bool TryGetFeatureId(int slotId, out string featureId)
        {
            lock (Sync)
            {
                if (_slotToFeatureId.TryGetValue(slotId, out var value))
                {
                    featureId = value;
                    return true;
                }

                featureId = string.Empty;
                return false;
            }
        }
    }
}
```

- [ ] **步骤 2：创建 `FrameworkFeatureCommandSlots.cs` 基础结构**

写入文件头部和基础类型：

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public abstract class FrameworkFeatureCommandSlot : IExternalCommand
    {
        protected abstract int SlotId { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return FeatureCommandDispatcher.ExecuteSlot(SlotId, commandData, ref message, elements);
        }
    }
```

- [ ] **步骤 3：在同一文件中加入 001 到 128 的 slot 类**

必须包含从 `FrameworkFeatureCommandSlot001` 到 `FrameworkFeatureCommandSlot128` 的完整连续声明。每个类使用三位数字补零，类体只覆写 `SlotId`。

代码形状如下，文件中要展开全部 128 个类，不保留省略号：

```csharp
    public sealed class FrameworkFeatureCommandSlot001 : FrameworkFeatureCommandSlot { protected override int SlotId => 1; }
    public sealed class FrameworkFeatureCommandSlot002 : FrameworkFeatureCommandSlot { protected override int SlotId => 2; }
    public sealed class FrameworkFeatureCommandSlot003 : FrameworkFeatureCommandSlot { protected override int SlotId => 3; }
```

最后一个必须是：

```csharp
    public sealed class FrameworkFeatureCommandSlot128 : FrameworkFeatureCommandSlot { protected override int SlotId => 128; }
```

- [ ] **步骤 4：在同一文件末尾加入 slot 类型解析器**

在命名空间关闭前加入：

```csharp
    internal static class FrameworkFeatureCommandSlots
    {
        public static Type CommandTypeFor(int slotId)
        {
            switch (slotId)
            {
                case 1: return typeof(FrameworkFeatureCommandSlot001);
                case 2: return typeof(FrameworkFeatureCommandSlot002);
                case 3: return typeof(FrameworkFeatureCommandSlot003);
            }

            throw new ArgumentOutOfRangeException(nameof(slotId), "Feature command slot must be between 1 and " + FeatureSlotRegistry.MaxSlots + ".");
        }
    }
}
```

将 `switch` 扩展为 1 到 128 的完整 case。`case 128` 必须返回 `typeof(FrameworkFeatureCommandSlot128)`。

- [ ] **步骤 5：运行验证并确认仍失败在 Ribbon 映射**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：失败，错误指向 `FeatureSlotRegistry.Replace` 或 `new CommandTarget(assemblyPath, feature.CommandType)`。

- [ ] **步骤 6：提交 slot 基础设施**

```bash
git add src/PlugHub.Revit2020/FeatureSlotRegistry.cs src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs
git commit -m "feat: add framework feature command slots"
```

## 任务 4：修改 Ribbon 构建，使功能按钮绑定 slot

**文件：**
- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`

- [ ] **步骤 1：增加 `System.Diagnostics` using**

在 using 区加入：

```csharp
using System.Diagnostics;
```

- [ ] **步骤 2：在 `Build` 中一次性计算有序功能和 slot 映射**

保留空 composition 的早退，但在早退前清空 slot registry：

```csharp
if (!composition.Features.Any())
{
    FeatureSlotRegistry.Replace(new Dictionary<int, string>(), new List<string>());
    return;
}
```

然后替换当前按 group 直接遍历的逻辑。新的 `Build` 核心结构为：

```csharp
var orderedGroups = composition.Features
    .GroupBy(f => new { f.GroupId, f.GroupName, f.GroupOrder })
    .OrderBy(group => group.Key.GroupOrder)
    .ThenBy(group => group.Key.GroupName)
    .ToList();

var orderedFeatures = orderedGroups
    .SelectMany(group => OrderFeaturesForRibbon(group))
    .ToList();
var slotAssignments = BuildSlotAssignments(orderedFeatures);
FeatureSlotRegistry.Replace(slotAssignments.SlotToFeatureId, slotAssignments.SkippedFeatureIds);

foreach (var skippedFeatureId in slotAssignments.SkippedFeatureIds)
{
    Trace.TraceWarning("PH-FEATURE-SLOT-LIMIT: Feature was not assigned a Revit command slot: " + skippedFeatureId);
}

foreach (var group in orderedGroups)
{
    var panelName = SafeDisplayName(group.Key.GroupName, fallbackPanelName);
    var panel = GetOrCreatePanel(application, tabName, panelName);
    AddFeatureButtons(panel, OrderFeaturesForRibbon(group), slotAssignments.FeatureIdToSlot);
}
```

- [ ] **步骤 3：修改 `AddFeatureButtons` 和 `CreateFeatureButtonData` 签名**

改为：

```csharp
private void AddFeatureButtons(RibbonPanel panel, IEnumerable<FeatureViewModel> features, IReadOnlyDictionary<string, int> featureIdToSlot)
```

循环内先检查 slot：

```csharp
if (!featureIdToSlot.ContainsKey(feature.FeatureId))
{
    continue;
}

var data = CreateFeatureButtonData(feature, featureIdToSlot);
```

移除未使用的 `private void AddFeatureButton(RibbonPanel panel, FeatureViewModel feature)` 重载。

`CreateFeatureButtonData` 改为：

```csharp
private PushButtonData CreateFeatureButtonData(FeatureViewModel feature, IReadOnlyDictionary<string, int> featureIdToSlot)
{
    var buttonName = SafeInternalName(feature.FeatureId);
    var slotId = featureIdToSlot[feature.FeatureId];
    var commandType = FrameworkFeatureCommandSlots.CommandTypeFor(slotId);
    var data = new PushButtonData(
        buttonName,
        SafeDisplayName(feature.DisplayName, "Feature"),
        _assemblyPath,
        commandType.FullName);

    data.ToolTip = BuildToolTip(feature);
    data.LongDescription = feature.Description;
    data.Image = LoadFeatureIcon(feature.IconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
    data.LargeImage = LoadFeatureIcon(feature.IconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();

    return data;
}
```

- [ ] **步骤 4：删除 `ResolveCommandTarget`、`ResolveAssemblyPath` 和 `CommandTarget`**

删除 `FeatureRibbonBuilder` 中以下成员：

- `ResolveCommandTarget`
- `ResolveAssemblyPath`
- 嵌套类 `CommandTarget`

保留 `BuildToolTip` 中的 `CommandType` 展示；它只显示信息，不绑定外部 DLL。

- [ ] **步骤 5：新增 slot assignment helper**

在 `IsSmall` 附近加入：

```csharp
private static SlotAssignmentResult BuildSlotAssignments(IReadOnlyList<FeatureViewModel> orderedFeatures)
{
    var slotToFeatureId = new Dictionary<int, string>();
    var featureIdToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var skippedFeatureIds = new List<string>();

    var slotId = 1;
    foreach (var feature in orderedFeatures ?? new List<FeatureViewModel>())
    {
        if (string.IsNullOrWhiteSpace(feature.FeatureId))
        {
            continue;
        }

        if (featureIdToSlot.ContainsKey(feature.FeatureId))
        {
            continue;
        }

        if (slotId > FeatureSlotRegistry.MaxSlots)
        {
            skippedFeatureIds.Add(feature.FeatureId);
            continue;
        }

        slotToFeatureId[slotId] = feature.FeatureId;
        featureIdToSlot[feature.FeatureId] = slotId;
        slotId++;
    }

    return new SlotAssignmentResult(slotToFeatureId, featureIdToSlot, skippedFeatureIds);
}
```

在文件末尾新增结果类型：

```csharp
private sealed class SlotAssignmentResult
{
    public SlotAssignmentResult(
        IReadOnlyDictionary<int, string> slotToFeatureId,
        IReadOnlyDictionary<string, int> featureIdToSlot,
        IReadOnlyList<string> skippedFeatureIds)
    {
        SlotToFeatureId = slotToFeatureId ?? throw new ArgumentNullException(nameof(slotToFeatureId));
        FeatureIdToSlot = featureIdToSlot ?? throw new ArgumentNullException(nameof(featureIdToSlot));
        SkippedFeatureIds = skippedFeatureIds ?? throw new ArgumentNullException(nameof(skippedFeatureIds));
    }

    public IReadOnlyDictionary<int, string> SlotToFeatureId { get; }
    public IReadOnlyDictionary<string, int> FeatureIdToSlot { get; }
    public IReadOnlyList<string> SkippedFeatureIds { get; }
}
```

- [ ] **步骤 6：运行验证并确认通过 runtime routing 断言**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：如果只有 runtime routing 改动，验证应通过。若出现编译错误，修复命名空间、using 或不可达成员后重跑同一命令。

- [ ] **步骤 7：提交 Ribbon slot 改造**

```bash
git add src/PlugHub.Revit2020/FeatureRibbonBuilder.cs
git commit -m "refactor: bind feature buttons to framework slots"
```

## 任务 5：更新架构和开发文档

**文件：**
- 修改：`docs/architecture.md`
- 修改：`docs/development.md`

- [ ] **步骤 1：更新 `docs/architecture.md`**

在启动链路中，将 Ribbon 和命令路由描述改成包含 slot 调度：

```markdown
8. `FeatureRibbonBuilder` 创建 `PlugHub` Ribbon tab、panel 和按钮；业务功能按钮绑定到 `PlugHub.Revit2020.dll` 内的稳定 slot 命令。
9. `FeatureSlotRegistry` 保存 slot 到 feature id 的映射。
10. 用户点击业务按钮时，slot 命令进入 `FeatureCommandDispatcher`，由框架校验状态并在点击时加载实际 `IExternalCommand`。
```

在模块契约段落补充：

```markdown
业务功能仍通过 feature 的 `commandAssembly` / `commandType` 指向实际 `IExternalCommand`，但 Revit Ribbon 不直接绑定该业务程序集。Revit 2020 适配层先进入稳定框架 slot 命令，再由框架调度器在点击时加载业务命令。
```

- [ ] **步骤 2：更新 `docs/development.md`**

在命令开发或插件包说明附近加入：

```markdown
在 Revit 2020 中，PlugHub 不承诺已加载业务 DLL 的真正热重载。Ribbon 按钮会绑定到框架 slot 命令，业务 `commandAssembly` 只在用户点击功能时由调度器加载。后续 shadow copy 加载器会以该调度点为入口，避免 Revit 直接锁住安装目录中的业务 DLL。
```

- [ ] **步骤 3：运行验证**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：通过，输出形如：

```text
passed: modules=0, features=0, views=1, presets=0
```

- [ ] **步骤 4：提交文档更新**

```bash
git add docs/architecture.md docs/development.md
git commit -m "docs: describe framework slot command routing"
```

## 任务 6：最终验证和收尾

**文件：**
- 检查：所有已修改文件

- [ ] **步骤 1：查看工作区状态**

运行：

```bash
git status --short --branch
```

预期：没有未提交文件，分支 ahead 数量等于本计划产生的提交数量加已有本地提交。

- [ ] **步骤 2：运行最终静态验证**

运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：

```text
passed: modules=0, features=0, views=1, presets=0
```

- [ ] **步骤 3：检查业务 DLL 直连是否移除**

运行：

```powershell
rg -n "new CommandTarget\\(assemblyPath, feature\\.CommandType\\)|Assembly\\.LoadFrom" src/PlugHub.Revit2020
```

预期：

```text
src/PlugHub.Revit2020/CommandAssemblyLoader.cs:<line>:            var commandType = Assembly.LoadFrom(assemblyPath).GetType(commandTypeName, throwOnError: false);
```

`FrameworkFeatureCommand.cs` 和 `FeatureRibbonBuilder.cs` 不应包含 `Assembly.LoadFrom`。

- [ ] **步骤 4：检查 slot 连续性**

运行：

```powershell
rg -n "FrameworkFeatureCommandSlot001|FrameworkFeatureCommandSlot064|FrameworkFeatureCommandSlot128|case 1:|case 64:|case 128:" src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs
```

预期：六个 token 都能命中。

- [ ] **步骤 5：报告结果**

最终报告必须包含：

- 设计规格路径。
- 实现计划路径。
- 新增的关键运行时入口：`FeatureCommandDispatcher`、`FeatureSlotRegistry`、`FrameworkFeatureCommandSlots`、`ICommandAssemblyLoader`。
- 验证命令和结果。
- 明确说明 Revit 2020 仍不承诺真正热重载，本阶段只是收口命令入口并为 shadow copy / ALC 预留加载边界。
