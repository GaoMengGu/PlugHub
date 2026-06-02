# PlugHub 插件开发指南

PlugHub 插件包通过 `package.json` 或 `*.package.json` 声明模块和功能。业务功能在外部 DLL 中实现 Revit `IExternalCommand`，PlugHub 框架只负责发现、展示、启用/禁用、排序、日志和命令路由。

## 运行时硬性要求

当前运行时识别插件包清单的硬性条件很少：

- 根对象必须包含 `schemaVersion` 和 `modules`。
- `modules` 必须是数组；当前静态验证要求包清单至少包含 1 个 module。
- 每个 module 应包含 `id`、`enabled`、`visible` 和 `features`。`features` 可以为空，但空数组不会生成业务按钮。
- 每个真实功能应包含稳定的 `id`。同一运行时中的 feature id 必须唯一，否则会按 `conflictPolicy.duplicateFeatureId` 产生诊断并跳过重复项。

Revit 兼容性只按 `revitVersions` 做当前运行时过滤。根级 `revitVersions` 会下推到未单独声明版本的 module；如果非空且不包含 `2020`，该 module 会被跳过。`frameworkVersionRange` 目前只作为元数据保留，不做版本范围求值。

## 最小 package.json

```json
{
  "schemaVersion": "1.0",
  "version": "1.0.0",
  "revitVersions": ["2020"],
  "frameworkVersionRange": ">=1.3.0",
  "modules": [
    {
      "id": "hello-world",
      "assembly": "HelloWorld.dll",
      "displayName": "Hello World",
      "enabled": true,
      "visible": true,
      "features": [
        {
          "id": "hello-world.run",
          "displayName": "Hello",
          "category": "examples",
          "group": "examples",
          "order": 100,
          "defaultState": "Visible",
          "buttonSize": "large",
          "iconPath": "icons/hello.png",
          "commandAssembly": "HelloWorld.dll",
          "commandType": "HelloWorld.HelloCommand"
        }
      ]
    }
  ]
}
```

如果 `commandAssembly` 与 module 的 `assembly` 相同，可以省略 feature 级 `commandAssembly`，框架会回落到 module 的 `assembly`。如果缺少 `commandType`，Ribbon 按钮会进入框架状态窗口，不会执行业务命令。

## 推荐字段

根级推荐字段：

- `version`：插件包版本，仓库页用于展示和更新判断。
- `revitVersions`：声明支持的 Revit 版本。当前可运行适配层是 Revit 2020，因此发布包应包含 `"2020"`。
- `frameworkVersionRange`：框架兼容范围元数据。当前运行时保留但不执行范围求值。
- `sha256` / `signature`：仓库分发元数据。安装时如果把多 module 清单拆成单 module 本地包，这两个字段不会复制到本地清单，避免签名和 hash 指向已经改写过的旧根清单。

Module 推荐字段：

- `id`：稳定唯一 ID，不要随显示名变化。
- `assembly`：业务 DLL 相对路径。feature 未设置 `commandAssembly` 时使用它作为默认命令程序集路径。
- `displayName` / `description` / `tags`：仓库页、设置页和诊断展示信息。
- `enabled` / `visible` / `order`：启动发现、功能列表和默认排序。
- `features`：功能入口列表。

Feature 推荐字段：

- `id`：稳定唯一 feature ID，也是 Ribbon layout 引用的权威键。
- `displayName` / `description`：用户可见功能名和说明；`name` 只作为兼容回退。
- `category` / `group` / `tags`：用于匹配 `workspace` 分组、搜索和筛选。
- `order`：同组内排序。
- `defaultState`：`Visible`、`Disabled` 或 `Hidden`。
- `buttonSize`：`large` 或 `small`。
- `iconPath`：相对插件包目录的图片路径，也可以使用框架内置图标值；为空时使用默认图标。
- `commandAssembly` / `commandType`：指向实际 Revit `IExternalCommand`。

## 不再建议写入的字段

以下字段可能仍被模型兼容或安装器写回，但外部插件包不应主动依赖：

- `packageDirectories`、`moduleSources`、`repositories`、`conflictPolicy`：这是框架根配置或安装后本地单 module 清单的兼容字段，不是普通仓库插件包应维护的内容。
- `sourceId`、`resolvedBaseDirectory`：运行时来源解析元数据，由框架填充。
- `dependsOn`：当前模型保留该字段，但运行时没有依赖调度器。
- `commandKey`：仅用于旧命令入口兼容和诊断展示；新包应优先使用稳定 feature `id`。
- `type` / `IPlugHubModule.Describe()`：保留为模块侧契约和未来诊断扩展点。默认启动发现不实例化模块类型，也不调用 `Describe()`。
- 高级 Ribbon 容器字段：插件包清单只声明功能，不直接声明用户布局。PushButton、PulldownButton、SplitButton 和 Stack 的组合保存在 `views.json` 当前 workspace 的 `ribbon.panels` 下，由设置页维护。

## 命令约束

- 命令类型必须实现 Revit `IExternalCommand`。
- 插件不应依赖 PlugHub 框架内部类型。
- 插件可引用 `PlugHub.Contracts`，但不要引用 `PlugHub.Framework`。
- Revit 2020 不支持通过 ALC 卸载已加载程序集。
- 更新已加载 DLL 后，仍建议重启 Revit 验收。

## 布局边界

插件包清单只声明功能，不直接声明用户的高级 Ribbon 容器。高级布局由用户设置保存，引用已安装 featureId。删除布局项只会移除 Ribbon 引用，不会卸载插件包或删除 feature。默认布局由框架根据已安装功能生成，后续可在框架层统一演进插件包 JSON 总体格式。
