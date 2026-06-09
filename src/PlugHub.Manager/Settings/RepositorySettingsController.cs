using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Manager.Settings.Rows;

namespace PlugHub.Manager.Settings
{
    internal sealed class RepositorySettingsController
    {
        public IReadOnlyList<RepositoryPackageRow> ApplyPackageFilters(IEnumerable<RepositoryPackageRow> rows, RepositoryPackageFilterState filter)
        {
            var search = (filter.SearchText ?? string.Empty).Trim();
            var state = (filter.Status ?? string.Empty).Trim();
            var repositoryId = (filter.RepositoryId ?? string.Empty).Trim();
            var tagOrCategory = (filter.TagOrCategory ?? string.Empty).Trim();

            var filtered = (rows ?? Enumerable.Empty<RepositoryPackageRow>())
                .Where(row => string.IsNullOrWhiteSpace(search) || ContainsText(row.SearchText, search))
                .Where(row => string.IsNullOrWhiteSpace(repositoryId) || string.Equals(repositoryId, "全部", StringComparison.OrdinalIgnoreCase) || string.Equals(row.RepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase))
                .Where(row => string.IsNullOrWhiteSpace(tagOrCategory) || string.Equals(tagOrCategory, "全部", StringComparison.OrdinalIgnoreCase) || ContainsText(row.TagsText, tagOrCategory) || ContainsText(row.CategoryText, tagOrCategory));

            if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "全部", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row => ContainsText(row.InstallState, state));
            }

            return SortRepositoryPackages(filtered).ToList();
        }

        public IEnumerable<RepositoryPackageRow> SortRepositoryPackages(IEnumerable<RepositoryPackageRow> rows)
        {
            return (rows ?? Enumerable.Empty<RepositoryPackageRow>())
                .OrderBy(row => row.StatusPriority)
                .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase);
        }

        public string PrimaryActionFor(RepositoryPackageRow row)
        {
            var state = row?.InstallState ?? string.Empty;
            if (ContainsText(state, "待重启")) return RepositoryPackageAction.None.ToString();
            if (ContainsText(state, "可更新")) return RepositoryPackageAction.Update.ToString();
            if (ContainsText(state, "未安装")) return RepositoryPackageAction.Install.ToString();
            if (ContainsText(state, "已安装")) return RepositoryPackageAction.Reinstall.ToString();
            return RepositoryPackageAction.None.ToString();
        }

        public string PrimaryActionLabelFor(string action)
        {
            if (string.Equals(action, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return "安装";
            if (string.Equals(action, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return "有更新";
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return "已安装";
            if (string.Equals(action, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)) return "已安装";
            return "需重启";
        }

        public string BuildSearchText(RepositoryPackageRow row)
        {
            if (row == null) return string.Empty;
            return string.Join(" ", new[]
            {
                row.RepositoryId,
                row.RepositoryDisplayName,
                row.PackageId,
                row.ModuleId,
                row.DisplayName,
                row.Description,
                row.Version,
                row.InstallState,
                row.TagsText,
                row.CategoryText
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        public string RepositoryDisplayName(RepositoryRow row)
        {
            if (row == null) return string.Empty;
            return SettingsMetrics.RepositoryDisplayName(row.CustomName, row.Id, row.Repository);
        }

        public int StatusPriority(string installState)
        {
            if (ContainsText(installState, "可更新")) return 10;
            if (ContainsText(installState, "待重启")) return 20;
            if (ContainsText(installState, "已安装")) return 30;
            if (ContainsText(installState, "未安装")) return 40;
            return 90;
        }

        public void PreparePackageRow(RepositoryPackageRow row, IEnumerable<RepositoryRow> repositories)
        {
            if (row == null) return;
            var repository = (repositories ?? Enumerable.Empty<RepositoryRow>())
                .FirstOrDefault(item => string.Equals(item.Id, row.RepositoryId, StringComparison.OrdinalIgnoreCase));
            row.RepositoryDisplayName = repository == null ? row.RepositoryId : RepositoryDisplayName(repository);
            row.StatusPriority = StatusPriority(row.InstallState);
            row.PrimaryAction = PrimaryActionFor(row);
            row.PrimaryActionLabel = PrimaryActionLabelFor(row.PrimaryAction);
            row.SearchText = BuildSearchText(row);
        }

        private static bool ContainsText(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class RepositoryPackageFilterState
    {
        public string SearchText { get; set; } = string.Empty;
        public string Status { get; set; } = "全部";
        public string RepositoryId { get; set; } = "全部";
        public string TagOrCategory { get; set; } = "全部";
    }

    internal enum RepositoryPackageAction
    {
        None,
        Install,
        Update,
        Reinstall,
        Uninstall
    }
}
