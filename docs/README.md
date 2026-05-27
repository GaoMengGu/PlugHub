# PlugHub 文档入口

这个目录面向参与项目协作的 agent 和开发者。建议先读 `agent-handbook.md`，再按任务类型进入专题文档。

## 推荐阅读顺序

1. `agent-handbook.md`：项目目标、边界、架构、配置、开发流程和验证要求的总览。
2. `requirements.md`：需求范围、必须达成、不做范围和团队分工。
3. `architecture.md`：Contracts / Framework / Revit2020 Adapter / BuiltinModule 分层与运行时组合规则。
4. `module-contract.md`：模块实现方必须遵守的契约边界。
5. `frontend-ux.md`：功能列表、view、group、排序、按钮大小和可视化配置原则。
6. `verification.md`：提交前验证命令和 Revit 2020 实机验证边界。
7. `review.md`：静态审查关注点和风险提示。

## 当前核心入口

- 解决方案：`../PlugHub.sln`
- 静态验证：`../src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj`
- Revit 2020 入口：`../src/PlugHub.Revit2020/ExternalApplicationEntry.cs`
- 可视化设置入口：`../src/PlugHub.Revit2020/FrameworkSettingsCommand.cs`
- 配置样例：`../config/*.example.json`

## 协作原则

- 框架层只做模块契约、发现、启用/禁用、排序/组合、诊断和 Revit 2020 入口适配。
- 不在 V1 中实现真实 Revit 建模、出图、族管理、参数写入等业务命令。
- `PlugHub.Contracts`、`PlugHub.Framework` 不得引用 `Autodesk.Revit`；Revit 适配层和明确的业务命令模块可以引用 Revit API。
- 修改配置、组合、Ribbon 或契约后，至少运行静态验证命令。
