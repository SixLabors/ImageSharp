// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class UnPackedPixelTests
{
    [Fact]
    public void Color_Types_From_Bytes_Produce_Equal_Scaled_Component_OutPut()
    {
        Rgba32 color = new(24, 48, 96, 192);
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        Assert.Equal(color.R, (byte)(colorVector.R * 255));
        Assert.Equal(color.G, (byte)(colorVector.G * 255));
        Assert.Equal(color.B, (byte)(colorVector.B * 255));
        Assert.Equal(color.A, (byte)(colorVector.A * 255));
    }

    [Fact]
    public void Color_Types_From_Floats_Produce_Equal_Scaled_Component_OutPut()
    {
        Rgba32 color = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        Assert.Equal(color.R, (byte)(colorVector.R * 255));
        Assert.Equal(color.G, (byte)(colorVector.G * 255));
        Assert.Equal(color.B, (byte)(colorVector.B * 255));
        Assert.Equal(color.A, (byte)(colorVector.A * 255));
    }

    [Fact]
    public void Color_Types_From_Vector4_Produce_Equal_Scaled_Component_OutPut()
    {
        Rgba32 color = new(new Vector4(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F));
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        Assert.Equal(color.R, (byte)(colorVector.R * 255));
        Assert.Equal(color.G, (byte)(colorVector.G * 255));
        Assert.Equal(color.B, (byte)(colorVector.B * 255));
        Assert.Equal(color.A, (byte)(colorVector.A * 255));
    }

    [Fact]
    public void Color_Types_From_Vector3_Produce_Equal_Scaled_Component_OutPut()
    {
        Rgba32 color = new(new Vector3(24 / 255F, 48 / 255F, 96 / 255F));
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F);

        Assert.Equal(color.R, (byte)(colorVector.R * 255));
        Assert.Equal(color.G, (byte)(colorVector.G * 255));
        Assert.Equal(color.B, (byte)(colorVector.B * 255));
        Assert.Equal(color.A, (byte)(colorVector.A * 255));
    }

    [Fact]
    public void Color_Types_From_Hex_Produce_Equal_Scaled_Component_OutPut()
    {
        Rgba32 color = Color.ParseHex("183060C0").ToPixel<Rgba32>();
        RgbaVector colorVector = RgbaVector.FromHex("183060C0");

        Assert.Equal(color.R, (byte)(colorVector.R * 255));
        Assert.Equal(color.G, (byte)(colorVector.G * 255));
        Assert.Equal(color.B, (byte)(colorVector.B * 255));
        Assert.Equal(color.A, (byte)(colorVector.A * 255));
    }

    [Fact]
    public void Color_Types_To_Vector4_Produce_Equal_OutPut()
    {
        Rgba32 color = new(24, 48, 96, 192);
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        Assert.Equal(color.ToVector4(), colorVector.ToVector4());
    }

    [Fact]
    public void Color_Types_To_RgbaBytes_Produce_Equal_OutPut()
    {
        Rgba32 color = new(24, 48, 96, 192);
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        Rgba32 rgba = color.ToRgba32();
        Rgba32 rgbaVector = colorVector.ToRgba32();

        Assert.Equal(rgba, rgbaVector);
    }

    [Fact]
    public void Color_Types_To_Hex_Produce_Equal_OutPut()
    {
        Rgba32 color = new(24, 48, 96, 192);
        RgbaVector colorVector = new(24 / 255F, 48 / 255F, 96 / 255F, 192 / 255F);

        // 183060C0
        Assert.Equal(color.ToHex(), colorVector.ToHex());
    }
}
