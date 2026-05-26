# Revit 2020 插件框架架构设计

## 1. 分层

```text
Revit 2020
  -> PlugHub.Revit2020        # Revit API 入口适配层，负责 Ribbon 渲染
  -> PlugHub.Framework        # 配置、发现、注册、组合、诊断
  -> PlugHub.Contracts        # 稳定模块契约

PlugHub.SampleModule -> PlugHub.Contracts
PlugHub.BuiltinModule   -> PlugHub.Contracts + Revit API
```

依赖方向固定：SampleModule 只依赖 Contracts；Framework 不依赖 Revit API；Revit2020 Adapter 引用 Revit API 并负责 UI 适配。承载真实 Revit 业务命令的模块可以引用 Contracts 和 Revit API，但不能依赖 Framework。

## 2. 模块治理

- `modules.example.json` 描述模块、程序集、类型、enabled、visible、features。
- disabled 模块不注册入口。
- hidden 模块可保留安装但不进入当前功能列表。
- 从配置或目录移除模块即视为卸载/跳过。
- 运行时会将模块清单、程序集发现结果和诊断信息汇总为同一快照。

## 3. 功能组合

`views.example.json` 只定义一个 `workspace` 工作台。工作台不是多个用户视图的集合，而是 PlugHub 在 Revit 中渲染的唯一功能入口。

组合顺序：先应用 workspace-level exclude，再检查 workspace-level include，然后必须匹配某个 group，最后按 sort 指定顺序排序，默认保持 group.order、feature.order、feature.name、feature.id。Revit 入口将组合结果渲染为 Ribbon tab、panel 和按钮，并按 feature.buttonSize 决定大按钮或 stacked 小按钮。

模块/功能开关通过运行时快照尽量即时生效。Ribbon panel、按钮新增/删除、图标和大小属于 Revit 结构类变更，保存后标记为待重启生效。

## 4. 验证边界

当前非 Revit 环境可验证目录、配置、schema 和 C# 静态约束；Windows + Revit 2020 上需额外验证实际加载、Ribbon 显示和 Revit API 兼容性。
