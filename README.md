# PlugHub

PlugHub 是面向 Revit 2020 的模块化插件框架。它提供统一 Ribbon 入口、模块发现、功能开关、排序组合、诊断和设置界面，用于承载后续 Revit 功能模块。

## 核心能力

- 单一 `PlugHub` Ribbon tab 和 `workspace` 工作台。
- 通过 JSON 配置管理模块来源、插件包、功能、分组、显示名、图标、按钮大小和排序。
- 支持 `packages/dropins` 投放目录、指定本地文件夹和 GitHub 仓库来源。
- 设置入口采用 Ribbon 按钮，设置窗口采用 WPF。
- 框架层隔离 Revit API，不包含内置业务功能；具体业务命令由外部模块实现。

## 模块来源

默认配置不包含任何业务模块。模块可通过以下方式接入：

- 复制插件包文件夹到 `packages/dropins`，包内使用 `package.json`。
- 平铺投放 DLL 时，使用 `<DllName>.package.json` 作为邻接清单。
- 在 `config\sources.json` 中配置指定本地文件夹或 GitHub 仓库来源。

## 验证

当前非 Revit 环境只做 C# 静态验证：

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

静态验证不能替代 Windows + Revit 2020 实机测试。

## Revit 2020 构建

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

## 文档

内部设计、进度、架构和协作规则见 [docs/README.md](docs/README.md)。
