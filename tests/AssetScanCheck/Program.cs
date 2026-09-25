using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MrGlim.HorseTack.Framework;
using StardewModdingAPI;

// Builds a fake mod folder, runs AssetRegistry.Reload(), and checks what the wizard would offer.
internal static class Program
{
    private static int Failures;

    private static int Main()
    {
        Log.Init(ConsoleMonitor.Create());
        Log.Verbose = false;
        string root = Path.Combine(Path.GetTempPath(), "horsetack-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 1. no assets folder at all, then empty folders: nothing offered, no crash
            var registry = new AssetRegistry(FakeHelper.Create(root, elleDir: null));
            Directory.CreateDirectory(root);
            registry.Reload();
            Check(registry.TotalCount == 0, "missing assets folder -> 0 options");
            foreach (string d in new[] { "coats", "saddles", "pads", "bridles", "styles" })
                Directory.CreateDirectory(Path.Combine(root, "assets", d));
            registry.Reload();
            Check(registry.TotalCount == 0, "empty folders -> 0 options");
            Check(registry.IsValid(TackLayer.Saddle, ""), "None is always valid");
            // another player's art this computer doesn't have: accepted if well-formed, drawn as vanilla/none here
            Check(registry.IsValid(TackLayer.Saddle, "saddles/brown"), "unknown but well-formed id accepted (multiplayer: host may lack a farmhand's art)");
            Check(registry.IsValid(TackLayer.Coat, "Elle.CuterHorses/SolidBrown"), "unknown Elle id accepted");
            Check(!registry.IsValid(TackLayer.Saddle, "../../evil"), "malformed id rejected");
            Check(!registry.IsValid(TackLayer.Saddle, new string('a', 200) + "/b"), "overlong id rejected");
            Check(registry.GetPixels("Elle.CuterHorses/SolidBrown", BodyShape.Elle) == null, "missing art -> null pixels, no exception");
            Check(registry.GetPixels("saddles/brown") == null, "missing art again -> null (logged once)");

            // 2. mixed-case folders and files
            Directory.Delete(Path.Combine(root, "assets"), true);
            string assets = Path.Combine(root, "Assets");
            Png(assets, "Coats", "PintoSilver.png", 1);
            Png(assets, "Coats", "Solid_Brown.PNG", 2);
            Png(assets, "Coats", "PrismaticOverlay.png", 3);            // routed to styles
            Png(assets, "SADDLES", "Saddle_Brown.png", 4);
            Png(assets, "SADDLES", "Brown.png", 5);                     // same name as Saddle_Brown -> duplicate
            Png(assets, "SADDLES", "Pad_Red.png", 6);                   // prefix routes to pads
            Png(assets, "SADDLES", "Saddle_Light_Blue.png", 7);
            Png(assets, "pads", "Blue.png", 8);
            Png(assets, "pads", "BlueCopy.png", 8);                     // identical content -> duplicate
            Png(assets, "Bridles", "Bridle_Black.png", 9);
            Png(assets, "bridles", "TooBig.png", 10, 448, 256);         // wrong size -> skipped
            File.WriteAllText(Path.Combine(assets, "bridles", "NotAPng.png"), "hello");  // not a PNG -> skipped
            Png(assets, "styles", "Rainbow Mane.png", 11);
            Png(assets, "misc", "Ignored.png", 12);                     // unknown folder ignored

            registry.Reload();
            string Dump(TackLayer l) => string.Join(", ", registry.Get(l).Select(o => $"{o.Id}='{o.DisplayName}'"));
            foreach (TackLayer l in TackLayers.DrawOrder)
                Console.WriteLine($"  {l}: {Dump(l)}");

            Check(registry.Get(TackLayer.Coat).Select(o => o.Id).SequenceEqual(new[] { "coats/pinto-silver", "coats/solid-brown" }), "coats ids");
            Check(registry.Get(TackLayer.Coat).Select(o => o.DisplayName).SequenceEqual(new[] { "Pinto Silver", "Solid Brown" }), "coat names");
            Check(registry.Get(TackLayer.Style).Select(o => o.Id).SequenceEqual(new[] { "styles/prismatic", "styles/rainbow-mane" }), "styles (overlay routed from coats)");
            Check(registry.Get(TackLayer.Saddle).Select(o => o.Id).SequenceEqual(new[] { "saddles/brown", "saddles/light-blue" }), "saddles deduped by name, prefix stripped");
            Check(registry.Get(TackLayer.Saddle).Select(o => o.DisplayName).SequenceEqual(new[] { "Brown", "Light Blue" }), "saddle names");
            Check(registry.Get(TackLayer.Pad).Select(o => o.Id).SequenceEqual(new[] { "pads/blue", "pads/red" }), "pads: prefix-routed + content dedupe");
            Check(registry.Get(TackLayer.Bridle).Select(o => o.Id).SequenceEqual(new[] { "bridles/black" }), "bridles: bad size / non-PNG skipped");
            Check(registry.TotalCount == 9, "total 9");
            Check(registry.Canonical("pads/blue-copy") == "pads/blue", "content duplicate aliases to kept option");
            Check(registry.IsValid(TackLayer.Pad, "PADS/BLUE"), "ids case-insensitive");
            Check(!registry.IsValid(TackLayer.Saddle, "pads/blue"), "id must match layer");
            Check(registry.Get(TackLayer.Saddle)[0].RelativePath == "Assets/SADDLES/Brown.png" || registry.Get(TackLayer.Saddle)[0].RelativePath == "Assets/SADDLES/Saddle_Brown.png", "relative path keeps on-disk case");

            // 3. @elle fit variants and collections.json
            Directory.Delete(assets, true);
            assets = Path.Combine(root, "assets");
            Png(assets, "saddles", "SpiritsEve_Pumpkin.png", 20);
            Png(assets, "saddles", "SpiritsEve_Pumpkin@elle.png", 21);   // variant, not its own option
            Png(assets, "pads", "Lewis_LuckyPurpleShorts.png", 22);
            Png(assets, "pads", "Orphan@elle.png", 23);                  // no base file -> ignored
            Png(assets, "coats", "Galaxy_Stardust.png", 24);
            Png(assets, "styles", "Plain.png", 25);                      // no prefix -> "Other"
            File.WriteAllText(Path.Combine(assets, "collections.json"), "{ // comment\n \"Collections\": { \"SpiritsEve\": \"Spirit's Eve\", \"Lewis\": \"Lewis\", \"Galaxy\": \"Galaxy\" },\n \"Names\": { \"Lewis_LuckyPurpleShorts\": \"Lucky Purple Shorts\" }, }");
            registry.Reload();
            foreach (TackLayer l in TackLayers.DrawOrder)
                Console.WriteLine($"  {l}: {Dump(l)}");
            TackOption pumpkin = registry.Get(TackLayer.Saddle).Single();
            Check(pumpkin.Id == "saddles/spirits-eve-pumpkin", "variant base id");
            Check(pumpkin.DisplayName == "Spirit's Eve: Pumpkin" && pumpkin.ShortName == "Pumpkin" && pumpkin.Collection == "Spirit's Eve", "collection display name");
            Check(pumpkin.ElleVariantRelativePath == "assets/saddles/SpiritsEve_Pumpkin@elle.png", "@elle variant attached to base");
            Check(pumpkin.Source == "HorseTack" && pumpkin.Shape == BodyShape.Vanilla, "bundled source/shape");
            Check(registry.Get(TackLayer.Pad).Single().DisplayName == "Lewis: Lucky Purple Shorts", "name override");
            Check(registry.Get(TackLayer.Pad).Single().ElleVariantRelativePath == null, "orphan variant ignored");
            Check(registry.Get(TackLayer.Style).Single().Collection == "collection.other" && registry.Get(TackLayer.Style).Single().DisplayName == "Plain", "unprefixed art -> Other");
            Check(registry.TotalCount == 4, "variants not counted as options");

            // 4. Elle's Cuter Horses installed (fake folder found via the mod registry)
            string elle = Path.Combine(root, "EllesCuterHorses");
            Png(Path.Combine(elle, "assets"), "Horse", "SolidBrown.png", 30);
            Png(Path.Combine(elle, "assets"), "Horse", "AppaloosaBlack.png", 31);
            Png(Path.Combine(elle, "assets"), "Horse", "Red.png", 32);
            Png(Path.Combine(elle, "assets"), "Horse", "WhiteShire.png", 33);
            Png(Path.Combine(elle, "assets"), "Horse", "Andalusian.png", 34);
            Png(Path.Combine(elle, "assets"), "Horse", "PrismaticOverlay.png", 35);
            Png(Path.Combine(elle, "assets"), "Saddles", "Saddle_Brown.png", 36);
            Png(Path.Combine(elle, "assets"), "Saddles", "Pad_LightBlue.png", 37);
            Png(Path.Combine(elle, "assets"), "Saddles", "Bridle_Black.png", 38);
            Png(Path.Combine(elle, "assets"), "Saddles", "Readme.png", 39);          // no tack prefix -> ignored
            Png(Path.Combine(elle, "assets"), "Saddles", "Saddle_Big.png", 40, 448, 256); // wrong size -> skipped
            var withElle = new AssetRegistry(FakeHelper.Create(root, elleDir: elle));
            withElle.Reload();
            foreach (TackLayer l in TackLayers.DrawOrder)
                Console.WriteLine($"  {l}: {string.Join(", ", withElle.Get(l).Select(o => $"{o.Id}='{o.DisplayName}' [{o.Source}/{o.Collection}]"))}");
            Check(withElle.CountFrom("Elle") == 9 && withElle.CountFrom("HorseTack") == 4, "both sources merged (9 Elle + 4 HorseTack)");
            Check(withElle.TryGet("Elle.CuterHorses/SolidBrown", out TackOption solid) && solid.Layer == TackLayer.Coat && solid.Shape == BodyShape.Elle && solid.Source == "Elle", "Elle coat id/shape/source");
            Check(solid.Collection == "collection.elle-family", "Elle family collection");
            Check(withElle.TryGet("Elle.CuterHorses/PrismaticOverlay", out TackOption prism) && prism.Layer == TackLayer.Style, "prismatic overlay is a style");
            Check(withElle.TryGet("Elle.CuterHorses/Pad_LightBlue", out TackOption pad) && pad.Layer == TackLayer.Pad && pad.DisplayName == "Light Blue", "Elle pad");
            Check(!withElle.TryGet("Elle.CuterHorses/Readme", out _), "unprefixed Elle tack file ignored");
            Check(withElle.Get(TackLayer.Saddle).Select(o => o.Source).SequenceEqual(new[] { "HorseTack", "Elle" }), "HorseTack art listed before Elle art");
            Check(withElle.Get(TackLayer.Coat).Select(o => o.Id).SequenceEqual(new[] { "coats/galaxy-stardust", "Elle.CuterHorses/SolidBrown", "Elle.CuterHorses/AppaloosaBlack", "Elle.CuterHorses/WhiteShire", "Elle.CuterHorses/Red", "Elle.CuterHorses/Andalusian" }), "Elle coats ordered by family");
            Check(withElle.Collections(TackLayer.Coat).Count == 4, "coat collections: Galaxy + 3 Elle groups (family, colours, breeds)");
            Check(AssetRegistry.ElleCoatFamily("VoidShire") == "collection.elle-family" && AssetRegistry.ElleCoatFamily("Teal") == "collection.elle-colours" && AssetRegistry.ElleCoatFamily("Epona") == "collection.elle-breeds", "Elle family grouping");

            // 5. layer order: pads go under saddles, so an Elle pad never paints over a HorseTack saddle (or the other way round)
            var mixed = new TackSelection { Saddle = "saddles/spirits-eve-pumpkin", Pad = "Elle.CuterHorses/Pad_LightBlue" };
            Check(withElle.OverlayOrder(mixed).SequenceEqual(new[] { TackLayer.Style, TackLayer.Pad, TackLayer.Saddle, TackLayer.Bridle }), "Elle pad drawn under HorseTack saddle");
            var hers = new TackSelection { Saddle = "Elle.CuterHorses/Saddle_Brown", Pad = "pads/lewis-lucky-purple-shorts" };
            Check(withElle.OverlayOrder(hers).SequenceEqual(new[] { TackLayer.Style, TackLayer.Pad, TackLayer.Saddle, TackLayer.Bridle }), "HorseTack pad drawn under Elle saddle");

            // 6. per-season files: Name.<season>.png / Name.<season>@elle.png attach to Name.png and aren't options of their own
            Directory.Delete(assets, true);
            Png(assets, "pads", "ForestSpirit_MossCloak.png", 50);
            Png(assets, "pads", "ForestSpirit_MossCloak@elle.png", 51);
            Png(assets, "pads", "ForestSpirit_MossCloak.fall.png", 52);
            Png(assets, "pads", "ForestSpirit_MossCloak.Winter.png", 53);
            Png(assets, "pads", "ForestSpirit_MossCloak.fall@elle.png", 54);
            Png(assets, "pads", "Orphan.spring.png", 55);                 // no base file -> ignored
            Png(assets, "styles", "Crown.png", 56);
            Png(assets, "styles", "Crown.spring.png", 57);                // no @elle fit at all -> vanilla seasonal used on Elle bodies
            Png(assets, "saddles", "Plain.png", 58);
            Png(assets, "saddles", "Plain@elle.png", 59);
            Png(assets, "saddles", "Plain.fall.png", 60);                 // has @elle fit but no fall@elle -> Elle bodies keep the @elle fit
            var seasons = new AssetRegistry(FakeHelper.Create(root, elleDir: null)) { SeasonProvider = () => "Fall" };
            seasons.Reload();
            Check(seasons.TotalCount == 3, "seasonal files not counted as options (3 options)");
            TackOption cloak = seasons.Get(TackLayer.Pad).Single();
            Check(cloak.Id == "pads/forest-spirit-moss-cloak" && cloak.IsSeasonal && cloak.SeasonalPaths.Count == 3, "seasonal files attached to the base option (same id)");
            Check(cloak.ResolvePath(false, "fall") == "assets/pads/ForestSpirit_MossCloak.fall.png", "fall, vanilla body");
            Check(cloak.ResolvePath(true, "fall") == "assets/pads/ForestSpirit_MossCloak.fall@elle.png", "fall, Elle body");
            Check(cloak.ResolvePath(false, "winter") == "assets/pads/ForestSpirit_MossCloak.Winter.png", "season names case-insensitive");
            Check(cloak.ResolvePath(true, "winter") == "assets/pads/ForestSpirit_MossCloak@elle.png", "Elle body without that season's fit keeps the @elle fit");
            Check(cloak.ResolvePath(false, "spring") == "assets/pads/ForestSpirit_MossCloak.png", "missing season -> base file");
            Check(cloak.ResolvePath(false, null) == "assets/pads/ForestSpirit_MossCloak.png", "no season -> base file");
            TackOption crown = seasons.Get(TackLayer.Style).Single();
            Check(crown.ResolvePath(true, "spring") == "assets/styles/Crown.spring.png", "no @elle fit -> vanilla seasonal on Elle body");
            Check(seasons.Get(TackLayer.Saddle).Single().ResolvePath(true, "fall") == "assets/saddles/Plain@elle.png", "body fit wins over season");
            Check(seasons.CurrentSeason() == "fall", "season provider normalised");
            Check(seasons.SeasonKey(new TackSelection { Saddle = "saddles/plain", Pad = "pads/forest-spirit-moss-cloak" }) == "@fall", "season in cache key when a seasonal layer is chosen");
            Check(seasons.SeasonKey(new TackSelection { Style = "styles/unknown" }) == "", "no season key without seasonal layers");
            seasons.SeasonProvider = () => "not-a-season";
            Check(seasons.CurrentSeason() == "summer", "bad season -> summer");

            // 7. pad-needs-saddle rule
            var sel = new TackSelection { Pad = "pads/blue" }.Normalize();
            Check(sel.Pad == "", "pad dropped without saddle");
            sel = new TackSelection { Saddle = "saddles/brown", Pad = "pads/blue" }.Normalize();
            Check(sel.Pad == "pads/blue", "pad kept with saddle");

            // 8. 1.4.1: a chosen HorseTack coat never depends on the game's horse texture (seasonal horse packs, asset propagation)
            {
                const int W = 224, H = 128;
                PixelData Solid(byte r, byte g, byte b, byte a = 255) => new(W, H, Enumerable.Repeat(new Microsoft.Xna.Framework.Color(r, g, b, a), W * H).ToArray());
                PixelData SaddleOverlay()
                {
                    var data = new Microsoft.Xna.Framework.Color[W * H];
                    data[0] = new Microsoft.Xna.Framework.Color(200, 0, 0, 255); // one opaque saddle pixel, the rest transparent
                    return new PixelData(W, H, data);
                }
                var coatPx = Solid(10, 20, 30);
                var springBase = Solid(0, 255, 0);  // a seasonal pack's tinted horse
                var winterBase = Solid(0, 0, 255);
                var overlay = SaddleOverlay();
                var order = new[] { TackLayer.Style, TackLayer.Pad, TackLayer.Saddle, TackLayer.Bridle };
                PixelData? Pixels(string id, BodyShape shape) => id == "coats/galaxy" ? coatPx : id == "saddles/iridium" ? overlay : null;
                BodyShape? CoatShape(string id) => id == "coats/galaxy" ? BodyShape.Vanilla : null;

                int baseReads = 0;
                PixelData? current = springBase;
                PixelData? LoadBase() { baseReads++; return current; }

                var chosen = new TackSelection { Coat = "coats/galaxy", Saddle = "saddles/iridium" };
                PixelData? a = TextureManager.ComposePixels(chosen, Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase);
                current = winterBase; // the pack switches season (asset invalidated + propagated)
                PixelData? b = TextureManager.ComposePixels(chosen, Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase);
                Check(baseReads == 0, "chosen coat: the game's horse texture is never read");
                Check(a != null && b != null && a.Data.SequenceEqual(b.Data), "chosen coat: identical composite before and after the base changes");
                Check(a != null && a.Data[1] == coatPx.Data[1] && a.Data[0] == overlay.Data[0], "chosen coat: coat pixels + tack on top");

                var keep = new TackSelection { Saddle = "saddles/iridium" };
                current = springBase;
                PixelData? k1 = TextureManager.ComposePixels(keep, Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase);
                current = winterBase;
                PixelData? k2 = TextureManager.ComposePixels(keep, Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase);
                Check(k1 != null && k2 != null && k1.Data[1] == springBase.Data[1] && k2.Data[1] == winterBase.Data[1] && k2.Data[0] == overlay.Data[0],
                    "Keep current + tack: follows the live base, tack stays on top");

                Check(TextureManager.ComposePixels(new TackSelection(), Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase) == null,
                    "Keep current, no tack: no composite (the horse shows the live game texture)");

                baseReads = 0;
                var missing = new TackSelection { Coat = "coats/not-installed-here", Saddle = "saddles/iridium" };
                PixelData? m = TextureManager.ComposePixels(missing, Pixels, CoatShape, BodyShape.Vanilla, order, LoadBase);
                Check(baseReads == 1 && m != null && m.Data[1] == winterBase.Data[1], "synced coat missing on this computer: falls back to the live base");
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }

        Console.WriteLine(Failures == 0 ? "AssetScanCheck: PASS" : $"AssetScanCheck: FAIL ({Failures})");
        return Failures == 0 ? 0 : 1;
    }

    private static void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}");
        if (!ok) Failures++;
    }

    /// <summary>Write a minimal PNG-like file: real signature + IHDR size, and a seed so content differs.</summary>
    private static void Png(string assets, string folder, string name, byte seed, int w = 224, int h = 128)
    {
        string dir = Path.Combine(assets, folder);
        Directory.CreateDirectory(dir);
        byte[] b = new byte[40];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        b[39] = seed;
        File.WriteAllBytes(Path.Combine(dir, name), b);
    }
}

