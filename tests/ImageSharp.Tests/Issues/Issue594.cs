// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Issues;

public class Issue594
{
    [Fact]
    public void NormalizedByte4Test()
    {
        // Test PackedValue
        Assert.Equal(0x0U, new NormalizedByte4(Vector4.Zero).PackedValue);
        Assert.Equal(0x7F7F7F7FU, new NormalizedByte4(Vector4.One).PackedValue);
        Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);

        // Test ToVector4
        Assert.True(Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4()));
        Assert.True(Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4()));
        Assert.True(Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4()));
        Assert.True(Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4()));
        Assert.True(Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4()));

        // Test ToScaledVector4.
        Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
        Assert.Equal(0, scaled.X);
        Assert.Equal(0, scaled.Y);
        Assert.Equal(0, scaled.Z);
        Assert.Equal(0, scaled.W);

        // Test FromScaledVector4.
        NormalizedByte4 pixel = NormalizedByte4.FromScaledVector4(scaled);
        Assert.Equal(0x81818181, pixel.PackedValue);

        // Test Ordering
        const float x = 0.1f;
        const float y = -0.3f;
        const float z = 0.5f;
        const float w = -0.7f;

        pixel = new NormalizedByte4(x, y, z, w);
        Assert.Equal(0xA740DA0D, pixel.PackedValue);
        NormalizedByte4 n = NormalizedByte4.FromRgba32(pixel.ToRgba32());
        Assert.Equal(0xA740DA0D, n.PackedValue);

        Assert.Equal(958796544U, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
    }

    [Fact]
    public void NormalizedShort4Test()
    {
        // Test PackedValue
        Assert.Equal(0x0UL, new NormalizedShort4(Vector4.Zero).PackedValue);
        Assert.Equal(0x7FFF7FFF7FFF7FFFUL, new NormalizedShort4(Vector4.One).PackedValue);
        Assert.Equal(0x8001800180018001, new NormalizedShort4(-Vector4.One).PackedValue);

        // Test ToVector4
        Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One).ToVector4()));
        Assert.True(Equal(Vector4.Zero, new NormalizedShort4(Vector4.Zero).ToVector4()));
        Assert.True(Equal(-Vector4.One, new NormalizedShort4(-Vector4.One).ToVector4()));
        Assert.True(Equal(Vector4.One, new NormalizedShort4(Vector4.One * 1234.0f).ToVector4()));
        Assert.True(Equal(-Vector4.One, new NormalizedShort4(Vector4.One * -1234.0f).ToVector4()));

        // Test ToScaledVector4.
        Vector4 scaled = new NormalizedShort4(Vector4.One).ToScaledVector4();
        Assert.Equal(1, scaled.X);
        Assert.Equal(1, scaled.Y);
        Assert.Equal(1, scaled.Z);
        Assert.Equal(1, scaled.W);

        // Test FromScaledVector4.
        NormalizedShort4 pixel = NormalizedShort4.FromScaledVector4(scaled);
        Assert.Equal(0x7FFF7FFF7FFF7FFFUL, pixel.PackedValue);

        // Test Ordering
        const float x = 0.1f;
        const float y = -0.3f;
        const float z = 0.5f;
        const float w = -0.7f;
        Assert.Equal(0xa6674000d99a0ccd, new NormalizedShort4(x, y, z, w).PackedValue);
        Assert.Equal(4150390751449251866UL, new NormalizedShort4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
    }

    [Fact]
    public void Short4Test()
    {
        // Test the limits.
        Assert.Equal(0x0UL, new Short4(Vector4.Zero).PackedValue);
        Assert.Equal(0x7FFF7FFF7FFF7FFFUL, new Short4(Vector4.One * 0x7FFF).PackedValue);
        Assert.Equal(0x8000800080008000, new Short4(Vector4.One * -0x8000).PackedValue);

        // Test ToVector4.
        Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 0x7FFF).ToVector4());
        Assert.Equal(Vector4.Zero, new Short4(Vector4.Zero).ToVector4());
        Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -0x8000).ToVector4());
        Assert.Equal(Vector4.UnitX * 0x7FFF, new Short4(Vector4.UnitX * 0x7FFF).ToVector4());
        Assert.Equal(Vector4.UnitY * 0x7FFF, new Short4(Vector4.UnitY * 0x7FFF).ToVector4());
        Assert.Equal(Vector4.UnitZ * 0x7FFF, new Short4(Vector4.UnitZ * 0x7FFF).ToVector4());
        Assert.Equal(Vector4.UnitW * 0x7FFF, new Short4(Vector4.UnitW * 0x7FFF).ToVector4());

        // Test ToScaledVector4.
        Vector4 scaled = new Short4(Vector4.One * 0x7FFF).ToScaledVector4();
        Assert.Equal(1, scaled.X);
        Assert.Equal(1, scaled.Y);
        Assert.Equal(1, scaled.Z);
        Assert.Equal(1, scaled.W);

        // Test FromScaledVector4.
        Short4 pixel = Short4.FromScaledVector4(scaled);
        Assert.Equal(0x7FFF7FFF7FFF7FFFUL, pixel.PackedValue);

        // Test clamping.
        Assert.Equal(Vector4.One * 0x7FFF, new Short4(Vector4.One * 1234567.0f).ToVector4());
        Assert.Equal(Vector4.One * -0x8000, new Short4(Vector4.One * -1234567.0f).ToVector4());

        // Test Ordering
        float x = 0.1f;
        float y = -0.3f;
        float z = 0.5f;
        float w = -0.7f;
        Assert.Equal(18446462598732840960, new Short4(x, y, z, w).PackedValue);

        x = 11547;
        y = 12653;
        z = 29623;
        w = 193;
        Assert.Equal(0x00c173b7316d2d1bUL, new Short4(x, y, z, w).PackedValue);
    }

    // TODO: Use tolerant comparer.
    // Comparison helpers with small tolerance to allow for floating point rounding during computations.
    public static bool Equal(float a, float b) => Math.Abs(a - b) < 1e-5;

    public static bool Equal(Vector4 a, Vector4 b) => Equal(a.X, b.X) && Equal(a.Y, b.Y) && Equal(a.Z, b.Z) && Equal(a.W, b.W);
}
