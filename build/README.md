# Windows/Revit 构建说明

本地 Revit 2020 构建默认引用本机 Revit 安装目录中的 API DLL：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

脚本会编译 `PlugHub.Revit2020`，复制框架 DLL、运行时配置、运行时插件包投放目录和 `.addin` 到 `dist\Revit2020`。

CI 发布构建不要求安装 Revit，改用 NuGet 编译引用：

```powershell
.\scripts\build-revit2020.ps1 -UseRevitApiNuGet
```

`build\Directory.Build.props` 已预留 `RevitVersion`、`RevitApiReferenceMode`、`RevitApiNuGetVersion` 以及 2018/2020/2022/2024 的安装目录属性。后续新增 Revit 版本适配项目时，应复用这些属性，不要把 Revit API DLL 提交到仓库。

Revit 2025+ ALC 适配需要单独的 net8 项目和 .NET SDK 8。本仓库当前构建脚本仍只闭环 Revit 2020；不要仅因为存在 `AlcLoadRules` 就把构建产物或文档声明为 Revit 2025 可用。
