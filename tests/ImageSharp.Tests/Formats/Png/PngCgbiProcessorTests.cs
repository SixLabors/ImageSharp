// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngCgbiProcessorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    public void ApplyTransform_RgbWithAlpha_MatchesScalar(int pixelCount)
    {
        // Drives the full V512/V256/V128/scalar dispatch, so it covers each
        // path that is hardware-accelerated on the host plus the scalar tail.
        byte[] input = CreateBgraScanline(pixelCount);
        byte[] processorOutput = (byte[])input.Clone();
        byte[] scalarOutput = (byte[])input.Clone();

        PngCgbiProcessor.ApplyTransform(Configuration.Default, processorOutput, PngColorType.RgbWithAlpha);
        ApplyCgbiTransformScalarReference(scalarOutput);

        Assert.Equal(scalarOutput, processorOutput);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    public void ApplyTransformVector512_MatchesScalar(int pixelCount) =>
        // Vector512 uses Vector512_.ShuffleNative which falls back to the software
        // Vector512.Shuffle when Avx512BW is unavailable, so the body runs regardless
        // of whether Vector512 is hardware-accelerated on the host.
        AssertVectorMatchesScalar(
            pixelCount,
            scanline => PngCgbiProcessor.ApplyTransformVector512(scanline, scanline.Length / 4),
            blockSize: 16);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(64)]
    public void ApplyTransformVector256_MatchesScalar(int pixelCount) => AssertVectorMatchesScalar(
            pixelCount,
            scanline => PngCgbiProcessor.ApplyTransformVector256(scanline, 0, scanline.Length / 4),
            blockSize: 8);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(64)]
    public void ApplyTransformVector128_MatchesScalar(int pixelCount) => AssertVectorMatchesScalar(
            pixelCount,
            scanline => PngCgbiProcessor.ApplyTransformVector128(scanline, 0, scanline.Length / 4),
            blockSize: 4);

    private static void AssertVectorMatchesScalar(int pixelCount, Func<byte[], int> applyVector, int blockSize)
    {
        byte[] input = CreateBgraScanline(pixelCount);
        byte[] vectorOutput = (byte[])input.Clone();
        byte[] scalarOutput = (byte[])input.Clone();

        int processed = applyVector(vectorOutput);

        int expectedProcessed = (pixelCount / blockSize) * blockSize;
        Assert.Equal(expectedProcessed, processed);

        // The vector path is responsible for whole blocks only; remaining pixels are
        // handled by the scalar tail in ApplyTransform. Run the scalar reference
        // over every pixel and compare the prefix the vector path actually wrote.
        ApplyCgbiTransformScalarReference(scalarOutput);

        Span<byte> vectorProcessed = vectorOutput.AsSpan(0, processed * 4);
        Span<byte> scalarProcessed = scalarOutput.AsSpan(0, processed * 4);
        Assert.True(vectorProcessed.SequenceEqual(scalarProcessed), $"Mismatch at pixelCount={pixelCount}");

        // Pixels past the vector's processed prefix must be untouched.
        Span<byte> vectorTail = vectorOutput.AsSpan(processed * 4);
        Span<byte> inputTail = input.AsSpan(processed * 4);
        Assert.True(vectorTail.SequenceEqual(inputTail));
    }

    /// <summary>
    /// Builds synthetic input for tests. Produces inputs that exercise all three alpha cases.
    /// </summary>
    /// <returns>Channel values laid out as a defiltered CgBI scanline in premultiplied BGRA order</returns>
    private static byte[] CreateBgraScanline(int pixelCount)
    {
        // The distinct strides keep the channels from being equal to each other or constant across pixels
        // So a bug that e.g. swaps two channels or reuses one channel's value doesn't accidentally pass
        const int alphaCaseCount = 7;
        const int redStride = 13;
        const int greenStride = 29;
        const int blueStride = 53;

        byte[] bytes = new byte[pixelCount * 4];
        for (int p = 0; p < pixelCount; p++)
        {
            // Cycling alpha through [0..255], and an odd partial value every pixel rotation
            // ensures all three branches get covered within any scanline of 3 or more pixels
            byte a = (p % alphaCaseCount) switch
            {
                0 => byte.MinValue,
                1 => byte.MaxValue,
                _ => (byte)(((p * 37) + 23) | 1) // Produce a spread of alpha values and make sure to never get 0
            };

            int offset = p * 4;
            bytes[offset + 0] = Premultiply((byte)(p * blueStride), a);
            bytes[offset + 1] = Premultiply((byte)(p * greenStride), a);
            bytes[offset + 2] = Premultiply((byte)(p * redStride), a);
            bytes[offset + 3] = a;
        }

        return bytes;

        // CgBI stores channels premultiplied by alpha
        static byte Premultiply(byte channel, byte alpha) => (byte)(channel * alpha / byte.MaxValue);
    }

    private static void ApplyCgbiTransformScalarReference(Span<byte> scanline)
    {
        Span<Rgba32> pixels = MemoryMarshal.Cast<byte, Rgba32>(scanline);
        for (int i = 0; i < pixels.Length; i++)
        {
            ref Rgba32 pixel = ref pixels[i];
            pixel = new Rgba32(pixel.B, pixel.G, pixel.R, pixel.A);

            byte a = pixel.A;
            if (a is 0 or byte.MaxValue)
            {
                continue;
            }

            int half = a >> 1;
            byte r = (byte)Math.Min(byte.MaxValue, ((pixel.R * byte.MaxValue) + half) / a);
            byte g = (byte)Math.Min(byte.MaxValue, ((pixel.G * byte.MaxValue) + half) / a);
            byte b = (byte)Math.Min(byte.MaxValue, ((pixel.B * byte.MaxValue) + half) / a);
            pixel = new Rgba32(r, g, b, a);
        }
    }
}
