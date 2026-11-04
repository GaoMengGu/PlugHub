---
title: 插件包启动生命周期与状态清理设计
proposalUuid: 91981d6f-8fb8-4097-8ca8-e24b89432488
documentUuid: 73138c6f-e8e3-417c-8d88-d517b38ad4bd
---

# 插件包启动生命周期与状态清理设计

框架唯一工作区为 `/home/yilan/codex-project/PlugHub`，本提案所有框架源码、配置、文档、测试和本地方案均在该目录维护，保留已有未提交改动。

## 最小契约

提案已批准；以下契约已落地并通过本机 C# 回归和适配器编译，启动任务已独立审查并验收；尚未发布或完成 Revit 实机验证。沿用 `modules[]` 的模块 ID 作为包声明单元，不新增顶层包模型。同一清单可包含多个模块，每个模块独立选择是否自启；一个模块只声明一个入口。

```json
{
  "schemaVersion": "1.0",
  "modules": [{
    "id": "Example.EventPackage",
    "version": "1.0.0",
    "assembly": "dist/Example.EventPackage.dll",
    "autoStart": true,
    "startupType": "Example.EventPackage.Startup",
    "features": []
  }]
}
```

新增 `ModuleConfiguration.AutoStart`（bool，默认 false）、`StartupType`（string）。`autoStart` 只接受 JSON boolean，`startupType` 是同记录 assembly 内实现 `Autodesk.Revit.UI.IExternalApplication` 的公开、非抽象且可无参构造的类型全名。true 缺少有效 assembly/startupType 为该包自启失败，不默认为其他入口；false 即使带有类型也不实例化。用于旧 Describe 的 `type` 不回退成启动类型，`commandType` 仍是按钮命令类型。

直接复用 Revit 已有 `IExternalApplication.OnStartup(UIControlledApplication)` / `OnShutdown(UIControlledApplication)`，不向 Contracts/Framework 引入 Autodesk 依赖，也不向 `IModuleContext` 塞入 object 或 Revit 上下文。现有 `IPlugHubModule.Initialize/Shutdown` 不作为新启动入口自动调用；这次只移除其描述对象中的状态成员，不顺带重建整个模块描述契约。

## 调用点和生命周期

`ExternalApplicationEntry.OnStartup` 先经现有 FrameworkRuntime.Load 完成来源合并、发现/兼容与 ID 校验，建立框架快照；再以 `FrameworkRuntimeSnapshot.StartupModules` 中的合格模块配置建立启动候选，在 Ribbon 构建之前同步初始化，最后构建 Ribbon 返回。候选不能取 Composition.Features，否则无按钮/被布局筛选的包会漏掉。`ModuleDiscoveryResult.Configurations` 与描述对象保持对应，`FeatureRegistry.Register` 返回注册接受结果，重复模块 ID 沿用同一 first-wins 结果，不让冲突记录绕过校验启动。

在 Revit2020 适配层以 `PackageStartupSession` 会话级生命周期管理对象持有尝试过的模块 ID 和成功实例。顺序沿用 Order，然后 ID 的不区分大小写排序；这不是依赖调度。进入加载前记录已尝试，关闭后不支持同进程重新启动一套包。FrameworkRuntime.Load 只负责元数据，不在刷新时调用启动入口。新安装、更新或修改参数须重启 Revit 生效。

逐包执行现有相对路径解析 → 影子复制 → 取得类型及构造 → 调用 OnStartup。共享 `Net48ShadowCopyCommandAssemblyLoader` 已有 payload 哈希、复制、缓存清理和 Assembly.LoadFrom 路径，只提取一处内部 `LoadAssembly` 步骤，命令加载与自启加载共同使用，不复制整套实现，不直接锁源文件。程序集身份沿用现有 net48 规则，不承诺同会话热替换。连续刷新或点击不得另建启动管理器。

所有入口在 Revit 主线程调用；OnStartup 返回 Result.Succeeded 才进入成功列表。记录 ID、阶段、耗时、结果，异常消息沿用现有敏感内容脱敏和日志。缺文件/缺类型/不实现接口/无法构造/抛异常/非成功返回均隔离；候选后的其他包继续，框架不会因为单包失败返回失败。异常不改变旧功能状态，不阻止其他已注册功能，也不把点击当成重试入口。

若已进入包 OnStartup 后失败，马上尝试一次同实例 OnShutdown，记录清理结果并不进入成功列表；实例构造前失败没有实例可清理。包作者应在自身入口失败时撤销已做的注册，并使 OnShutdown 对未完全初始化安全。框架 OnShutdown 对成功列表逆序调用并隔离异常/非成功，释放会话持有引用。若后续框架 Ribbon 构建失败，也先尽力清理已成功的实例，再按原框架启动失败策略返回，避免遗留事件。

