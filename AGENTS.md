# PlugHub 项目协作规则

- 框架唯一工作区与成果目录为 `/home/yilan/codex-project/PlugHub`，必须持续保留。
- 所有源码、配置、文档、测试及本地方案都在本工作区内维护；临时文件放在 `.tmp/`。
- `.chorus/` 是仅本地保留的内部协作目录，保持 Git 忽略，禁止提交或推送其中内容；文档仍可同步到 Chorus 平台。
- 当前非 Revit 环境只做 C# 静态验证，不声称 Revit 实机测试。
- 框架层不实现具体 Revit 业务操作，只提供模块契约、发现、安装/卸载、排序/组合、诊断和 Revit 2020 入口适配；插件包是否提供功能按安装状态处理。
- 修改后至少运行：`dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj`。
