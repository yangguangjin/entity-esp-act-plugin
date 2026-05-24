using System;

namespace EntityEspActPlugin.Core.Services;

public static class RelativeAddressResolver
{
    public static bool TryResolveRipRelative(byte[] data, int instructionOffset, long instructionAddress, int displacementOffset, int instructionLength, out long resolvedAddress, out string error)
    {
        resolvedAddress = 0;
        error = string.Empty;
        if (data == null)
        {
            error = "data is null";
            return false;
        }

        if (instructionOffset < 0 || displacementOffset < 0 || instructionLength <= 0)
        {
            error = "invalid resolve parameters";
            return false;
        }

        var dispIndex = instructionOffset + displacementOffset;
        if (dispIndex + 4 > data.Length)
        {
            error = "disp32 is outside data range";
            return false;
        }

        var displacement = BitConverter.ToInt32(data, dispIndex);
        resolvedAddress = instructionAddress + instructionLength + displacement;
        return true;
    }
}
