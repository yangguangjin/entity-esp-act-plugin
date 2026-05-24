using System;
using System.Numerics;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class ControlCameraReader
{
    public const string ControlInstanceSignature = "4C 8D 35 ?? ?? ?? ?? 48 8B 09";
    public const int ResolveOffset = 3;
    public const int InstructionLength = 7;
    public const int ViewProjectionMatrixOffset = 0x76B0;

    private readonly ProcessMemoryReader _memoryReader;
    private PatternScanResult? _cachedScan;

    public ControlCameraReader(ProcessMemoryReader memoryReader)
    {
        _memoryReader = memoryReader;
    }

    public PatternScanResult ScanControlInstance()
    {
        if (_cachedScan != null)
        {
            _cachedScan.IsCached = true;
            return _cachedScan;
        }

        _cachedScan = _memoryReader.ScanMainModule("Control.Instance", ControlInstanceSignature, ResolveOffset, InstructionLength, ".text");
        return _cachedScan;
    }

    public bool TryReadCamera(int width, int height, out CameraSnapshot camera, out PatternScanResult scan, out string status)
    {
        camera = new CameraSnapshot
        {
            Viewport = new ViewportRect(0, 0, width, height),
            IsValid = false,
        };
        status = string.Empty;
        scan = ScanControlInstance();
        if (!scan.Success || scan.ResolvedAddress == 0)
        {
            status = "Control.Instance signature not found";
            return false;
        }

        var matrixAddress = scan.ResolvedAddress + ViewProjectionMatrixOffset;
        if (!_memoryReader.TryReadBytes(matrixAddress, 64, out var bytes))
        {
            status = "failed to read Control.ViewProjectionMatrix at 0x" + matrixAddress.ToString("X") + ": " + _memoryReader.Status.LastError;
            return false;
        }

        var matrix = ReadMatrix(bytes);
        if (!IsPlausibleMatrix(matrix))
        {
            status = "Control.ViewProjectionMatrix read but matrix is not plausible at 0x" + matrixAddress.ToString("X");
            return false;
        }

        camera = new CameraSnapshot
        {
            ViewProjectionMatrix = matrix,
            Viewport = new ViewportRect(0, 0, width, height),
            IsValid = true,
        };
        status = "Control.ViewProjectionMatrix module+0x" + (matrixAddress - _memoryReader.Status.ModuleBase).ToString("X") + " read via Control.Instance signature";
        return true;
    }

    private static Matrix4x4 ReadMatrix(byte[] bytes)
    {
        return new Matrix4x4(
            BitConverter.ToSingle(bytes, 0),
            BitConverter.ToSingle(bytes, 4),
            BitConverter.ToSingle(bytes, 8),
            BitConverter.ToSingle(bytes, 12),
            BitConverter.ToSingle(bytes, 16),
            BitConverter.ToSingle(bytes, 20),
            BitConverter.ToSingle(bytes, 24),
            BitConverter.ToSingle(bytes, 28),
            BitConverter.ToSingle(bytes, 32),
            BitConverter.ToSingle(bytes, 36),
            BitConverter.ToSingle(bytes, 40),
            BitConverter.ToSingle(bytes, 44),
            BitConverter.ToSingle(bytes, 48),
            BitConverter.ToSingle(bytes, 52),
            BitConverter.ToSingle(bytes, 56),
            BitConverter.ToSingle(bytes, 60));
    }

    private static bool IsPlausibleMatrix(Matrix4x4 matrix)
    {
        var values = new[]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        };

        var nonZero = 0;
        foreach (var value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1000000f)
            {
                return false;
            }

            if (Math.Abs(value) > 0.000001f)
            {
                nonZero++;
            }
        }

        return nonZero >= 4;
    }
}
