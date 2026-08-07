using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.RibbonEditing
{
    public sealed class RibbonLayoutEditor
    {
        private const string DefaultPanelId = "default";
        private const string DefaultPanelName = "默认";
        private readonly RibbonDesignerMapper _mapper = new RibbonDesignerMapper();

        public List<RibbonDesignerNodeRow> Load(RibbonConfiguration ribbon, IEnumerable<RibbonDesignerFeatureRow> features)
        {
            ribbon = ribbon ?? new RibbonConfiguration();
            var featureRows = VisibleFeatures(features);
            var normalizedRibbon = new RibbonConfiguration
            {
                TabName = ribbon.TabName,
                FallbackPanelName = ribbon.FallbackPanelName,
                LayoutVersion = ribbon.LayoutVersion,
                Panels = MergePanelsByDisplayName(ribbon.Panels)
            };
            var tabs = _mapper.FromConfiguration(normalizedRibbon, featureRows);
            Synchronize(tabs, featureRows);
            return tabs;
        }

        public void Synchronize(IList<RibbonDesignerNodeRow> tabs, IEnumerable<RibbonDesignerFeatureRow> features)
        {
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            var featureRows = VisibleFeatures(features);
            RemoveUnavailableFeatures(tabs, new HashSet<string>(featureRows.Select(feature => feature.FeatureId), StringComparer.OrdinalIgnoreCase));
            EnsureAllVisibleFeatures(tabs, featureRows);
            NormalizeChildSizes(tabs);
        }

        public List<RibbonPanelLayoutConfiguration> PrepareForSave(
            IList<RibbonDesignerNodeRow> tabs,
            IEnumerable<RibbonDesignerFeatureRow> features)
        {
            Synchronize(tabs, features);
            NormalizeStacks(tabs);
            Validate(tabs);
            return _mapper.ToPanels(tabs, VisibleFeatures(features));
        }

        public bool RemoveContainer(IList<RibbonDesignerNodeRow> tabs, RibbonDesignerNodeRow container)
        {
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            if (container == null) return false;
            if (!Flatten(tabs).Any(node => ReferenceEquals(node, container))) return false;
            var featureNodes = Flatten(new[] { container })
                .Where(node => RibbonDesignerMapper.IsType(node, RibbonDesignerNodeRow.PushButton))
                .Where(node => !string.IsNullOrWhiteSpace(node.FeatureId))
                .ToList();
            if (featureNodes.Count > 0)
            {
                var panel = EnsureDefaultPanel(tabs);
                foreach (var source in featureNodes)
                {
                    panel.Children.Add(new RibbonDesignerNodeRow
                    {
                        NodeType = RibbonDesignerNodeRow.PushButton,
                        Id = source.Id,
                        Text = source.Text,
                        FeatureId = source.FeatureId,
                        Size = "large",
                        IconPath = source.IconPath,
                        Order = (panel.Children.Count + 1) * 100,
                        RequiresRestart = true,
                        StatusText = "需重启"
                    });
                }
            }

            return RemoveNode(tabs, container);
        }

        public void Validate(IEnumerable<RibbonDesignerNodeRow> tabs)
        {
            var nestedStack = FindNestedStack(tabs, false);
            if (nestedStack != null)
            {
                throw new InvalidOperationException("堆叠控件不能嵌套堆叠: " + SafeText(nestedStack.Text, SafeText(nestedStack.Id, "堆叠")));
            }

            var invalidStack = Flatten(tabs)
                .FirstOrDefault(row => RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack)
                    && row.Children.Count != 2
                    && row.Children.Count != 3);
            if (invalidStack != null)
            {
                throw new InvalidOperationException("堆叠控件需要包含 2 或 3 个按钮: " + SafeText(invalidStack.Text, SafeText(invalidStack.Id, "堆叠")));
            }

            var duplicates = Flatten(tabs)
                .Where(row => !string.IsNullOrWhiteSpace(row.FeatureId))
                .GroupBy(row => row.FeatureId, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(featureId => featureId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException("布局中存在重复功能: " + string.Join(", ", duplicates));
            }
        }

        public string InferButtonSize(RibbonDesignerNodeRow parent, RibbonDesignerNodeRow child)
        {
            if (child == null) return "large";
            if (RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.Panel)
                || RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.Tab))
            {
                return "large";
            }

            return parent != null && RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.Panel) ? "large" : "small";
        }

        private static List<RibbonDesignerFeatureRow> VisibleFeatures(IEnumerable<RibbonDesignerFeatureRow> features)
        {
            return (features ?? new List<RibbonDesignerFeatureRow>())
                .Where(feature => feature != null && feature.Visible && !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<RibbonPanelLayoutConfiguration> MergePanelsByDisplayName(IEnumerable<RibbonPanelLayoutConfiguration> panels)
        {
            var result = new List<RibbonPanelLayoutConfiguration>();
            var panelsByName = new Dictionary<string, RibbonPanelLayoutConfiguration>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in panels ?? new List<RibbonPanelLayoutConfiguration>())
            {
                var panel = ClonePanel(source);
                var key = SafeText(panel.Name, SafeText(panel.Id, "默认工具"));
                if (!panelsByName.TryGetValue(key, out var existing))
                {
                    panelsByName[key] = panel;
                    result.Add(panel);
                    continue;
                }

                var existingFeatureIds = new HashSet<string>(CollectFeatureIds(existing.Items), StringComparer.OrdinalIgnoreCase);
                foreach (var item in panel.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.FeatureId) && !existingFeatureIds.Add(item.FeatureId))
                    {
                        continue;
                    }

                    existing.Items.Add(item);
                }
            }

            return result;
        }

        private static RibbonPanelLayoutConfiguration ClonePanel(RibbonPanelLayoutConfiguration panel)
        {
            panel = panel ?? new RibbonPanelLayoutConfiguration();
            return new RibbonPanelLayoutConfiguration
            {
                Id = panel.Id ?? string.Empty,
                Name = panel.Name ?? string.Empty,
                Order = panel.Order,
                Items = (panel.Items ?? new List<RibbonItemLayoutConfiguration>()).Select(CloneItem).ToList()
            };
        }

        private static RibbonItemLayoutConfiguration CloneItem(RibbonItemLayoutConfiguration item)
        {
            item = item ?? new RibbonItemLayoutConfiguration();
            return new RibbonItemLayoutConfiguration
            {
                Type = item.Type ?? string.Empty,
                Id = item.Id ?? string.Empty,
                Text = item.Text ?? string.Empty,
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = item.Size ?? string.Empty,
                IconPath = item.IconPath ?? string.Empty,
                TextOverride = item.TextOverride ?? string.Empty,
                IconPathOverride = item.IconPathOverride ?? string.Empty,
                Order = item.Order,
                Items = (item.Items ?? new List<RibbonItemLayoutConfiguration>()).Select(CloneItem).ToList()
            };
        }

        private static IEnumerable<string> CollectFeatureIds(IEnumerable<RibbonItemLayoutConfiguration> items)
        {
            foreach (var item in items ?? new List<RibbonItemLayoutConfiguration>())
            {
                if (!string.IsNullOrWhiteSpace(item.FeatureId)) yield return item.FeatureId;
                foreach (var featureId in CollectFeatureIds(item.Items)) yield return featureId;
            }
        }

        private static void RemoveUnavailableFeatures(IList<RibbonDesignerNodeRow> roots, ISet<string> visibleFeatureIds)
        {
            for (var index = roots.Count - 1; index >= 0; index--)
            {
                var row = roots[index];
                if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton)
                    && !string.IsNullOrWhiteSpace(row.FeatureId)
                    && !visibleFeatureIds.Contains(row.FeatureId))
                {
                    roots.RemoveAt(index);
                    continue;
                }

                RemoveUnavailableFeatures(row.Children, visibleFeatureIds);
                if (IsEmptyContainer(row)) roots.RemoveAt(index);
            }
        }

        private static bool IsEmptyContainer(RibbonDesignerNodeRow row)
        {
            return row != null
                && row.Children.Count == 0
                && (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)
                    || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PulldownButton)
                    || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton)
                    || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack));
        }

        private static void EnsureAllVisibleFeatures(IList<RibbonDesignerNodeRow> tabs, IEnumerable<RibbonDesignerFeatureRow> features)
        {
            var placedFeatureIds = new HashSet<string>(
                Flatten(tabs).Where(row => !string.IsNullOrWhiteSpace(row.FeatureId)).Select(row => row.FeatureId),
                StringComparer.OrdinalIgnoreCase);
            var missing = features.Where(feature => !placedFeatureIds.Contains(feature.FeatureId)).ToList();
            if (missing.Count == 0) return;

            var panel = EnsureDefaultPanel(tabs);
            foreach (var feature in missing)
            {
                panel.Children.Add(RibbonDesignerMapper.CreateFeatureNode(feature, (panel.Children.Count + 1) * 100));
            }
        }

        private static RibbonDesignerNodeRow EnsureDefaultPanel(IList<RibbonDesignerNodeRow> tabs)
        {
            var tab = tabs.FirstOrDefault();
            if (tab == null)
            {
                tab = new RibbonDesignerNodeRow { NodeType = RibbonDesignerNodeRow.Tab, Id = "tab", Text = "PlugHub", Order = 100 };
                tabs.Add(tab);
            }

            var panel = tab.Children.FirstOrDefault(row =>
                RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)
                && (string.Equals(row.Id, DefaultPanelId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.Text, DefaultPanelName, StringComparison.OrdinalIgnoreCase)));
            if (panel == null)
            {
                panel = RibbonDesignerMapper.CreateContainerNode(
                    RibbonDesignerNodeRow.Panel,
                    DefaultPanelName,
                    (tab.Children.Count + 1) * 100);
                tab.Children.Add(panel);
            }

            panel.Id = DefaultPanelId;
            panel.Text = DefaultPanelName;
            return panel;
        }

        private void NormalizeChildSizes(IEnumerable<RibbonDesignerNodeRow> roots)
        {
            foreach (var parent in roots ?? new List<RibbonDesignerNodeRow>())
            {
                foreach (var child in parent.Children)
                {
                    child.Size = InferButtonSize(parent, child);
                    if (!RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.PushButton)) child.IconPath = string.Empty;
                }

                NormalizeChildSizes(parent.Children);
            }
        }

        private static void NormalizeStacks(IList<RibbonDesignerNodeRow> rows)
        {
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                var row = rows[index];
                NormalizeStacks(row.Children);
                if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack)) continue;
                if (row.Children.Count == 0)
                {
                    rows.RemoveAt(index);
                }
                else if (row.Children.Count == 1)
                {
                    var child = row.Children[0];
                    child.Order = row.Order;
                    rows[index] = child;
                }
            }
        }

        private static RibbonDesignerNodeRow? FindNestedStack(IEnumerable<RibbonDesignerNodeRow> rows, bool insideStack)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                var isStack = RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack);
                if (insideStack && isStack) return row;
                var nested = FindNestedStack(row.Children, insideStack || isStack);
                if (nested != null) return nested;
            }

            return null;
        }

        private static IEnumerable<RibbonDesignerNodeRow> Flatten(IEnumerable<RibbonDesignerNodeRow> roots)
        {
            foreach (var root in roots ?? new List<RibbonDesignerNodeRow>())
            {
                yield return root;
                foreach (var child in Flatten(root.Children)) yield return child;
            }
        }

        private static bool RemoveNode(IList<RibbonDesignerNodeRow> roots, RibbonDesignerNodeRow target)
        {
            if (roots.Remove(target)) return true;
            foreach (var root in roots)
            {
                if (RemoveNode(root.Children, target)) return true;
            }

            return false;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty).Trim() : value.Trim();
        }
    }
}
