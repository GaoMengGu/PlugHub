# Revit 2020 模块化插件框架需求说明

## 1. 目标

交付一个 Revit 2020 插件框架底座：支持后续功能模块以独立方式接入、启用、禁用、隐藏、排序和组合。框架层不实现具体 Revit 建模、出图、族管理、参数写入等业务操作命令。

## 2. 必须达成

1. 所有成果集中在 `/home/yilan/plughub`。
2. 提供 Contracts / Framework / Revit2020 Adapter / BuiltinModule 分层源码骨架。
3. 模块可通过配置启用、禁用、隐藏或移除。
4. 功能列表支持 view、group、tag、category、order 的排序与组合。
5. 提供配置样例、JSON schema、验证脚本和静态测试。
6. 明确当前 Linux 环境不能伪造 Revit 实机测试。

## 3. 不做范围

- 不实现真实 Revit 业务命令。
- 不做插件市场、远程下载、授权系统、自动更新服务。
- 不承诺 .NET Framework 已加载程序集的真正热卸载；V1 的“卸载”定义为配置级禁用/隐藏/跳过注册，并在下次 Revit 启动时不加载。

## 4. 团队分工

- devpm：需求与验收标准。
- architect：分层架构、模块治理、验证边界。
- backend：契约、框架核心、Revit 入口适配、内置命令模块、测试。
- frontend：功能列表组织、视图/预设配置、配置 UX 文档。
- reviewer：静态验收与风险审查。
