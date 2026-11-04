---
title: 插件包开发迁移指南与旧机制核查
proposalUuid: 91981d6f-8fb8-4097-8ca8-e24b89432488
documentUuid:
---

# 插件包开发迁移指南与旧机制核查

## 交付状态与接收方

2026-11-04：用户已确认迁移规则；框架提案尚待批准，下面的新参数和删除项是目标契约，当前源码尚未实现。此文交给 PlugHub_Packages 开发 agent，先清点并准备迁移，待框架新 Contracts 和实现可用后再重编译、验证及协调发布。不要直接改正在使用的安装包，也不要假设新功能已发布。

框架唯一工作区与成果目录为 `/home/yilan/codex-project/PlugHub`；插件包源码只读核查位置 `/home/yilan/codex-project/PlugHub_Packages`。本轮未修改包仓库。2026-11-04 按用户指示将本次方案迁回框架工作区并修改 AGENTS.md；该目录原有未提交源码变更保留，不属于本提案的实现。插件包 agent 后续从本指南的新位置读取框架契约和交接内容。

## 必须迁移的旧状态

| 旧声明/消费 | 插件包与框架的迁移动作 |
| --- | --- |
| ModuleDescriptor.State / ModuleState（含 Failed） | 包源码删除 State 赋值及对枚举的引用；失败转诊断，不另造模块启停字段 |
| FeatureDescriptor.DefaultState / FeatureState | 包源码删除 DefaultState 赋值和枚举引用；功能由安装、注册与布局规则提供 |
| modules[].enabled / visible、features[].defaultState | 新清单、生成器及模板停止输出；框架通用 JSON 读取忽略旧键，不依据旧值跳过已安装功能；正常写回不再输出 |
| preset.moduleOverrides[].enabled / visible | 删除状态覆盖；保留 moduleId/order 的排序用途；不删除整个预设机制 |
| Manager FeatureRow.Visible、注册/组合/执行状态判断 | 框架负责删除该链，包不得增加替代开关绕回旧机制；保留窗口/控件自身 Visibility |

按用户选择，旧配置中隐藏/禁用的已安装功能会恢复正常资格，不逐项询问迁移；未声明自启的包仍不初始化。保留用户配置回退副本，不自动批量重写所有文件。旧 DLL 若引用已删除成员存在加载失败风险，必须重新编译，不用保留空壳属性或旧枚举掩盖问题。

已确认 9 个模块均实现 IPlugHubModule，Describe 中有 `State = ModuleState.Enabled` 和 `DefaultState = FeatureState.Visible`，Initialize/Shutdown 为空：

- PlugHub.GridVisibility、PlugHub.LevelVisibility、PlugHub.ReferencePlaneVisibility。
- PlugHub.DuctPreferredJunction、PlugHub.FamilyMaterialParameters、PlugHub.FamilyFileSaver。
- PlugHub.HubeiReportParameters、PlugHub.ProjectAutoSave、PlugHub.MepTypeFilterVisibility。

对应源码在 `src/<模块名>/<模块名去掉 PlugHub. 前缀>Module.cs`，以实际 rg 结果为准。删除上述两行状态赋值后保留 Describe 里的功能 ID、名称、分组、排序、commandKey/命令映射；不要为这次迁移改业务行为或图标。检索其余生成器、测试、模板和随包 Contracts，防止仅修改 9 个类却发出旧产物。

## 新启动声明用法（待框架实现后）

在要自启的 `modules[]` 记录声明 `autoStart: true` 和 `startupType`，使用该记录的 `assembly`。入口为 DLL 中实现 Revit IExternalApplication 的类型，公开可无参构造；OnStartup 只注册必要事件/资源，OnShutdown 成对释放，失败前已建立的注册必须撤销。无需求则两个字段均省略，不默认给现有包打开自启。

```json
{
  "id": "Example.EventPackage",
  "version": "1.0.0",
  "assembly": "dist/Example.EventPackage.dll",
  "autoStart": true,
  "startupType": "Example.EventPackage.Startup",
  "features": []
}
```

这是模块记录片段，应放入现有 schemaVersion/modules 外壳。框架不会把旧 `type`、IPlugHubModule.Initialize 或 commandType 当成启动入口。初始化在 Revit 主线程和框架 OnStartup 返回前执行，不能假设有活动文档，不自行通过 Task.Run 调用 Revit API；不要给包再注册独立 .addin 造成双重启动。生命周期官方示例来自 2021 邻近版本 [1](ref:4519a163-8438-4d93-83b1-c43339e4eabc)，2020 的 ApplicationInitialized 是更晚阶段 [2](ref:e6216302-f749-46fb-9765-889b85f97285)。

