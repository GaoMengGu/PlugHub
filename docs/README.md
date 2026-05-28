# PlugHub 内部文档

本目录面向项目维护者和后续 agent。根目录 `README.md` 只保留对外摘要；实现背景、架构约束、当前进度和验证流程集中在这里。

## 阅读顺序

1. [project-overview.md](project-overview.md)：项目背景、目标、开发思路、当前进度和已知边界。
2. [architecture.md](architecture.md)：当前架构、运行链路、配置模型、模块契约和关键设计决策。
3. [development.md](development.md)：本地开发、构建、验证、Revit 实机验收和 agent 协作规则。
4. [signing.md](signing.md)：DLL 签名的免费/公开分发方案、脚本用法和证书安全约束。

## 文档维护原则

- 内部文档记录对后续决策有用的信息，不保留已经被实现替代的临时讨论。
- 新增架构约束、运行时行为或验证要求时，同步更新对应文档和 `PlugHub.StaticValidation`。
- 当前非 Revit 环境只允许声明静态验证和本机构建结果，不得表述为 Revit 实机测试。
- 删除旧方案时同步删除旧文档入口，避免后续 agent 依据过期内容工作。
