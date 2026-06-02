# 开发与验证

## 本地环境

项目目标运行环境是 Windows + Revit 2020 + .NET Framework 4.8。当前仓库可在非 Revit 环境中做静态验证，但不能声明 Revit 实机加载成功。

关键路径：

- 解决方案：`PlugHub.sln`
- Revit 入口：`src\PlugHub.Revit2020\ExternalApplicationEntry.cs`
- 设置入口：`src\PlugHub.Revit2020\FrameworkSettingsCommand.cs`
- 静态验证：`src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj`
- 构建脚本：`scripts\build-revit2020.ps1`
- 输出目录：`dist\Revit2020`

## 必跑静态验证

每次修改源码、配置、文档结构、构建脚本或验证规则后，至少运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

验证器覆盖：

- 必需项目文件、配置文件和内部文档入口。
- `tests` 目录只在存在真实测试项目时保留；当前自动验证入口是 `src\PlugHub.StaticValidation`。
- Contracts / Framework 不引用 Revit API。
- 默认配置不包含业务模块或业务功能。
- workspace 在没有外部模块时不暴露业务按钮。
- Revit 适配层包含 Ribbon、WPF 设置、默认图标和命令路由。
- `dist\Revit2020` 不包含已删除的样例模块或旧内置模块产物。

## Revit 2020 构建

本地构建默认使用 `Installed` 引用模式，从本机 Revit 安装目录读取 `RevitAPI.dll` 和 `RevitAPIUI.dll`：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

脚本行为：

- 编译 `PlugHub.Revit2020` 及其依赖。
- 复制 DLL、运行时配置、运行时插件包投放目录和 `.addin` 到 `dist\Revit2020`。
- 默认将 `.addin` 中的 Assembly 写成本机 `PlugHub.Revit2020.dll` 绝对路径；release workflow 使用相对路径 `PlugHub.Revit2020.dll`。
- 构建前清理旧样例模块、旧内置模块 DLL/PDB 和旧 `modules` 投放目录残留。

CI 发布构建使用 `NuGet` 引用模式，只把 Revit API 当作编译引用：

```powershell
.\scripts\build-revit2020.ps1 -UseRevitApiNuGet
```

该模式不会把 `RevitAPI.dll` 或 `RevitAPIUI.dll` 放入仓库，也不需要仓库 secret。当前使用 `Autodesk.Revit.SDK` NuGet 包是为了 CI 编译便利；本地和实机验收仍以真实 Revit 安装目录为准。

