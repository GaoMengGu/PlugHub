# 当前架构

## 分层

```text
Revit 2020
  -> PlugHub.Revit2020        # IExternalApplication、Ribbon、WPF UI、Revit 命令路由
  -> PlugHub.Framework        # 配置、来源解析、发现、注册、组合、诊断、运行时快照
  -> PlugHub.Contracts        # 模块和功能契约

PlugHub.StaticValidation      # 静态验证入口
```

硬性约束：

- `PlugHub.Contracts` 不引用 Framework 或 Revit API。
- `PlugHub.Framework` 不引用 Revit API。
- `PlugHub.Revit2020` 是 Revit 入口适配层，负责启动、Ribbon 渲染、WPF 窗口和命令路由。
- 外部业务命令模块可以引用 Revit API，但不能依赖 Framework。
- 框架层不实现具体 Revit 建模、出图、族管理或参数写入业务。

## 启动链路

1. `ExternalApplicationEntry.OnStartup` 定位插件目录和 `config` 目录。
2. `FrameworkRuntime.Load` 加载运行时配置。
3. `FrameworkConfigurationLoader` 读取 `sources.json`、`views.json`、`feature-combinations.json`。
4. `ModuleSourceResolver` 合并根配置、`packageDirectories`、本地来源和 GitHub 来源。
5. `ModuleDiscoveryService` 根据 `assembly` 和 `type` 做模块发现与诊断。
6. `FeatureRegistry` 注册 enabled 且 visible 的模块功能，处理重复模块和重复 feature。
7. `FeatureViewComposer` 按 `workspace` 的 group、category、tag 和 sort 组合功能。
8. `FeatureRibbonBuilder` 创建 `PlugHub` Ribbon tab、panel 和按钮。
9. `FrameworkRuntimeState` 保存快照，供命令和 WPF 设置页读取。

## 配置模型

### `sources.json`

负责框架层的插件包来源，不是插件包自身清单。

- `packageDirectories`：自动扫描的投放目录，当前默认保留 `packages/dropins`。
- `moduleSources`：显式来源，可配置本地文件夹或 GitHub 仓库。
- `modules`：根配置内联插件包列表，当前默认为空，框架不随包提供业务模块。
- `conflictPolicy`：重复模块、重复功能、缺失模块类型等冲突策略。

### `package.json` / `*.package.json`

外部插件包目录推荐使用 `package.json` 作为插件包清单。平铺投放单个 DLL 时，可使用 `<DllName>.package.json` 作为邻接清单。一个来源目录可以包含多个插件包文件夹或多个邻接清单；框架会递归扫描 `packageDirectories` 中的 `package.json` 和 `*.package.json`，显式来源则按 `manifestPath` 读取指定清单。

模块关键字段：

- `id`：模块唯一 ID。
- `assembly` / `type`：模块程序集和实现 `IPlugHubModule` 的类型。
- `displayName`：用户可见模块名。
- `enabled` / `visible`：是否加载、是否进入功能列表。
- `order`：模块排序，设置页以「第 N 项」展示，不暴露裸数字。
- `features`：功能入口列表。

功能关键字段：

- `id`：全局唯一 feature ID。
- `displayName`：用户可见功能名。
- `category` / `group` / `tags`：匹配 `workspace` 分组。
- `defaultState`：`Visible`、`Disabled` 或 `Hidden`。
- `buttonSize`：`large` 或 `small`。
- `iconPath`：可选图标路径；为空时使用内置默认图标。
- `commandAssembly` / `commandType`：指向实际 `IExternalCommand`。

### `views.json`

只定义一个 `workspace`。功能进入 Ribbon 的条件是：

1. 功能默认状态为 `Visible`。
2. 未命中 workspace 的排除 tag/category。
3. 命中 workspace 的包含 tag/category，或 workspace 没有设置包含条件。
4. 命中已配置 group；如果 workspace 没有配置 group，则按 feature 的 `group`、`category` 或 `moduleId` 生成 fallback panel。
5. 最终按 `group.order`、`feature.order`、`feature.name`、`feature.id` 排序。

### `feature-combinations.json`

兼容保留。当前入口治理主要依赖 `sources.json`、插件包清单、来源配置和 workspace group。

## 设置窗口

设置入口是 `FrameworkSettingsCommand`，打开 `FrameworkSettingsWindow`。当前设置页提供：

- 插件包：显示外部清单发现的 module，允许整体启用、禁用、隐藏、改显示名和排序；不在 Revit 设置页新建业务模块。
- 功能：显示/隐藏、显示名、所属分组、按钮大小、图标路径和拖拽/右键排序；不在 Revit 设置页新建空功能。
- 分组：集中管理 Revit Ribbon panel 的显示名和顺序，功能通过「所属分组」决定进入哪个 panel。
- 来源：启用/禁用本地文件夹或 GitHub 来源，维护路径、仓库、分支和清单路径。
- 诊断：只读展示当前运行时诊断。

设置页只保存配置，不执行 Git 拉取、程序集加载或运行时刷新。Ribbon 结构类变更保存后需要重启 Revit。

## 模块契约

模块实现 `IPlugHubModule`，通过 `Describe()` 返回 `ModuleDescriptor` 和 `FeatureDescriptor`。框架会将运行时描述与配置清单合并，用于发现、校验、诊断和 Ribbon 组合。

业务功能如果需要调用 Revit API，应在外部业务模块中实现 `IExternalCommand`，并通过 feature 的 `commandAssembly` / `commandType` 指向该命令。外部来源中的相对 `commandAssembly` 和 `iconPath` 按模块清单所在目录解析。Revit 适配层负责路由，Framework 不直接执行 Revit API。

## 关键设计决策

- 不保留样例模块、空白模块、占位功能或内置业务功能；默认配置不暴露任何业务按钮。
- 不使用 DockablePane 承载设置页，避免关闭和保存时造成 Revit UI 状态复杂化。
- 不在 Ribbon 暴露「刷新配置」「状态」「诊断摘要」等重复入口；固定保留「设置」入口。
- 默认图标由 `DefaultRibbonIconProvider` 代码生成，避免依赖额外资源文件。
- 构建脚本会清理旧样例模块和旧内置模块产物，防止 `dist\Revit2020` 中残留已删除内容。
