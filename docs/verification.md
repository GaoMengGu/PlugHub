# 验证说明

## C# 静态验证

```powershell
dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
```

验证内容包括：

- 必需项目文件和文档存在。
- Contracts / Framework / SampleModule 不引用 Revit API。
- SampleModule 只依赖 Contracts。
- BuiltinModule 中的两个迁入功能声明了可路由的 `commandAssembly` 和 `commandType`。
- 配置文件的模块、功能和单工作台关系一致。
- workspace 工作台的功能组合结果符合预期。
- 框架运行时包含配置加载、模块来源解析、模块发现、注册、组合和诊断快照。
- Revit 2020 适配层包含 Ribbon tab、panel、button 和 WPF 设置入口。

## Windows/Revit 必验

- 在 Windows + Revit 2020 + Autodesk Revit API 环境中运行 `scripts\build-revit2020.ps1`。
- 验证 IExternalApplication 入口能被 Revit 识别。
- 验证 `PlugHub` Ribbon tab、`入门` / `项目流程` / `机电风管` / `族批处理` panel 和配置中的按钮展示一致。
- 点击未声明 `commandType` 的框架占位按钮时只显示框架状态信息。
- 点击 `切换风管首选连接` 和 `批量添加材质参数` 时分别进入对应 Revit API 命令，并在测试模型副本/族文件副本中验证结果。
- 不得把静态验证描述成 Revit 实机测试。
