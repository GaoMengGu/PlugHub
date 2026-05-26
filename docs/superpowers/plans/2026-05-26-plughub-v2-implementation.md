# PlugHub V2 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 按 `docs/superpowers/specs/2026-05-26-plughub-v2-design.md` 将 PlugHub V1 迁移为 PlugHub V2 的可静态验证实现。

**架构：** 先用静态验证定义红灯，再迁移配置模型和运行时组合，最后做 Revit 适配层和项目命名迁移。Revit 运行期不可可靠热替换的 Ribbon 结构变更只记录待重启诊断，执行开关通过命令代理即时阻断。

**技术栈：** C# .NET Framework 4.8、WinForms、Revit 2020 API 适配层、JSON 配置、PowerShell/dotnet 静态验证。

---

## 文件结构

- 修改：`src/PlugHub.StaticValidation/Program.cs`，先增加 PlugHub V2 红灯验证，再随迁移更新路径和断言。
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`，增加 `displayName`、`iconPath`、`moduleSources`、单工作台字段。
- 修改：`src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs`，支持可选 feature combinations、单工作台、displayName 解析和来源合并入口。
- 创建：`src/PlugHub.Framework/Configuration/DisplayNameResolver.cs`，集中处理显示名回退。
- 创建：`src/PlugHub.Framework/Sources/ModuleSourceResolver.cs`，解析本地文件夹和 GitHub 来源，GitHub 在网络不可用时只产生诊断。
- 修改：`src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs`，携带来源目录解析程序集并输出 PlugHub 诊断。
- 修改：`src/PlugHub.Framework/Runtime/FrameworkRuntimeState.cs` 和 `FrameworkRuntime.cs`，支持运行时刷新。
- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`、`FrameworkFeatureCommand.cs`、`FrameworkSettingsCommand.cs`，切换 PlugHub 文案、命令代理和 DockablePane 设置入口。
- 创建：`src/PlugHub.Revit2020/FrameworkSettingsPane.cs`，实现 DockablePane Provider。
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsForm.cs`，重构为可嵌入 DockablePane 的设置控件，支持右键菜单、拖拽排序、displayName、iconPath 和待重启提示。
- 修改：`config/*.example.json` 和 `config/schemas/*.json`，迁移为 PlugHub 单工作台配置。
- 修改：`README.md`、`docs/*.md`、`scripts/*.ps1`、`manifests/*.template`，同步 PlugHub 文案和验证命令。
- 机械迁移：`PlugHub.sln`、`PlugHub.slnx`、`src/PlugHub.*` 目录和 csproj 重命名到 `PlugHub.*`。

当前目录不是 git 仓库，不能按技能要求每个任务 commit。本计划每个任务用静态验证替代 commit 检查，并在最终报告中说明。

### 任务 1：写 PlugHub V2 红灯验证

**文件：**
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：编写失败的静态验证**

在 `Main()` 中调用新增方法：

```csharp
ValidatePlugHubV2Specification();
```

新增验证方法，断言 PlugHub 品牌、单工作台、displayName、iconPath、moduleSources、DockablePane 和命令代理关键 token：

```csharp
private static void ValidatePlugHubV2Specification()
{
    Require(File.Exists(FullPath("PlugHub.sln")), "PlugHub.sln is required.");
    Require(File.Exists(FullPath("src/PlugHub.Contracts/PlugHub.Contracts.csproj")), "PlugHub.Contracts project is required.");
    Require(!File.Exists(FullPath("PlugHub.sln")), "legacy PlugHub.sln should be removed after rename.");

    var modules = ReadObject("config/modules.example.json");
    var views = ReadObject("config/views.example.json");
    Require(StringValue(views, "defaultView") == "workspace", "PlugHub must use the single workspace view.");
    Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
    Require(ArrayValue(modules, "moduleSources").Count >= 2, "moduleSources must include localFolder and github examples.");

    var modulesText = ReadText("config/modules.example.json");
    Require(modulesText.Contains("\"displayName\""), "modules config must support displayName.");
    Require(modulesText.Contains("\"iconPath\""), "modules config must support iconPath.");
    Require(modulesText.Contains("\"type\": \"github\""), "modules config must include a github module source example.");

    var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
    Require(revitText.Contains("DockablePaneProviderData"), "settings must use a DockablePane provider.");
    Require(revitText.Contains("FeatureExecutionGate"), "feature execution must be gated by latest runtime configuration.");
}
```

- [ ] **步骤 2：运行验证确认红灯**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：FAIL，包含 `PlugHub.sln is required.` 或后续 PlugHub V2 断言失败。

### 任务 2：配置模型与单工作台

**文件：**
- 修改：`src/PlugHub.Framework/Configuration/ConfigurationModels.cs`
- 创建：`src/PlugHub.Framework/Configuration/DisplayNameResolver.cs`
- 修改：`src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs`
- 修改：`src/PlugHub.Framework/Composition/FeatureViewComposer.cs`
- 修改：`config/modules.example.json`
- 修改：`config/views.example.json`
- 修改：`config/feature-combinations.example.json`
- 修改：`config/schemas/modules.schema.json`
- 修改：`config/schemas/views.schema.json`

- [ ] **步骤 1：实现最小模型扩展**

在 `ModuleConfiguration` 添加：

```csharp
public string DisplayName { get; set; } = string.Empty;
public string SourceId { get; set; } = string.Empty;
```

在 `FeatureConfiguration` 添加：

```csharp
public string DisplayName { get; set; } = string.Empty;
public string IconPath { get; set; } = string.Empty;
```

在 `ModulesConfiguration` 添加：

```csharp
public List<ModuleSourceConfiguration> ModuleSources { get; set; } = new List<ModuleSourceConfiguration>();
```

并创建 `ModuleSourceConfiguration`，字段为 `Id`、`Type`、`Path`、`Repository`、`Ref`、`ManifestPath`、`Enabled`。

- [ ] **步骤 2：集中 displayName 解析**

创建 `DisplayNameResolver`：

```csharp
internal static class DisplayNameResolver
{
    public static string Resolve(string displayName, string name, string descriptorName, string id)
    {
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
        if (!string.IsNullOrWhiteSpace(descriptorName)) return descriptorName.Trim();
        return id ?? string.Empty;
    }
}
```

- [ ] **步骤 3：迁移配置样例**

将 `views.example.json` 改为单个 `workspace` 视图，`tabName` 为 `PlugHub`。将模块 id 前缀改为 `plughub.`，增加 `moduleSources`、`displayName` 和至少一个 `iconPath` 示例。`feature-combinations.example.json` 保留空 presets 或单 workspace preset，加载器必须允许它不存在或为空。

- [ ] **步骤 4：运行验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：仍可能 FAIL 于重命名或 DockablePane token，但配置模型相关断言不再失败。

### 任务 3：模块来源与诊断

**文件：**
- 创建：`src/PlugHub.Framework/Sources/ModuleSourceResolver.cs`
- 修改：`src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs`
- 修改：`src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs`
- 修改：`src/PlugHub.Framework/Runtime/FrameworkRuntime.cs`
- 修改：`src/PlugHub.StaticValidation/Program.cs`

- [ ] **步骤 1：实现来源解析器**

`ModuleSourceResolver` 接收 base/config modules，返回带诊断的 `ModulesConfiguration`。本地来源读取 `<path>/modules.json` 或 `manifestPath`；GitHub 来源 V2 不联网下载，只检查缓存路径，不存在时产生 `PH-SOURCE-MISSING` warning。

- [ ] **步骤 2：发现服务支持 source base directory**

为 `ModuleConfiguration` 添加内部解析用 `ResolvedBaseDirectory` 字段或在发现服务中根据 `SourceId` 映射来源目录。程序集解析顺序：绝对路径 > 来源目录 > PlugHub base directory。

- [ ] **步骤 3：验证来源失败不阻断启动**

在静态验证中断言 `ModuleSourceResolver` 存在、包含 `PH-SOURCE-MISSING` 和 `localFolder`、`github` token。

- [ ] **步骤 4：运行验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：来源相关断言通过。

### 任务 4：运行时刷新与命令代理 gating

**文件：**
- 创建：`src/PlugHub.Framework/Runtime/FeatureExecutionGate.cs`
- 修改：`src/PlugHub.Framework/Runtime/FrameworkRuntimeState.cs`
- 修改：`src/PlugHub.Revit2020/FeatureRibbonBuilder.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkFeatureCommand.cs`

- [ ] **步骤 1：添加执行门禁**

`FeatureExecutionGate` 从 `FrameworkRuntimeState.Current` 查找 feature id 或 command key，若模块/功能被禁用或隐藏，返回阻断结果。

- [ ] **步骤 2：Ribbon 按钮统一走代理**

`FeatureRibbonBuilder.CreateFeatureButtonData()` 的按钮程序集和类型默认指向 `FrameworkFeatureCommand`，真实命令信息通过 feature id 在运行时快照中查找并反射执行。保留真实命令 assembly/type 在 view model 中。

- [ ] **步骤 3：代理执行真实命令**

`FrameworkFeatureCommand.Execute()` 读取当前 feature id，先调用 `FeatureExecutionGate`。通过门禁后，反射加载 `CommandAssembly` 和 `CommandType`，创建 `IExternalCommand` 并转发 Execute。

- [ ] **步骤 4：运行验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：`FeatureExecutionGate` token 通过，Revit adapter token 通过。

### 任务 5：DockablePane 设置页

**文件：**
- 创建：`src/PlugHub.Revit2020/FrameworkSettingsPane.cs`
- 修改：`src/PlugHub.Revit2020/ExternalApplicationEntry.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsCommand.cs`
- 修改：`src/PlugHub.Revit2020/FrameworkSettingsForm.cs`

- [ ] **步骤 1：注册 DockablePane**

创建 `FrameworkSettingsPane : IDockablePaneProvider`，使用 `FrameworkSettingsForm` 作为 `FrameworkElement` 或 WinForms host 的内容。`ExternalApplicationEntry.OnStartup()` 注册 pane。

- [ ] **步骤 2：设置命令打开 pane**

`FrameworkSettingsCommand.Execute()` 查找 PlugHub 设置 DockablePane id，调用 `Show()`。若 Pane 不可用，回退打开现有设置窗体。

- [ ] **步骤 3：补齐 UI 行为 token**

在设置控件中支持模块/功能右键菜单、displayName 编辑、iconPath、buttonSize、上下移动和拖拽排序保存。拖拽可以先用 WinForms `AllowDrop`、`MouseDown`、`DragOver`、`DragDrop` 实现同表排序。

- [ ] **步骤 4：保存后刷新运行时**

保存 JSON 后调用运行时刷新方法；涉及 Ribbon 结构的变更在 UI 提示“待重启 Revit 生效”。

- [ ] **步骤 5：运行验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：DockablePane token 和设置页 token 通过。

### 任务 6：PlugHub 机械重命名与文档同步

**文件：**
- 移动：`PlugHub.sln` -> `PlugHub.sln`
- 移动：`PlugHub.slnx` -> `PlugHub.slnx`
- 移动：`src/PlugHub.*` -> `src/PlugHub.*`
- 移动：`manifests/PlugHub.addin.template` -> `manifests/PlugHub.addin.template`
- 修改：所有 csproj、sln、slnx、源码 namespace/use、README、docs、scripts、config、manifest 中的品牌名和路径。

- [ ] **步骤 1：执行机械重命名**

用 PowerShell `Move-Item` 重命名解决方案和项目目录；使用受控替换将旧框架品牌迁移为 `PlugHub`，将旧配置前缀迁移为 `plughub.`，将旧内置模块文案替换为 PlugHub/Builtin。

- [ ] **步骤 2：更新静态验证自身路径**

把 `Program.cs` 的 required files 更新到 PlugHub 路径，并把命令文档改为：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

- [ ] **步骤 3：运行最终验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：PASS，输出类似 `passed: modules=6, features=11, views=1, presets=0`。

### 任务 7：最终核对

**文件：**
- 修改：`README.md`
- 修改：`docs/verification.md`
- 修改：`docs/architecture.md`
- 修改：`docs/frontend-ux.md`
- 修改：`AGENTS.md` 如需同步验证命令

- [ ] **步骤 1：扫描旧品牌**

运行：

```powershell
Select-String -Path (Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj|dist)\\' }).FullName -Pattern 'PlugHub|PlugHub'
```

预期：只剩兼容迁移说明或没有结果。

- [ ] **步骤 2：运行项目要求验证**

运行：

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

预期：PASS。

- [ ] **步骤 3：记录非 Revit 验证边界**

最终报告中明确：当前环境只完成 C# 静态验证，未做 Revit 2020 实机加载测试。
