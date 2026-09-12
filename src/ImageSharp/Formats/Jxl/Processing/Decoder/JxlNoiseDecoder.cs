// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using static System.Net.Mime.MediaTypeNames;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal static class JxlNoiseDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BitsToFloatingPoint(ReadOnlySpan<uint> randomBits, Span<float> floats)
    {
        Vector<uint> bits = new(randomBits);
        Vector<float> rand12 = ((bits >> 9) | new Vector<uint>(0x3F800000u)).As<uint, float>();
        rand12.StoreUnsafe(ref MemoryMarshal.GetReference(floats));
    }

    public static void GenerateRandomImage(JxlXorShift rng, Rectangle rectangle, JxlImageF noise)
    {
        const int floatsPerBatch = JxlXorShift.Generators * sizeof(ulong) / sizeof(float);

        int xSize = rectangle.Width;
        int ySize = rectangle.Height;

        Span<ulong> batch64 = stackalloc ulong[JxlXorShift.Generators];
        Span<uint> batch32 = stackalloc uint[JxlXorShift.Generators * 2];

        // stackalloc doesn't zero-initialize, so clear values
        batch64.Clear();
        batch32.Clear();

        int n = Vector<float>.Count;

        for (int y = 0; y < ySize; y++)
        {
            Span<float> row = noise.GetRow(rectangle, y);
            int x = 0;
            for (; x + floatsPerBatch < xSize; x += floatsPerBatch)
            {
                rng.Fill(batch64);
                MemoryMarshal.Cast<uint, ulong>(batch32).CopyTo(batch64);
                for (int i = 0; i < floatsPerBatch; i += n)
                {
                    BitsToFloatingPoint(batch32[i..], row[(x + i)..]);
                }
            }

            rng.Fill(batch64);
            MemoryMarshal.Cast<uint, ulong>(batch32).CopyTo(batch64);

            int batchPos = 0;

            for (; x < xSize; x += n)
            {
                BitsToFloatingPoint(batch32[batchPos..], row[x..]);
                batchPos += n;
            }
        }
    }

    public static void Random3Planes(
        int visibleFrameIndex,
        int nonVisibleFrameIndex,
        int x0,
        int y0,
        (JxlImageF Image, Rectangle Bounds) plane0,
        (JxlImageF Image, Rectangle Bounds) plane1,
        (JxlImageF Image, Rectangle Bounds) plane2)
    {
        JxlXorShift rng = new((uint)visibleFrameIndex, (uint)nonVisibleFrameIndex, (uint)x0, (uint)y0);

        GenerateRandomImage(rng, plane0.Bounds, plane0.Image);
        GenerateRandomImage(rng, plane1.Bounds, plane1.Image);
        GenerateRandomImage(rng, plane2.Bounds, plane2.Image);
    }

    private static void PrepareNoiseInput(
        JxlPassesDecoderState decState,
        JxlFrameDimensions frameDim,
        JxlFrameHeader frameHeader,
        int groupIndex,
        int thread)
    {
        int groupDim = frameDim.GroupDimension;

        int gx = groupIndex % frameDim.XSizeGroups;
        int gy = groupIndex / frameDim.XSizeGroups;

        JxlRenderPipelineInput input = decState.RenderPipeline.GetInputBuffers(groupIndex, thread);
        int noiseCStart = 3 + frameHeader.Metadata!.ImageMetadata!.ExtraChannelCount;

        InlineArray3<(JxlImageF Image, Rectangle Rect)> rects = default;

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
