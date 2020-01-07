// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Dds
{
    using static TestImages.Dds;

    public class DdsDecoderTests
    {
        [Theory]
        [WithFile(AtcRgb, PixelTypes.Rgba32)]
        public void DdsDecoder_CanDecode_Atc_Rgb<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (Texture texture = provider.GetTexture(new DdsDecoder()))
            {
                SaveTextures(texture, provider.SourceFileOrDescription);
                //image.DebugSave(provider);
                //DdsTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Dxt1, PixelTypes.Rgba32)]
        public void DdsDecoder_CanDecode_Dxt1_Rgb<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (Texture texture = provider.GetTexture(new DdsDecoder()))
            {
                SaveTextures(texture, provider.SourceFileOrDescription);
                //image.DebugSave(provider);
                //DdsTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Dxt3, PixelTypes.Rgba32)]
        public void DdsDecoder_CanDecode_Dxt3_Rgb<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (Texture texture = provider.GetTexture(new DdsDecoder()))
            {
                SaveTextures(texture, provider.SourceFileOrDescription);
                //image.DebugSave(provider);
                //DdsTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Dxt5, PixelTypes.Rgba32)]
        public void DdsDecoder_CanDecode_Dxt5_Rgb<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (Texture texture = provider.GetTexture(new DdsDecoder()))
            {
                SaveTextures(texture, provider.SourceFileOrDescription);
                //image.DebugSave(provider);
                //DdsTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        [Theory]
        [WithFile(Dxt1Cubemap, PixelTypes.Rgba32)]
        public void DdsDecoder_CanDecode_Dxt1Cubemap_Rgb<TPixel>(TestImageProvider<TPixel> provider) where TPixel : struct, IPixel<TPixel>
        {
            using (Texture texture = provider.GetTexture(new DdsDecoder()))
            {
                SaveTextures(texture, provider.SourceFileOrDescription);
                //image.DebugSave(provider);
                //DdsTestUtils.CompareWithReferenceDecoder(provider, image);
            }
        }

        private void SaveTextures(Texture texture, string name)
        {
            for (var i = 0; i < texture.Images.Length; i++)
            {
                for (var j = 0; j < texture.Images[i].Length; j++)
                {
                    texture.Images[i][j].Save($"d:\\{Path.GetFileNameWithoutExtension(name)}-depth{i}-mip{j}.png");
                }
            }
        }


        //public const string AtcRgb = "Dds/shannon-atc-rgb.dds";
        //public const string AtcRgbaExplicit = "Dds/shannon-atc-rgba-explicit.dds";
        //public const string AtcRgbaInterpolated = "Dds/shannon-atc-rgba-interpolated.dds";
        //public const string Dxt1 = "Dds/shannon-dxt1.dds";
        //public const string Dxt3 = "Dds/shannon-dxt3.dds";
        //public const string Dxt5 = "Dds/shannon-dxt5.dds";
    }
}
