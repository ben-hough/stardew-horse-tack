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
        string root = Path.Combine(Path.GetTempPath(), "horsetack-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 1. no assets folder at all, then empty folders: nothing offered, no crash
            var registry = new AssetRegistry(FakeHelper.Create(root));
            Directory.CreateDirectory(root);
            registry.Reload();
            Check(registry.TotalCount == 0, "missing assets folder -> 0 options");
            foreach (string d in new[] { "coats", "saddles", "pads", "bridles", "styles" })
                Directory.CreateDirectory(Path.Combine(root, "assets", d));
            registry.Reload();
            Check(registry.TotalCount == 0, "empty folders -> 0 options");
            Check(registry.IsValid(TackLayer.Saddle, ""), "None is always valid");
            Check(!registry.IsValid(TackLayer.Saddle, "saddles/brown"), "unknown id invalid when empty");

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

            // 3. pad-needs-saddle rule
            var sel = new TackSelection { Pad = "pads/blue" }.Normalize();
            Check(sel.Pad == "", "pad dropped without saddle");
            sel = new TackSelection { Saddle = "saddles/brown", Pad = "pads/blue" }.Normalize();
            Check(sel.Pad == "pads/blue", "pad kept with saddle");
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

/// <summary>An IModHelper stub that only provides DirectoryPath.</summary>
public class FakeHelper : DispatchProxy
{
    public string Dir = "";

    public static IModHelper Create(string dir)
    {
        IModHelper proxy = Create<IModHelper, FakeHelper>();
        ((FakeHelper)(object)proxy).Dir = dir;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod?.Name == "get_DirectoryPath" ? this.Dir : null;
}
