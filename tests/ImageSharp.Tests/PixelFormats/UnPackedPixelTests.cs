// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colors
{
    [Trait("Category", "PixelFormats")]
    public class UnPackedPixelTests
    {
        [Fact]
        public void Color_Types_From_Bytes_Produce_Equal_Scaled_Component_OutPut()
        {
            var color = new Rgba32(24, 48, 96, 192);
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Floats_Produce_Equal_Scaled_Component_OutPut()
        {
            var color = new Rgba32(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Vector4_Produce_Equal_Scaled_Component_OutPut()
        {
            var color = new Rgba32(new Vector4(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F));
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Vector3_Produce_Equal_Scaled_Component_OutPut()
        {
            var color = new Rgba32(new Vector3(24 / 255F, 48 / 255F, 96 / 255F));
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Hex_Produce_Equal_Scaled_Component_OutPut()
        {
            var color = Rgba32.ParseHex("183060C0");
            var colorVector = RgbaVector.FromHex("183060C0");

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_To_Vector4_Produce_Equal_OutPut()
        {
            var color = new Rgba32(24, 48, 96, 192);
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Assert.Equal(color.ToVector4(), colorVector.ToVector4());
        }

        [Fact]
        public void Color_Types_To_RgbaBytes_Produce_Equal_OutPut()
        {
            var color = new Rgba32(24, 48, 96, 192);
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Rgba32 rgba = default;
            Rgba32 rgbaVector = default;
            color.ToRgba32(ref rgba);
            colorVector.ToRgba32(ref rgbaVector);

            Assert.Equal(rgba, rgbaVector);
        }

        [Fact]
        public void Color_Types_To_Hex_Produce_Equal_OutPut()
        {
            var color = new Rgba32(24, 48, 96, 192);
            var colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            // 183060C0
            Assert.Equal(color.ToHex(), colorVector.ToHex());
        }
    }
}
