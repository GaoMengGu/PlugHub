# PlugHub 插件开发指南

PlugHub 插件包通过 `package.json` 或 `*.package.json` 声明模块和功能。业务功能在外部 DLL 中实现 Revit `IExternalCommand`，PlugHub 框架只负责发现、展示、启用禁用、排序、日志和命令路由。

## 最小 package.json

```json
{
  "schemaVersion": "1.0",
  "version": "1.0.0",
  "revitVersions": ["2020"],
  "frameworkVersionRange": ">=1.2.0",
  "modules": [
    {
      "id": "hello-world",
      "assembly": "HelloWorld.dll",
      "type": "HelloWorld.Module",
      "displayName": "Hello World",
      "enabled": true,
      "visible": true,
      "features": [
        {
          "id": "hello-world.run",
          "displayName": "Hello",
          "group": "examples",
          "order": 100,
          "defaultState": "Visible",
          "commandAssembly": "HelloWorld.dll",
          "commandType": "HelloWorld.HelloCommand"
        }
      ]
    }
  ]
}
```

## 命令约束

- 命令类型必须实现 Revit `IExternalCommand`。
- 插件不应依赖 PlugHub 框架内部类型。
- Revit 2020 不支持通过 ALC 卸载已加载程序集。
- 更新已加载 DLL 后，仍建议重启 Revit 验收。

## 布局边界

插件包清单只声明功能，不直接声明用户的高级 Ribbon 容器。高级布局由用户设置保存，引用已安装 featureId。删除布局项只会移除 Ribbon 引用，不会卸载插件包或删除 feature。默认布局由框架根据已安装功能生成，后续可在框架层统一演进插件包 JSON 总体格式。
