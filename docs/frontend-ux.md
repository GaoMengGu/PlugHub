# 功能列表组织与配置 UX

## 配置工件

- `config/modules.example.json`：模块与功能元数据。
- `config/views.example.json`：单工作台、分组、排序规则。
- `config/feature-combinations.example.json`：兼容保留，可为空。

## UX 原则

- 用户只面对一个 PlugHub 工作台，不再切换视图集。
- 项目、机电、族等入口通过 group/category/tag 在同一工作台中组织，用户侧主要感知模块显示名。
- 功能排序稳定、可解释，可通过 WPF 设置窗口拖拽调整。
- 不需要的模块通过 enabled/visible 禁用/隐藏。
- 功能入口支持 `buttonSize`，设计人员可以把按钮设为 `large` 或 `small`，由 Ribbon 以大按钮或 stacked 小按钮方式呈现。
- 设置页面以 WPF 窗口展现，分为模块、功能、来源和诊断四个分区；右键菜单处理启用/禁用、图标、大小和排序等高频动作。
- 来源页覆盖三类入口：自动读取 `modules/dropins` 等模块目录、指定本地文件夹、GitHub 仓库缓存/拉取；GitHub 设置允许维护缓存路径、仓库、分支、清单路径和自动拉取开关。
- 图标、按钮大小、Ribbon 分组结构等 Revit Ribbon 结构类变更保存后提示待重启生效。
