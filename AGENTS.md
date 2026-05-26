# PlugHub 项目协作规则

- 本目录是 Revit 2020 模块化插件框架成果目录，必须持续保留。
- 所有源码、配置、文档、测试都应写入 `/home/yilan/plughub`，不要散落到其他目录。
- 当前非 Revit 环境只做 C# 静态验证，不声称 Revit 实机测试。
- 框架层不实现具体 Revit 业务操作，只提供模块契约、发现、启用/禁用、排序/组合、诊断和 Revit 2020 入口适配。
- 修改后至少运行：`dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj`。
