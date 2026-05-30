# PlugHub Runtime Routing 重构设计

日期：2026-05-30

## 背景

PlugHub 当前主线面向 Revit 2020，项目目标框架是 .NET Framework 4.8。这个运行时不能使用 .NET Core / .NET 5+ 的 `AssemblyLoadContext`，因此 Revit 2020 不能通过 ALC 实现程序集卸载或依赖隔离。

当前代码存在两个会影响插件更新和未来隔离能力的加载入口：

- `ModuleDiscoveryService` 在发现阶段通过 `Assembly.LoadFrom` 加载模块程序集，并调用模块类型的 `Describe()`。
- `FeatureRibbonBuilder` 在功能有 `commandType` 且程序集存在时，会把 Revit Ribbon 按钮直接绑定到外部业务 `IExternalCommand` 程序集。

第二点会绕过 `FrameworkFeatureCommand`，导致任何后续 shadow copy 或 Revit 2025+ ALC 方案都无法完整接管业务命令加载。

## 目标

第一阶段目标是收口命令入口，让所有业务功能点击都先进入稳定的 PlugHub 框架调度层。

具体目标：

- Revit Ribbon 不再直接绑定业务插件 DLL。
- 每个业务功能按钮绑定到 `PlugHub.Revit2020.dll` 中的稳定代理命令。
- 代理命令把 feature id 传给统一调度器，再由调度器查找功能、校验状态并执行业务命令。
- Revit 2020 继续保持可运行，不引入 ALC。
- 为后续 shadow copy 加载器和 Revit 2025+ ALC 加载器保留接口边界。

## 非目标

第一阶段不实现以下内容：

- 不在 Revit 2020 中承诺真正热重载或程序集卸载。
- 不引入 AppDomain 隔离。
- 不迁移到 .NET 8。
- 不改变默认 Gitee 仓库、包安装来源或 release workflow。
- 不实现 Revit 2025+ ALC 加载器，只保留后续可替换的加载边界。

## 推荐方案

采用“稳定代理命令 slot + 统一框架调度”的方案。

`PlugHub.Revit2020` 中提供一组固定代理命令类型，例如：

- `FrameworkFeatureCommandSlot001`
- `FrameworkFeatureCommandSlot002`
- ...
- `FrameworkFeatureCommandSlot128`

所有 slot 类型都实现 `IExternalCommand`，但内部不包含业务逻辑，只把自身 slot id 交给框架调度器。框架在启动创建 Ribbon 时建立 `slot -> feature id` 映射，并在用户点击时根据 slot 找回目标 feature。

这样 Revit 直接加载的始终是稳定框架程序集，而不是业务插件程序集。

## 组件设计

### FeatureCommandSlot

职责：

- 作为 Revit Ribbon 可绑定的稳定 `IExternalCommand` 类型。
- 暴露固定 slot id。
- 调用统一的 feature 命令调度器。

约束：

- slot 类型不能引用业务插件程序集。
- slot 数量应有上限，第一阶段建议 128 个。
- 超过 slot 数量的功能不创建 Ribbon 按钮，并输出诊断日志。

### FeatureCommandDispatcher

职责：

- 根据 slot id 找到 feature id。
- 从 `FrameworkRuntimeState.Current` 获取当前快照。
- 使用 `FeatureExecutionGate` 判断功能是否允许执行。
- 解析 `CommandAssembly` 和 `CommandType`。
- 委托命令加载器创建业务 `IExternalCommand` 实例。
- 执行业务命令并返回 Revit `Result`。

约束：

- 调度器是唯一允许执行业务命令的入口。
- 业务命令加载失败时必须产生可见诊断，并返回 `Result.Failed` 或 `Result.Cancelled`。
- 没有 `CommandType` 的功能保持当前回退行为，显示框架状态或日志。

### FeatureSlotRegistry

职责：

- 在 Ribbon 构建时按稳定顺序为 feature 分配 slot。
- 保存 `slot id -> feature id` 映射。
- 支持运行时刷新后替换映射。

约束：

- slot 分配顺序必须稳定，建议使用当前 Ribbon 排序结果。
- 映射只保存 feature id，不保存业务程序集或业务类型引用。
- 映射刷新必须是原子替换，避免点击时读到半更新状态。

### ICommandAssemblyLoader

职责：

- 根据 command assembly 路径和 command type 名称返回 `IExternalCommand` 实例。
- 隔离具体加载策略。

第一阶段实现：

- `Net48DirectCommandAssemblyLoader`
- 使用现有 `Assembly.LoadFrom` 行为，但只在点击时加载。

后续实现：

- `Net48ShadowCopyCommandAssemblyLoader`：复制主 DLL、依赖和资源到 hash 缓存目录后加载缓存副本。
- `Net8AlcCommandAssemblyLoader`：在 Revit 2025+ 中使用 collectible ALC 和 `AssemblyDependencyResolver`。

## 数据流

启动时：

1. `FrameworkRuntime.Load` 加载配置和包清单。
2. `FeatureViewComposer` 生成可见功能列表。
3. `FeatureRibbonBuilder` 为每个可见 feature 分配 slot。
4. Ribbon 按钮绑定到框架 slot 命令类型。
5. `FeatureSlotRegistry` 记录 `slot id -> feature id`。

