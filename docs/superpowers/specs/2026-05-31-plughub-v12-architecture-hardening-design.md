# PlugHub V1.2 Architecture Hardening 设计

日期：2026-05-31

## 背景

PlugHub 当前主线仍以 Revit 2020 / .NET Framework 4.8 为可运行目标。V1.1.0 已完成仓库插件包展示、更新状态和运行时路由的关键修正：Revit Ribbon 绑定稳定框架 slot 命令，业务命令在点击时由 `FeatureCommandDispatcher` 通过 net48 shadow-copy 加载器执行。

这个方向不需要推翻。下一阶段的主要问题不是功能模型错误，而是维护边界、安全边界和诊断边界已经开始变重：

- `FrameworkSettingsWindow` 同时负责 WPF 组装、行模型、配置读写、仓库操作、状态计算、拖拽和错误展示。
- `PackageRepositoryService` 同时负责仓库同步、包清单解析、安装复制、待处理操作、文件锁检测和状态刷新。
- `PlugHub.StaticValidation` 仍是必要验收闸门，但文件偏大，包含大量字符串断言。
- `System.Web.Script.Serialization` 仍在 Framework 和 Revit 适配层中使用，阻塞未来 `PlugHub.Framework` 多目标化。
- 私有仓库 `apiKey` 仍以明文字段保存，并在 Git 远程 URL 中临时拼接。
- 插件业务命令 `Execute` 缺少统一异常兜底，单个插件异常可能影响 Revit 会话稳定性。

V1.2 的定位是架构整理和稳定性补强，不引入 Revit 2025+ 适配，也不承诺 Revit 2020 的真正热重载或插件沙箱。

## 目标

V1.2 聚焦以下目标：

- 降低核心文件复杂度，让设置 UI、仓库访问、包安装、待处理操作和清单解析可以独立理解与测试。
- 改善本地构建体验，避免普通 build 必然复制到可能被 Revit 占用的 `dist\Revit2020`。
- 增强静态验证，把包清单合法性、路径引用、兼容字段和结构化输出纳入验证边界。
- 提升插件包操作可解释性，让用户能看到并取消待处理更新或卸载。
- 加固私有仓库凭据、日志脱敏和业务命令异常兜底。
- 为未来 Revit 多版本、Revit 2025+ ALC 和企业部署保留清晰接口，但不在本阶段扩大实现面。

## 非目标

V1.2 不做以下内容：

- 不在 Revit 2020 中引入 `AssemblyLoadContext`。
- 不把 AppDomain 隔离作为插件沙箱方案。
- 不实现强制 Revit API 权限隔离。框架可以声明、提示、校验和禁用插件，但不能可靠阻止同进程 `IExternalCommand` 修改模型。
- 不实现 Revit 2025+ 可运行适配层。
- 不实现框架自动更新。
- 不支持 FTP/SFTP、私有 NuGet 源或企业批量部署。
- 不把设置页改成 DockablePane；当前仍保持 WPF Window。

## 推荐方案

采用“维护性重构优先，安全和诊断同步补强”的方案。

阶段顺序：

1. 先整理构建和验证边界，降低后续改动的回归成本。
2. 再拆分仓库和包生命周期服务，保留当前 `PackageRepositoryService` public facade，减少调用方改动。
3. 再拆分设置窗口，让 WPF Window 只负责 UI 事件和布局，业务状态转移移到可测试类。
4. 同步补上凭据保护、待处理操作管理、异常兜底和日志导出。
5. 最后更新文档和示例，为插件作者提供稳定接入入口。

这个方案比直接做 Revit 2025+ ALC 更稳，因为 ALC 的前置条件之一是 Framework 多目标化，而当前 JSON、UI 和包仓库边界还没有足够干净。V1.2 完成后，再做 Revit 2025+ 适配会更可控。

## 构建与发布边界

### Stage 输出开关

