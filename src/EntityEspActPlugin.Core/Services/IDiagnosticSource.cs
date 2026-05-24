using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public interface IDiagnosticSource
{
    bool IsReady { get; }
    string Status { get; }
}

public interface IProcessMemoryDiagnosticSource : IDiagnosticSource
{
    ProcessMemoryStatus? ProcessMemoryStatus { get; }
}
