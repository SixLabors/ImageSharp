// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Quantization.PaletteLookup
{
    public class KdTreePaletteLookupTests
    {
        private readonly ITestOutputHelper output;

        [Fact]
        public void Precondition_LinearLookupIsCorrect()
        {
            Rgba32[] data =
            {
                new Rgba32(10, 20, 30, 40),
                new Rgba32(50, 60, 70, 80),
                new Rgba32(90, 100, 110, 120),
                new Rgba32(130, 140, 150, 160),
                new Rgba32(170, 180, 190, 200),
            };

            var lookup = new LinearPaletteLookup<Rgba32>(Configuration.Default, data);

            var c0 = new Rgba32(10, 20, 30, 40);
            var c3 = new Rgba32(130, 140, 150, 160);
            var c2 = new Rgba32(81, 102, 106, 123);

            Assert.Equal(0, lookup.GetPaletteIndexFor(c0));
            Assert.Equal(2, lookup.GetPaletteIndexFor(c2));
            Assert.Equal(3, lookup.GetPaletteIndexFor(c3));
        }

        [Fact]
        public void Constructor_BuildsBalancedTree()
        {
            var a = new Rgba32(7, 6, 0);
            var b = new Rgba32(1, 3, 2);
            var c = new Rgba32(2, 1, 3);
            var d = new Rgba32(5, 1, 2);
            var e = new Rgba32(7, 2, 1);
            var f = new Rgba32(4, 4, 4);

            Rgba32[] palette = { a, b, c, d, e, f };
            var lookup = new KdTreePaletteLookup<Rgba32>(Configuration.Default, palette);
            ushort[] ind = lookup.PaletteIndices;

            Assert.Equal(d, palette[ind[0]]);
            Assert.Equal(b, palette[ind[1]]);
            Assert.Equal(a, palette[ind[2]]);
            Assert.Equal(c, palette[ind[3]]);
            Assert.Equal(f, palette[ind[4]]);
            Assert.Equal(e, palette[ind[5]]);
        }

        [Fact]
        public void GetPaletteIndexFor_Basic()
        {
            var a = new Rgba32(7, 6, 0);
            var b = new Rgba32(1, 3, 2);
            var c = new Rgba32(2, 1, 3);
            var d = new Rgba32(5, 1, 2);
            var e = new Rgba32(7, 2, 1);
            var f = new Rgba32(4, 4, 4);

            Rgba32[] originalPalette = { a, b, c, d, e, f };

            var lookup = new KdTreePaletteLookup<Rgba32>(Configuration.Default, originalPalette);

            Assert.Equal(0, lookup.GetPaletteIndexFor(a));
            Assert.Equal(5, lookup.GetPaletteIndexFor(f));
            Assert.Equal(4, lookup.GetPaletteIndexFor(e));
            Assert.Equal(3, lookup.GetPaletteIndexFor(new Rgba32(5, 1, 1)));
            Assert.Equal(0, lookup.GetPaletteIndexFor(new Rgba32(8, 5, 0)));
            Assert.Equal(1, lookup.GetPaletteIndexFor(new Rgba32(0, 3, 3)));
        }

        public static TheoryData<object, Color[]> LookupData = new TheoryData<object, Color[]>()
        {
            { new TestPixel<Rgba32>(), Color.WebSafePalette.ToArray() },
            { new TestPixel<Rgba32>(), CreateDegeneratePalette() },
            { new TestPixel<Rgba32>(), CreateRandomPalette(1) },
            { new TestPixel<Rgba32>(), CreateRandomPalette(2) },
            { new TestPixel<Rgba32>(), CreateRandomPalette(3) },
            { new TestPixel<Rgba32>(), CreateRandomPalette(13) },
            { new TestPixel<Rgba32>(), CreateRandomPalette(256) },
            { new TestPixel<Rgb24>(), Color.WernerPalette.ToArray() },
            { new TestPixel<Rgb24>(), CreateDegeneratePalette() },
            { new TestPixel<Bgra32>(), CreateRandomPalette(13) },
            { new TestPixel<Bgr24>(), CreateRandomPalette(27) },
        };

        public KdTreePaletteLookupTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [MemberData(nameof(LookupData))]
        public void GetPaletteIndexFor_FetchesSameColorsAsLinear<TPixel>(TestPixel<TPixel> dummy, Color[] paletteSource)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel[] palette = paletteSource.Select(c => c.ToPixel<TPixel>()).ToArray();
            var rnd = new Random(paletteSource.Length + typeof(TPixel).GetHashCode());

            TPixel[] toLookup =
            {
                palette.First(),
                palette.Last(),
                Color.FromRgb(123, 13, 192).ToPixel<TPixel>(),
                Color.FromRgba(0, 255, 17, 200).ToPixel<TPixel>(),
                Color.FromRgba(240, 111, 3, 0).ToPixel<TPixel>(),
                GetRandomColor(rnd).ToPixel<TPixel>(),
                GetRandomColor(rnd).ToPixel<TPixel>(),
                GetRandomColor(rnd).ToPixel<TPixel>(),
            };

            Action<string> print = null;
            if (paletteSource.Length == 3)
            {
                print = s => this.output.WriteLine(s);
            }

            var linear = new LinearPaletteLookup<TPixel>(Configuration.Default, palette);
            var kd = new KdTreePaletteLookup<TPixel>(Configuration.Default, palette, print);

            for (int i = 0; i < toLookup.Length; i++)
            {
                ref TPixel c = ref toLookup[i];
                byte linearIndex = linear.GetPaletteIndexFor(c);
                byte kdIndex = kd.GetPaletteIndexFor(c);

                if (linearIndex != kdIndex)
                {
                    kd.GetPaletteIndexFor(c);
                }

                Assert.True(linearIndex == kdIndex, $"{linearIndex} != {kdIndex} @ test color {i}");
            }
        }

        private static Color GetRandomColor(Random rnd)
        {
            byte r = (byte)(rnd.Next() & 255);
            byte g = (byte)(rnd.Next() & 255);
            byte b = (byte)(rnd.Next() & 255);
            byte a = (byte)(rnd.Next() & 255);
            return Color.FromRgba(r, g, b, a);
        }

        private static Color[] CreateDegeneratePalette()
        {
            var result = new Color[199];
            for (int i = 0; i < result.Length; i++)
            {
                var c = new Rgb24(100, (byte)i, 100);
                result[i] = c;
            }

            return result;
        }

        private static Color[] CreateRandomPalette(int length)
        {
            var rnd = new Random(length);

            byte[] raw = rnd.GenerateRandomByteArray(length * 4);
            Span<Rgba32> rgbaData = MemoryMarshal.Cast<byte, Rgba32>(raw);
            var result = new Color[rgbaData.Length];
            for (int i = 0; i < rgbaData.Length; i++)
            {
                result[i] = rgbaData[i];
            }

            return result;
        }
    }
}
