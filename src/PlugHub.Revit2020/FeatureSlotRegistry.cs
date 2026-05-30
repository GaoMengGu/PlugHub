using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Revit2020
{
    internal static class FeatureSlotRegistry
    {
        public const int MaxSlots = 128;
        private static readonly object Sync = new object();
        private static Dictionary<int, string> _slotToFeatureId = new Dictionary<int, string>();
        private static IReadOnlyList<string> _skippedFeatureIds = new List<string>();

        public static IReadOnlyList<string> SkippedFeatureIds
        {
            get
            {
                lock (Sync)
                {
                    return _skippedFeatureIds.ToList();
                }
            }
        }

        public static void Replace(IReadOnlyDictionary<int, string> slotToFeatureId, IReadOnlyList<string> skippedFeatureIds)
        {
            lock (Sync)
            {
                _slotToFeatureId = (slotToFeatureId ?? new Dictionary<int, string>())
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                _skippedFeatureIds = new List<string>(skippedFeatureIds ?? new List<string>());
            }
        }

        public static bool TryGetFeatureId(int slotId, out string featureId)
        {
            lock (Sync)
            {
                if (_slotToFeatureId.TryGetValue(slotId, out var value))
                {
                    featureId = value;
                    return true;
                }

                featureId = string.Empty;
                return false;
            }
        }
    }
}
