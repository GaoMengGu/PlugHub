# 模块契约

模块实现 `IPlugHubModule`，通过 `Describe()` 返回 `ModuleDescriptor` 和 `FeatureDescriptor` 列表；框架会把它与模块清单做发现、校验和诊断对照。

纯元数据模块只应依赖 `PlugHub.Contracts`，不得反向依赖 Framework，也不得直接依赖 Revit API。需要执行 Revit API 业务的功能应放在单独的 Revit 命令模块中：模块类仍实现 `IPlugHubModule`，功能入口通过 `FeatureDescriptor.CommandAssembly` 和 `FeatureDescriptor.CommandType` 指向同一程序集里的 `IExternalCommand`。Revit 适配层只负责把 Ribbon 按钮路由到该命令类型，框架层不承载具体业务操作。
