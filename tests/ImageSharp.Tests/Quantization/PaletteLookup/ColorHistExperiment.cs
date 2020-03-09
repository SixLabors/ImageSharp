using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1516
#pragma warning disable SA1028
#pragma warning disable SA1509

namespace SixLabors.ImageSharp.Tests.Quantization.PaletteLookup
{
    public class ColorHistExperiment
    {
        private ITestOutputHelper output;

        public ColorHistExperiment(ITestOutputHelper output)
        {
            this.output = output;
        }

        public static TheoryData<int, int, string> Data = new TheoryData<int, int, string>()
            {
                { 3, 3, nameof(WebSafePalette) },
                { 4, 4, nameof(WebSafePalette) },
                { 4, 4, nameof(WebSafePalette) },
                { 5, 5, nameof(WebSafePalette) },
                { 5, 5, nameof(WebSafePalette) },
                { 5, 6, nameof(WebSafePalette) },
                { 6, 6, nameof(WebSafePalette) },
                { 6, 6, nameof(WebSafePalette) },
                { 6, 7, nameof(WebSafePalette) },
                { 7, 7, nameof(WebSafePalette) },

                { 3, 3, nameof(Ducky) },
                { 4, 4, nameof(Ducky) },
                { 4, 4, nameof(Ducky) },
                { 5, 5, nameof(Ducky) },
                { 5, 5, nameof(Ducky) },
                { 5, 6, nameof(Ducky) },
                { 6, 6, nameof(Ducky) },
                { 6, 6, nameof(Ducky) },
                { 6, 7, nameof(Ducky) },
                { 7, 7, nameof(Ducky) },

                { 3, 3, nameof(Bike) },
                { 4, 4, nameof(Bike) },
                { 4, 4, nameof(Bike) },
                { 5, 5, nameof(Bike) },
                { 5, 5, nameof(Bike) },
                { 5, 6, nameof(Bike) },
                { 6, 6, nameof(Bike) },
                { 6, 6, nameof(Bike) },
                { 6, 7, nameof(Bike) },
                { 7, 7, nameof(Bike) },

                { 3, 3, nameof(Rgb48Bpp) },
                { 4, 4, nameof(Rgb48Bpp) },
                { 4, 4, nameof(Rgb48Bpp) },
                { 5, 5, nameof(Rgb48Bpp) },
                { 5, 5, nameof(Rgb48Bpp) },
                { 5, 6, nameof(Rgb48Bpp) },
                { 6, 6, nameof(Rgb48Bpp) },
                { 6, 6, nameof(Rgb48Bpp) },
                { 6, 7, nameof(Rgb48Bpp) },
                { 7, 7, nameof(Rgb48Bpp) },

                { 3, 3, nameof(CalliphoraPartial) },
                { 4, 4, nameof(CalliphoraPartial) },
                { 4, 4, nameof(CalliphoraPartial) },
                { 5, 5, nameof(CalliphoraPartial) },
                { 5, 5, nameof(CalliphoraPartial) },
                { 5, 6, nameof(CalliphoraPartial) },
                { 6, 6, nameof(CalliphoraPartial) },
                { 6, 6, nameof(CalliphoraPartial) },
                { 6, 7, nameof(CalliphoraPartial) },
                { 7, 7, nameof(CalliphoraPartial) },

                { 3, 3, nameof(CalliphoraPartialGrayscale) },
                { 4, 4, nameof(CalliphoraPartialGrayscale) },
                { 4, 4, nameof(CalliphoraPartialGrayscale) },
                { 5, 5, nameof(CalliphoraPartialGrayscale) },
                { 5, 5, nameof(CalliphoraPartialGrayscale) },
                { 5, 6, nameof(CalliphoraPartialGrayscale) },
                { 6, 6, nameof(CalliphoraPartialGrayscale) },
                { 6, 6, nameof(CalliphoraPartialGrayscale) },
                { 6, 7, nameof(CalliphoraPartialGrayscale) },
                { 7, 7, nameof(CalliphoraPartialGrayscale) },
            };

        public static Rgb24[] WebSafePalette => Color.WebSafePalette.ToArray().Select(c => c.ToRgb24()).ToArray();
        public static Rgb24[] WernerPalette => Color.WebSafePalette.ToArray().Select(c => c.ToRgb24()).ToArray();
        public static Rgb24[] Ducky => CreatePaletteFrom(TestImages.Png.Ducky);
        public static Rgb24[] Bike => CreatePaletteFrom(TestImages.Png.Bike);
        public static Rgb24[] Rgb48Bpp => CreatePaletteFrom(TestImages.Png.Rgb48Bpp);
        public static Rgb24[] CalliphoraPartial => CreatePaletteFrom(TestImages.Png.CalliphoraPartial);
        public static Rgb24[] CalliphoraPartialGrayscale => CreatePaletteFrom(TestImages.Png.CalliphoraPartialGrayscale);

        private static Rgb24[] CreatePaletteFrom(string imgPath)
        {
            using var img = Image.Load<Rgb24>(TestFile.Create(imgPath).Bytes);

            using IFrameQuantizer<Rgb24> q = new OctreeQuantizer().CreateFrameQuantizer<Rgb24>(Configuration.Default);
            q.BuildPalette(img.Frames.RootFrame, img.Bounds());
            return q.Palette.ToArray();
        }

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
        public void TestCollisions(int rbBits, int gBits, string paletteName)
        {
            var rgb = (Rgb24[])this.GetType().GetProperty(paletteName).GetValue(null);

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