`PlugHub.Revit2020.csproj` 当前在每次 build 后执行 `StagePlugHubOutput`，复制 DLL、配置和 `.addin` 到 `PlugHubOutputDir`。V1.2 应增加显式开关：

- `StagePlugHubOutput` 默认值保持 `true`，不破坏现有脚本和 release workflow。
- 支持 `/p:StagePlugHubOutput=false`，用于日常编译和静态验证，避免 Revit 正在运行时锁住 `dist\Revit2020` 导致 build 失败。
- `scripts/build-revit2020.ps1` 继续传入输出目录并保持 stage 行为。

### Clean 能力

新增 PowerShell clean 脚本或 `build-revit2020.ps1 -Clean` 参数：

- 清理 `dist\Revit2020`、相关 `bin`、`obj`。
- 默认只清理仓库内路径，必须校验目标路径在仓库根目录下。
- 不删除用户 Revit Addins 目录中的 `.addin`，除非使用明确的 `-CleanAddin` 或单独 uninstall 参数。

### 安装回滚

`install-addin.ps1` 和 `build-revit2020.ps1 -InstallAddin` 应支持基本回滚：

- 覆盖 `C:\ProgramData\Autodesk\Revit\Addins\2020\PlugHub.addin` 前备份旧文件。
- 复制失败时恢复备份。
- 输出目标路径、备份路径和失败原因。

## 静态验证边界

### 模块化验证器

保留 `PlugHub.StaticValidation` 作为唯一静态验证入口，但拆成多个验证模块：

- `RepositoryStructureValidation`
- `BuildConfigurationValidation`
- `RuntimeRoutingValidation`
- `PackageRepositoryValidation`
- `SettingsUiValidation`
- `PackageManifestValidation`
- `SecurityValidation`

主程序只负责编排、汇总结果和设置退出码。这样可以减少单文件复杂度，并降低字符串断言集中堆积的维护成本。

### 包清单验证

新增包清单验证能力：

- 校验 `package.json` 和 `*.package.json` 的基本格式。
- 基于 `config/schemas` 中的 schema 检查必填字段、字段类型、枚举值。
- 校验 `assembly`、`commandAssembly`、`iconPath` 等相对路径是否能在包目录内解析。
- 校验 module id、feature id、group/order 的重复和冲突。
- 校验新增兼容字段：`revitVersions`、`frameworkVersionRange`。

schema 应拆分为专用 `package.schema.json`，避免继续把运行时 sources 配置和插件包清单混在一个 schema 中。

### 结构化报告

静态验证输出保持控制台摘要，同时增加可选参数：

- `--report-json <path>` 输出机器可读结果。
- `--report-html <path>` 输出本地可浏览报告。

结果至少包含：

- severity：Error、Warning、Info
- code：稳定错误码
- file：相关文件
- message：问题说明
- suggestion：修复建议

默认验证命令仍保持：

```powershell
dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj
```

## 包生命周期与仓库服务

### 服务拆分

保留 `PackageRepositoryService` 作为兼容 facade，内部拆分：

- `RepositoryBrowser`：同步仓库缓存、sparse checkout、读取仓库中的包清单。
- `PackageManifestReader`：读取、规范化和验证包清单。
- `PackageInstallService`：安装、更新、卸载和回滚安装文件。
- `PendingPackageOperationStore`：读取、写入、取消和应用待处理操作。
- `RepositoryCredentialService`：处理凭据加密、解密和脱敏展示。

设置页和运行时仍通过 facade 调用，避免一次性改动过大。

### 待处理操作管理

当前待处理操作已经存在，但用户只能从状态文本间接感知。V1.2 应增加：

- 设置页仓库区域展示待处理操作列表。
- 用户可以取消尚未应用的 update/delete/restart 操作。
- 取消 update 时清理 staging 目录。
- 取消 delete 时保留当前安装目录，并刷新包状态。
- 启动应用待处理操作失败时继续保留操作，并写入 warning 诊断。

