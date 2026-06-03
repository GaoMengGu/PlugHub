# PlugHub

PlugHub 是面向 Revit 2020 的模块化插件框架。它提供统一 Ribbon 入口、模块发现、功能开关、排序组合、日志和设置界面，用于承载后续 Revit 功能模块。

## 核心能力

- 单一 `PlugHub` Ribbon tab 和 `workspace` 工作台。
- 通过 JSON 配置管理已安装插件包、仓库、功能、分组、显示名、图标、按钮大小和排序。
- 启动时只扫描本地 `packages` 目录；仓库只在设置页中由用户显式浏览或检查更新，再选择安装、更新或卸载。
- 设置入口采用 Ribbon 按钮，设置窗口采用 WPF。
- 框架层隔离 Revit API，不包含内置业务功能；具体业务命令由外部模块实现。

## 插件包和仓库

默认配置不包含任何业务模块。模块可通过以下方式接入：

- 复制插件包文件夹到 `packages`，包内使用 `package.json`。
- 平铺投放 DLL 时，使用 `<DllName>.package.json` 作为邻接清单。
- 默认公开仓库使用 Gitee `https://gitee.com/GaoMengGu/PlugHub_Packages`；也可以在设置页的「仓库」中配置 GitHub 或 Gitee 的公开/私有仓库，私有仓库填写 `apiKey`；浏览仓库后通过搜索、状态、来源、分类/标签筛选和行内按钮选择插件安装到 `packages`。同一个 `package.json` 中的多个 module 会显示为多个插件行，安装时由 PlugHub 拆成单插件本地包。
- 浏览仓库通过 HTTP archive 下载并刷新本地仓库缓存，普通用户不需要安装 Git；安装只复制选中插件的单模块清单和引用文件，不复制整个仓库。
- 如果 Revit 正在占用已加载 DLL，更新和卸载会先移除本地 `package.json` 中的模块声明，并写入待处理操作；下次启动 PlugHub 会在模块发现前删除或替换文件。
- Revit 启动时不会拉取仓库，也不会直接从仓库缓存加载插件包。

## 验证

当前非 Revit 环境只做 C# 静态验证：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

静态验证不能替代 Windows + Revit 2020 实机测试。

仓库中的 `docs/` 目录作为本地协作资料目录处理，后续提交默认忽略其中内容；公开使用说明和静态验证约束以本 README、源码、配置、脚本和 workflow 为准。

## Revit 2020 构建

本地构建默认引用本机 Revit 安装目录中的 API DLL：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

输出目录：

```text
dist\Revit2020
```

