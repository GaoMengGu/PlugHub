# 当前架构

## 分层

```text
Revit 2020
  -> PlugHub.Revit2020        # IExternalApplication、Ribbon、WPF UI、Revit 命令路由
  -> PlugHub.Framework        # 配置、本地包解析、仓库安装服务、发现、注册、组合、日志、运行时快照
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
4. `ModuleSourceResolver` 合并根配置和本地 `packageDirectories`。
5. `ModuleDiscoveryService` 以 `package.json` / `*.package.json` 清单为权威来源生成模块和功能描述，并产生日志；启动发现不加载业务 DLL，也不调用模块 `Describe()`。
6. `FeatureRegistry` 注册 enabled 且 visible 的模块功能，处理重复模块和重复 feature。
7. `FeatureViewComposer` 按 `workspace` 的 group、category、tag 和 sort 组合功能。
8. `FeatureRibbonBuilder` 创建 `PlugHub` Ribbon tab、panel 和按钮；业务功能按钮绑定到 `PlugHub.Revit2020.dll` 内的稳定 slot 命令。
9. `FeatureSlotRegistry` 保存 slot 到 feature id 的映射；`FrameworkRuntimeState` 保存快照，供命令调度和 WPF 设置页读取。
10. 用户点击业务按钮时，slot 命令进入 `FeatureCommandDispatcher`，由框架校验状态，并通过 net48 shadow-copy 加载器从 `runtime-cache` 缓存副本加载实际 `IExternalCommand`。

## 配置模型

### `sources.json`

负责框架层的已安装插件包和可浏览仓库，不是插件包自身清单。

- `packageDirectories`：自动扫描的安装目录，当前默认只保留 `packages`。Revit 启动只从这里发现插件包。
- `moduleSources`：兼容保留，默认为空；不再配置启动时拉取或加载的仓库来源。
- `repositories`：设置页可浏览的插件包仓库。`provider` 支持 GitHub 和 Gitee；默认公开仓库使用 Gitee `https://gitee.com/GaoMengGu/PlugHub_Packages`；公开仓库无需凭据，私有仓库需要 `apiKey`；仓库内容只有在用户选择安装或更新后才会复制到 `packages`。
- `modules`：根配置内联插件包列表，当前默认为空，框架不随包提供业务模块。
- `conflictPolicy`：重复模块、重复功能等冲突策略；`missingModuleType` 为兼容旧配置保留，默认启动发现不再按模块类型做强制校验。

### `package.json` / `*.package.json`

外部插件包目录推荐使用 `package.json` 作为插件包清单。平铺投放单个 DLL 时，可使用 `<DllName>.package.json` 作为邻接清单。`packages` 中可以包含多个插件包文件夹或多个邻接清单；框架会递归扫描 `packageDirectories` 中的 `package.json` 和 `*.package.json`。仓库浏览得到的包清单不会直接参与启动加载，只有安装到 `packages` 后才会进入发现流程。

模块关键字段：

- `id`：模块唯一 ID。
- `assembly` / `type`：可选模块实现元数据。启动发现不依赖它们加载模块；当功能未单独配置 `commandAssembly` 时，`assembly` 仍作为默认命令程序集路径。
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

兼容保留。当前入口治理主要依赖 `sources.json`、已安装插件包清单、仓库安装结果和 workspace group。

## 设置窗口

设置入口是 `FrameworkSettingsCommand`，打开 `FrameworkSettingsWindow`。当前设置页提供：

- 功能：显示/隐藏、显示名、所属分组、按钮大小、图标路径和拖拽/右键排序；不在 Revit 设置页新建空功能，也不提供插件包整体设置页。
- 分组：集中管理 Revit Ribbon panel 的显示名和顺序，支持通过右键菜单新增或删除未使用的自定义分组，功能通过「所属分组」决定进入哪个 panel。
- 仓库：通过 `类型` 列选择 GitHub 或 Gitee，通过 `可见性` 列选择公开或私有，维护仓库、分支和私有仓库 `apiKey`；用户可手动浏览仓库中的 `package.json` 和 `*.package.json`。一个清单中的多个 module 会作为多个插件行展示，安装和更新由 PlugHub 拆成单插件本地包，只复制所选插件的清单及其引用的 DLL/资源到本地 `packages`。右键菜单只提供一个 `新增仓库` 入口，新增后在表格中调整类型和可见性。
- 日志：只读展示当前运行时日志。

设置页保存配置时不执行 Git 拉取、程序集加载或运行时刷新。只有用户在仓库页显式选择“浏览仓库插件包”时才访问仓库；仓库访问使用 sparse checkout，只取包清单、DLL 和图标等包资产，不拉取源码目录。安装和更新只是复制选中插件的单模块清单和必要文件到 `packages`，卸载只删除 `packages` 中对应已安装目录。新增、更新或卸载已加载 DLL 后需要重启 Revit；如果 DLL 被占用，PlugHub 会先改写清单让模块不再被发现，并把删除或替换文件的动作记录为下次启动前执行的待处理操作。

## 运行时缓存

Revit 2020 适配层的业务命令加载器是 `Net48ShadowCopyCommandAssemblyLoader`。用户点击业务功能时，加载器会从 `packages` 中推导插件包根目录，按包内容计算 hash，将包内 DLL、依赖和资源复制到 `runtime-cache/<package>/<hash>/`，再从缓存副本加载业务命令。旧 hash 缓存会被尽力清理；如果目录被已加载程序集占用，路径会记录到 `runtime-cache/pending-cleanup.txt`，下次命令加载前重试。

这个机制只减少 Revit 锁住安装目录业务 DLL 的概率，不提供 .NET Framework 程序集卸载能力，也不承诺同一 Revit 会话内替换已加载的同名程序集。

## 模块契约

插件包清单是模块和功能入口的权威来源。`IPlugHubModule` 与 `Describe()` 保留为模块侧契约和后续可选诊断/校验扩展点，但默认启动链路不实例化模块类型，也不把运行时描述合并回 Ribbon 组合。

业务功能如果需要调用 Revit API，应在外部业务模块中实现 `IExternalCommand`。业务功能仍通过 feature 的 `commandAssembly` / `commandType` 指向实际 `IExternalCommand`，但 Revit Ribbon 不直接绑定该业务程序集。Revit 2020 适配层先进入稳定框架 slot 命令，再由框架调度器在点击时通过 shadow-copy 加载器加载业务命令。已安装插件包中的相对 `commandAssembly` 和 `iconPath` 按模块清单所在目录解析。Revit 适配层负责路由，Framework 不直接执行 Revit API。

## 关键设计决策

- 不保留样例模块、空白模块、占位功能或内置业务功能；默认配置不暴露任何业务按钮。
- 不使用 DockablePane 承载设置页，避免关闭和保存时造成 Revit UI 状态复杂化。
- 不在 Ribbon 暴露「刷新配置」「状态」「日志摘要」等重复入口；固定保留「设置」入口。
- 默认图标和一组内置可选图标由 `DefaultRibbonIconProvider` 代码生成，避免依赖额外资源文件。
- 构建脚本会清理旧样例模块和旧内置模块产物，防止 `dist\Revit2020` 中残留已删除内容。