交互方式优先使用设置页内联状态和列表，不恢复频繁弹窗。只有 destructive 或不可逆操作需要明确确认。

### 版本与兼容字段

包描述增加兼容字段，但 V1.2 只做校验和拦截，不做复杂版本管理：

- `revitVersions`: 允许的 Revit 主版本列表，例如 `["2020"]`。
- `frameworkVersionRange`: 插件要求的 PlugHub 框架版本范围。
- `version`: 继续表示插件包版本。

安装或启动发现时：

- 不兼容当前 Revit 版本的插件不加载，并输出 warning。
- 不兼容当前 Framework 版本的插件不加载，并输出 warning。
- 兼容字段缺失时按当前行为兼容处理，但静态验证给出 Info 或 Warning。

版本回滚和版本锁定暂不实现，只在模型中预留扩展点。

## 设置 UI 边界

### Window 瘦身

`FrameworkSettingsWindow` 应拆成以下职责：

- Window：布局、事件绑定、对话框生命周期。
- ViewModel：当前行集合、选择状态、命令状态、状态文本。
- `SettingsConfigurationStore`：读取和写回 sources/views/feature-combinations/package manifests。
- `RepositorySettingsController`：浏览仓库、安装、更新、卸载、取消待处理操作。
- Row models：功能、分组、仓库、包、日志行独立文件。

拆分后，Window 不直接拼装包生命周期规则，不直接读写 pending operations。

### 信息架构

保留现有 tab 思路，但名称和职责应更明确：

- 功能：功能显示、隐藏、分组、按钮大小、图标、排序。
- 分组：Ribbon panel 显示名和顺序。
- 仓库：仓库配置、浏览、安装、更新、卸载、待处理操作。
- 日志：运行时诊断、操作日志、导出日志。

暂不新增“插件管理”独立 tab，避免与仓库 tab 和功能 tab 重叠。待包管理能力继续变大时再拆。

## 安全边界

### apiKey 加密

私有仓库 `apiKey` 不应继续明文写入配置。V1.2 使用 Windows DPAPI：

- 当前用户范围加密，适配单机个人使用。
- 配置文件中保存加密值和标记字段。
- 设置页编辑时只显示占位符，不回显明文。
- 用户输入新 token 时覆盖旧密文。
- 读取失败时提示重新输入，不阻塞公开仓库。

为兼容旧配置：

- 发现旧明文 `apiKey` 时仍可使用。
- 保存配置时自动迁移为加密字段。

### 脱敏

所有日志和诊断输出必须脱敏：

- Git URL 中的 token。
- `apiKey` 字段值。
- 未来可能出现的签名密码或凭据参数。

本阶段不强制脱敏用户本地路径，因为路径对诊断仍有价值；但导出日志时可提供“脱敏本地路径”选项作为后续扩展。

### 包签名与哈希

V1.2 只做设计预留，不强制验签：

- package schema 预留 `sha256` 和 `signature` 字段。
- 静态验证可以检查字段格式。
- release 签名继续使用现有 PlugHub 主框架签名流程。

插件包强制签名、签名信任策略和企业证书分发放到后续版本。

## 稳定性边界

### 插件命令异常兜底

`FeatureCommandDispatcher` 应捕获业务 `IExternalCommand.Execute` 抛出的异常：

- 记录 module id、feature id、command type、异常类型和堆栈。
- 返回 `Result.Failed`，并给出可读错误。
- 不吞掉 Revit transaction 语义；业务命令返回值仍按原样传递。
- 避免重复弹出大量窗口，优先显示简短错误并提供查看日志入口。

这不是沙箱，不能阻止插件主动崩溃进程或调用危险 API，但能覆盖普通异常导致的未处理崩溃。

### 坏插件禁用

增加可选的本地禁用记录：

