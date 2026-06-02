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

## 安装包

发布 `V*` tag 后，GitHub Release 会同时生成：

- `PlugHub-Revit2020-<tag>.zip`：手动部署包。
- `PlugHub-Setup-<tag>.exe`：安装程序，默认安装目录为 `D:\Program Files\PlugHub`，用户可以手动选择其他目录，安装后会在安装目录写入 `PlugHub-Uninstall.exe`。

安装程序会复制 PlugHub 文件，自动把 `PlugHub.addin` 中的 DLL 地址改为安装目录下的 `PlugHub.Revit2020.dll` 绝对路径，并复制 addin 到机器级 Revit 2020 插件目录：

```text
C:\ProgramData\Autodesk\Revit\Addins\2020\PlugHub.addin
```

发布 workflow 使用 NuGet 编译引用，不需要把 `RevitAPI.dll` 或 `RevitAPIUI.dll` 放入仓库。GitHub 使用 `.github\workflows\release.yml` 发布；`.github\workflows\sync-gitee.yml` 会把 `main` 和 `V*` tag 同步到 Gitee；Gitee Go 使用 `.gitee\workflows\release.yml` 打包并通过 Gitee API 发布 release。正常发布只需要本地推送 GitHub，不需要从本机直推 Gitee tag。

## 文档

内部设计、进度、架构和协作规则见 [docs/README.md](docs/README.md)。

## 许可

PlugHub 源码公开供个人使用、学习和非商业研究。未经作者书面许可，不得商用，不得将本项目或其衍生版本用于销售、集成收费交付或闭源再分发。
