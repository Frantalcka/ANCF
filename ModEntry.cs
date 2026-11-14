using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AssetNameCaseFixes;

public class ModEntry : Mod
{
    private const string HarmonyId = "Frantalcka.AssetNameCaseFixes";

    internal static ModEntry Instance = null!;

    private CaseMap Map = null!;

    private Harmony? Harmony;

    private bool LoggedPatchError;

    private bool Enabled = true;

    private bool CaseSensitiveFileSystem;

    private long LookupCount;

    private long RewriteCount;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        this.CaseSensitiveFileSystem = this.DetectCaseSensitiveFileSystem();
        if (!this.CaseSensitiveFileSystem)
        {
            this.Monitor.Log("This file system is case-insensitive, so no patches are needed.", LogLevel.Info);
            return;
        }

        this.Map = new CaseMap(Constants.ContentPath);
        this.Monitor.Log($"Indexed {this.Map.Count} content asset names.", LogLevel.Trace);

        helper.ConsoleCommands.Add(
            "casefix_resolve",
            "Show the real capitalization for an asset name.\n\nUsage: casefix_resolve <asset name>",
            this.CommandResolve
        );
        helper.ConsoleCommands.Add(
            "casefix_test",
            "Load an asset with deliberately wrong capitalization to test the fix.\n\nUsage: casefix_test [asset name]",
            this.CommandTest
        );
        helper.ConsoleCommands.Add(
            "casefix_test_tex",
            "Load a texture with deliberately wrong capitalization to test the fix.\n\nUsage: casefix_test_tex [asset name]",
            this.CommandTestTexture
        );
        helper.ConsoleCommands.Add(
            "casefix_rescan",
            "Rebuild the content asset name index.",
            this.CommandRescan
        );
        helper.ConsoleCommands.Add(
            "casefix_toggle",
            "Enable or disable the fixes at runtime (for testing).",
            this.CommandToggle
        );
        helper.ConsoleCommands.Add(
            "casefix_stats",
            "Show how many asset names were checked and rewritten this session.",
            this.CommandStats
        );

