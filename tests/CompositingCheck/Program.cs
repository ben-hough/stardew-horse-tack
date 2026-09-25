using System;
using System.IO;
using Microsoft.Xna.Framework;
using MrGlim.HorseTack.Framework;

// usage: CompositingCheck <out.raw> <base.raw> [overlay.raw ...]   (raw = premultiplied RGBA bytes, all the same size)
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: CompositingCheck <out.raw> <base.raw> [overlay.raw ...]");
    return 2;
}
Color[] Read(string path)
{
    byte[] b = File.ReadAllBytes(path);
    var c = new Color[b.Length / 4];
    for (int i = 0; i < c.Length; i++)
        c[i] = new Color(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
    return c;
}
Color[] result = Read(args[1]);
for (int i = 2; i < args.Length; i++)
{
    Color[] overlay = Read(args[i]);
    if (overlay.Length != result.Length)
    {
        Console.Error.WriteLine($"size mismatch: {args[i]}");
        return 3;
    }
    Compositor.OverPremultiplied(result, overlay);
}
var outBytes = new byte[result.Length * 4];
for (int i = 0; i < result.Length; i++)
{
    outBytes[i * 4] = result[i].R;
    outBytes[i * 4 + 1] = result[i].G;
    outBytes[i * 4 + 2] = result[i].B;
    outBytes[i * 4 + 3] = result[i].A;
}
File.WriteAllBytes(args[0], outBytes);
Console.WriteLine($"composed {args.Length - 2} overlays over {result.Length} px");
return 0;