/// <summary>An IModHelper stub providing DirectoryPath, a mod registry (Elle installed or not) and a content pack factory.</summary>
public class FakeHelper : DispatchProxy
{
    public string Dir = "";
    public string? ElleDir;

    public static IModHelper Create(string dir, string? elleDir)
    {
        IModHelper proxy = Create<IModHelper, FakeHelper>();
        ((FakeHelper)(object)proxy).Dir = dir;
        ((FakeHelper)(object)proxy).ElleDir = elleDir;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        "get_DirectoryPath" => this.Dir,
        "get_ModRegistry" => FakeRegistry.Create(this.ElleDir),
        "get_ContentPacks" => Create<IContentPackHelper, FakePackHelper>(),
        _ => null
    };
}

public class FakeRegistry : DispatchProxy
{
    public string? ElleDir;

    public static IModRegistry Create(string? elleDir)
    {
        IModRegistry proxy = Create<IModRegistry, FakeRegistry>();
        ((FakeRegistry)(object)proxy).ElleDir = elleDir;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        "Get" => this.ElleDir != null && (string?)args![0] == "Elle.CuterHorses" ? new FakeModInfo(this.ElleDir) : null,
        "IsLoaded" => this.ElleDir != null && (string?)args![0] == "Elle.CuterHorses",
        _ => null
    };
}

/// <summary>Like SMAPI's IModMetadata: exposes DirectoryPath (read by reflection).</summary>
public class FakeModInfo : IModInfo
{
    public FakeModInfo(string dir) => this.DirectoryPath = dir;
    public string DirectoryPath { get; }
    public IManifest Manifest => null!;
    public bool IsContentPack => true;
}

public class FakePackHelper : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod?.Name == "CreateTemporary" ? Create<IContentPack, FakePack>() : null;
}

public class FakePack : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
}

/// <summary>Prints mod log lines to the console, prefixed with their level.</summary>
public class ConsoleMonitor : DispatchProxy
{
    public static IMonitor Create() => Create<IMonitor, ConsoleMonitor>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "Log")
            Console.WriteLine($"    [{args![1]}] {args[0]}");
        return null;
    }
}
