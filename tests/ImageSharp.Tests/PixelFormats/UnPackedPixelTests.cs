// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colors
{
    public class UnPackedPixelTests
    {
        [Fact]
        public void Color_Types_From_Bytes_Produce_Equal_Scaled_Component_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Floats_Produce_Equal_Scaled_Component_OutPut()
        {
            Rgba32 color = new Rgba32(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);
            RgbaVector colorVector = new RgbaVector(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Vector4_Produce_Equal_Scaled_Component_OutPut()
        {
            Rgba32 color = new Rgba32(new Vector4(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F));
            RgbaVector colorVector = new RgbaVector(new Vector4(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F));

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Vector3_Produce_Equal_Scaled_Component_OutPut()
        {
            Rgba32 color = new Rgba32(new Vector3(24 / 255F, 48 / 255F, 96 / 255F));
            RgbaVector colorVector = new RgbaVector(new Vector3(24 / 255F, 48 / 255F, 96 / 255F));

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_From_Hex_Produce_Equal_Scaled_Component_OutPut()
        {
            Rgba32 color = Rgba32.FromHex("183060C0");
            RgbaVector colorVector = RgbaVector.FromHex("183060C0");

            Assert.Equal(color.R, (byte)(colorVector.R * 255));
            Assert.Equal(color.G, (byte)(colorVector.G * 255));
            Assert.Equal(color.B, (byte)(colorVector.B * 255));
            Assert.Equal(color.A, (byte)(colorVector.A * 255));
        }

        [Fact]
        public void Color_Types_To_Vector4_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            Assert.Equal(color.ToVector4(), colorVector.ToVector4());
        }

        [Fact]
        public void Color_Types_To_RgbBytes_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            byte[] rgb = new byte[3];
            byte[] rgbVector = new byte[3];

            color.ToXyzBytes(rgb, 0);
            colorVector.ToXyzBytes(rgbVector, 0);

            Assert.Equal(rgb, rgbVector);
        }

        [Fact]
        public void Color_Types_To_RgbaBytes_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            byte[] rgba = new byte[4];
            byte[] rgbaVector = new byte[4];

            color.ToXyzwBytes(rgba, 0);
            colorVector.ToXyzwBytes(rgbaVector, 0);

            Assert.Equal(rgba, rgbaVector);
        }

        [Fact]
        public void Color_Types_To_BgrBytes_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            byte[] bgr = new byte[3];
            byte[] bgrVector = new byte[3];

            color.ToZyxBytes(bgr, 0);
            colorVector.ToZyxBytes(bgrVector, 0);

            Assert.Equal(bgr, bgrVector);
        }

        [Fact]
        public void Color_Types_To_BgraBytes_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            byte[] bgra = new byte[4];
            byte[] bgraVector = new byte[4];

            color.ToZyxwBytes(bgra, 0);
            colorVector.ToZyxwBytes(bgraVector, 0);

            Assert.Equal(bgra, bgraVector);
        }

        [Fact]
        public void Color_Types_To_Hex_Produce_Equal_OutPut()
        {
            Rgba32 color = new Rgba32(24, 48, 96, 192);
            RgbaVector colorVector = new RgbaVector(24, 48, 96, 192);

            // 183060C0
            Assert.Equal(color.ToHex(), colorVector.ToHex());
        }
    }
}