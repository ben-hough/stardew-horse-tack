using Microsoft.Xna.Framework;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Pure pixel maths for layering sprite sheets. Game textures are premultiplied-alpha, so "source over" is out = src + dst * (1 - srcA).</summary>
    internal static class Compositor
    {
        /// <summary>Draw <paramref name="src"/> over <paramref name="dst"/> in place. Both arrays hold premultiplied colours of the same size.</summary>
        public static void OverPremultiplied(Color[] dst, Color[] src)
        {
            int n = dst.Length < src.Length ? dst.Length : src.Length;
            for (int i = 0; i < n; i++)
            {
                Color s = src[i];
                int sa = s.A;
                if (sa == 0)
                    continue;
                if (sa == 255)
                {
                    dst[i] = s;
                    continue;
                }
                Color d = dst[i];
                int inv = 255 - sa;
                dst[i] = new Color(
                    (byte)Clamp(s.R + Div255(d.R * inv)),
                    (byte)Clamp(s.G + Div255(d.G * inv)),
                    (byte)Clamp(s.B + Div255(d.B * inv)),
                    (byte)Clamp(sa + Div255(d.A * inv))
                );
            }
        }

        /// <summary>Rounded x/255 for 0..65025.</summary>
        private static int Div255(int x)
        {
            x += 128;
            return (x + (x >> 8)) >> 8;
        }

        private static int Clamp(int v) => v > 255 ? 255 : v;
    }
}
