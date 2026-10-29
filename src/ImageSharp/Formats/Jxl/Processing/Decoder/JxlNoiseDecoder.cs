// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlNoiseDecoder
{
    /// <summary>
    /// Converts random words to floats in [1,2) without changing values outside the destination.
    /// </summary>
    /// <param name="randomBits">One random word per output sample.</param>
    /// <param name="floats">The logical output samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BitsToFloatingPoint(ReadOnlySpan<uint> randomBits, Span<float> floats)
    {
        const int mantissaShift = 9;
        const uint unitExponentBits = 0x3F800000U;
        ref uint input = ref MemoryMarshal.GetReference(randomBits);
        ref float output = ref MemoryMarshal.GetReference(floats);
        uint i = 0;

        // Each UInt32 supplies 23 random mantissa bits. The fixed exponent produces [1,2), as in native noise reconstruction.
        // The later noise filter has zero coefficient sum, so this constant offset contributes nothing to its output.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i + Vector512<uint>.Count <= floats.Length; i += (uint)Vector512<uint>.Count)
            {
                Vector512<uint> bits = Vector512.LoadUnsafe(ref input, i);
                ((bits >> mantissaShift) | Vector512.Create(unitExponentBits)).AsSingle().StoreUnsafe(ref output, i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i + Vector256<uint>.Count <= floats.Length; i += (uint)Vector256<uint>.Count)
            {
                Vector256<uint> bits = Vector256.LoadUnsafe(ref input, i);
                ((bits >> mantissaShift) | Vector256.Create(unitExponentBits)).AsSingle().StoreUnsafe(ref output, i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i + Vector128<uint>.Count <= floats.Length; i += (uint)Vector128<uint>.Count)
            {
                Vector128<uint> bits = Vector128.LoadUnsafe(ref input, i);
                ((bits >> mantissaShift) | Vector128.Create(unitExponentBits)).AsSingle().StoreUnsafe(ref output, i);
            }
        }

        // Store only real samples. The scalar tail uses the identical bit pattern and does not write row padding or adjacent rectangles.
        for (; i < floats.Length; i++)
        {
            Unsafe.Add(ref output, i) = BitConverter.UInt32BitsToSingle((Unsafe.Add(ref input, i) >> mantissaShift) | unitExponentBits);
        }
    }

    /// <summary>
    /// Fills one rectangle with the native random sequence while preserving samples outside it.
    /// </summary>
    /// <param name="rng">The generator advanced by this rectangle.</param>
    /// <param name="rectangle">The logical output rectangle.</param>
    /// <param name="noise">The plane that owns the output storage.</param>
    public static void GenerateRandomImage(JxlXorShift rng, Rectangle rectangle, JxlPlane<float> noise)
    {
        const int floatsPerBatch = JxlXorShift.Generators * sizeof(ulong) / sizeof(float);

        int xSize = rectangle.Width;
        int ySize = rectangle.Height;

        InlineArray8<ulong> batchStorage = default;
        Span<ulong> batch64 = batchStorage;
        ReadOnlySpan<uint> batch32 = MemoryMarshal.Cast<ulong, uint>(batch64);
        Span<float> storage = noise.GetRow(0);
        int stride = noise.PixelsPerRow;

        for (int y = 0; y < ySize; y++)
        {
            Span<float> row = storage.Slice(((rectangle.Y + y) * stride) + rectangle.X, xSize);
            int x = 0;

            for (; x + floatsPerBatch < xSize; x += floatsPerBatch)
            {
                rng.Fill(batch64);
                BitsToFloatingPoint(batch32, row.Slice(x, floatsPerBatch));
            }

            // Native consumes one final batch per row, including an exactly full batch or an empty row.
            // The UInt32 view borrows the freshly filled words directly. No copied or uninitialized batch is read.
            rng.Fill(batch64);
            BitsToFloatingPoint(batch32, row[x..]);
        }
    }

    /// <summary>
    /// Fills three noise rectangles with one continuous frame-and-position random sequence.
    /// </summary>
    /// <param name="visibleFrameIndex">The visible frame seed.</param>
    /// <param name="nonVisibleFrameIndex">The non-visible frame seed.</param>
    /// <param name="x0">The group's horizontal seed coordinate.</param>
    /// <param name="y0">The group's vertical seed coordinate.</param>
    /// <param name="plane0">The first output plane and rectangle.</param>
    /// <param name="plane1">The second output plane and rectangle.</param>
    /// <param name="plane2">The third output plane and rectangle.</param>
    public static void Random3Planes(
        int visibleFrameIndex,
        int nonVisibleFrameIndex,
        int x0,
        int y0,
        (JxlPlane<float> Image, Rectangle Bounds) plane0,
        (JxlPlane<float> Image, Rectangle Bounds) plane1,
        (JxlPlane<float> Image, Rectangle Bounds) plane2)
    {
        JxlXorShift rng = new((uint)visibleFrameIndex, (uint)nonVisibleFrameIndex, (uint)x0, (uint)y0);

        GenerateRandomImage(rng, plane0.Bounds, plane0.Image);
        GenerateRandomImage(rng, plane1.Bounds, plane1.Image);
        GenerateRandomImage(rng, plane2.Bounds, plane2.Image);
    }

    private static void PrepareNoiseInput(
        JxlPassesDecoderContext decState,
        JxlFrameDimensions frameDim,
        JxlFrameHeader frameHeader,
        int groupIndex,
        int thread)
    {
        int groupDim = frameDim.GroupDimension;

        int gx = groupIndex % frameDim.XSizeGroups;
        int gy = groupIndex / frameDim.XSizeGroups;

        JxlRenderPipelineInput input = decState.RenderPipeline.GetInputBuffers(groupIndex, thread);
        int noiseCStart = 3 + frameHeader.Metadata!.ImageMetadata.ExtraChannelCount;

        InlineArray3<(JxlPlane<float> Image, Rectangle Rect)> rects = default;

        for (int iy = 0; iy < frameHeader.Upsampling; iy++)
        {
            for (int ix = 0; ix < frameHeader.Upsampling; ix++)
            {
                for (int c = 0; c < 3; c++)
                {
                    var r = input.GetBuffer(noiseCStart + c);

                    int x1 = r.Rect.X0 + r.Rect.XSize;
                    int y1 = r.Rect.Y0 + r.Rect.YSize;

                    rects[c] = (
                        r.Image,
                        RectangleUtils.CreateRectangle(
                            r.Rect.X0 + (ix * groupDim),
                            r.Rect.Y0 + (iy * groupDim),
                            groupDim,
                            groupDim,
                            x1,
                            y1));
                }

                Random3Planes(
                    decState.VisibleFrameIndex,
                    decState.NonvisibleFrameIndex,
                    (int)((gx * frameHeader.Upsampling) + ix) * groupDim,
                    (int)((gy * frameHeader.Upsampling) + iy) * groupDim,
                    rects[0],
                    rects[1],
                    rects[2]);
            }
        }
    }

    public static float DecodeFloatParam(float precision, JxlBitReader br)
    {
        int absValQuant = (int)br.ReadBits32(10);
        return absValQuant / precision;
    }

    public static void DecodeNoise(JxlBitReader br, JxlNoiseParameters noiseParams)
    {
        for (int i = 0; i < noiseParams.Lookup.Length; i++)
        {
            noiseParams.Lookup[i] = DecodeFloatParam(JxlNoiseConstants.Precision, br);
        }
    }
}
