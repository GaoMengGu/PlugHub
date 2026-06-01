using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Revit2020.Settings.RibbonDesigner
{
    internal sealed class RibbonLayoutDiffService
    {
        public List<RibbonLayoutDiffRow> Compare(IEnumerable<RibbonDesignerNodeRow> originalTabs, IEnumerable<RibbonDesignerNodeRow> currentTabs)
        {
            var original = Flatten(originalTabs)
                .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var current = Flatten(currentTabs)
                .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var rows = new List<RibbonLayoutDiffRow>();

            foreach (var key in current.Keys.Where(key => !original.ContainsKey(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new RibbonLayoutDiffRow { ChangeType = "新增", Path = DisplayPath(current[key]), Detail = current[key].Text, Impact = "需重启" });
            }

            foreach (var key in original.Keys.Where(key => !current.ContainsKey(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new RibbonLayoutDiffRow { ChangeType = "删除", Path = DisplayPath(original[key]), Detail = original[key].Text, Impact = "需重启" });
            }

            foreach (var key in current.Keys.Where(original.ContainsKey).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                var before = original[key];
                var after = current[key];
                if (!SameUserProperties(before, after))
                {
                    rows.Add(new RibbonLayoutDiffRow
                    {
                        ChangeType = "修改",
                        Path = DisplayPath(after),
                        Detail = before.Text + " -> " + after.Text,
                        Impact = "需重启"
                    });
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(new RibbonLayoutDiffRow { ChangeType = "无变更", Path = "布局", Detail = "当前布局与已加载配置一致", Impact = "无" });
            }

            return rows;
        }

        private static IEnumerable<RibbonDesignerNodeRow> Flatten(IEnumerable<RibbonDesignerNodeRow> rows)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                yield return row;
                foreach (var child in Flatten(row.Children))
                {
                    yield return child;
                }
            }
        }

        private static string Key(RibbonDesignerNodeRow row)
        {
            return string.IsNullOrWhiteSpace(row.FeatureId)
                ? row.NodeType + ":" + row.Id + ":" + row.Text
                : row.NodeType + ":" + row.FeatureId;
        }

        private static bool SameUserProperties(RibbonDesignerNodeRow before, RibbonDesignerNodeRow after)
        {
            return string.Equals(before.NodeType, after.NodeType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(before.Text, after.Text, StringComparison.Ordinal)
                && string.Equals(before.Size, after.Size, StringComparison.OrdinalIgnoreCase)
                && string.Equals(before.IconPath, after.IconPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(before.DefaultFeatureId, after.DefaultFeatureId, StringComparison.OrdinalIgnoreCase);
        }

        private static string DisplayPath(RibbonDesignerNodeRow row)
        {
            return string.IsNullOrWhiteSpace(row.FeatureId)
                ? row.NodeType + " / " + row.Text
                : row.FeatureId + " / " + row.Text;
        }
    }
}
