using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class ProcessMemoryReader : IDisposable
{
    private const int ProcessVmRead = 0x0010;
    private const int ProcessQueryInformation = 0x0400;
    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int MemCommit = 0x1000;
    private const int PageNoAccess = 0x01;
    private const int PageGuard = 0x100;

    private IntPtr _handle = IntPtr.Zero;
    /// <summary>功能：记录最近一次进程/模块刷新时间，用于避免未就绪或实时采样路径高频 OpenProcess。</summary>
    private DateTime _lastRefreshAttemptUtc = DateTime.MinValue;

    public ProcessMemoryReader(string processName = "ffxiv_dx11")
    {
        ProcessName = processName;
        Refresh();
    }

    public string ProcessName { get; }
    public ProcessMemoryStatus Status { get; private set; } = new ProcessMemoryStatus();

    public bool IsReady => Status.ProcessFound && Status.ModuleFound && Status.HasHandle;

    public void Refresh()
    {
        _lastRefreshAttemptUtc = DateTime.UtcNow;
        CloseHandleIfOpen();
        var status = new ProcessMemoryStatus();
        try
        {
            var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (process == null)
            {
                status.LastError = ProcessName + " process not found";
                Status = status;
                return;
            }

            status.ProcessFound = true;
            status.ProcessId = process.Id;
            try
            {
                var module = process.MainModule;
                if (module != null)
                {
                    status.ModuleFound = true;
                    status.ModuleBase = module.BaseAddress.ToInt64();
                    status.ModuleSize = module.ModuleMemorySize;
                }
                else
                {
                    status.LastError = "main module not found";
                }
            }
            catch (Exception ex)
            {
                status.LastError = "main module error: " + ex.Message;
            }

            _handle = OpenProcess(ProcessVmRead | ProcessQueryInformation | ProcessQueryLimitedInformation, false, process.Id);
            status.HasHandle = _handle != IntPtr.Zero;
            if (!status.HasHandle && string.IsNullOrWhiteSpace(status.LastError))
            {
                status.LastError = "OpenProcess failed: " + Marshal.GetLastWin32Error();
            }
        }
        catch (Exception ex)
        {
            status.LastError = ex.Message;
        }

        Status = status;
        RefreshSections();
    }

    /// <summary>功能：按最小间隔刷新进程/模块句柄，返回本次是否真的执行 Refresh。</summary>
    public bool RefreshIfDue(TimeSpan minInterval)
    {
        var safeInterval = minInterval <= TimeSpan.Zero ? TimeSpan.Zero : minInterval;
        var now = DateTime.UtcNow;
        if (_lastRefreshAttemptUtc != DateTime.MinValue && now - _lastRefreshAttemptUtc < safeInterval)
        {
            return false;
        }

        Refresh();
        return true;
    }

    public IReadOnlyList<MemoryRegion> GetReadableMemoryRegions(long maxRegionSize = 64 * 1024 * 1024)
    {
        var regions = new List<MemoryRegion>();
        if (!IsReady)
        {
            return regions;
        }

        var address = IntPtr.Zero;
        var maxAddress = Environment.Is64BitProcess ? long.MaxValue : uint.MaxValue;
        while (address.ToInt64() >= 0 && address.ToInt64() < maxAddress)
        {
            if (VirtualQueryEx(_handle, address, out var info, (IntPtr)Marshal.SizeOf(typeof(MemoryBasicInformation))) == IntPtr.Zero)
            {
                break;
            }

            var baseAddress = info.BaseAddress.ToInt64();
            var regionSize = info.RegionSize.ToInt64();
            if (regionSize <= 0)
            {
                break;
            }

            if (info.State == MemCommit && (info.Protect & PageNoAccess) == 0 && (info.Protect & PageGuard) == 0)
            {
                regions.Add(new MemoryRegion(baseAddress, Math.Min(regionSize, maxRegionSize), info.Protect));
            }

            var next = baseAddress + regionSize;
            if (next <= baseAddress)
            {
                break;
            }

            address = new IntPtr(next);
        }

        return regions;
    }

    public bool TryReadBytes(long address, int count, out byte[] bytes)
    {
        bytes = new byte[Math.Max(0, count)];
        if (!IsReady || count <= 0)
        {
            Status.LastError = "reader is not ready";
            return false;
        }

        if (!ReadProcessMemory(_handle, new IntPtr(address), bytes, bytes.Length, out var bytesRead) || bytesRead.ToInt64() != count)
        {
            Status.LastError = "ReadProcessMemory failed: " + Marshal.GetLastWin32Error();
            bytes = Array.Empty<byte>();
            return false;
        }

        Status.LastError = string.Empty;
        return true;
    }

    public PatternScanResult ScanMainModule(string name, string pattern, int resolveOffset = 3, int instructionLength = 7, string sectionName = ".text")
    {
        var result = new PatternScanResult
        {
            Name = name,
            Pattern = pattern,
            ResolveOffset = resolveOffset,
            InstructionLength = instructionLength,
            SectionName = sectionName,
        };
        if (!IsReady)
        {
            result.Error = Status.LastError;
            return result;
        }

        var start = Status.ModuleBase;
        var size = Status.ModuleSize;
        if (!string.IsNullOrWhiteSpace(sectionName))
        {
            var section = Status.Sections.FirstOrDefault(item => string.Equals(item.Name, sectionName, StringComparison.OrdinalIgnoreCase));
            if (section == null)
            {
                result.Error = "section not found: " + sectionName;
                return result;
            }

            start = section.StartAddress;
            size = Math.Min(section.ScanSize, Math.Max(0, Status.ModuleSize - section.VirtualAddress));
        }

        if (!TryReadBytes(start, size, out var moduleBytes))
        {
            result.Error = Status.LastError;
            return result;
        }

        return PatternScanner.Scan(moduleBytes, start, name, pattern, resolveOffset, instructionLength, sectionName);
    }

    private void RefreshSections()
    {
        if (!IsReady)
        {
            return;
        }

        if (!TryReadBytes(Status.ModuleBase, Math.Min(Status.ModuleSize, 0x4000), out var headerBytes))
        {
            return;
        }

        Status.Sections = PeSectionParser.Parse(headerBytes, Status.ModuleBase);
    }

    public RipCandidateScanResult ScanRipCandidates(string sectionName = ".text", int limit = 40, int minRefs = 1, int maxRefs = int.MaxValue)
    {
        var result = new RipCandidateScanResult { SectionName = sectionName };
        if (!IsReady)
        {
            result.Error = Status.LastError;
            return result;
        }

        var section = Status.Sections.FirstOrDefault(item => string.Equals(item.Name, sectionName, StringComparison.OrdinalIgnoreCase));
        if (section == null)
        {
            result.Error = "section not found: " + sectionName;
            return result;
        }

        var size = Math.Min(section.ScanSize, Math.Max(0, Status.ModuleSize - section.VirtualAddress));
        if (!TryReadBytes(section.StartAddress, size, out var bytes))
        {
            result.Error = Status.LastError;
            return result;
        }

        var scan = RipCandidateScanner.Scan(bytes, section.StartAddress, Status.Sections, sectionName, limit, minRefs, maxRefs);
        ProbeCandidatePointers(scan);
        return scan;
    }

    public EntityTableProbeResult ProbeEntityTableSlot(long slotAddress)
    {
        var result = new EntityTableProbeResult { SlotAddress = slotAddress };
        if (slotAddress <= 0)
        {
            result.Error = "slot address is empty";
            return result;
        }

        if (!TryReadBytes(slotAddress, 8, out var pointerBytes))
        {
            result.Error = Status.LastError;
            return result;
        }

        result.PointerReadSuccess = true;
        result.PointerValue = BitConverter.ToInt64(pointerBytes, 0);
        result.PointerTargetSection = FindSectionName(result.PointerValue);
        ProbeEntityTablePointer(result);
        return result;
    }

    private void ProbeEntityTablePointer(EntityTableProbeResult result)
    {
        if (result.PointerValue <= 0x100000000L)
        {
            result.Error = "pointer is null or too small";
            return;
        }

        if (!TryReadBytes(result.PointerValue, 128, out var bytes))
        {
            result.Error = Status.LastError;
            return;
        }

        for (var index = 0; index < 8; index++)
        {
            result.Qwords.Add(BuildQwordProbe(bytes, index));
        }

        for (var index = 0; index < 8; index++)
        {
            result.Floats.Add(BitConverter.ToSingle(bytes, index * 4));
        }

        ProbeArrayCandidates("root", result.PointerValue, result.ArrayCandidates);
        ProbeFieldHints("root", result.PointerValue, result.FieldHints);
        ProbeEntityTableChildPointers(result);
    }

    private void ProbeEntityTableChildPointers(EntityTableProbeResult result)
    {
        if (!TryReadBytes(result.PointerValue, 0x200, out var bytes))
        {
            return;
        }

        for (var offset = 0; offset <= bytes.Length - 8 && result.ChildPointers.Count < 8; offset += 8)
        {
            var pointer = BitConverter.ToInt64(bytes, offset);
            if (pointer <= 0x100000000L || !string.Equals(FindSectionName(pointer), "<outside>", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var child = new EntityTableChildPointerProbe
            {
                Offset = offset,
                PointerValue = pointer,
                PointerTargetSection = "<outside>",
            };
            if (TryReadBytes(pointer, 64, out var childBytes))
            {
                for (var index = 0; index < 4; index++)
                {
                    child.Qwords.Add(BuildQwordProbe(childBytes, index));
                }

                for (var index = 0; index < 8; index++)
                {
                    child.Floats.Add(BitConverter.ToSingle(childBytes, index * 4));
                }

                var owner = "child+0x" + offset.ToString("X");
                ProbeArrayCandidates(owner, pointer, result.ArrayCandidates);
                ProbeFieldHints(owner, pointer, result.FieldHints);
            }
            else
            {
                child.Error = Status.LastError;
            }

            result.ChildPointers.Add(child);
        }
    }

    private void ProbeFieldHints(string owner, long objectAddress, List<FieldHintProbe> output)
    {
        if (output.Count >= 24 || !TryReadBytes(objectAddress, 0x180, out var bytes))
        {
            return;
        }

        for (var offset = 0; offset <= bytes.Length - 8 && output.Count < 24; offset += 8)
        {
            var raw = BitConverter.ToInt64(bytes, offset);
            var section = FindSectionName(raw);
            if (LooksLikeHeapPointer(raw))
            {
                output.Add(new FieldHintProbe { Owner = owner, Offset = offset, Kind = "heap-ptr", RawQword = raw, TargetSection = section });
                continue;
            }

            if (raw > 0 && raw <= 4096)
            {
                output.Add(new FieldHintProbe { Owner = owner, Offset = offset, Kind = "small-int", RawQword = raw, TargetSection = section });
                continue;
            }

            if (offset <= bytes.Length - 16)
            {
                var floats = new List<float>();
                for (var i = 0; i < 4; i++)
                {
                    floats.Add(BitConverter.ToSingle(bytes, offset + i * 4));
                }

                if (LooksLikeFloatVector(floats))
                {
                    output.Add(new FieldHintProbe { Owner = owner, Offset = offset, Kind = "float4", RawQword = raw, TargetSection = section, Floats = floats });
                }
            }
        }
    }

    private static bool LooksLikeFloatVector(List<float> floats)
    {
        var meaningful = 0;
        foreach (var value in floats)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return false;
            }

            if (Math.Abs(value) >= 0.001f && Math.Abs(value) <= 100000f)
            {
                meaningful++;
            }
        }

        return meaningful >= 2;
    }

    private void ProbeArrayCandidates(string owner, long objectAddress, List<ArrayCandidateProbe> output)
    {
        if (output.Count >= 12 || !TryReadBytes(objectAddress, 0x180, out var bytes))
        {
            return;
        }

        for (var offset = 0; offset <= bytes.Length - 24 && output.Count < 12; offset += 8)
        {
            var first = BitConverter.ToInt64(bytes, offset);
            var second = BitConverter.ToInt64(bytes, offset + 8);
            var third = BitConverter.ToInt64(bytes, offset + 16);
            TryAddPointerRangeCandidate(owner, offset, first, second, third, output);
            TryAddPointerCountCandidate(owner, offset, first, second, third, output);
        }
    }

    private void TryAddPointerRangeCandidate(string owner, int offset, long begin, long end, long capacity, List<ArrayCandidateProbe> output)
    {
        if (!LooksLikeHeapPointer(begin) || end < begin || capacity < end || capacity - begin > 0x200000)
        {
            return;
        }

        var bytesUsed = end - begin;
        var stride = GuessStride(bytesUsed);
        if (stride == 0)
        {
            return;
        }

        AddArrayCandidate(owner, offset, "ptr-range", begin, end, capacity, (int)(bytesUsed / stride), stride, output);
    }

    private void TryAddPointerCountCandidate(string owner, int offset, long begin, long countValue, long capacityValue, List<ArrayCandidateProbe> output)
    {
        if (!LooksLikeHeapPointer(begin) || countValue <= 0 || countValue > 4096 || capacityValue < countValue || capacityValue > 8192)
        {
            return;
        }

        AddArrayCandidate(owner, offset, "ptr-count", begin, begin + countValue * 8, begin + capacityValue * 8, (int)countValue, 8, output);
    }

    private bool LooksLikeHeapPointer(long value)
    {
        return value > 0x100000000L && string.Equals(FindSectionName(value), "<outside>", StringComparison.OrdinalIgnoreCase);
    }

    private static int GuessStride(long bytesUsed)
    {
        if (bytesUsed <= 0)
        {
            return 0;
        }

        foreach (var stride in new[] { 8, 16, 24, 32, 48, 64, 80, 96, 128, 144, 160, 192, 256 })
        {
            if (bytesUsed % stride == 0 && bytesUsed / stride <= 4096)
            {
                return stride;
            }
        }

        return 0;
    }

    private void AddArrayCandidate(string owner, int offset, string kind, long begin, long end, long capacity, int count, int stride, List<ArrayCandidateProbe> output)
    {
        var candidate = new ArrayCandidateProbe
        {
            Owner = owner,
            Offset = offset,
            Kind = kind,
            Begin = begin,
            End = end,
            Capacity = capacity,
            Count = count,
            StrideGuess = stride,
        };

        if (TryReadBytes(begin, 32, out var sampleBytes))
        {
            for (var index = 0; index < 4; index++)
            {
                candidate.Samples.Add(BuildQwordProbe(sampleBytes, index));
            }
        }
        else
        {
            candidate.Error = Status.LastError;
        }

        output.Add(candidate);
    }

    private PointerQwordProbe BuildQwordProbe(byte[] bytes, int index)
    {
        var value = BitConverter.ToInt64(bytes, index * 8);
        var targetSection = FindSectionName(value);
        var probe = new PointerQwordProbe
        {
            Index = index,
            Value = value,
            TargetSection = targetSection,
            AsciiPreview = string.Equals(targetSection, ".rdata", StringComparison.OrdinalIgnoreCase) ? TryReadAsciiPreview(value) : string.Empty,
        };
        if (string.Equals(targetSection, ".rdata", StringComparison.OrdinalIgnoreCase))
        {
            ProbeRdataTable(probe);
        }

        return probe;
    }

    private void ProbeCandidatePointers(RipCandidateScanResult scan)
    {
        foreach (var candidate in scan.Candidates)
        {
            if (!TryReadBytes(candidate.ResolvedAddress, 8, out var pointerBytes))
            {
                candidate.PointerReadError = Status.LastError;
                continue;
            }

            candidate.PointerReadSuccess = true;
            candidate.PointerValue = BitConverter.ToInt64(pointerBytes, 0);
            candidate.PointerTargetSection = FindSectionName(candidate.PointerValue);
            ProbeHeapPointer(candidate);
        }
    }

    private void ProbeHeapPointer(RipCandidate candidate)
    {
        if (!string.Equals(candidate.TargetSection, ".data", StringComparison.OrdinalIgnoreCase)
            || candidate.PointerValue <= 0x100000000L
            || !string.Equals(candidate.PointerTargetSection, "<outside>", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryReadBytes(candidate.PointerValue, 64, out var bytes))
        {
            candidate.HeapProbeError = Status.LastError;
            return;
        }

        candidate.HeapProbeSuccess = true;
        for (var index = 0; index < 4; index++)
        {
            candidate.HeapQwords.Add(BuildQwordProbe(bytes, index));
        }
    }

    private void ProbeRdataTable(PointerQwordProbe probe)
    {
        if (!TryReadBytes(probe.Value, 32, out var bytes))
        {
            return;
        }

        for (var index = 0; index < 4; index++)
        {
            var value = BitConverter.ToInt64(bytes, index * 8);
            var targetSection = FindSectionName(value);
            probe.TableQwords.Add(new PointerQwordProbe
            {
                Index = index,
                Value = value,
                TargetSection = targetSection,
                AsciiPreview = string.Equals(targetSection, ".rdata", StringComparison.OrdinalIgnoreCase) ? TryReadAsciiPreview(value) : string.Empty,
            });
        }
    }

    private string TryReadAsciiPreview(long address)
    {
        if (!TryReadBytes(address, 64, out var bytes))
        {
            return string.Empty;
        }

        var chars = new System.Text.StringBuilder();
        foreach (var b in bytes)
        {
            if (b == 0)
            {
                break;
            }

            if (b >= 32 && b <= 126)
            {
                chars.Append((char)b);
            }
            else if (chars.Length > 0)
            {
                break;
            }

            if (chars.Length >= 24)
            {
                break;
            }
        }

        return chars.ToString();
    }

    private string FindSectionName(long address)
    {
        foreach (var section in Status.Sections)
        {
            var size = section.ScanSize;
            if (size <= 0)
            {
                continue;
            }

            if (address >= section.StartAddress && address < section.StartAddress + size)
            {
                return section.Name;
            }
        }

        return "<outside>";
    }

    public void Dispose()
    {
        CloseHandleIfOpen();
    }

    private void CloseHandleIfOpen()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, int size, out IntPtr numberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualQueryEx(IntPtr processHandle, IntPtr baseAddress, out MemoryBasicInformation buffer, IntPtr length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

public sealed class MemoryRegion
{
    public MemoryRegion(long baseAddress, long size, int protect)
    {
        BaseAddress = baseAddress;
        Size = size;
        Protect = protect;
    }

    public long BaseAddress { get; }
    public long Size { get; }
    public int Protect { get; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MemoryBasicInformation
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public int AllocationProtect;
    public IntPtr RegionSize;
    public int State;
    public int Protect;
    public int Type;
}
