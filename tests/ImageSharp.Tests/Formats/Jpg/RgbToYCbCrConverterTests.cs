// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Colorspaces.Conversion;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public class RgbToYCbCrConverterTests
    {
        public RgbToYCbCrConverterTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Fact]
        public void TestLutConverter()
        {
            Rgb24[] data = CreateTestData();
            var target = RgbToYCbCrConverterLut.Create();

            Block8x8F y = default;
            Block8x8F cb = default;
            Block8x8F cr = default;

            target.Convert(data.AsSpan(), ref y, ref cb, ref cr);

            Verify(data, ref y, ref cb, ref cr, new ApproximateColorSpaceComparer(1F));
        }

        [Fact]
        public void TestVectorizedConverter()
        {
            if (!RgbToYCbCrConverterVectorized.IsSupported)
            {
                this.Output.WriteLine("No AVX and/or FMA present, skipping test!");
                return;
            }

            Rgb24[] data = CreateTestData();

            Block8x8F y = default;
            Block8x8F cb = default;
            Block8x8F cr = default;

            RgbToYCbCrConverterVectorized.Convert(data.AsSpan(), ref y, ref cb, ref cr);

            Verify(data, ref y, ref cb, ref cr, new ApproximateColorSpaceComparer(0.0001F));
        }

        private static void Verify(ReadOnlySpan<Rgb24> data, ref Block8x8F yResult, ref Block8x8F cbResult, ref Block8x8F crResult, ApproximateColorSpaceComparer comparer)
        {
            for (int i = 0; i < data.Length; i++)
            {
                int r = data[i].R;
                int g = data[i].G;
                int b = data[i].B;

                float y = (0.299F * r) + (0.587F * g) + (0.114F * b);
                float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
                float cr = 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b));

                Assert.True(comparer.Equals(new YCbCr(y, cb, cr), new YCbCr(yResult[i], cbResult[i], crResult[i])), $"Pos {i}, Expected {y} == {yResult[i]}, {cb} == {cbResult[i]}, {cr} == {crResult[i]}");
            }
        }

        private static Rgb24[] CreateTestData()
        {
            var data = new Rgb24[64];
            var r = new Random();

            var random = new byte[3];
            for (int i = 0; i < data.Length; i++)
            {
                r.NextBytes(random);
                data[i] = new Rgb24(random[0], random[1], random[2]);
            }

            return data;
        }
    }
}
