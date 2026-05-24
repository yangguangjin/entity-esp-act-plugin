using System;
using System.Collections.Generic;
using System.Text;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class PeSectionParser
{
    public static List<PeSectionInfo> Parse(byte[] image, long moduleBase)
    {
        var sections = new List<PeSectionInfo>();
        if (image == null || image.Length < 0x100)
        {
            return sections;
        }

        if (ReadUInt16(image, 0) != 0x5A4D)
        {
            return sections;
        }

        var peOffset = ReadInt32(image, 0x3C);
        if (peOffset <= 0 || peOffset + 0x18 > image.Length)
        {
            return sections;
        }

        if (ReadUInt32(image, peOffset) != 0x00004550)
        {
            return sections;
        }

        var fileHeaderOffset = peOffset + 4;
        var sectionCount = ReadUInt16(image, fileHeaderOffset + 2);
        var optionalHeaderSize = ReadUInt16(image, fileHeaderOffset + 16);
        var sectionTableOffset = fileHeaderOffset + 20 + optionalHeaderSize;
        for (var i = 0; i < sectionCount; i++)
        {
            var offset = sectionTableOffset + (i * 40);
            if (offset + 40 > image.Length)
            {
                break;
            }

            var name = ReadSectionName(image, offset);
            var virtualSize = ReadInt32(image, offset + 8);
            var virtualAddress = ReadInt32(image, offset + 12);
            var rawSize = ReadInt32(image, offset + 16);
            sections.Add(new PeSectionInfo
            {
                Name = name,
                VirtualAddress = virtualAddress,
                VirtualSize = virtualSize,
                RawSize = rawSize,
                StartAddress = moduleBase + virtualAddress,
            });
        }

        return sections;
    }

    private static string ReadSectionName(byte[] image, int offset)
    {
        var length = 0;
        while (length < 8 && offset + length < image.Length && image[offset + length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(image, offset, length);
    }

    private static ushort ReadUInt16(byte[] image, int offset) => BitConverter.ToUInt16(image, offset);
    private static uint ReadUInt32(byte[] image, int offset) => BitConverter.ToUInt32(image, offset);
    private static int ReadInt32(byte[] image, int offset) => BitConverter.ToInt32(image, offset);
}
