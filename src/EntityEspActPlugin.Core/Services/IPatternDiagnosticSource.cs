using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public interface IPatternDiagnosticSource : IDiagnosticSource
{
    IReadOnlyList<PatternScanResult> PatternScans { get; }
}