如需安装 addin manifest：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020" -InstallAddin
```

安装位置：

```text
%APPDATA%\Autodesk\Revit\Addins\2020\PlugHub.addin
```

如果复制到 `dist\Revit2020` 失败，优先检查 Revit 是否正在占用 DLL。

## Release 安装程序

发布 `V*` tag 时，Release workflow 会在 `PlugHub-Revit2020-<tag>.zip` 之外构建 `PlugHub-Setup-<tag>.exe`。安装程序是 `src\PlugHub.Installer` 生成的 C# WinForms EXE，内部嵌入同一次 release 的 Revit 2020 zip payload。

安装程序行为：

- 默认安装目录为 `D:\Program Files\PlugHub`，用户可以手动选择其他目录。
- 解压并复制 PlugHub 文件到安装目录。
- 自动把安装目录中的 `PlugHub.addin` 的 `<Assembly>` 写成 `PlugHub.Revit2020.dll` 的绝对路径。
- 复制 addin 到当前用户 `%APPDATA%\Autodesk\Revit\Addins\2020\PlugHub.addin`；如果已有文件，会先写 `.bak` 备份。
- 安装程序只注册 Revit 2020 addin，不声明 Revit 实机验收通过。

### 跳过 dist staging

Revit 正在运行时可能占用 `dist\Revit2020` 中的 DLL，导致构建后的复制输出失败。只需要验证编译、不需要刷新 `dist` 目录时，可以显式关闭 staging：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

该参数只跳过复制输出到 `dist\Revit2020`，不代表 Revit 实机加载通过。

## Revit 实机验收

实机验收必须在 Windows + Revit 2020 中进行。建议使用测试模型副本和族文件副本。

检查项：

- Revit 能识别 `PlugHub.addin` 并加载 `IExternalApplication`。
- Ribbon 中出现 `PlugHub` tab。
- 固定出现 `框架` panel 和「设置」按钮。
- 未配置外部模块时不出现业务 panel。
- 把插件包安装到 `packages` 后，出现对应外部模块 panel。
- 在设置页仓库中浏览公开或私有仓库，选择安装、更新、卸载后，插件包文件只落到 `packages`；新增插件、卸载插件和 Ribbon 结构变化后重启 Revit 再验收 Ribbon。未被占用的业务 DLL 更新会直接替换 `packages` 文件，不应显示为待重启。
- 「设置」打开 WPF 窗口，关闭后 Revit 可继续操作。
- 布局页是唯一的 Ribbon 布局调整入口；功能池来自已安装插件包，布局画布保存到当前 workspace 的 `ribbon.panels`。
- 没有已保存布局时，设置页会按框架默认策略从当前已安装功能生成默认布局；用户也可以在布局页手动重置默认布局。
- 布局画布会阻止同一个 featureId 重复添加；如果功能池中显示“已放置”，再次添加会被拦截并提示。
- 布局页调整 Ribbon 结构、图标和大小后，重启 Revit 能看到新布局。
- Ribbon 结构包括 panel、PushButton、PulldownButton、SplitButton 和 Stack；设置页保存后不会尝试在当前 Revit 会话中实时替换已有 Ribbon 控件，必须重启 Revit 后验收。
- Ribbon 容器规则必须固定：Panel 可以放 PushButton、PulldownButton、SplitButton 和 Stack；Stack 只能放 2-3 个 PushButton、PulldownButton 或 SplitButton，Stack 不能嵌套 Stack；PulldownButton 和 SplitButton 只能包含 PushButton。拖拽、属性里的控件类型切换、保存前校验都要遵守这套规则。
- 外部模块按钮能进入对应 Revit API 命令，例如分离后的 `Tee/Tap 切换` 和 `批量材质参数`。

不要把静态验证或本机构建表述为实机通过。

在 Revit 2020 中，PlugHub 不承诺已加载业务 DLL 的真正热重载。Ribbon 按钮不再直接绑定业务 `commandAssembly`，而是绑定到框架 slot 命令；`ModuleDiscoveryService` 以插件包清单为权威来源，启动发现不加载业务 DLL，也不调用 `IPlugHubModule.Describe()`。命令实例由 `FeatureCommandDispatcher` 在用户点击功能的路径上创建或加载；如果功能没有配置 `commandAssembly`，框架会默认解析为 `module.Assembly`。net48 命令加载器会把插件包复制到 `runtime-cache/<package>/<hash>/` 后从缓存副本加载，旧缓存清理失败会记录到 `runtime-cache/pending-cleanup.txt` 并在后续加载前重试。该机制用于降低安装目录 DLL 被占用的概率，不能承诺 .NET Framework 已加载程序集卸载或同一 Revit 会话内真热重载。

Revit 2025+ ALC 需要单独的 net8 适配层、.NET SDK 8、Revit 2025 API 引用以及可被 net8 适配层引用的 Contracts/Framework 目标框架。`PlugHub.Contracts` 已多目标到 `net48;netstandard2.1`，供 Revit 2020 和未来 net8 适配层共享契约；`PlugHub.Framework` 仍保持 `net48`，因为配置、包仓库和设置写回路径仍依赖 `System.Web.Script.Serialization`，后续要先替换这个 JSON 边界才能继续多目标化。当前本机只有 .NET SDK 3.1，当前仓库也仍是 Revit 2020 成果目录；这里的第四阶段只固化 `AlcLoadRules` 共享程序集边界，不声明 Revit 2025 实机支持。

## 配置变更注意事项

- 新增外部插件包时，安装或复制到 `packages`；Revit 启动只扫描 `packages`。
- `moduleSources` 兼容保留但默认为空，不用于配置启动时仓库拉取。
- 仓库配置写在 `repositories`。`provider` 支持 `github` 和 `gitee`；公开仓库使用 `visibility: "public"`；私有仓库使用 `visibility: "private"` 并提供 `apiKey`。
- 默认公开仓库使用 Gitee，指向 `https://gitee.com/GaoMengGu/PlugHub_Packages`。仓库不会在 Revit 启动或打开设置页时拉取或加载；设置页只会先显示本地缓存，只有用户在仓库页选择“浏览仓库插件包”或“检查更新”时才访问远端。浏览使用 sparse checkout，只取包清单、DLL 和图标等包资产。
- 仓库根 `package.json` 如果包含多个 module，设置页应展示为多个插件行；安装时 PlugHub 会按选中 module 拆成单插件本地包。
- 仓库插件包列表必须面向大量插件保持可浏览：使用虚拟化插件列表，保留搜索、安装状态筛选、来源筛选、分类/标签筛选和行内安装/更新/卸载入口，避免把所有字段都挤到主表格。
- 安装和更新会把选中插件的单模块清单及其引用的 DLL/资源复制到 `packages/<插件ID>`，不会复制整个仓库；卸载只删除 `packages` 下对应已安装目录。无锁更新成功后不写入待重启状态；如果本会话已经执行过旧业务 DLL，仍建议重启 Revit 后验收新版本。
- 如果 DLL 正被 Revit 占用，更新和卸载不会直接失败：PlugHub 会先移除本地清单中的模块声明，写入 `repository-cache/.package-install/pending-operations.json`，并在下次启动、模块发现之前删除或替换对应插件目录。
- 业务命令执行后可能生成 `runtime-cache`。这个目录是运行时缓存，可以删除；如果当前 Revit 会话仍占用其中旧 DLL，PlugHub 会在后续加载前继续尝试清理。
- 日志默认写入安装目录下的 `logs`；如果安装目录不可写，回退到用户本地应用数据目录。设置页不提供普通日志页签，只通过诊断菜单保留打开日志目录、导出日志和查看诊断入口。
- 外部插件包文件夹推荐使用 `package.json`；平铺投放 DLL 时使用 `<DllName>.package.json`；框架来源配置文件名统一为 `config\sources.json`。
- 功能如果没有 `commandAssembly` / `commandType`，Ribbon 按钮会回落到框架状态窗口。
- workspace 未配置 group 时，Composer 会按 feature 的 `group`、`category` 或 `moduleId` 生成 fallback panel。

