using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Framework.RibbonEditing
{
    public enum RibbonDesignerDropPlacement
    {
        Before,
        Inside,
        After
    }

    public sealed class RibbonDesignerDropPlan
    {
        public bool IsAllowed { get; set; }
        public string Message { get; set; } = string.Empty;
        public RibbonDesignerNodeRow? Parent { get; set; }
        public RibbonDesignerNodeRow? Sibling { get; set; }
        public RibbonDesignerDropPlacement Placement { get; set; } = RibbonDesignerDropPlacement.Inside;
    }

    public sealed class RibbonDesignerDropService
    {
        public RibbonDesignerDropPlan PlanFeatureDrop(IEnumerable<RibbonDesignerNodeRow> roots, RibbonDesignerFeatureRow feature, RibbonDesignerNodeRow target)
        {
            if (feature == null) return Reject("请选择一个功能。");
            if (target == null) return Reject("请选择一个有效的放置位置。");
            if (IsFeaturePlaced(roots, feature.FeatureId)) return Reject("该功能已在布局中，不能重复添加。");
            if (!CanContainFeature(target)) return Reject("功能只能放入面板、下拉、拆分或未满的堆叠控件。");
            return Allow(target, null, RibbonDesignerDropPlacement.Inside);
        }

        public RibbonDesignerDropPlan PlanNodeMove(IEnumerable<RibbonDesignerNodeRow> roots, RibbonDesignerNodeRow source, RibbonDesignerNodeRow target)
        {
            if (source == null || target == null || ReferenceEquals(source, target)) return Reject("请选择不同的拖拽目标。");
            if (ContainsNode(source.Children, target)) return Reject("不能把布局项拖到自己的子项中。");
            if (!CanContainNode(target, source)) return Reject(ContainmentMessage(target, source));
            return Allow(target, null, RibbonDesignerDropPlacement.Inside);
        }

        public bool ApplyDrop(IList<RibbonDesignerNodeRow> roots, RibbonDesignerNodeRow node, RibbonDesignerDropPlan plan)
        {
            if (roots == null || node == null || plan == null || !plan.IsAllowed || plan.Parent == null) return false;
            RemoveNode(roots, node);
            InsertNode(plan.Parent, node, plan.Sibling, plan.Placement);
            return true;
        }

        public bool InsertRibbonDesignerNode(RibbonDesignerNodeRow node, RibbonDesignerDropPlan plan)
        {
            if (node == null || plan == null || !plan.IsAllowed || plan.Parent == null) return false;
            InsertNode(plan.Parent, node, plan.Sibling, plan.Placement);
            return true;
        }

        public bool CanContainFeature(RibbonDesignerNodeRow target)
        {
            if (target == null) return false;
            if (IsType(target, RibbonDesignerNodeRow.Panel)) return true;
            if (IsType(target, RibbonDesignerNodeRow.PulldownButton)) return true;
            if (IsType(target, RibbonDesignerNodeRow.SplitButton)) return true;
            return IsType(target, RibbonDesignerNodeRow.Stack) && target.Children.Count < 3;
        }

        public bool CanContainNode(RibbonDesignerNodeRow parent, RibbonDesignerNodeRow child)
        {
            if (parent == null || child == null) return false;
            if (IsType(parent, RibbonDesignerNodeRow.Tab)) return IsType(child, RibbonDesignerNodeRow.Panel);
            if (IsType(parent, RibbonDesignerNodeRow.Panel)) return !IsType(child, RibbonDesignerNodeRow.Tab) && !IsType(child, RibbonDesignerNodeRow.Panel);
            if (IsType(parent, RibbonDesignerNodeRow.Stack)) return CanStackContainNode(parent, child);
            if (IsType(parent, RibbonDesignerNodeRow.PulldownButton) || IsType(parent, RibbonDesignerNodeRow.SplitButton)) return CanPulldownOrSplitContainNode(child);
            return false;
        }

        private static bool CanStackContainNode(RibbonDesignerNodeRow stack, RibbonDesignerNodeRow child)
        {
            return (stack.Children.Contains(child) || stack.Children.Count < 3)
                && (IsType(child, RibbonDesignerNodeRow.PushButton)
                    || IsType(child, RibbonDesignerNodeRow.PulldownButton)
                    || IsType(child, RibbonDesignerNodeRow.SplitButton));
        }

        private static bool CanPulldownOrSplitContainNode(RibbonDesignerNodeRow child)
        {
            return IsType(child, RibbonDesignerNodeRow.PushButton);
        }

        private static string ContainmentMessage(RibbonDesignerNodeRow parent, RibbonDesignerNodeRow child)
        {
            if (IsType(parent, RibbonDesignerNodeRow.Stack) && IsType(child, RibbonDesignerNodeRow.Stack))
            {
                return "不能在堆叠中嵌套堆叠。堆叠只能包含常规按钮、下拉按钮或拆分按钮。";
            }

            if (IsType(parent, RibbonDesignerNodeRow.PulldownButton) || IsType(parent, RibbonDesignerNodeRow.SplitButton))
            {
                return "下拉按钮和拆分按钮内部只能放常规按钮，不能继续嵌套容器。";
            }

            return "该布局项不能放入目标容器。";
        }

        public bool IsFeaturePlaced(IEnumerable<RibbonDesignerNodeRow> roots, string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId)) return false;
            return Flatten(roots).Any(node => string.Equals(node.FeatureId, featureId, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<RibbonDesignerNodeRow> Flatten(IEnumerable<RibbonDesignerNodeRow> roots)
        {
            foreach (var root in roots ?? new List<RibbonDesignerNodeRow>())
            {
                yield return root;
                foreach (var child in Flatten(root.Children))
                {
                    yield return child;
                }
            }
        }

        private static bool ContainsNode(IEnumerable<RibbonDesignerNodeRow> rows, RibbonDesignerNodeRow target)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                if (ReferenceEquals(row, target)) return true;
                if (ContainsNode(row.Children, target)) return true;
            }

            return false;
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

        private static void InsertNode(RibbonDesignerNodeRow parent, RibbonDesignerNodeRow node, RibbonDesignerNodeRow? sibling, RibbonDesignerDropPlacement placement)
        {
            if (sibling == null || !parent.Children.Contains(sibling))
            {
                parent.Children.Add(node);
                return;
            }

            var index = parent.Children.IndexOf(sibling);
            if (placement == RibbonDesignerDropPlacement.After)
            {
                index++;
            }

            if (index < 0 || index > parent.Children.Count)
            {
                parent.Children.Add(node);
                return;
            }

            parent.Children.Insert(index, node);
        }

        private static bool IsType(RibbonDesignerNodeRow node, string type)
        {
            return node != null && string.Equals(node.NodeType, type, StringComparison.OrdinalIgnoreCase);
        }

        private static RibbonDesignerDropPlan Allow(RibbonDesignerNodeRow? parent, RibbonDesignerNodeRow? sibling, RibbonDesignerDropPlacement placement)
        {
            return new RibbonDesignerDropPlan { IsAllowed = true, Parent = parent, Sibling = sibling, Placement = placement };
        }

        private static RibbonDesignerDropPlan Reject(string message)
        {
            return new RibbonDesignerDropPlan { IsAllowed = false, Message = message ?? string.Empty };
        }
    }
}
