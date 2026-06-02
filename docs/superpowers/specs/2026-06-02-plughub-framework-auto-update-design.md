# PlugHub Framework Auto Update Design

## Goal

在 PlugHub 设置窗口的「关于」页签增加两个按钮：`检查更新` 和 `更新框架`。用户先检查发布仓库的 latest release；存在新版本时，点击 `更新框架` 下载最新 PlugHub Revit 2020 框架包，并启动静默外部更新器。设置窗口左下角提示需要重启 Revit，外部更新器等待 Revit 退出后只覆盖框架 DLL。

该功能解决的是框架本体更新，不替代现有仓库页中的插件包安装、更新和卸载。

## Current State

当前仓库已经具备以下基础：

- GitHub Release 和 Gitee Go Release 会发布 `PlugHub-Revit2020-<tag>.zip` 和 `PlugHub-Setup-<tag>.exe`。
- 安装器可以把 release zip payload 解压到安装目录，并重写机器级 `PlugHub.addin`。框架自动更新不复用安装器的整包覆盖语义，只从 release zip 提取 DLL。
- 设置页「关于」区域已经显示框架版本，适合作为框架更新入口。
- 仓库页的 `PackageRepositoryService` 只处理 `packages` 下的插件包，不负责框架 DLL 或 Revit addin manifest。

必须保留的运行边界：

- Revit 进程内不直接覆盖 `PlugHub.Revit2020.dll`、`PlugHub.Framework.dll` 或其他已加载框架 DLL。
- .NET Framework 已加载程序集不能真正卸载，框架更新必须发生在 Revit 关闭之后。
- 非 Revit 环境只能做 C# 静态验证、构建验证和更新器文件流程验证，不能声明 Revit 实机测试成功。

## Product Decision

采用独立、静默运行的 `PlugHub.Updater` 外部 EXE。当前 Revit 进程不能直接覆盖已加载 DLL，因此仍需要外部进程等待 Revit 退出；但该进程不显示更新窗口，不改 addin，不碰插件包和用户配置。

PlugHub 主进程负责：

- 在「关于」页签提供 `检查更新` 和 `更新框架` 两个按钮。
- 查询 latest release。
- 比较当前框架版本和远端版本。
- 在用户点击 `更新框架` 后下载 `PlugHub-Revit2020-<tag>.zip`。
- 校验下载文件和 zip 内容。
- 启动静默 `PlugHub.Updater.exe`，传入安装目录、下载包路径、目标版本和当前 Revit 进程 ID。
- 在设置窗口左下角状态区提示需要重启 Revit。

`PlugHub.Updater.exe` 负责：

- 等待用户关闭 Revit，或等待指定进程退出。
- 备份当前安装目录中的框架 DLL。
- 从 release zip 提取并覆盖框架 DLL。
- 不覆盖 `PlugHub.addin`、`packages`、`config`、仓库缓存和日志。
- 失败时回滚备份，并写入可诊断日志。

## User Experience

入口放在「关于」页签：

- 按钮文案：`检查更新`、`更新框架`。
- 按钮位置：当前版本信息附近，和框架说明、安装路径等信息放在同一组。
- `检查更新` 始终可点击，用于查询 latest release 和比较版本。
- `更新框架` 只有在发现新版本后可点击；点击后下载更新包并排队静默覆盖。
- 设置窗口左下角状态区显示检查进度、当前版本、最新版本、下载状态和「需要重启 Revit」提示。

用户流程：

1. 用户打开设置窗口，进入「关于」页签。
2. 点击 `检查更新`。
3. 如果没有新版本，左下角显示「当前已是最新版本」。
4. 如果有新版本，启用 `更新框架` 按钮。
5. 用户点击 `更新框架`。
6. PlugHub 下载并校验 release zip。
7. 下载完成后启动静默 updater，并在左下角提示「框架更新已准备好，请重启 Revit」。
8. updater 等待当前 Revit 进程退出。
9. Revit 退出后，updater 静默覆盖框架 DLL。
10. 用户下次打开 Revit 时加载新框架 DLL。

如果下载或校验失败，设置页只显示失败原因和重试建议，不修改本地框架文件。

## Release Source

默认来源为 GitHub 仓库 `GaoMengGu/PlugHub` 的 latest release：

- API：`https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest`
- 资产：`PlugHub-Revit2020-<tag>.zip`

Gitee release 作为后续兼容来源，更新模型必须保持 provider 边界清晰：

- GitHub 使用 release API 获取 latest。
- Gitee 使用 Gitee release API 或已知 tag 资产地址。
- 第一阶段可以只实现 GitHub latest，保留配置扩展点，不把 Gitee 镜像逻辑硬编码进 GitHub 客户端。

## Version Rules

版本来源按以下顺序处理：

1. 远端版本来自 latest release 的 tag，例如 `V1.4.5`。
2. 本地版本来自 `PlugHub.Revit2020` 程序集信息版本或文件版本。
3. 比较时去掉 `V` 前缀，并按语义化版本比较。
4. 无法解析版本时不执行自动覆盖，只提示用户手动下载。

如果远端版本等于或低于本地版本，不下载更新包。

## Download And Validation

下载目录：

