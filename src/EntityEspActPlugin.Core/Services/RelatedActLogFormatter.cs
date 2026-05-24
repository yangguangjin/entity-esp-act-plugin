using System;
using System.Text.RegularExpressions;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public static class RelatedActLogFormatter
{
    public static string Format(string line, RelatedActLogSimplifyConfig? simplify)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var type = RelatedActLogStore.GetLineType(line);
        if (simplify == null || !IsSimplified(type, simplify))
        {
            return StripDisplayPrefix(line, type);
        }

        var fields = ExtractFields(line, type);
        if (fields.Length == 0)
        {
            return StripDisplayPrefix(line, type);
        }

        return type switch
        {
            "03" => Format03(fields, line),
            "04" => Format04(fields, line),
            "14" => simplify.Log14Alt ? Format14Alt(fields, line) : Format14(fields, line),
            "15" => Format15Or16("15", fields, line),
            "16" => Format15Or16("16", fields, line),
            "17" => Format17(fields, line),
            "18" => Format18(fields, line),
            "19" => Format19(fields, line),
            "1A" => simplify.Log1AAlt2 ? Format1AAlt2(fields, line) : simplify.Log1AAlt ? Format1AAlt(fields, line) : Format1A(fields, line),
            "1B" => Format1B(fields, line),
            "1C" => Format1C(fields, line),
            "1D" => Format1D(fields, line),
            "1E" => Format1E(fields, line),
            "21" => Format21(fields, line),
            "23" => Format23(fields, line),
            "26" => Format26(fields, line),
            "27" => Format27(fields, line),
            "2A" => Format2A(fields, line),
            "105" => Format105(fields, line),
            "107" => Format107(fields, line),
            "108" => Format108(fields, line),
            "10F" => Format10F(fields, line),
            "110" => Format110(fields, line),
            "111" => Format111(fields, line),
            "112" => Format112(fields, line),
            _ => StripDisplayPrefix(line, type),
        };
    }

    private static bool IsSimplified(string type, RelatedActLogSimplifyConfig simplify)
    {
        return type switch
        {
            "03" => simplify.Log03,
            "04" => simplify.Log04,
            "14" => simplify.Log14 || simplify.Log14Alt,
            "15" => simplify.Log15,
            "16" => simplify.Log16,
            "17" => simplify.Log17,
            "18" => simplify.Log18,
            "19" => simplify.Log19,
            "1A" => simplify.Log1A || simplify.Log1AAlt || simplify.Log1AAlt2,
            "1B" => simplify.Log1B,
            "1C" => simplify.Log1C,
            "1D" => simplify.Log1D,
            "1E" => simplify.Log1E,
            "21" => simplify.Log21,
            "23" => simplify.Log23,
            "26" => simplify.Log26,
            "27" => simplify.Log27,
            "2A" => simplify.Log2A,
            "105" => simplify.Log105,
            "107" => simplify.Log107,
            "108" => simplify.Log108,
            "10F" => simplify.Log10F,
            "110" => simplify.Log110,
            "111" => simplify.Log111,
            "112" => simplify.Log112,
            _ => false,
        };
    }

    private static string[] ExtractFields(string line, string type)
    {
        var marker = type + ":";
        var searchStart = 0;
        var timestampEnd = line.IndexOf(']');
        if (timestampEnd >= 0 && timestampEnd + 1 < line.Length)
        {
            searchStart = timestampEnd + 1;
        }

        var start = line.IndexOf(marker, searchStart, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return Array.Empty<string>();
        }

        return line.Substring(start + marker.Length).Split(':');
    }

    private static string Format03(string[] f, string fallback) =>
        f.Length >= 18 ? $"03 Add id={F(f, 0)} name={F(f, 1)} bnpc={F(f, 7)} nameId={F(f, 6)} pos=({F(f, 14)},{F(f, 15)},{F(f, 16)}) h={F(f, 17)}" : StripDisplayPrefix(fallback, "03");

    private static string Format04(string[] f, string fallback) =>
        f.Length >= 2 ? $"04 Remove id={F(f, 0)} name={F(f, 1)}" : StripDisplayPrefix(fallback, "04");

    private static string Format14(string[] f, string fallback) =>
        f.Length >= 11 ? $"14 Cast src={F(f, 0)} {F(f, 2)}:{F(f, 3)} -> tgt={F(f, 4)} {F(f, 5)} t={F(f, 6)} pos=({F(f, 7)},{F(f, 8)},{F(f, 9)}) h={F(f, 10)}" : StripDisplayPrefix(fallback, "14");

    private static string Format14Alt(string[] f, string fallback) =>
        f.Length >= 7 ? $"{F(f, 1)} -> 技能名：{F(f, 3)} | 技能ID：{F(f, 2)} | 时间：{F(f, 6)}" : StripDisplayPrefix(fallback, "14");

    private static string Format15Or16(string type, string[] f, string fallback) =>
        f.Length >= 6 ? $"{type} Ability src={F(f, 0)} {F(f, 2)}:{F(f, 3)} -> tgt={F(f, 4)} {F(f, 5)} fx={F(f, 6)}/{F(f, 7)} seq={F(f, 36)} idx={F(f, 37)}/{F(f, 38)}" : fallback;

    private static string Format17(string[] f, string fallback) =>
        f.Length >= 5 ? $"17 Cancel src={F(f, 0)} {F(f, 2)}:{F(f, 3)} reason={F(f, 4)}" : fallback;

    private static string Format18(string[] f, string fallback) =>
        f.Length >= 2 ? $"18 Tick src={F(f, 0)} tgt={F(f, 1)} hp={F(f, 2)}/{F(f, 3)}" : fallback;

    private static string Format19(string[] f, string fallback) =>
        f.Length >= 4 ? $"19 Death tgt={F(f, 0)} {F(f, 1)} src={F(f, 2)} {F(f, 3)}" : fallback;

    private static string Format1A(string[] f, string fallback) =>
        f.Length >= 8 ? $"1A StatusAdd {F(f, 0)}:{F(f, 1)} dur={F(f, 2)} src={F(f, 3)} {F(f, 4)} -> tgt={F(f, 5)} {F(f, 6)} stack={F(f, 7)}" : StripDisplayPrefix(fallback, "1A");

    private static string Format1AAlt(string[] f, string fallback) =>
        f.Length >= 8 ? $"Buff名:{F(f, 1)} | BuffID:{F(f, 0)} | Buff时间:{F(f, 2)} | Buff类型:{F(f, 7)}   - - ->>>   {F(f, 6)}" : StripDisplayPrefix(fallback, "1A");

    private static string Format1AAlt2(string[] f, string fallback) =>
        f.Length >= 7 ? $"Buff名:{F(f, 1)} | BuffID:{F(f, 0)} | Buff时间:{F(f, 2)}   - - ->>>   {F(f, 6)}" : StripDisplayPrefix(fallback, "1A");

    private static string Format1B(string[] f, string fallback) =>
        f.Length >= 6 ? $"1B HeadMarker tgt={F(f, 0)} {F(f, 1)} src={F(f, 2)} {F(f, 3)} type={F(f, 5)}" : fallback;

    private static string Format1C(string[] f, string fallback) =>
        f.Length >= 7 ? $"1C Waymark op={F(f, 0)} mark={F(f, 1)} id={F(f, 2)} name={F(f, 3)} pos=({F(f, 4)},{F(f, 5)},{F(f, 6)})" : fallback;

    private static string Format1D(string[] f, string fallback) =>
        f.Length >= 6 ? $"1D Sign op={F(f, 0)} mark={F(f, 1)} src={F(f, 2)} {F(f, 3)} -> tgt={F(f, 4)} {F(f, 5)}" : fallback;

    private static string Format1E(string[] f, string fallback) =>
        f.Length >= 7 ? $"1E StatusRemove {F(f, 0)}:{F(f, 1)} src={F(f, 2)} {F(f, 3)} -> tgt={F(f, 4)} {F(f, 5)} stack={F(f, 6)}" : fallback;

    private static string Format21(string[] f, string fallback) =>
        f.Length >= 6 ? $"21 ActorControl inst={F(f, 0)} cmd={F(f, 1)} data={F(f, 2)},{F(f, 3)},{F(f, 4)},{F(f, 5)}" : fallback;

    private static string Format23(string[] f, string fallback) =>
        f.Length >= 7 ? $"23 Tether src={F(f, 0)} {F(f, 1)} -> tgt={F(f, 2)} {F(f, 3)} type={F(f, 6)}" : fallback;

    private static string Format26(string[] f, string fallback) =>
        f.Length >= 3 ? $"26 StatusList tgt={F(f, 0)} {F(f, 1)} job={F(f, 2)} buffs={Math.Max(0, (f.Length - 3) / 3)}" : fallback;

    private static string Format27(string[] f, string fallback) =>
        f.Length >= 4 ? $"27 HP id={F(f, 0)} {F(f, 1)} hp={F(f, 2)}/{F(f, 3)}" : fallback;

    private static string Format2A(string[] f, string fallback) =>
        f.Length >= 3 ? $"2A StatusList3 tgt={F(f, 0)} {F(f, 1)} job={F(f, 2)} buffs={Math.Max(0, (f.Length - 3) / 3)}" : fallback;

    private static string Format105(string[] f, string fallback)
    {
        if (f.Length < 2)
        {
            return fallback;
        }

        var id = f.Length > 1 ? f[1] : string.Empty;
        return $"105 {F(f, 0)} id={id} name={FindProperty(f, "Name")} bnpc={FindProperty(f, "BNpcID")} nameId={FindProperty(f, "BNpcNameID")} pos=({FindProperty(f, "PosX")},{FindProperty(f, "PosY")},{FindProperty(f, "PosZ")})";
    }

    private static string Format107(string[] f, string fallback) =>
        f.Length >= 6 ? $"107 CastPos src={F(f, 0)} aid={F(f, 1)} pos=({F(f, 2)},{F(f, 3)},{F(f, 4)}) h={F(f, 5)}" : fallback;

    private static string Format108(string[] f, string fallback) =>
        f.Length >= 8 ? $"108 AbilityPos src={F(f, 0)} aid={F(f, 1)} seq={F(f, 2)} flag={F(f, 3)} pos=({F(f, 4)},{F(f, 5)},{F(f, 6)}) h={F(f, 7)}" : fallback;

    private static string Format10F(string[] f, string fallback) =>
        f.Length >= 7 ? $"10F SetPos id={F(f, 0)} h={F(f, 1)} pos=({F(f, 4)},{F(f, 5)},{F(f, 6)})" : fallback;

    private static string Format110(string[] f, string fallback) =>
        f.Length >= 4 ? $"110 SpawnExtra id={F(f, 0)} parent={F(f, 1)} tether={F(f, 2)} anim={F(f, 3)}" : fallback;

    private static string Format111(string[] f, string fallback) =>
        f.Length >= 4 ? $"111 ControlExtra id={F(f, 0)} type={F(f, 1)} p={F(f, 2)},{F(f, 3)}" : fallback;

    private static string Format112(string[] f, string fallback) =>
        f.Length >= 4 ? $"112 ControlSelf id={F(f, 0)} type={F(f, 1)} p={F(f, 2)},{F(f, 3)}" : fallback;

    public static string StripDisplayPrefix(string line)
    {
        return StripDisplayPrefix(line, RelatedActLogStore.GetLineType(line));
    }

    private static string StripDisplayPrefix(string line, string type)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line ?? string.Empty;
        }

        var noTimestamp = StripBracketedTimestamps(line.Trim());
        if (string.IsNullOrWhiteSpace(type))
        {
            return noTimestamp;
        }

        var marker = type + ":";
        var start = noTimestamp.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return start >= 0 ? noTimestamp.Substring(start) : noTimestamp;
    }

    private static string StripBracketedTimestamps(string line)
    {
        return Regex.Replace(line, @"\s*\[\d{1,2}:\d{2}:\d{2}(?:\.\d{1,3})?\]\s*", " ").Trim();
    }

    private static string F(string[] fields, int index) => index >= 0 && index < fields.Length ? fields[index] : string.Empty;

    private static string FindProperty(string[] fields, string name)
    {
        for (var i = 0; i < fields.Length - 1; i++)
        {
            if (string.Equals(fields[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return fields[i + 1];
            }
        }

        return string.Empty;
    }
}
