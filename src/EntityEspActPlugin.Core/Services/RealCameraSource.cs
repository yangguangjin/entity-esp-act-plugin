using System;
using System.Collections.Generic;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class RealCameraSource : ICameraSource, IProcessMemoryDiagnosticSource, IPatternDiagnosticSource
{
    private readonly ProcessMemoryReader _memoryReader;
    private readonly ControlCameraReader _controlCameraReader;

    public RealCameraSource()
        : this(new ProcessMemoryReader())
    {
    }

    public RealCameraSource(ProcessMemoryReader memoryReader)
    {
        _memoryReader = memoryReader;
        _controlCameraReader = new ControlCameraReader(_memoryReader);
    }

    public bool IsReady { get; private set; }
    public string Status { get; private set; } = "not sampled";
    public ProcessMemoryStatus? ProcessMemoryStatus => _memoryReader.Status;
    public IReadOnlyList<PatternScanResult> PatternScans { get; private set; } = new List<PatternScanResult>();

    public CameraSnapshot GetCamera(int width, int height)
    {
        if (!_memoryReader.IsReady)
        {
            // 功能：相机源未就绪时按 1 秒节流重连，避免高 FPS overlay 每帧 OpenProcess。
            _memoryReader.RefreshIfDue(TimeSpan.FromSeconds(1));
        }

        if (!_memoryReader.IsReady)
        {
            IsReady = false;
            Status = _memoryReader.Status.LastError;
            PatternScans = new List<PatternScanResult>();
            return CreateInvalidCamera(width, height);
        }

        var success = _controlCameraReader.TryReadCamera(width, height, out var camera, out var controlScan, out var status);
        PatternScans = new List<PatternScanResult> { controlScan };
        IsReady = success;
        Status = status;
        return camera;
    }

    private static CameraSnapshot CreateInvalidCamera(int width, int height)
    {
        return new CameraSnapshot
        {
            Viewport = new ViewportRect(0, 0, width, height),
            IsValid = false,
        };
    }
}