失败会记录并继续，按钮不会自动重试初始化。成功实例在关闭时逆序清理。刷新不会重新初始化，安装/更新/卸载已自启包需重启完成切换；net48 不提供本方案所需的独立程序集热卸载保证。包入口必须足够短，框架无法安全强杀挂住的主线程。

## 其他旧机制核查

核查范围为模块声明、包发现/安装/启动、按钮执行、预设与 Manager 的相关链路，不是全仓历史代码删除审计。以下区分确认消费者和缺失行为，不能把“legacy”字样当作删除依据。

| 机制 | 当前源码证据 | 本轮处置与包开发动作 |
| --- | --- | --- |
| modules[].type / IPlugHubModule.Describe、Initialize/Shutdown | 元数据发现不加载 Describe；IModuleContext 仅诊断，当前包生命周期为空；writer 不写 type | 不复用为 startupType、不承诺旧 Initialize 会被调用；本次保留描述接口的非状态部分，不强制重写包架构 |
| dependsOn | ConfigurationModels 和配置克隆保留，无运行调度消费者 | 记录为不生效元数据；包不得依赖它决定启动顺序；移除全接口/新增调度均不纳入此次自动清理 |
| conflictPolicy.missingModuleType | 配置对象和克隆保留，清单权威发现没有执行该策略 | 不用它吞掉 startupType 错误；新自启有独立明确诊断；记录后续清理候选，不连带删除其他冲突检查 |
| commandKey | FeatureExecutionGate 及旧 journal 路由仍消费；新稳定槽位路径按 feature ID | 保留当前必要消费者与诊断；迁移不得删功能 ID/commandType 或随意更改 commandKey |
| feature-combinations / presets | FrameworkConfigurationLoader.ApplyPreset 仍应用模块排序及视图选择 | 只删除状态覆盖；保留排序、视图和组合规则 |
| moduleSources | ModuleSourceResolver 及 Settings 仍处理 enabled localFolder | 保留可用本地来源与其 enabled；不同于模块启停，不迁移成插件自启开关 |
| frameworkVersionRange | 明确保留元数据，ModuleDiscoveryService 不求值 | 不宣称可以阻止不兼容 Contracts 装载；通过协调框架与包版本发布，本次不新增版本解释器 |
| revitVersions | ModuleDiscoveryService 的真实 2020 过滤 | 保留，启动候选使用相同有效过滤 |
| repositories.enabled / visibility、WPF Visibility、安装状态、运行时快照 | 来源开关/权限语义、界面控件、安装和运行时均有当前消费者 | 保留，禁止全局按名字清除 State/Visible/Enabled |
| runtime-cache、待执行安装/清理、稳定 feature ID、包相对路径 | 命令影子复制与包操作仍使用 | 保留并复用；不将缓存目录当成已安装包，不直接从源 DLL 初始化 |

## 插件包开发 agent 的验收与交付

1. 用实际源码清点上述状态成员、模板/清单/测试/随包 Contracts，回报额外旧机制的证据及处置建议；未明确授权的额外删除单列，不顺带改业务。
2. 新框架产物可用后删除状态成员引用，用新 Contracts、net48/Revit 2020 参考重新编译所有受影响包；验证完整 dist 依赖、相对路径和 commandType，并跑包仓库现有校验。
3. 检查未声明包不会自启；如某包确有事件注册需求，明确选定 startupType 并做成对清理，不因包名称默认启用。
4. 将编译/清单检查与 Revit 实机检查分开记录；实机需验证启动时机、无文档启动、单包失败不影响其他包、刷新不重复注册、关闭注销和重启切换。当前 Linux 环境只能记录静态结果。
5. 回报修改模块列表、框架/Contracts 目标版本、构建/校验命令和结果，以及未测项；匹配版本联调通过前不发布新旧混装产物。回退同时恢复框架、包 DLL 和清单备份，不只回滚 JSON。

本轮已通过框架基线命令：`dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj`，结果 `passed: modules=0, features=0, views=1, presets=0`。它不是新功能或包迁移完成的证据。框架实现后的集成任务负责更新本指南、验证参数往返并登记包 agent 的回报与未完成项。
