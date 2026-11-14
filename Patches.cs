using Microsoft.Xna.Framework.Content;

namespace AssetNameCaseFixes;

internal static class Patches
{
    public static void BeforeParseAssetName(ref string rawName)
    {
        ModEntry.Instance.TryRewrite(ref rawName);
    }

    public static void BeforeOpenStream(ContentManager __instance, ref string assetName)
    {
        ModEntry.Instance.TryRewrite(__instance, ref assetName);
    }
}
