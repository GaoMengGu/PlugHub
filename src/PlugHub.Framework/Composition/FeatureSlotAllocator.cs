using System;
using System.Collections.Generic;

namespace PlugHub.Framework.Composition
{
    public sealed class FeatureSlotAllocator
    {
        public FeatureSlotAssignment Allocate(IReadOnlyList<FeatureViewModel> orderedFeatures, int maxSlots)
        {
            if (maxSlots < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlots), "Feature slot capacity must be greater than zero.");
            }

            var slotToFeatureId = new Dictionary<int, string>();
            var featureIdToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var skippedFeatureIds = new List<string>();
            var seenFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var slotId = 1;

            foreach (var feature in orderedFeatures ?? new List<FeatureViewModel>())
            {
                if (feature == null || string.IsNullOrWhiteSpace(feature.FeatureId))
                {
                    continue;
                }

                var featureId = feature.FeatureId.Trim();
                if (!seenFeatureIds.Add(featureId))
                {
                    continue;
                }

                if (slotId > maxSlots)
                {
                    skippedFeatureIds.Add(featureId);
                    continue;
                }

                slotToFeatureId[slotId] = featureId;
                featureIdToSlot[featureId] = slotId;
                slotId++;
            }

            return new FeatureSlotAssignment(slotToFeatureId, featureIdToSlot, skippedFeatureIds);
        }
    }

    public sealed class FeatureSlotAssignment
    {
        internal FeatureSlotAssignment(
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