点击时：

1. Revit 调用稳定 slot 命令。
2. slot 命令调用 `FeatureCommandDispatcher.Execute(slotId, ...)`。
3. 调度器解析 feature id 和当前 feature。
4. 调度器检查功能状态。
5. 调度器通过 `ICommandAssemblyLoader` 创建业务命令实例。
6. 调度器执行业务命令。

## 错误处理

- 找不到 slot 映射：显示框架日志，返回 `Result.Cancelled`。
- 找不到 feature：显示框架日志，返回 `Result.Cancelled`。
- feature 被禁用或隐藏：沿用 `FeatureExecutionGate`，返回 `Result.Cancelled`。
- 找不到 command assembly：显示错误日志，返回 `Result.Failed`。
- 找不到 command type 或类型未实现 `IExternalCommand`：显示错误日志，返回 `Result.Failed`。
- 业务命令抛异常：记录异常消息，返回 `Result.Failed`。
- 可见功能数量超过 slot 上限：超出部分不创建按钮，并记录 warning 诊断。

## 与清单驱动的关系

第一阶段可以保留现有 `ModuleDiscoveryService` 行为，以降低改动风险。

第二阶段应把 `package.json` 作为功能声明权威来源，启动发现不再必须加载业务 DLL 调用 `IPlugHubModule.Describe()`。`Describe()` 可以降级为可选运行时校验或诊断能力。

这个顺序很重要：先收口 Ribbon 命令入口，再减少发现阶段加载。否则即使启动阶段不加载 DLL，Ribbon 直连业务命令仍会破坏隔离边界。

## 与 shadow copy 的关系

shadow copy 不应散落在 Ribbon 构建器或业务命令里，而应只存在于 `ICommandAssemblyLoader` 的 net48 实现中。

后续 shadow copy 规则：

- 缓存目录使用内容 hash，路径形如 `runtime-cache/<package-id>/<hash>/`。
- 复制主 DLL、依赖 DLL、native DLL、资源文件和业务命令需要的相对文件。
- 从缓存路径加载业务命令。
- 被锁定的旧缓存不强删，记录 pending cleanup，下次启动清理。
- 已加载旧版本的功能不承诺当前 Revit 会话内完全替换，必要时提示重启生效。

## 与 Revit 2025+ ALC 的关系

未来 Revit 2025+ 可新增 `Net8AlcCommandAssemblyLoader`，但必须遵守共享程序集规则：

- `RevitAPI.dll` 和 `RevitAPIUI.dll` 不应在插件 ALC 中重复加载。
- `PlugHub.Contracts.dll` 不应在插件 ALC 中重复加载，否则接口类型身份会不一致。
- 业务插件依赖可以通过 `AssemblyDependencyResolver` 从插件目录解析。
- ALC 卸载必须清理静态引用、事件订阅、后台线程、WPF 窗口和委托缓存。

## 测试与验证

静态验证应新增或调整以下检查：

- `FeatureRibbonBuilder` 不再返回外部业务 `commandAssembly` 作为 Ribbon 按钮目标。
- 所有业务 feature 按钮目标类型都来自 `PlugHub.Revit2020` 的 slot 命令。
- `FrameworkFeatureCommand` 或新的 dispatcher 仍能根据 feature id 执行业务命令。
- `ICommandAssemblyLoader` 的 net48 实现只在点击执行路径加载业务 DLL。
- slot 数量不足时产生诊断。
- 现有命令回退行为保持不变。

修改后至少运行：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

## 分阶段实施

### 阶段 1：命令入口收口

- 新增 slot 命令类型和 slot registry。
- 新增 feature command dispatcher。
- 修改 Ribbon 构建逻辑，使业务 feature 按钮统一绑定框架 slot 命令。
- 保留现有 `Assembly.LoadFrom` 加载策略，但迁移到加载器接口后只在点击时发生。
- 更新静态验证。

### 阶段 2：清单权威化

- 减少启动发现阶段对业务 DLL 的强制加载。
- 让清单足以驱动 Ribbon、设置页和诊断。
- 将 `IPlugHubModule.Describe()` 降级为可选校验。

### 阶段 3：net48 shadow copy

- 新增 hash 缓存目录。
- 加载缓存副本而不是安装目录源 DLL。
- 增加缓存清理和重启提示状态。

### 阶段 4：Revit 2025+ ALC

- 在独立目标框架和 Revit 版本适配层中实现 ALC 加载器。
- 复用 dispatcher 和清单模型。
- 增加 ALC 卸载成功/失败诊断。

## 验收标准

第一阶段完成时应满足：

- Revit Ribbon 不再直接绑定业务插件 DLL。
- 业务功能点击统一进入框架调度器。
- 当前 Revit 2020 静态验证通过。
- 未改变仓库安装、Gitee 默认源和 release 流程。
- 文档明确说明 Revit 2020 不承诺真正热重载。
- 后续 shadow copy 和 ALC 能通过加载器接口接入，而不需要再次改 Ribbon 按钮入口。
