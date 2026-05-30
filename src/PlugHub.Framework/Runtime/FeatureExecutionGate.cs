using System;
using System.Linq;
using PlugHub.Contracts.Features;

namespace PlugHub.Framework.Runtime
{
    public sealed class FeatureExecutionGate
    {
        public FeatureExecutionDecision CanExecuteFeatureId(string featureId)
        {
            return CanExecute(featureId, false);
        }

        public FeatureExecutionDecision CanExecute(string featureIdOrCommandKey)
        {
            return CanExecute(featureIdOrCommandKey, true);
        }

        private FeatureExecutionDecision CanExecute(string featureIdOrCommandKey, bool matchCommandKey)
        {
            var snapshot = FrameworkRuntimeState.Current;
            if (snapshot == null)
            {
                return FeatureExecutionDecision.Blocked("PlugHub runtime state is not available.");
            }

            if (string.IsNullOrWhiteSpace(featureIdOrCommandKey))
            {
                return FeatureExecutionDecision.Blocked("Feature id or command key is required.");
            }

            var feature = snapshot.Features.FirstOrDefault(item =>
                string.Equals(item.Id, featureIdOrCommandKey, StringComparison.OrdinalIgnoreCase)
                || (matchCommandKey && string.Equals(item.CommandKey, featureIdOrCommandKey, StringComparison.OrdinalIgnoreCase)));

            if (feature == null)
            {
                return FeatureExecutionDecision.Blocked("Feature is not registered: " + featureIdOrCommandKey);
            }

            if (feature.DefaultState != FeatureState.Visible)
            {
                return FeatureExecutionDecision.Blocked("Feature is disabled or hidden: " + feature.Name);
            }

            var visibleInWorkspace = snapshot.Composition.Features.Any(item =>
                string.Equals(item.FeatureId, feature.Id, StringComparison.OrdinalIgnoreCase));
            if (!visibleInWorkspace)
            {
                return FeatureExecutionDecision.Blocked("Feature is not visible in the current PlugHub workspace: " + feature.Name);
            }

            return FeatureExecutionDecision.Allow(feature.Id);
        }
    }

    public sealed class FeatureExecutionDecision
    {
        private FeatureExecutionDecision(bool allowed, string featureId, string message)
        {
            Allowed = allowed;
            FeatureId = featureId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Allowed { get; }
        public string FeatureId { get; }
        public string Message { get; }

        public static FeatureExecutionDecision Allow(string featureId) => new FeatureExecutionDecision(true, featureId, string.Empty);
        public static FeatureExecutionDecision Blocked(string message) => new FeatureExecutionDecision(false, string.Empty, message);
    }
}