如需安装 addin manifest：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020" -InstallAddin
```

只需要验证编译、不需要刷新 `dist\Revit2020` 时，可以关闭 staging：

```powershell
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
```

Ribbon 结构、图标和按钮大小变更后需要重启 Revit。布局页是唯一的 Ribbon 布局入口，布局画布会阻止同一个 featureId 重复添加。Ribbon 容器规则固定为：Panel 可放 PushButton、PulldownButton、SplitButton 和 Stack；Stack 不能嵌套 Stack，且只能放 2-3 个 PushButton、PulldownButton 或 SplitButton；PulldownButton 和 SplitButton 只能包含 PushButton。

## 安装包

发布 `V*` tag 后，GitHub Release 会同时生成：

- `PlugHub-Revit2020-<tag>.zip`：手动部署包。
- `PlugHub-Setup-<tag>.exe`：安装程序，默认安装目录为 `D:\Program Files\PlugHub`，用户可以手动选择其他目录，安装后会在安装目录写入 `PlugHub-Uninstall.exe`。
- `PlugHub-Revit2020-<tag>.zip.sigstore.json` 和 `PlugHub-Setup-<tag>.exe.sigstore.json`：zip 与 exe 的 cosign 签名 bundle；不发布单个 DLL 的签名 JSON。

安装程序会复制 PlugHub 文件，自动把 `PlugHub.addin` 中的 DLL 地址改为安装目录下的 `PlugHub.Revit2020.dll` 绝对路径，并复制 addin 到机器级 Revit 2020 插件目录：

```text
C:\ProgramData\Autodesk\Revit\Addins\2020\PlugHub.addin
```

发布 workflow 使用 NuGet 编译引用，不需要把 `RevitAPI.dll` 或 `RevitAPIUI.dll` 放入仓库。GitHub 使用 `.github\workflows\release.yml` 作为唯一发布入口：每次 `main` 推送后自动创建下一个 patch 版本 tag，例如 `V1.4.6` 后创建 `V1.4.7`；只有需要指定版本号时，才手动触发该 workflow 并填写 `version`。本地直接推送 `V*` tag 时，release workflow 按该 tag 发布。每次 release 会根据上一个 `V*` tag 之后的提交生成简要更新信息，并写入 GitHub/Gitee release 正文。`.github\workflows\sync-gitee.yml` 只负责把 `main` 和 `V*` tag 同步到 Gitee；GitHub release 发布完成后，`.github\workflows\release.yml` 会等待 Gitee tag 可见，再使用 `GITEE_TOKEN` 调用 Gitee API 创建 release，并上传与 GitHub release 同名的 zip、exe 以及 zip/exe 签名 JSON 资产。不再使用 Gitee Go `.workflow` 流水线，也不需要配置 Gitee Windows agent 或 `PLUGHUB_WINDOWS_HOST_GROUP_ID`。正常发布只需要本地推送 GitHub，不需要从本机直推 Gitee tag。

Revit API 引用通过 NuGet 仅用于 CI 编译；本地和实机验收仍以真实 Revit 安装目录为准。签名脚本支持 self-signed、signtool、Thumbprint、SHA256 时间戳签名；公开分发前可评估 SignPath Foundation 或其他可信签名方案。Release workflow 使用 cosign keyless blob signing。

## 框架更新

设置窗口的「关于」页签左上角显示 `PlugHub` 和当前框架版本，版本后方提供两个小图标：检查更新和升级框架。

- 检查更新图标：优先查询 Gitee tags/release 下载源，GitHub latest release 作为回退，定位 `PlugHub-Revit2020-<tag>.zip`，并在左下角提示结果。
- 升级框架图标：发现新版本后弹出目标版本号和 release 更新信息；确认后按 Gitee、GitHub 顺序尝试下载更新包并启动静默 updater，左下角提示需要重启 Revit；关闭弹窗则退出更新。

框架更新只覆盖框架 DLL，不覆盖 `PlugHub.addin`、`packages`、`config`、缓存和日志。当前 Revit 会话不会热替换已加载 DLL；关闭并重新打开 Revit 后，新框架 DLL 才会生效。

## 插件开发要点

插件包使用 `package.json` 描述模块和功能；功能命令实现 Revit `IExternalCommand`，框架层只负责发现、路由和状态提示，不实现具体业务操作。

运行时硬性要求包括：插件包必须声明可识别的模块、功能、命令类型和目标 DLL；推荐字段包括 `description`、`category`、`tags`、`revitVersions`、`frameworkVersionRange`、`sha256` 和 `signature`。不再建议把用户 Ribbon 布局写进插件包清单；插件包清单只声明功能，用户布局由设置页保存。

仓库凭据使用 DPAPI 保护。PlugHub.Contracts 当前多目标到 `net48;netstandard2.1`，为未来适配层共享契约；PlugHub.Framework 仍保持 net48，因为配置、仓库和设置写回边界仍依赖 `System.Web.Script.Serialization`。V1.2 之后的架构硬化以配置、仓库、路由和静态验证为边界。

Revit 2025+ ALC 需要单独 net8 适配层、.NET SDK 8、Revit 2025 API 引用和 `AlcLoadRules` 共享程序集边界；当前仓库不声明 Revit 2025 实机支持，也不能声明 Revit 实机测试成功。

## 许可

PlugHub 源码公开供个人使用、学习和非商业研究。未经作者书面许可，不得商用，不得将本项目或其衍生版本用于销售、集成收费交付或闭源再分发。
