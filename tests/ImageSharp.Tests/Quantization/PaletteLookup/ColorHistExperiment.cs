using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Quantization.PaletteLookup
{
    public class ColorHistExperiment
    {
        private ITestOutputHelper output;

        public ColorHistExperiment(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static TheoryData<int, int, string, Color[]> Data = new TheoryData<int, int, string, Color[]>()
            {
                { 4, 4, nameof(Color.WernerPalette), Color.WernerPalette.ToArray() },
                { 4, 4, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() },
                { 5, 5, nameof(Color.WernerPalette), Color.WernerPalette.ToArray() },
                { 5, 5, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() },
                { 5, 6, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() },
                { 6, 6, nameof(Color.WernerPalette), Color.WernerPalette.ToArray() },
                { 6, 6, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() },
                { 6, 7, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() },
                { 7, 7, nameof(Color.WebSafePalette), Color.WebSafePalette.ToArray() }
            };

        struct BitInfo
        {
            public int BitsUsed;
            public int Size;
            public int RemainderBits;
            public int RemainderCount;

            public BitInfo(int bitsUsed)
            {
                this.BitsUsed = bitsUsed;
                this.Size = 1 << bitsUsed;
                this.RemainderBits = 8 - bitsUsed;
                this.RemainderCount = (1 << this.RemainderBits) - 1;
            }
        }

        [Theory]
        [MemberData(nameof(Data))]
        public void TestCollisions(int rbBits, int gBits, string paletteName, Color[] palette)
        {
            Rgb24[] rgb = palette.Select(p => p.ToRgb24()).ToArray();

            var iR = new BitInfo(rbBits);
            var iG = new BitInfo(gBits);
            var iB = new BitInfo(rbBits);

            var hist = new List<Rgb24>[iR.Size, iG.Size, iB.Size];

            foreach (Rgb24 c in rgb)
            {
                int r = c.R >> iR.RemainderBits;
                int g = c.G >> iG.RemainderBits;
                int b = c.B >> iB.RemainderBits;

                List<Rgb24> cols = hist[r, g, b];
                if (cols == null)
                {
                    cols = new List<Rgb24>();
                    hist[r, g, b] = cols;
                }

                cols.Add(c);
            }

            int maxCollision = -1;

            for (int r = 0; r < iR.Size; r++)
            {
                for (int g = 0; g < iG.Size; g++)
                {
                    for (int b = 0; b < iB.Size; b++)
                    {
                        List<Rgb24> cols = hist[r, g, b];
                        int cnt = cols?.Count ?? 0;
                        maxCollision = Math.Max(maxCollision, cnt);
                        if (cnt > 1)
                        {
                            int r0 = r << iR.RemainderBits;
                            int r1 = r0 + iR.RemainderCount;
                            int g0 = g << iG.RemainderBits;
                            int g1 = g0 + iG.RemainderCount;
                            int b0 = b << iB.RemainderBits;
                            int b1 = b0 + iB.RemainderCount;
                            this.output.WriteLine($"{cnt} @ ({r0},{g0},{b0})..({r1},{g1},{b1})");
                            this.output.WriteLine(string.Concat(cols));
                        }
                    }
                }
            }

            this.output.WriteLine("Max: " + maxCollision);
        }
    }
}