        this.ApplyPatches();
    }

    private bool DetectCaseSensitiveFileSystem()
    {
        try
        {
            string probe = Path.Combine(Constants.ContentPath, "Data", "Quests.xnb");
            if (File.Exists(probe))
                return !File.Exists(Path.Combine(Constants.ContentPath, "DATA", "QUESTS.XNB"));

            return !Directory.Exists(Constants.GamePath.ToUpperInvariant());
        }
        catch
        {
            return true;
        }
    }

    private void ApplyPatches()
    {
        this.Harmony = new Harmony(HarmonyId);

        int applied = 0;
        int failed = 0;

        void Patch(MethodInfo? method, string label, string prefixName)
        {
            if (method is null)
            {
                this.Monitor.Log($"Can't find a method for '{label}', skipping that patch.", LogLevel.Warn);
                failed++;
                return;
            }

            try
            {
                this.Harmony!.Patch(method, prefix: new HarmonyMethod(typeof(Patches), prefixName));
                applied++;
                this.Monitor.Log($"Patched {label}.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                failed++;
                this.Monitor.Log($"Failed to patch {label}: {ex}", LogLevel.Error);
            }
        }

        Type? assetNameType = AccessTools.TypeByName("StardewModdingAPI.Framework.Content.AssetName");
        MethodInfo? parseMethod = assetNameType is null
            ? null
            : AccessTools.Method(assetNameType, "Parse", new[] { typeof(string), typeof(Func<string, LocalizedContentManager.LanguageCode?>) });
        Patch(parseMethod, "AssetName.Parse(string, ...)", nameof(Patches.BeforeParseAssetName));

        Patch(
            AccessTools.Method(typeof(ContentManager), "OpenStream", new[] { typeof(string) }),
            "ContentManager.OpenStream(string)",
            nameof(Patches.BeforeOpenStream)
        );

        Type? localizedContentManager = AccessTools.TypeByName("StardewValley.LocalizedContentManager");
        MethodInfo? localizedOpenStream = localizedContentManager is null
            ? null
            : AccessTools.Method(localizedContentManager, "OpenStream", new[] { typeof(string) });
        if (localizedOpenStream?.DeclaringType == localizedContentManager)
            Patch(localizedOpenStream, "LocalizedContentManager.OpenStream(string)", nameof(Patches.BeforeOpenStream));

        this.Monitor.Log(
            applied > 0
                ? $"Asset name case fixes are active ({applied} patches applied{(failed > 0 ? $", {failed} failed" : "")})."
                : "Asset name case fixes couldn't patch the content pipeline; see the log above.",
            applied > 0 ? LogLevel.Info : LogLevel.Error
        );
    }

    internal bool TryRewrite(ref string assetName)
    {
        try
        {
            if (!this.Enabled)
                return false;

            this.LookupCount++;
            string requested = assetName;
            string? resolved = this.Map.Resolve(requested);
            if (resolved is null)
                return false;

            this.RewriteCount++;
            assetName = resolved;
            if (this.Monitor.IsVerbose)
                this.Monitor.Log($"Asset name '{requested}' resolved to '{resolved}'.");
            return true;
        }
        catch (Exception ex)
        {
            this.LogPatchError(ex);
            return false;
        }
    }

    internal void TryRewrite(ContentManager contentManager, ref string assetName)
    {
        try
        {
            if (!this.Enabled)
                return;

            this.LookupCount++;
            string requested = assetName;
            string? resolved = this.Map.Resolve(requested);
            if (resolved is null || !this.Map.ExistsUnder(contentManager.RootDirectory, resolved))
                return;

            this.RewriteCount++;
            assetName = resolved;
            if (this.Monitor.IsVerbose)
                this.Monitor.Log($"Asset name '{requested}' resolved to '{resolved}' (direct content manager load).");
        }
        catch (Exception ex)
        {
            this.LogPatchError(ex);
        }
    }

    private void LogPatchError(Exception ex)
    {
        if (this.LoggedPatchError)
            return;

        this.LoggedPatchError = true;
        this.Monitor.Log($"Unexpected error while resolving an asset name; the original name will be used. Details: {ex}", LogLevel.Error);
    }

    private void CommandResolve(string command, string[] args)
    {
        if (args.Length == 0)
        {
            this.Monitor.Log("Usage: casefix_resolve <asset name>", LogLevel.Info);
            return;
        }

        string name = args[0];
        string? resolved = this.Map.Resolve(name);
        this.Monitor.Log(
            resolved is null
                ? $"'{name}' is already correct or isn't a vanilla content asset."
                : $"'{name}' -> '{resolved}'",
            LogLevel.Info
        );
    }

    private void CommandTest(string command, string[] args)
    {
        string name = args.Length > 0 ? args[0] : "data/quests";

        this.TestLoad("SMAPI helper", () => this.Helper.GameContent.Load<Dictionary<string, string>>(name));
        this.TestLoad("game content manager", () => Game1.content.Load<Dictionary<string, string>>(name));

        bool exists = this.Helper.GameContent.DoesAssetExist<Dictionary<string, string>>(this.Helper.GameContent.ParseAssetName(name));
        this.Monitor.Log($"DoesAssetExist<Dictionary<string, string>>(\"{name}\") = {exists}.", LogLevel.Info);
    }

    private void CommandTestTexture(string command, string[] args)
    {
        string name = args.Length > 0 ? args[0] : "characters/abigail";

        this.TestLoad("SMAPI helper", () => this.Helper.GameContent.Load<Texture2D>(name).Width.ToString());
        this.TestLoad("game content manager", () => Game1.content.Load<Texture2D>(name).Width.ToString());
    }

    private void TestLoad(string label, Func<Dictionary<string, string>> load)
    {
        try
        {
            Dictionary<string, string> data = load();
            this.Monitor.Log($"{label}: loaded {data.Count} entries.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{label}: FAILED - {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
        }
    }

    private void TestLoad(string label, Func<string> load)
    {
        try
        {
            string result = load();
            this.Monitor.Log($"{label}: loaded ({result}).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"{label}: FAILED - {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
        }
    }

    private void CommandRescan(string command, string[] args)
    {
        this.Map = new CaseMap(Constants.ContentPath);
        this.Monitor.Log($"Reindexed {this.Map.Count} content asset names.", LogLevel.Info);
    }

    private void CommandToggle(string command, string[] args)
    {
        this.Enabled = !this.Enabled;
        this.Monitor.Log($"Asset name case fixes are now {(this.Enabled ? "enabled" : "disabled")} for this session.", LogLevel.Info);
    }

    private void CommandStats(string command, string[] args)
    {
        this.Monitor.Log($"Asset name lookups: {this.LookupCount:n0}; rewritten: {this.RewriteCount:n0}.", LogLevel.Info);
    }
}
