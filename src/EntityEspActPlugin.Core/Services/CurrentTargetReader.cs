using System;

namespace EntityEspActPlugin.Core.Services;

public sealed class CurrentTargetReader
{
    private const int TargetSystemOffset = 0x190;
    private const int HardTargetPointerOffset = 0x80;
    private const int GameObjectEntityIdOffset = 0x78;

    private readonly ProcessMemoryReader _memoryReader;
    private readonly ControlCameraReader _controlReader;

    public CurrentTargetReader(ProcessMemoryReader memoryReader, ControlCameraReader controlReader)
    {
        _memoryReader = memoryReader;
        _controlReader = controlReader;
    }

    public string Status { get; private set; } = "not sampled";

    public bool TryReadCurrentTargetEntityId(out uint entityId)
    {
        entityId = 0;
        var scan = _controlReader.ScanControlInstance();
        if (!scan.Success || scan.ResolvedAddress == 0)
        {
            Status = "target unavailable: Control.Instance not resolved";
            return false;
        }

        var targetPointerAddress = scan.ResolvedAddress + TargetSystemOffset + HardTargetPointerOffset;
        if (!_memoryReader.TryReadBytes(targetPointerAddress, 8, out var pointerBytes))
        {
            Status = "target unavailable: target pointer read failed";
            return false;
        }

        var targetPointer = BitConverter.ToInt64(pointerBytes, 0);
        if (targetPointer <= 0)
        {
            Status = "target unavailable: no hard target";
            return false;
        }

        if (!_memoryReader.TryReadBytes(targetPointer + GameObjectEntityIdOffset, 4, out var idBytes))
        {
            Status = "target unavailable: EntityId read failed";
            return false;
        }

        entityId = BitConverter.ToUInt32(idBytes, 0);
        if (entityId == 0)
        {
            Status = "target unavailable: EntityId is zero";
            return false;
        }

        Status = "target EntityId=0x" + entityId.ToString("X8");
        return true;
    }
}
