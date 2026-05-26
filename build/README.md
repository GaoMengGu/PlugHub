# Windows/Revit 构建说明

运行：

```powershell
.\scripts\build-revit2020.ps1 -RevitApiDir "D:\Program Files\Autodesk\Revit 2020"
```

脚本会编译 `PlugHub.Revit2020`，复制框架 DLL、样例模块、运行时配置和 `.addin` 到 `dist\Revit2020`。
