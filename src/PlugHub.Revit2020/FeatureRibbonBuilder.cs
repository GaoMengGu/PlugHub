using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020
{
    internal sealed class FeatureRibbonBuilder
    {
        private static readonly string[] SameNameIconExtensions = { ".png", ".ico", ".jpg", ".jpeg", ".bmp" };
        private readonly string _assemblyPath;
        private readonly string _baseDirectory;

        public FeatureRibbonBuilder(string assemblyPath, string baseDirectory)
        {
            _assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        }

        public void Build(UIControlledApplication application, ViewConfiguration view, FeatureViewCompositionResult composition)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (composition == null) throw new ArgumentNullException(nameof(composition));

            var tabName = SafeDisplayName(view.Ribbon?.TabName, "PlugHub");
            var fallbackPanelName = SafeDisplayName(view.Ribbon?.FallbackPanelName, "Framework");
            EnsureRibbonTab(application, tabName);
            AddFrameworkButtons(GetOrCreatePanel(application, tabName, "框架"));

            if (!composition.Features.Any())
            {
                FeatureSlotRegistry.Replace(new Dictionary<int, string>(), new List<string>());
                return;
            }

            var layout = new RibbonLayoutComposer().Compose(view, composition.Features);
            var slotAssignments = BuildSlotAssignments(layout.ClickableFeatures);
            FeatureSlotRegistry.Replace(slotAssignments.SlotToFeatureId, slotAssignments.SkippedFeatureIds);

            foreach (var skippedFeatureId in slotAssignments.SkippedFeatureIds)
            {
                Trace.TraceWarning("PH-FEATURE-SLOT-LIMIT: Feature was not assigned a Revit command slot: " + skippedFeatureId);
            }

            foreach (var panelModel in layout.Panels)
            {
                var panelName = SafeDisplayName(panelModel.Name, fallbackPanelName);
                var panel = GetOrCreatePanel(application, tabName, panelName);
                AddRibbonLayoutItems(panel, panelModel.Items, slotAssignments.FeatureIdToSlot);
            }
        }

        private void AddFrameworkButtons(RibbonPanel panel)
        {
            AddFrameworkButton(
                panel,
                "PlugHub_Framework_Settings",
                "设置",
                typeof(FrameworkSettingsCommand),
                "打开 PlugHub Windows 设置程序。",
                "在 Windows 下打开完整设置窗口，用于管理插件、仓库、更新和 Ribbon 布局。",
                "settings");
            AddFrameworkButton(
                panel,
                "PlugHub_Framework_Status",
                "状态",
                typeof(FrameworkStatusCommand),
                "查看当前 PlugHub 运行状态。",
                "显示当前 Revit 会话已加载的配置、模块、功能和日志数量；配置变更通常需要重启 Revit 后重绘 Ribbon。",
                "diagnostics");
        }

        private void AddFrameworkButton(RibbonPanel panel, string buttonName, string text, Type commandType, string tooltip, string longDescription, string iconKey)
        {
            if (panel.GetItems().Any(item => string.Equals(item.Name, buttonName, StringComparison.OrdinalIgnoreCase))) return;

            var data = new PushButtonData(
                buttonName,
                text,
                _assemblyPath,
                commandType.FullName);

            data.ToolTip = tooltip;
            data.LongDescription = longDescription;
            data.Image = DefaultRibbonIconProvider.CreateSmallIcon(iconKey);
            data.LargeImage = DefaultRibbonIconProvider.CreateLargeIcon(iconKey);
            panel.AddItem(data);
        }

        private void AddRibbonLayoutItems(RibbonPanel panel, IEnumerable<RibbonItemViewModel> items, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            var smallPushButtons = new List<RibbonItemData>();
            foreach (var item in items ?? new List<RibbonItemViewModel>())
            {
                if (item == null)
                {
                    continue;
                }

                if (IsSmallPushButton(item))
                {
                    var smallData = CreateRibbonItemData(item, featureIdToSlot);
                    if (smallData == null || ContainsRibbonItem(panel, smallData.Name))
                    {
                        continue;
                    }

                    smallPushButtons.Add(smallData);
                    if (smallPushButtons.Count == 3)
                    {
                        FlushSmallPushButtons(panel, smallPushButtons);
                    }

                    continue;
                }

                FlushSmallPushButtons(panel, smallPushButtons);

                if (IsRibbonItemType(item, RibbonItemViewModel.Stack))
                {
                    AddStackLayout(panel, item, featureIdToSlot);
                    continue;
                }

                var data = CreateRibbonItemData(item, featureIdToSlot);
                if (data == null || ContainsRibbonItem(panel, data.Name))
                {
                    continue;
                }

                var added = panel.AddItem(data);
                PopulateContainer(added, item, featureIdToSlot);
            }

            FlushSmallPushButtons(panel, smallPushButtons);
        }

        private RibbonItemData? CreateRibbonItemData(RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            if (item == null) return null;
            if (IsRibbonItemType(item, RibbonItemViewModel.PushButton))
            {
                return CreateFeatureButtonData(item, featureIdToSlot);
            }

            if (IsRibbonItemType(item, RibbonItemViewModel.PulldownButton))
            {
                return CreateContainerButtonData(
                    item,
                    new PulldownButtonData(
                        SafeContainerName(item),
                        SafeDisplayName(item.Text, SafeDisplayName(item.Id, "Menu"))));
            }

            if (IsRibbonItemType(item, RibbonItemViewModel.SplitButton))
            {
                return CreateContainerButtonData(
                    item,
                    new SplitButtonData(
                        SafeContainerName(item),
                        SafeDisplayName(item.Text, SafeDisplayName(item.Id, "Split"))));
            }

            Trace.TraceWarning("PH-RIBBON-ITEM-SKIPPED: Unsupported ribbon item type: " + item.Type);
            return null;
        }

        private PulldownButtonData CreateContainerButtonData(RibbonItemViewModel item, PulldownButtonData data)
        {
            ApplyRibbonItemIcon(data, item.IconPath);
            return data;
        }

        private SplitButtonData CreateContainerButtonData(RibbonItemViewModel item, SplitButtonData data)
        {
            ApplyRibbonItemIcon(data, item.IconPath);
            return data;
        }

        private void ApplyRibbonItemIcon(PulldownButtonData data, string iconPath)
        {
            data.Image = LoadConfiguredIcon(iconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
            data.LargeImage = LoadConfiguredIcon(iconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();
        }

        private void ApplyRibbonItemIcon(SplitButtonData data, string iconPath)
        {
            data.Image = LoadConfiguredIcon(iconPath, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
            data.LargeImage = LoadConfiguredIcon(iconPath, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();
        }

        private void PopulateContainer(RibbonItem added, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            if (added is PulldownButton pulldown)
            {
                AddPulldownButton(pulldown, item, featureIdToSlot);
                return;
            }

            if (added is SplitButton split)
            {
                AddSplitButton(split, item, featureIdToSlot);
            }
        }

        private void AddPulldownButton(PulldownButton pulldown, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            foreach (var child in item.Items ?? new List<RibbonItemViewModel>())
            {
                var data = CreateFeatureButtonData(child, featureIdToSlot);
                if (data == null)
                {
                    continue;
                }

                pulldown.AddPushButton(data);
            }
        }

        private void AddSplitButton(SplitButton split, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            foreach (var child in item.Items ?? new List<RibbonItemViewModel>())
            {
                var data = CreateFeatureButtonData(child, featureIdToSlot);
                if (data == null)
                {
                    continue;
                }

                split.AddPushButton(data);
            }
        }

        private void AddStackLayout(RibbonPanel panel, RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            var children = item.Items ?? new List<RibbonItemViewModel>();
            if (children.Count == 1)
            {
                AddSingleStackChildFallback(panel, children[0], featureIdToSlot);
                return;
            }

            if (children.Count < 2 || children.Count > 3)
            {
                Trace.TraceWarning("PH-RIBBON-STACK-SKIPPED: Stack item requires 2 or 3 child items: " + SafeDisplayName(item.Id, item.Text));
                return;
            }

            var data = children
                .Select(child => CreateRibbonItemData(child, featureIdToSlot))
                .ToList();
            if (data.Any(childData => childData == null))
            {
                Trace.TraceWarning("PH-RIBBON-STACK-SKIPPED: Stack item contains child item without valid ribbon data: " + SafeDisplayName(item.Id, item.Text));
                return;
            }

            var typedData = data
                .Cast<RibbonItemData>()
                .ToList();
            if (typedData.Any(childData => ContainsRibbonItem(panel, childData.Name)))
            {
                Trace.TraceWarning("PH-RIBBON-STACK-SKIPPED: Stack item contains an item already present on the panel: " + SafeDisplayName(item.Id, item.Text));
                return;
            }

            var addedItems = AddStackItemData(panel, typedData);
            for (var index = 0; index < addedItems.Count && index < children.Count; index++)
            {
                PopulateContainer(addedItems[index], children[index], featureIdToSlot);
            }
        }

        private void AddSingleStackChildFallback(RibbonPanel panel, RibbonItemViewModel child, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            if (IsRibbonItemType(child, RibbonItemViewModel.Stack))
            {
                AddStackLayout(panel, child, featureIdToSlot);
                return;
            }

            var data = CreateRibbonItemData(child, featureIdToSlot);
            if (data == null || ContainsRibbonItem(panel, data.Name))
            {
                Trace.TraceWarning("PH-RIBBON-STACK-FALLBACK-SKIPPED: Single stack child has no valid ribbon data: " + SafeDisplayName(child?.Id, SafeDisplayName(child?.Text, "child")));
                return;
            }

            var added = panel.AddItem(data);
            PopulateContainer(added, child, featureIdToSlot);
        }

        private static IList<RibbonItem> AddStackItemData(RibbonPanel panel, IReadOnlyList<RibbonItemData> data)
        {
            if (data.Count == 2)
            {
                return panel.AddStackedItems(data[0], data[1]);
            }

            if (data.Count == 3)
            {
                return panel.AddStackedItems(data[0], data[1], data[2]);
            }

            return new List<RibbonItem>();
        }

        private static void FlushSmallPushButtons(RibbonPanel panel, List<RibbonItemData> smallPushButtons)
        {
            if (smallPushButtons.Count == 0) return;
            if (smallPushButtons.Count == 1)
            {
                if (!ContainsRibbonItem(panel, smallPushButtons[0].Name))
                {
                    panel.AddItem(smallPushButtons[0]);
                }

                smallPushButtons.Clear();
                return;
            }

            AddStackItemData(panel, smallPushButtons);
            smallPushButtons.Clear();
        }

        private PushButtonData? CreateFeatureButtonData(RibbonItemViewModel item, IReadOnlyDictionary<string, int> featureIdToSlot)
        {
            if (item == null || item.Feature == null || string.IsNullOrWhiteSpace(item.Feature.FeatureId))
            {
                return null;
            }

            var feature = item.Feature;
            if (!featureIdToSlot.TryGetValue(feature.FeatureId, out var slotId))
            {
                return null;
            }

            var buttonName = SafeInternalName(feature.FeatureId);
            var commandType = FrameworkFeatureCommandSlots.CommandTypeFor(slotId);
            var iconPath = string.IsNullOrWhiteSpace(item.IconPath) ? feature.IconPath : item.IconPath;
            var data = new PushButtonData(
                buttonName,
                SafeDisplayName(item.Text, SafeDisplayName(feature.DisplayName, "Feature")),
                _assemblyPath,
                commandType.FullName);

            data.ToolTip = BuildToolTip(feature);
            data.LongDescription = feature.Description;
            data.Image = LoadFeatureIcon(iconPath, feature.CommandAssembly, false) ?? DefaultRibbonIconProvider.CreateSmallIcon();
            data.LargeImage = LoadFeatureIcon(iconPath, feature.CommandAssembly, true) ?? DefaultRibbonIconProvider.CreateLargeIcon();

            return data;
        }

        private static void EnsureRibbonTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Revit throws when a tab already exists. Existing tabs are acceptable during startup.
            }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            var existing = application.GetRibbonPanels(tabName).FirstOrDefault(panel => string.Equals(panel.Name, panelName, StringComparison.OrdinalIgnoreCase));
            return existing ?? application.CreateRibbonPanel(tabName, panelName);
        }

        private static bool ContainsRibbonItem(RibbonPanel panel, string name)
        {
            return panel.GetItems().Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeDisplayName(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
        }

        private static string SafeContainerName(RibbonItemViewModel item)
        {
            var id = SafeDisplayName(item.Id, SafeDisplayName(item.Text, item.Type));
            return SafeInternalName(item.Type + "_" + id);
        }

        private static string SafeInternalName(string value)
        {
            var builder = new StringBuilder("PlugHub_");
            foreach (var ch in value ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.Length == "PlugHub_".Length ? "PlugHub_Feature" : builder.ToString();
        }

        private static string BuildToolTip(FeatureViewModel feature)
        {
            return string.IsNullOrWhiteSpace(feature.Description) ? string.Empty : feature.Description.Trim();
        }

        private static bool IsRibbonItemType(RibbonItemViewModel item, string type)
        {
            return string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSmallPushButton(RibbonItemViewModel item)
        {
            return IsRibbonItemType(item, RibbonItemViewModel.PushButton) && IsSmall(item.Size);
        }

        private static bool IsSmall(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase);
        }

        private static SlotAssignmentResult BuildSlotAssignments(IReadOnlyList<FeatureViewModel> orderedFeatures)
        {
            var slotToFeatureId = new Dictionary<int, string>();
            var featureIdToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var skippedFeatureIds = new List<string>();

            var slotId = 1;
            foreach (var feature in orderedFeatures ?? new List<FeatureViewModel>())
            {
                if (string.IsNullOrWhiteSpace(feature.FeatureId))
                {
                    continue;
                }

                if (featureIdToSlot.ContainsKey(feature.FeatureId))
                {
                    continue;
                }

                if (slotId > FeatureSlotRegistry.MaxSlots)
                {
                    skippedFeatureIds.Add(feature.FeatureId);
                    continue;
                }

                slotToFeatureId[slotId] = feature.FeatureId;
                featureIdToSlot[feature.FeatureId] = slotId;
                slotId++;
            }

            return new SlotAssignmentResult(slotToFeatureId, featureIdToSlot, skippedFeatureIds);
        }

        private ImageSource? LoadFeatureIcon(string iconPath, string commandAssembly, bool large)
        {
            return LoadConfiguredIcon(iconPath, large) ?? LoadDllSiblingIcon(commandAssembly, large);
        }

        private ImageSource? LoadDllSiblingIcon(string commandAssembly, bool large)
        {
            if (string.IsNullOrWhiteSpace(commandAssembly)) return null;
            var resolvedAssembly = Path.IsPathRooted(commandAssembly)
                ? commandAssembly
                : Path.GetFullPath(Path.Combine(_baseDirectory, commandAssembly));
            var directory = Path.GetDirectoryName(resolvedAssembly);
            var stem = Path.GetFileNameWithoutExtension(resolvedAssembly);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem)) return null;

            foreach (var extension in SameNameIconExtensions)
            {
                var icon = LoadConfiguredIcon(Path.Combine(directory, stem + extension), large);
                if (icon != null) return icon;
            }

            return null;
        }

        private ImageSource? LoadConfiguredIcon(string iconPath, bool large)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;
            if (DefaultRibbonIconProvider.TryCreateIcon(iconPath, large, out var builtinIcon))
            {
                return builtinIcon;
            }

            var resolvedPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.GetFullPath(Path.Combine(_baseDirectory, iconPath));
            if (!File.Exists(resolvedPath)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private sealed class SlotAssignmentResult
        {
            public SlotAssignmentResult(
                IReadOnlyDictionary<int, string> slotToFeatureId,
                IReadOnlyDictionary<string, int> featureIdToSlot,
                IReadOnlyList<string> skippedFeatureIds)
            {
                SlotToFeatureId = slotToFeatureId ?? throw new ArgumentNullException(nameof(slotToFeatureId));
                FeatureIdToSlot = featureIdToSlot ?? throw new ArgumentNullException(nameof(featureIdToSlot));
                SkippedFeatureIds = skippedFeatureIds ?? throw new ArgumentNullException(nameof(skippedFeatureIds));
            }

            public IReadOnlyDictionary<int, string> SlotToFeatureId { get; }
            public IReadOnlyDictionary<string, int> FeatureIdToSlot { get; }
            public IReadOnlyList<string> SkippedFeatureIds { get; }
        }
    }
}
