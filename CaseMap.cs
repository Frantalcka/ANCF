using System;
using System.Collections.Generic;
using System.IO;

namespace AssetNameCaseFixes;

internal sealed class CaseMap
{
    private readonly string ContentPath;

    private readonly Dictionary<string, string> Map;

    private readonly HashSet<string> AmbiguousKeys;

    private readonly HashSet<string> ExactNames;

    public int Count => this.Map.Count;

    public CaseMap(string contentPath)
    {
        this.ContentPath = contentPath;
        this.Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        this.AmbiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        this.ExactNames = new HashSet<string>(StringComparer.Ordinal);

        if (!Directory.Exists(contentPath))
            return;

        foreach (string file in Directory.EnumerateFiles(contentPath, "*.xnb", SearchOption.AllDirectories))
        {
            string relative;
            try
            {
                relative = Path.GetRelativePath(contentPath, file);
            }
            catch
            {
                continue;
            }

            if (!relative.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                continue;

            string key = relative.Substring(0, relative.Length - 4).Replace('\\', '/');
            this.ExactNames.Add(key);
            if (this.Map.ContainsKey(key))
                this.AmbiguousKeys.Add(key);
            else
                this.Map[key] = key;
        }
    }

    public string? Resolve(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string key = name.IndexOf('\\') >= 0 ? name.Replace('\\', '/') : name;

        if (this.ExactNames.Contains(key))
            return null;

        return this.ResolveExact(key);
    }

    private string? ResolveExact(string key)
    {
        if (!this.Map.TryGetValue(key, out string? real))
            return null;

        if (string.Equals(real, key, StringComparison.Ordinal))
            return null;

        if (this.AmbiguousKeys.Contains(key) && File.Exists(this.GetPath(key)))
            return null;

        return real;
    }

    public bool ExistsUnder(string? rootDirectory, string assetName)
    {
        try
        {
            string root = string.IsNullOrWhiteSpace(rootDirectory) ? this.ContentPath : rootDirectory!;
            if (!Path.IsPathRooted(root))
                root = Path.Combine(StardewModdingAPI.Constants.GamePath, root);

            return File.Exists(Path.Combine(root, assetName.Replace('/', Path.DirectorySeparatorChar) + ".xnb"));
        }
        catch
        {
            return false;
        }
    }

    private string GetPath(string assetName)
    {
        return Path.Combine(this.ContentPath, assetName.Replace('/', Path.DirectorySeparatorChar) + ".xnb");
    }
}