- 插件加载或执行连续失败达到阈值后，建议用户禁用。
- 用户确认后写入本地 override，使该模块不再参与发现或执行。
- 记录禁用原因和时间。

V1.2 只实现手动禁用和原因记录；自动禁用作为后续扩展。

### 健康检查

增加轻量健康检查，运行于设置页或静态验证，不在启动时做重操作：

- 核心 DLL 是否存在。
- 配置文件是否存在且可解析。
- `packages`、`repository-cache`、`runtime-cache` 是否可读写。
- 待处理操作文件是否可解析。
- Revit API 引用模式是否配置合理。

健康检查输出进入日志 tab，并支持导出。

## 日志与诊断

### 统一日志模型

在现有 `DiagnosticMessage` 基础上扩展运行时日志模型：

- timestamp
- severity
- code
- moduleId
- featureId
- operation
- message
- exception

框架日志、插件执行日志、用户操作日志使用同一模型，靠 `operation` 和 `moduleId` 过滤。

### 文件日志与导出

V1.2 增加简单文件日志：

- 默认写入 `logs\plughub-YYYYMMDD.log`。
- 支持最大文件数量或按天清理。
- 设置页提供“导出日志”，打包当前日志、配置摘要和诊断报告。

日志设置页只提供必要选项：

- 日志级别：Info、Warning、Error。
- 打开日志目录。
- 导出日志。

Debug 级别暂不作为默认 UI 选项，避免普通用户误开过多日志。

## 文档与示例

V1.2 应更新：

- `docs/architecture.md`：补充 V1.2 边界、待处理操作、凭据保护、异常兜底。
- `docs/development.md`：补充 stage 开关、clean、静态验证报告、包 schema。
- `docs/project-overview.md`：更新 V1.1.0 之后的状态，去掉过期日期。
- 新增插件开发文档：package 字段、命令开发约束、Revit 2020 限制、日志与调试。

新增 HelloWorld 示例插件应放在独立示例目录或独立包仓库中，不把业务逻辑放回 PlugHub 框架层。主仓库可只放最小示例源码和说明，确保不被运行时默认加载。

## 后续阶段

### V1.3 候选

- 本地文件夹仓库源。
- 版本回滚和版本锁定。
- 插件包哈希校验。
- HelloWorld 示例插件完善。
- 静默安装参数。
- 更完整的健康检查报告。

### V1.4 或企业部署候选

- 私有 NuGet 源。
- SFTP 或企业制品库。
- 批量部署模板。
- 插件包签名强校验。
- 框架自动更新。

### Revit 2025+ 候选

- 替换 `System.Web.Script.Serialization`。
- `PlugHub.Framework` 多目标化。
- 独立 Revit 2025+ net8 适配层。
- `Net8AlcCommandAssemblyLoader`。
- ALC 卸载诊断和静态引用清理规则。

## 验收标准

V1.2 实现完成后，至少满足：

- `dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj` 通过。
- 支持 `/p:StagePlugHubOutput=false` 的构建路径。
- 设置页可以显示并取消待处理包操作。
- 私有仓库凭据不再以明文写回配置。
- 业务插件 `Execute` 抛出普通异常时，PlugHub 返回失败并记录日志，不出现未处理异常。
- 静态验证可输出 JSON 报告。
- 文档明确说明 Revit 2020 不承诺 ALC、AppDomain 沙箱或强权限隔离。

## 风险与约束

- DPAPI 加密值默认只能由同一 Windows 用户解密；导入配置到其他机器后需要重新输入 token。
- 插件执行异常兜底不能防止插件主动调用 `Environment.FailFast`、创建失控线程或直接破坏 Revit 状态。
- package schema 收紧后可能暴露旧包格式问题，需要提供兼容期和清晰提示。
- 文件日志要控制大小，避免长时间 Revit 会话写出过大日志。
- 拆分大类时应保持 public facade，避免一次性改动牵连 Ribbon、设置页和静态验证。
