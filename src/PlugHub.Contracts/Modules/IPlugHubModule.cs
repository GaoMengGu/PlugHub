namespace PlugHub.Contracts.Modules
{
    public interface IPlugHubModule
    {
        ModuleDescriptor Describe();
        void Initialize(IModuleContext context);
        void Shutdown();
    }

    public interface IModuleContext
    {
        void ReportDiagnostic(DiagnosticMessage message);
    }
}
