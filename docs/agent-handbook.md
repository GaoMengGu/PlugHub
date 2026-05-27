# PlugHub Agent 协作手册

## 1. 项目一句话

PlugHub 是面向 Revit 2020 的模块化插件框架底座。它负责让后续功能模块可以被发现、启用/禁用、隐藏、排序、组合、诊断并渲染到 Revit Ribbon；V1 不实现真实 Revit 业务操作。

## 2. 当前代码结构

```text
PlugHub.sln
src/
  PlugHub.Contracts/         # 稳定模块契约，不引用 Framework 或 Revit API
  PlugHub.Framework/         # 配置、发现、注册、组合、诊断、运行时快照
  PlugHub.Revit2020/         # Revit 2020 IExternalApplication 入口和 Ribbon/UI 适配
  PlugHub.SampleModule/      # 样例模块，只依赖 Contracts
  PlugHub.StaticValidation/  # 静态验证入口
config/
  modules.example.json
  views.example.json
  feature-combinations.example.json   # 兼容保留，可为空
  schemas/
scripts/
  build-revit2020.ps1
  install-addin.ps1
docs/
```

## 3. 分层规则

依赖方向固定：

```text
Revit 2020
  -> PlugHub.Revit2020
  -> PlugHub.Framework
  -> PlugHub.Contracts

PlugHub.SampleModule -> PlugHub.Contracts
```

硬性边界：

- `Contracts` 只定义稳定契约。
- `Framework` 不引用 Revit API，只处理配置、发现、组合、诊断和运行时状态。
- `Revit2020` 是唯一允许引用 `Autodesk.Revit` 的层。
- `SampleModule` 只用于验证契约和配置组合，不做真实业务。
- 不要把静态验证说成 Revit 实机测试。

## 4. 核心运行链路

Revit 启动时：

1. `ExternalApplicationEntry.OnStartup` 定位插件目录和 `config` 目录。
2. `FrameworkRuntime.Load` 加载运行时配置。
3. `FrameworkConfigurationLoader` 读取 `modules.json`、`views.json`、`feature-combinations.json`。
4. `ModuleSourceResolver` 合并内置模块、`moduleDirectories` 自动发现清单、指定本地文件夹来源和 GitHub 缓存/拉取来源诊断。
5. `ModuleDiscoveryService` 根据 manifest 的 `assembly` 和 `type` 做模块发现与诊断。
6. `FeatureRegistry` 注册 enabled 且 visible 的模块功能，处理重复模块和重复 feature。
7. `FeatureViewComposer` 按 workspace 的 include/exclude、group 和 sort 组合功能。
8. `FeatureRibbonBuilder` 把组合结果渲染为 Ribbon tab、panel 和按钮。
9. `FrameworkRuntimeState` 保存当前快照，供按钮命令和 DockablePane 设置页读取。

## 5. 配置模型速览

### modules.json

描述模块和功能入口：

- `id`：模块 ID。
- `assembly`：模块 DLL 文件名或路径。
- `type`：实现 `IPlugHubModule` 的完整类型名。
- `enabled`：是否启用模块。
- `visible`：是否进入当前功能列表。
- `order`：模块排序。
- `displayName`：用户自定义显示名。
- `sourceId`：模块来源 id。
- `features`：功能入口列表。

功能入口关键字段：

- `id`：全局唯一 feature ID。
- `name`：模块发布者提供的默认按钮名。
- `displayName`：用户自定义按钮名。
- `category`：用于 workspace 过滤。
- `group`：优先匹配 workspace group，决定 Ribbon panel。
- `tags`：用于 workspace/group 过滤。
- `order`：同一 group 中的排序。
- `defaultState`：`Visible`、`Disabled` 或 `Hidden`。
- `buttonSize`：`large` 或 `small`。`small` 会按 Revit stacked items 呈现。
- `iconPath`：图标路径。图标变更保存后标记为待重启生效。
- `commandKey`：命令分发和执行门禁用的稳定键。

### views.json

描述单一 `workspace` 工作台。PlugHub 不再让用户切换多个视图集。

workspace 组合规则：

