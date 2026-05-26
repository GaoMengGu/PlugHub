# PlugHub Framework

面向 Revit 2020 的模块化插件框架。目标是为后续 Revit 功能模块提供可治理的底座，而不是实现具体业务命令。

协作 agent 或新开发者请先阅读 [docs/README.md](docs/README.md) 和 [docs/agent-handbook.md](docs/agent-handbook.md)。

## 核心能力

- 模块契约：统一 `IPlugHubModule`、`ModuleDescriptor`、`FeatureDescriptor`。
- 模块发现/注册：从模块清单和程序集元数据收集、校验功能入口。
- 启用/禁用/隐藏：通过配置决定模块和功能是否进入 PlugHub 工作台。
- 排序/组合：按 workspace、group、tag、category、order 组织功能列表。
- 诊断：记录模块加载、跳过、冲突、失败原因，并汇总到运行时快照。
- 单工作台：取消多视图集，使用一个 `workspace` 展示模块和功能。
- 来源配置：支持本地文件夹和 GitHub 仓库来源的模块声明。
- Revit 2020 适配层：读取运行时配置，创建 `PlugHub` Ribbon tab/panel/button，Revit API 依赖隔离在最薄层。
- 业务命令模块：`PlugHub.BuiltinModule` 已接入两个 Revit API 命令入口，按钮通过 `commandAssembly` 和 `commandType` 路由到实际 `IExternalCommand`。

## 本机验证

```bash
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

当前非 Revit 环境只做 C# 静态验证；只有 Windows + Revit 2020 环境可以验证实际加载和 Ribbon 展示。

仓库同时保留 `PlugHub.slnx` 作为轻量项目清单；当前 .NET 8 SDK 可直接识别并构建的是标准 `PlugHub.sln`。

## Windows/Revit 2020 构建

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

构建产物位于 `dist\Revit2020`，其中 `.addin` 会指向生成的 `PlugHub.Revit2020.dll` 绝对路径。启动 Revit 2020 后应看到 `PlugHub` 页签，以及由 `config/views.example.json` 和 `config/modules.example.json` 组合出的面板与按钮。

## Revit 中看到什么

PlugHub 只暴露一个 `workspace` 工作台视图。Revit 启动时会创建一个 `PlugHub` Ribbon tab，里面按配置分组展示已启用且可见的功能。

```json
{
  "defaultView": "workspace"
}
```

Ribbon 中固定有一个 `框架设置` panel，点击 `设置` 可打开 DockablePane 配置页。设计人员可以在里面调整：

- 模块开关：调整模块是否启用、是否显示、显示名和模块顺序。
- 功能按钮：调整功能是否显示、显示名、所在面板、图标路径、按钮顺序和按钮大小。
- 模块来源：通过 `moduleSources` 声明本地文件夹或 GitHub 仓库来源。

`buttonSize` 支持 `large` 和 `small`。`large` 会作为普通大按钮渲染；`small` 会在同一 panel 内按 Revit stacked items 堆叠显示。功能位置主要由 `group` 和 `order` 决定，panel 位置由 workspace group 的 `order` 决定。

模块/功能开关会尽量通过运行时快照即时生效；Ribbon panel、按钮新增/删除、图标和大小调整属于 Revit 结构类变更，保存后标记为待重启生效。

## 编写和加载功能

1. 新建一个 .NET Framework 4.8 DLL 项目。
2. 引用 `PlugHub.Contracts.dll`。
3. 实现 `IPlugHubModule`，在 `Describe()` 返回 `ModuleDescriptor`，并在 `Features` 中声明功能入口。
4. 编译 DLL，把 DLL 放到 `dist\Revit2020` 或配置指定的模块目录。
5. 在 `dist\Revit2020\config\modules.json` 增加模块记录：

```json
{
  "id": "your.module.id",
  "assembly": "Your.Module.dll",
  "type": "Your.Module.Namespace.YourModule",
  "name": "你的模块",
  "displayName": "用户自定义模块名",
  "description": "模块说明",
  "enabled": true,
  "visible": true,
  "order": 300,
  "tags": ["project"],
  "dependsOn": [],
  "features": [
    {
      "id": "your.module.feature",
      "name": "你的功能入口",
      "displayName": "用户自定义功能名",
      "description": "入口说明",
      "category": "project",
      "group": "project-workflow",
      "tags": ["project"],
      "order": 310,
      "defaultState": "Visible",
      "buttonSize": "large",
      "iconPath": "icons/your-feature.png",
      "commandKey": "your.module.feature",
      "commandAssembly": "Your.Module.dll",
      "commandType": "Your.Module.Commands.YourExternalCommand"
    }
  ]
}
```

6. 如果功能只是占位或诊断入口，可以省略 `commandAssembly`/`commandType`，按钮会回落到框架状态命令；如果要执行 Revit API 业务，请让 `commandType` 指向一个 `IExternalCommand`。
7. 确认该功能的 `category`、`group` 或 `tags` 能匹配 `workspace` 的分组规则，否则模块已加载但按钮不会进入工作台。

## 已迁入的 Revit API 插件

`plugins` 目录中的两个旧命令已迁入 `src/PlugHub.BuiltinModule`：

- `DuctPreferredJunctionSwitcherCommand`：切换风管类型的 Tee/Tap 首选连接类型。
- `BatchAddMaterialParameterCommand`：批量选择 `.rfa` 族文件，添加并关联“材质”参数。

它们在 `config/modules.example.json` 中分别注册为 `plughub.builtin.duct-tools` 和 `plughub.builtin.family-tools`，默认会进入 `workspace` 工作台。
