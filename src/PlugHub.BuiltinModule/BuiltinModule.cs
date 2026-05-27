using System.Collections.Generic;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;

namespace PlugHub.BuiltinModule
{
    public sealed class DuctToolsModule : BuiltinToolModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = ModuleIds.DuctTools,
            Name = "机电风管",
            Description = "风管连接偏好等机电建模辅助入口。",
            State = ModuleState.Enabled,
            Order = 300,
            Tags = new[] { "mep", "duct", "revit-api" },
            Features = new List<FeatureDescriptor>
            {
                Feature(
                    id: "plughub.builtin.duct-tools.switch-preferred-junction",
                    moduleId: ModuleIds.DuctTools,
                    name: "切换风管首选连接",
                    description: "在已选或点选风管的类型上切换 Tee/Tap 首选连接类型。",
                    category: "mep",
                    group: "mep-tools",
                    order: 310,
                    commandKey: "builtin.duct.switch-preferred-junction",
                    commandType: CommandTypes.DuctPreferredJunctionSwitcher,
                    tags: new[] { "mep", "duct", "frequent" })
            }
        };
    }

    public sealed class FamilyToolsModule : BuiltinToolModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = ModuleIds.FamilyTools,
            Name = "族批处理",
            Description = "族文件批处理和族参数维护入口。",
            State = ModuleState.Enabled,
            Order = 320,
            Tags = new[] { "family", "material", "revit-api" },
            Features = new List<FeatureDescriptor>
            {
                Feature(
                    id: "plughub.builtin.family-tools.batch-add-material-parameter",
                    moduleId: ModuleIds.FamilyTools,
                    name: "批量添加材质参数",
                    description: "批量打开族文件，添加材质参数并关联实体材质参数。",
                    category: "family",
                    group: "family-tools",
                    order: 330,
                    commandKey: "builtin.family.batch-add-material-parameter",
                    commandType: CommandTypes.BatchAddMaterialParameter,
                    tags: new[] { "family", "material", "batch" })
            }
        };
    }

    public abstract class BuiltinToolModule : IPlugHubModule
    {
        public abstract ModuleDescriptor Describe();
        public void Initialize(IModuleContext context) { }
        public void Shutdown() { }

        protected static FeatureDescriptor Feature(
            string id,
            string moduleId,
            string name,
            string description,
            string category,
            string group,
            int order,
            string commandKey,
            string commandType,
            params string[] tags)
        {
            return new FeatureDescriptor
            {
                Id = id,
                ModuleId = moduleId,
                Name = name,
                Description = description,
                Category = category,
                Group = group,
                Tags = tags,
                Order = order,
                DefaultState = FeatureState.Visible,
                CommandKey = commandKey,
                CommandAssembly = ModuleIds.AssemblyName,
                CommandType = commandType,
                ButtonSize = "large"
            };
        }
    }

    internal static class ModuleIds
    {
        public const string AssemblyName = "PlugHub.BuiltinModule.dll";
        public const string DuctTools = "plughub.builtin.duct-tools";
        public const string FamilyTools = "plughub.builtin.family-tools";
    }

    internal static class CommandTypes
    {
        public const string DuctPreferredJunctionSwitcher = "PlugHub.BuiltinModule.Commands.DuctPreferredJunctionSwitcherCommand";
        public const string BatchAddMaterialParameter = "PlugHub.BuiltinModule.Commands.BatchAddMaterialParameterCommand";
    }
}