1. 先应用 workspace-level `excludeTags`、`excludeCategories`。
2. 再检查 workspace-level `includeTags`、`includeCategories`。如果 include 为空，则不做 include 限制。
3. feature 必须匹配某个 workspace group。
4. 最后按 `sort` 排序，默认是 `group.order`、`feature.order`、`feature.name`、`feature.id`。

### feature-combinations.json

兼容保留，可为空。V2 的主要入口治理通过 modules、moduleDirectories、moduleSources 和 workspace group 完成。

## 6. Revit 中能看到什么

默认 workspace 是 `workspace`，所以 Ribbon 里会看到：

- 固定的 `框架设置` panel 和 `设置` 按钮。
- `workspace` 下匹配出的 `入门`、`项目流程` 等 panel。
- 样例模块里的多个空白占位功能，用于测试加载、排序和按钮大小。

## 7. 可视化设置能力

`PlugHub.Revit2020` 中的 `FrameworkSettingsCommand` 打开 DockablePane 设置页。设计人员可以调整：

- 模块开关：调整模块是否启用、是否显示、显示名和模块顺序。
- 功能按钮：调整功能是否显示、显示名、所在面板、图标路径、按钮顺序和按钮大小。
- 右键菜单：启用、禁用、显示、隐藏、设置大小和上下移动。
- 拖拽排序：模块和功能表格支持拖拽排序。

设置页刻意不暴露 workspace 的 include/exclude 过滤、sort 顺序和 group 定义；这些仍由 `config\views.json` 维护，避免设计人员在常用页面中误改高级规则。

保存会写回运行目录下的：

- `config\modules.json`
- `config\views.json`
- `config\feature-combinations.json`

模块/功能开关保存后会刷新运行时快照并尽量即时生效；Ribbon 结构、图标和按钮大小变更需要重启 Revit 2020 才能重新渲染。

## 8. 新模块接入方式

1. 新建 .NET Framework 4.8 DLL 项目。
2. 引用 `PlugHub.Contracts.dll`。
3. 实现 `IPlugHubModule`。
4. 在 `Describe()` 返回 `ModuleDescriptor`，并声明 `FeatureDescriptor` 列表。
5. 编译 DLL，并把 DLL 放到插件输出目录或配置指定的模块目录。
6. 在 `config\modules.json` 添加模块 manifest。
7. 确认 feature 的 `category`、`group` 或 `tags` 能匹配 `workspace`。

最小 manifest 示例：

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
      "commandKey": "your.module.feature"
    }
  ]
}
```

## 9. 构建、安装和验证

本机静态验证：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

Revit 2020 构建与安装：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "C:\Program Files\Autodesk\Revit 2020" -InstallAddin
```

输出目录：

```text
dist\Revit2020
```

安装后的 addin manifest：

```text
%APPDATA%\Autodesk\Revit\Addins\2020\PlugHub.addin
```

Revit 实机必验：

- Revit 能识别 `IExternalApplication`。
- 能看到 `PlugHub` Ribbon tab。
- 能看到 `框架设置` panel 和 workspace 组合出的功能 panel。
- 点击占位按钮只显示框架状态或占位提示，不执行真实业务。

## 10. 提交前检查清单

- 代码是否仍保持分层边界。
- 是否不在 Framework/Contracts/SampleModule 引入 `Autodesk.Revit`。
- 是否没有把 `bin/`、`obj/`、`dist/` 等构建产物纳入提交。
- 配置 schema 是否跟新字段同步。
- 是否运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

## 11. 当前已知边界

- 不承诺 .NET Framework 已加载程序集的真正热卸载；新增模块程序集和二进制替换需要下次 Revit 启动生效。
- 不做插件市场、授权系统、自动更新服务。
- 静态验证不能替代 Windows + Revit 2020 实机验证。

## 12. 适合后续 agent 继续做的方向

- 给设置窗口增加新增/删除模块和 feature 的能力。
- 给配置编辑增加 JSON schema 校验、错误定位和恢复默认值。
- 增加单元测试项目，覆盖 composer、loader、registry、discovery。
- 增加 Revit 实机测试记录模板，区分静态验证和人工验收。