`%LocalAppData%\PlugHub\updates\<tag>\`

下载校验：

- 资产文件名必须匹配 `PlugHub-Revit2020-<tag>.zip`。
- zip 头必须有效。
- zip 内必须包含 `PlugHub.Revit2020.dll`、`PlugHub.Framework.dll` 和 `PlugHub.Contracts.dll`。
- zip 解压路径必须限制在 staging 目录内，拒绝路径穿越。
- 下载失败或校验失败不得启用 `更新框架` 的排队动作，也不得启动 updater。

第一阶段不强制实现 cosign 验签，但服务边界要保留签名校验扩展点。后续可以下载 `.sigstore.json` 并在外部校验工具可用时启用。

## Update Scope

更新器只覆盖框架 DLL。

默认覆盖：

- `PlugHub.Revit2020.dll`
- `PlugHub.Framework.dll`
- `PlugHub.Contracts.dll`
- 框架依赖 DLL

默认不覆盖：

- `PlugHub.addin`
- `packages`
- `config`
- `repository-cache`
- `runtime-cache`
- `logs`
- `PlugHub-Uninstall.exe`
- 内置资源和默认示例配置

覆盖前先备份当前安装目录中的框架 DLL。回滚只恢复本次更新覆盖的 DLL，不回滚用户在更新期间手动修改的其他文件。

## Updater Behavior

`PlugHub.Updater` 是静默外部 EXE，目标框架保持 `net48`。它不显示主窗口，所有状态写入日志；用户可见状态由设置窗口左下角提示承担。

启动参数：

- `/payloadZip <path>`
- `/installDir <path>`
- `/targetVersion <tag>`
- `/revitProcessId <pid>`

执行流程：

1. 校验参数、payload zip 和安装目录。
2. 如果 Revit 进程仍在运行，静默等待。
3. Revit 退出后创建备份目录。
4. 解压 payload 到临时 staging 目录。
5. 从 staging 复制框架 DLL 到安装目录。
6. 不修改机器级 addin manifest。
7. 清理临时目录。
8. 写入更新成功日志。

如果文件仍被占用，更新器停止并写入错误日志，不反复强制覆盖。

## Architecture

新增组件：

- `PlugHub.Framework.Updates.FrameworkUpdateService`：编排检查、比较、下载、校验和排队更新。
- `PlugHub.Framework.Updates.ReleaseClient`：获取 latest release 元数据。
- `PlugHub.Framework.Updates.ReleaseAssetDownloader`：下载 release 资产。
- `PlugHub.Framework.Updates.FrameworkUpdatePackageValidator`：校验 zip 文件和 DLL 内容。
- `PlugHub.Updater`：静默外部更新器项目。

修改组件：

- `FrameworkSettingsWindow.cs`：在「关于」页签增加 `检查更新` 和 `更新框架` 两个按钮，并在左下角显示检查、下载和需要重启状态。
- `PlugHub.StaticValidation`：增加两个框架更新按钮、项目引用、release DLL 资产校验和 updater 参数的静态规则。
- GitHub/Gitee release workflow：将 `PlugHub.Updater.exe` 打入 release zip 或作为安装目录中的框架工具随 zip 发布。

## Error Handling

用户可理解的错误信息必须覆盖：

- 无法访问 release API。
- latest release 没有 Revit 2020 zip。
- 下载文件不是 zip。
- zip 缺少框架 DLL。
- 本地版本无法解析。
- 框架更新已准备好，需要重启 Revit。
- 安装目录不可写。
- 更新失败并已回滚。
- 更新失败且回滚失败，需要手动重新运行安装器。

所有错误日志写入现有日志目录；如果安装目录不可写，回退到 `%LocalAppData%\PlugHub\logs`。

## Security

- 下载 URL 只接受 HTTPS。
- release 资产名必须匹配目标 tag。
- zip 解压必须检查路径穿越。
- 更新器只能写入传入的安装目录，拒绝空目录、磁盘根目录和明显异常路径。
- updater 只复制白名单 DLL，不复制 release zip 中的 `addin`、`config` 或 `packages`。
- 私有仓库 Token 不进入命令行参数和日志。
- 后续签名校验应在 `FrameworkUpdatePackageValidator` 中扩展，不进入 UI 层。

## Verification

必须运行：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

建议运行：

```powershell
dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release
dotnet build src\PlugHub.Revit2020\PlugHub.Revit2020.csproj -c Release /p:RevitApiReferenceMode=NuGet /p:StagePlugHubOutput=false
.\scripts\build-revit2020.ps1
```

非 Revit 环境验证只能声明：

- 静态验证通过。
- 更新器构建通过。
- release zip 校验逻辑通过。
- updater 对临时安装目录的备份、DLL 覆盖和回滚流程通过。

不能声明 Revit 实机加载、Ribbon 更新或安装目录被真实 Revit 释放后的行为已验证。

## Success Criteria

- 「关于」页签有 `检查更新` 和 `更新框架` 两个按钮。
- 无新版本时不会下载文件。
- 有新版本时启用 `更新框架`，点击后能下载 latest release 的 `PlugHub-Revit2020-<tag>.zip`。
- PlugHub 主进程不直接覆盖框架 DLL。
- 设置窗口左下角提示需要重启 Revit。
- 静默 updater 等待 Revit 关闭后只覆盖 DLL。
- 更新不覆盖 `PlugHub.addin`、`packages`、`config`、仓库缓存、运行时缓存和日志。
- 更新失败时能回滚或明确提示手动修复。
- 静态验证覆盖两个按钮、updater 项目、release zip DLL 资产和 Revit 运行边界。

## Out Of Scope

- Revit 进程内热替换框架 DLL。
- 用插件包仓库页管理框架本体版本。
- 自动更新第三方业务插件包。
- 强制关闭 Revit。
- 覆盖或重写 `PlugHub.addin`。
- 覆盖 `packages`、`config`、缓存和日志目录。
- 第一阶段强制完成 cosign 验签。
