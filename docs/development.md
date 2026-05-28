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
- 将 `.addin` 中的 Assembly 写成 `PlugHub.Revit2020.dll` 的绝对路径。
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

## Revit 实机验收

实机验收必须在 Windows + Revit 2020 中进行。建议使用测试模型副本和族文件副本。

检查项：

- Revit 能识别 `PlugHub.addin` 并加载 `IExternalApplication`。
- Ribbon 中出现 `PlugHub` tab。
- 固定出现 `框架` panel 和「设置」按钮。
- 未配置外部模块时不出现业务 panel。
- 配置 `D:\AI\code\PlugHub_Modules` 或其他外部模块来源后，出现对应外部模块 panel。
- 「设置」打开 WPF 窗口，关闭后 Revit 可继续操作。
- 插件包和功能显示名、开关、所属分组、图标、大小、排序保存后能写回配置。
- 自定义分组通过分组页右键菜单新增或删除；删除前必须先移走该分组下的功能。
- Ribbon 结构、图标和大小调整后，重启 Revit 能看到新布局。
- 外部模块按钮能进入对应 Revit API 命令，例如分离后的 `Tee/Tap 切换` 和 `批量材质参数`。

不要把静态验证或本机构建表述为实机通过。

## 配置变更注意事项

- 新增外部插件包时，优先放到 `packages/dropins` 或配置 `moduleSources`。
- 本地来源使用 `type: "localFolder"`，GitHub 来源使用 `type: "github"`。
- 默认 GitHub 来源指向 `GaoMengGu/PlugHub_Modules`，本地缓存目录为 `packages/github/GaoMengGu_PlugHub_Modules`。
- GitHub 来源会解析到本地缓存目录；启用 `autoUpdate` 后运行时才会尝试 Git 操作。
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

版本预留在 `build\Directory.Build.props` 中维护：`RevitVersion` 控制输出路径和 addin 目录，`RevitApiReferenceMode` 控制 `Installed` / `NuGet` 引用模式，`RevitApiNuGetVersion` 控制 CI 编译引用包版本。后续增加 2018、2022、2024 适配时，应复用这些属性。

## Agent 协作规则

- 先读 `docs/README.md`，再根据任务读 `project-overview.md`、`architecture.md` 或本文件。
- 不要恢复已删除的样例模块、内置业务模块、占位功能、视图集或旧 Ribbon 入口。
- 不要把 `bin/`、`obj/`、`dist/` 等构建产物纳入提交。
- 修改验证器时，先确认失败场景能被捕获，再让实现转绿。
- 修改设置页时，不要在 WPF 设置窗口中执行 Git 拉取、程序集加载或运行时刷新。
- 涉及 Revit 行为时，明确区分静态验证、本机构建和 Revit 实机测试。
