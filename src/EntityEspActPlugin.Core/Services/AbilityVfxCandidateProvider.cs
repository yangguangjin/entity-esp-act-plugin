using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using EntityEspActPlugin.Core.Models;

namespace EntityEspActPlugin.Core.Services;

public sealed class AbilityVfxCandidateProvider
{
    private readonly string _capturesDirectory;
    private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
    private readonly object _syncRoot = new object();
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private string _loadedFile = string.Empty;
    private DateTime _loadedWriteUtc = DateTime.MinValue;
    private Dictionary<string, AbilityVfxCandidate> _candidates = new Dictionary<string, AbilityVfxCandidate>(StringComparer.OrdinalIgnoreCase);

    public AbilityVfxCandidateProvider()
        : this(FindDefaultCapturesDirectory())
    {
    }

    public AbilityVfxCandidateProvider(string capturesDirectory)
    {
        _capturesDirectory = capturesDirectory;
    }

    public AbilityVfxCandidate? GetTopCandidate(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return null;
        }

        RefreshIfDue();
        lock (_syncRoot)
        {
            return _candidates.TryGetValue(abilityId, out var candidate) ? candidate : null;
        }
    }

    public string LoadedFile
    {
        get
        {
            lock (_syncRoot)
            {
                return _loadedFile;
            }
        }
    }

    private void RefreshIfDue()
    {
        var now = DateTime.UtcNow;
        lock (_syncRoot)
        {
            if ((now - _lastRefreshUtc).TotalSeconds < 5)
            {
                return;
            }

            _lastRefreshUtc = now;
        }

        Refresh();
    }

    private void Refresh()
    {
        try
        {
            if (!Directory.Exists(_capturesDirectory))
            {
                return;
            }

            var file = Directory.GetFiles(_capturesDirectory, "ability-vfx-merged-*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            var writeUtc = File.GetLastWriteTimeUtc(file);
            lock (_syncRoot)
            {
                if (string.Equals(file, _loadedFile, StringComparison.OrdinalIgnoreCase) && writeUtc == _loadedWriteUtc)
                {
                    return;
                }
            }

            var map = LoadCandidates(file);
            lock (_syncRoot)
            {
                _candidates = map;
                _loadedFile = file;
                _loadedWriteUtc = writeUtc;
            }
        }
        catch
        {
            // Candidate display is optional. Fail closed so ACT overlay never breaks because of a malformed capture file.
        }
    }

    private Dictionary<string, AbilityVfxCandidate> LoadCandidates(string file)
    {
        var result = new Dictionary<string, AbilityVfxCandidate>(StringComparer.OrdinalIgnoreCase);
        var root = _serializer.DeserializeObject(File.ReadAllText(file)) as IDictionary<string, object>;
        if (root == null || !TryGetValueIgnoreCase(root, "Candidates", out var candidatesObj) || !(candidatesObj is IEnumerable candidates))
        {
            return result;
        }

        foreach (var item in candidates)
        {
            if (!(item is IDictionary<string, object> ability))
            {
                continue;
            }

            var abilityId = GetString(ability, "AbilityId");
            if (string.IsNullOrWhiteSpace(abilityId) || !TryGetValueIgnoreCase(ability, "CandidateAvfx", out var avfxObj) || !(avfxObj is IEnumerable avfxList))
            {
                continue;
            }

            IDictionary<string, object>? top = null;
            foreach (var avfx in avfxList)
            {
                top = avfx as IDictionary<string, object>;
                if (top != null)
                {
                    break;
                }
            }

            if (top == null)
            {
                continue;
            }

            var path = GetString(top, "Path");
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            result[abilityId] = new AbilityVfxCandidate
            {
                AbilityId = abilityId,
                AbilityName = GetString(ability, "AbilityName"),
                Path = path,
                Score = GetDouble(top, "FinalScore"),
                RawScore = GetInt(top, "RawScore"),
                SeenInFiles = GetInt(top, "SeenInFiles"),
                Classification = GetString(top, "Classification"),
            };
        }

        return result;
    }

    private static string FindDefaultCapturesDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "tools", "EntityEspProbe", "captures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "Config", "EntityEspProbe", "captures");
    }

    private static bool TryGetValueIgnoreCase(IDictionary<string, object> map, string key, out object? value)
    {
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string GetString(IDictionary<string, object> map, string key)
    {
        return TryGetValueIgnoreCase(map, key, out var value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
    }

    private static int GetInt(IDictionary<string, object> map, string key)
    {
        return TryGetValueIgnoreCase(map, key, out var value) && value != null && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double GetDouble(IDictionary<string, object> map, string key)
    {
        return TryGetValueIgnoreCase(map, key, out var value) && value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
