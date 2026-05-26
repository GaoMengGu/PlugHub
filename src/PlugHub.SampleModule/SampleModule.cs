using System.Collections.Generic;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;

namespace PlugHub.SampleModule
{
    public sealed class SampleModule : NavigationModule
    {
    }

    public class NavigationModule : StaticSampleModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = "plughub.sample.navigation",
            Name = "示例导航模块",
            Description = "仅用于演示模块元数据和功能入口组织，不执行真实 Revit 业务操作。",
            State = ModuleState.Enabled,
            Order = 100,
            Tags = new[] { "sample", "basic", "training" },
            Features = new List<FeatureDescriptor>
            {
                Feature("plughub.sample.navigation.open-panel", "打开示例面板", "展示空面板入口。", "basic", "getting-started", 110, "sample.navigation.open-panel", "basic", "training"),
                Feature("plughub.sample.navigation.placeholder-one", "空白占位 A", "用于测试按钮排序和大图标。", "basic", "getting-started", 120, "sample.navigation.placeholder-one", "basic"),
                Feature("plughub.sample.navigation.placeholder-two", "空白占位 B", "用于测试小图标堆叠。", "basic", "getting-started", 130, "sample.navigation.placeholder-two", "basic"),
                Feature("plughub.sample.navigation.placeholder-three", "空白占位 C", "用于测试小图标堆叠。", "basic", "getting-started", 140, "sample.navigation.placeholder-three", "basic"),
                Feature("plughub.sample.navigation.placeholder-four", "空白占位 D", "用于测试按钮排序尾部位置。", "basic", "getting-started", 150, "sample.navigation.placeholder-four", "basic"),
                Feature("plughub.sample.navigation.show-diagnostics", "查看诊断摘要", "展示框架诊断信息入口。", "admin", "diagnostics", 160, "sample.navigation.show-diagnostics", "admin", "support")
            }
        };
    }

    public sealed class ProjectTemplateModule : StaticSampleModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = "plughub.sample.project-template",
            Name = "项目模板入口示例",
            Description = "演示按项目视图组合功能入口。",
            State = ModuleState.Enabled,
            Order = 200,
            Tags = new[] { "project", "frequent" },
            Features = new List<FeatureDescriptor>
            {
                Feature("plughub.sample.project-template.overview", "项目入口概览", "项目级功能组合入口占位。", "project", "project-workflow", 210, "sample.project-template.overview", "project", "frequent")
            }
        };
    }

    public sealed class ExperimentalModule : StaticSampleModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = "plughub.experimental.placeholder",
            Name = "实验模块占位",
            Description = "默认禁用。",
            State = ModuleState.Disabled,
            Order = 900,
            Tags = new[] { "experimental" },
            Features = new List<FeatureDescriptor>
            {
                Feature("plughub.experimental.placeholder.try", "实验入口", "实验入口占位。", "experimental", "experimental", 910, "experimental.try", "experimental")
            }
        };
    }

    public sealed class HiddenModule : StaticSampleModule
    {
        public override ModuleDescriptor Describe() => new ModuleDescriptor
        {
            Id = "plughub.hidden.placeholder",
            Name = "隐藏模块占位",
            Description = "可安装但隐藏。",
            State = ModuleState.Hidden,
            Order = 950,
            Tags = new[] { "hidden" },
            Features = new List<FeatureDescriptor>
            {
                Feature("plughub.hidden.placeholder.info", "隐藏入口", "隐藏入口占位。", "hidden", "hidden", 960, "hidden.info", "hidden")
            }
        };
    }

    public abstract class StaticSampleModule : IPlugHubModule
    {
        public abstract ModuleDescriptor Describe();
        public void Initialize(IModuleContext context) { }
        public void Shutdown() { }

        protected static FeatureDescriptor Feature(
            string id,
            string name,
            string description,
            string category,
            string group,
            int order,
            string commandKey,
            params string[] tags)
        {
            return new FeatureDescriptor
            {
                Id = id,
                ModuleId = ModuleIdFromFeatureId(id),
                Name = name,
                Description = description,
                Category = category,
                Group = group,
                Tags = tags,
                Order = order,
                CommandKey = commandKey,
                ButtonSize = id.Contains("placeholder-two") || id.Contains("placeholder-three") ? "small" : "large"
            };
        }

        private static string ModuleIdFromFeatureId(string featureId)
        {
            var lastDot = featureId.LastIndexOf('.');
            return lastDot > 0 ? featureId.Substring(0, lastDot) : featureId;
        }
    }
}