2021 官方外部应用示例支持成对注册/注销这一设计 [1](ref:4519a163-8438-4d93-83b1-c43339e4eabc)；2020 ApplicationInitialized 时机说明决定初始化不能延后到该事件 [2](ref:e6216302-f749-46fb-9765-889b85f97285)。不推断启动时已有文档，较新 API 参考不能替代 2020 验证 [3](ref:ac6366b1-312d-48dc-88de-ab9ef7c460ff)。

## 配置传递与状态清理范围

| 表面 | 必须修改或核查的点 |
| --- | --- |
| ConfigurationModels / PackageManifestDefaults | 新字段缺省；删除模块 Enabled/Visible、功能 DefaultState、预设 Enabled/Visible 覆盖及 NormalizeModuleState 中的状态补丁，保留名称、命令目标等有效默认值 |
| FrameworkConfigurationLoader / ModuleSourceResolver | ApplyPreset 与配置克隆传递新字段，只保留排序覆盖；合并来源不丢参数；旧字段读入为未知 JSON，不保留 DTO 兼容桥 |
| PackageInstallService / PackageManifestWriter | 安装、更新、序列化及下载索引→安装清单往返保留 autoStart/startupType；绝不能重复当前 writer 丢弃 type 的路径 |
| Contracts | 删除 ModuleDescriptor.State、ModuleState、FeatureDescriptor.DefaultState、FeatureState；故障使用 DiagnosticMessage，仍保留合法描述/命令字段 |
| Discovery / FeatureRegistry / FeatureViewComposer / FeatureExecutionGate | 删除旧状态映射和状态门禁；保留兼容、重复、注册、命令类型和工作区组成校验；自启资格独立于工作区可见性 |
| Manager / SettingsConfigurationStore | 删除 FeatureRow.Visible、状态赋值、筛选/保存及关联绑定；保留 WPF Visibility、来源启停和布局排布；Manager 保存不能丢新启动声明 |
| Schema / config 样例 / 模板 / 测试夹具 / 包校验工具 | 不再要求/输出旧状态字段；新增 boolean 和 true 时入口要求；支持未带新字段的旧清单；核查构建/发布携带样例的路径 |
| Tests / StaticValidation / 文档及本地技能 | 替换断言旧状态生效的测试，增加旧值无效、无状态写回、新字段往返回归；更新 AGENTS/开发与包模板中的过时产品能力描述，保持中英文工具说明一致 |

不要全局替换 Enabled/Visible/State 字符串。来源 enabled、仓库 visibility、WPF 可见性、安装状态、FrameworkRuntimeState 和日志严重程度均不属于本次删除对象。

## 验证与发布接口

三个任务顺序执行：T1 清理状态及回归 → T2 清单和启动生命周期 → T3 集成检查与包迁移交接更新。T1 可独立通过纯 C# 测试，T2 使用可注入的加载/调用边界与小型假入口验证真正调用链，不能只匹配源码字符串。

核心回归矩阵：缺省/false/true；旧隐藏/禁用组合；true 但无文件/类型错误/异常/非成功；两个成功包及中间失败；布局无呈现；重复 ID/不兼容；刷新后不重试；关闭逆序与清理异常；安装/设置保存后声明不丢；预设只改排序。至少运行项目要求的静态命令和相关 Tests。适配器编译采用已有 Revit 参考方式；本机没有 Revit，不编造实机会话结果。

源包读取权限、路径和装载失败均沿用原有校验；不扩大信任边界。发布时给新 Contracts 和包记录一致的目标版本，重新编译引用已删除成员的所有包。先在发布区准备完整框架/包与旧版本回退副本，再安装匹配版本；本提案不授权发布、推送或覆盖用户部署。实机验收记录留待 Windows/Revit 2020：启动返回前已注册、无文档安全、单包失败继续、布局独立、关闭注销及重启切换。

## 已落地验证接口

- `PackageManifestDefaults.ReadModules` 在 JSON 转换前检查 autoStart 的 boolean 类型和 startupType 的 string 类型；非法值按模块放入 `[ScriptIgnore] StartupConfigurationError`，合格相邻模块仍可发现，启动管理器在 configuration 阶段诊断后跳过。
- 清单缺省/false 正常写回时可省略 autoStart，保持 false 语义；startupType 按可选字符串保存，不作为 false 包的加载依据。
- `LegacyStateMigrationValidator` 与 `PackageStartupConfigurationValidator` 由规定的 StaticValidation 入口实际执行，覆盖旧状态迁移、配置/来源/资格和安装保存链。
- `PlugHub.RevitStartup.Tests` 链接生产 PackageStartupSession、ExternalApplicationEntry、CommandAssemblyLoader，以小型 Revit 接口替身检查真实调度与资源释放；输出隔离在 `.tmp/revit-startup-tests/`。它与真实 API 引用编译分别记录，均不替代实机。