## 签名

可选签名脚本：

```powershell
.\scripts\sign-revit2020.ps1 -Thumbprint "<Thumbprint>"
```

本地开发可用 self-signed 证书；公开分发前需要评估公开可信证书或 SignPath Foundation 等开源签名方案。详细说明见 [signing.md](signing.md)。

发布 `V*` tag 时，GitHub Actions 会运行 release workflow，使用 NuGet 编译引用构建 Revit 2020 包，并用 cosign 为 DLL 和 zip 产物生成 Sigstore 签名 bundle。

`main` 分支更新时，GitHub Actions 会运行 Gitee 同步 workflow，将当前 `main` 推送到 `https://gitee.com/GaoMengGu/PlugHub`。该 workflow 依赖仓库 secrets：`GITEE_PRIVATE_KEY`、`GITEE_TOKEN`、`GITEE_USER`。

版本预留在 `build\Directory.Build.props` 中维护：`RevitVersion` 控制输出路径和 addin 目录，`RevitApiReferenceMode` 控制 `Installed` / `NuGet` 引用模式，`RevitApiNuGetVersion` 控制 CI 编译引用包版本。后续增加 2018、2022、2024 适配时，应复用这些属性。

## Agent 协作规则

- 先读 `docs/README.md`，再根据任务读 `project-overview.md`、`architecture.md` 或本文件。
- 不要恢复已删除的样例模块、内置业务模块、占位功能、视图集或旧 Ribbon 入口。
- 不要把 `bin/`、`obj/`、`dist/` 等构建产物纳入提交。
- 修改验证器时，先确认失败场景能被捕获，再让实现转绿。
- 修改设置页时，不要在保存配置、Revit 启动或运行时刷新中执行 Git 拉取、程序集加载；仓库访问必须由用户在仓库页显式触发。
- 涉及 Revit 行为时，明确区分静态验证、本机构建和 Revit 实机测试。
