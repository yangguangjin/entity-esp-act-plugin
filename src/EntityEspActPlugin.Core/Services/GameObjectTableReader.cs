using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class GameObjectTableReader
{
    public const long ExperimentalObjectTableOffset = 0x2895040;
    public const string ObjectTableSignature = "48 8D 0D ?? ?? ?? ?? E8 EC 49 A1 00 48 3B C3 74";
    public const int ObjectTableResolveOffset = 3;
    public const int ObjectTableInstructionLength = 7;
    private const int GameObjectSize = 0x1A0;
    private const int EntityIdOffset = 0x78;
    private const int BaseIdOffset = 0x84;
    private const int OwnerIdOffset = 0x88;
    private const int ObjectIndexOffset = 0x8C;
    private const int ObjectKindOffset = 0x90;
    private const int NameOffset = 0x30;
    private const int NameLength = 64;
    private const int PositionOffset = 0xB0;
    private const int RotationOffset = 0xC0;
    private const int HitboxRadiusOffset = 0xD0;
    private const int CharacterDataOffset = GameObjectSize;
    private const int CurrentHpOffset = CharacterDataOffset + 0x0C;
    private const int MaxHpOffset = CharacterDataOffset + 0x10;
    private const int MaxSlots = 700;

    private readonly ProcessMemoryReader _memoryReader;
    private PatternScanResult? _cachedObjectTableScan;
    public PatternScanResult? LastObjectTableScan { get; private set; }
    public string LastStatus { get; private set; } = string.Empty;

    public GameObjectTableReader(ProcessMemoryReader memoryReader)
    {
        _memoryReader = memoryReader;
    }

    public IReadOnlyList<EntitySnapshot> ReadExperimentalObjectTable(out int matchedSlots)
    {
        matchedSlots = 0;
        var result = new List<EntitySnapshot>();
        if (!_memoryReader.IsReady)
        {
            return result;
        }

        var tableBase = ResolveObjectTableAddress();
        if (tableBase == 0)
        {
            return result;
        }

        TryReadSelfPosition(tableBase, out var selfPosition);
        for (var slot = 0; slot < MaxSlots; slot++)
        {
            if (!TryReadPointer(tableBase + slot * 8L, out var objectAddress) || !LooksLikeHeapPointer(objectAddress))
            {
                continue;
            }

            if (!TryReadGameObject(slot, objectAddress, selfPosition, out var entity))
            {
                continue;
            }

            matchedSlots++;
            result.Add(entity);
        }

        return result;
    }

    private long ResolveObjectTableAddress()
    {
        var scan = ScanObjectTable();
        LastObjectTableScan = scan;
        if (scan.Success && scan.ResolvedAddress != 0)
        {
            LastStatus = "ObjectTable signature module+0x" + (scan.ResolvedAddress - _memoryReader.Status.ModuleBase).ToString("X");
            return scan.ResolvedAddress;
        }

        var fallback = _memoryReader.Status.ModuleBase + ExperimentalObjectTableOffset;
        LastStatus = "ObjectTable signature failed (" + scan.Error + "); using fixed offset module+0x" + ExperimentalObjectTableOffset.ToString("X");
        return fallback;
    }

    private PatternScanResult ScanObjectTable()
    {
        if (_cachedObjectTableScan != null)
        {
            _cachedObjectTableScan.IsCached = true;
            return _cachedObjectTableScan;
        }

        _cachedObjectTableScan = _memoryReader.ScanMainModule("ObjectTable", ObjectTableSignature, ObjectTableResolveOffset, ObjectTableInstructionLength, ".text");
        return _cachedObjectTableScan;
    }

    private bool TryReadSelfPosition(long tableBase, out Vector3 selfPosition)
    {
        selfPosition = default;
        if (!TryReadPointer(tableBase, out var selfAddress) || !LooksLikeHeapPointer(selfAddress))
        {
            return false;
        }

        if (!TryReadGameObject(0, selfAddress, null, out var selfEntity))
        {
            return false;
        }

        selfPosition = selfEntity.Position;
        return true;
    }

    private bool TryReadGameObject(int expectedIndex, long objectAddress, Vector3? selfPosition, out EntitySnapshot entity)
    {
        entity = new EntitySnapshot();
        if (!_memoryReader.TryReadBytes(objectAddress, GameObjectSize, out var bytes))
        {
            return false;
        }

        var objectIndex = BitConverter.ToUInt16(bytes, ObjectIndexOffset);
        if (objectIndex != expectedIndex)
        {
            return false;
        }

        var objectKind = bytes[ObjectKindOffset];
        var kind = MapKind(objectKind);
        if (kind == EntityKind.Unknown)
        {
            return false;
        }

        var position = new Vector3(
            BitConverter.ToSingle(bytes, PositionOffset),
            BitConverter.ToSingle(bytes, PositionOffset + 4),
            BitConverter.ToSingle(bytes, PositionOffset + 8));
        if (!IsReasonablePosition(position))
        {
            return false;
        }

        entity.Address = unchecked((ulong)objectAddress);
        entity.EntityId = BitConverter.ToUInt32(bytes, EntityIdOffset);
        entity.BNpcId = BitConverter.ToUInt32(bytes, BaseIdOffset);
        entity.BNpcNameId = entity.BNpcId;
        entity.EObjNameId = entity.BNpcId;
        entity.OwnerId = BitConverter.ToUInt32(bytes, OwnerIdOffset);
        entity.Kind = kind;
        entity.Name = ReadUtf8String(bytes, NameOffset, NameLength);
        entity.Position = position;
        entity.Heading = BitConverter.ToSingle(bytes, RotationOffset);
        entity.HitboxRadius = BitConverter.ToSingle(bytes, HitboxRadiusOffset);
        entity.DistanceToPlayer = selfPosition.HasValue ? Vector3.Distance(selfPosition.Value, position) : 0f;
        if (TryReadCharacterHealth(objectAddress, kind, out var currentHp, out var maxHp))
        {
            entity.CurrentHp = currentHp;
            entity.MaxHp = maxHp;
        }

        entity.IsSelf = objectIndex == 0;
        entity.IsTargetable = true;
        entity.IsVisible = true;
        entity.LastSeen = DateTime.UtcNow;
        return true;
    }

    private bool TryReadPointer(long address, out long pointer)
    {
        pointer = 0;
        if (!_memoryReader.TryReadBytes(address, 8, out var bytes))
        {
            return false;
        }

        pointer = BitConverter.ToInt64(bytes, 0);
        return true;
    }

    private bool LooksLikeHeapPointer(long value)
    {
        return value > 0x100000000L && value < 0x0000800000000000L && FindSectionName(value) == "<outside>";
    }

    private string FindSectionName(long address)
    {
        foreach (var section in _memoryReader.Status.Sections)
        {
            if (section.ScanSize <= 0)
            {
                continue;
            }

            if (address >= section.StartAddress && address < section.StartAddress + section.ScanSize)
            {
                return section.Name;
            }
        }

        return "<outside>";
    }

    private static string ReadUtf8String(byte[] bytes, int offset, int maxLength)
    {
        if (offset < 0 || offset >= bytes.Length || maxLength <= 0)
        {
            return string.Empty;
        }

        var available = Math.Min(maxLength, bytes.Length - offset);
        var length = 0;
        while (length < available && bytes[offset + length] != 0)
        {
            length++;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(bytes, offset, length);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }
    }

    private static EntityKind MapKind(byte objectKind)
    {
        switch (objectKind)
        {
            case 1:
                return EntityKind.Player;
            case 2:
                return EntityKind.BattleNpc;
            case 3:
                return EntityKind.EventObj;
            case 5:
                return EntityKind.EventNpc;
            default:
                return EntityKind.Unknown;
        }
    }

    private static bool IsReasonablePosition(Vector3 position)
    {
        return IsFinite(position.X) && IsFinite(position.Y) && IsFinite(position.Z)
            && Math.Abs(position.X) < 100000f
            && Math.Abs(position.Y) < 100000f
            && Math.Abs(position.Z) < 100000f
            && Math.Abs(position.X) + Math.Abs(position.Y) + Math.Abs(position.Z) > 0.001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static bool TryReadCharacterHealth(byte[] bytes, EntityKind kind, out uint currentHp, out uint maxHp)
    {
        currentHp = 0;
        maxHp = 0;
        if (!KindCanHaveHealth(kind) || bytes.Length < MaxHpOffset + sizeof(uint))
        {
            return false;
        }

        currentHp = BitConverter.ToUInt32(bytes, CurrentHpOffset);
        maxHp = BitConverter.ToUInt32(bytes, MaxHpOffset);
        if (currentHp == 0 && maxHp == 0)
        {
            return false;
        }

        if (maxHp > 0 && currentHp > maxHp)
        {
            currentHp = 0;
            maxHp = 0;
            return false;
        }

        return true;
    }

    private bool TryReadCharacterHealth(long objectAddress, EntityKind kind, out uint currentHp, out uint maxHp)
    {
        currentHp = 0;
        maxHp = 0;
        if (!KindCanHaveHealth(kind))
        {
            return false;
        }

        if (!_memoryReader.TryReadBytes(objectAddress + CurrentHpOffset, sizeof(uint) * 2, out var bytes))
        {
            return false;
        }

        currentHp = BitConverter.ToUInt32(bytes, 0);
        maxHp = BitConverter.ToUInt32(bytes, sizeof(uint));
        if (currentHp == 0 && maxHp == 0)
        {
            return false;
        }

        if (maxHp > 0 && currentHp > maxHp)
        {
            currentHp = 0;
            maxHp = 0;
            return false;
        }

        return true;
    }

    private static bool KindCanHaveHealth(EntityKind kind)
    {
        return kind == EntityKind.Player || kind == EntityKind.BattleNpc || kind == EntityKind.EventNpc;
    }
}
