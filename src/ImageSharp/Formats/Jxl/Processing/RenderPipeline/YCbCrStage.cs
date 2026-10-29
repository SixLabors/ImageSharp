// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>Converts JPEG XL's centered Cb, Y and Cr rows to RGB in place.</summary>
/// <param name="configuration">The configuration used by the render stage.</param>
internal sealed class YCbCrStage(Configuration configuration) : RenderPipelineStageBase(configuration)
{
    /// <summary>The luma offset removed during JPEG XL encoding, with component units relative to RGB white at one.</summary>
    private const float LumaOffset = 128F / 255F;

    /// <inheritdoc />
    public override string Name => "YCbCr";

    /// <inheritdoc />
    public override void ProcessRow(
        Buffer2D<Memory<float>> inputRows,
        Buffer2D<Memory<float>> outputRows,
        int xExtraLeft,
        int xExtraRight,
        int width,
        int xPos,
        int yPos)
    {
        // Native stage_ycbcr consumes point-wise samples. The renderer supplies no horizontal halo for this stage.
        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("The YCbCr stage does not consume horizontal border samples.");
        }

        Span<float> row0 = this.GetInputRow(inputRows, 0, 0)[..width];
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0)[..width];
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0)[..width];
        ref float first0 = ref MemoryMarshal.GetReference(row0);
        ref float first1 = ref MemoryMarshal.GetReference(row1);
        ref float first2 = ref MemoryMarshal.GetReference(row2);
        int x = 0;

        // JPEG XL stores Cb, Y, Cr, with centered chroma and a biased Y. Restore Y before the shared BT.601 calculation.
        // Read all three components before writing R, G, B over those rows. Only full logical blocks enter SIMD;
        // narrower registers and the scalar tail complete the row without reading or changing its padded suffix.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> offset = Vector512.Create(LumaOffset);

            for (; x <= width - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> cb = Vector512.LoadUnsafe(ref first0, (uint)x);
                Vector512<float> y = Vector512.LoadUnsafe(ref first1, (uint)x) + offset;
                Vector512<float> cr = Vector512.LoadUnsafe(ref first2, (uint)x);
                ColorNumerics.ConvertBt601YCbCrToRgb(y, cb, cr, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b);
                r.StoreUnsafe(ref first0, (uint)x);
                g.StoreUnsafe(ref first1, (uint)x);
                b.StoreUnsafe(ref first2, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> offset = Vector256.Create(LumaOffset);

            for (; x <= width - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> cb = Vector256.LoadUnsafe(ref first0, (uint)x);
                Vector256<float> y = Vector256.LoadUnsafe(ref first1, (uint)x) + offset;
                Vector256<float> cr = Vector256.LoadUnsafe(ref first2, (uint)x);
                ColorNumerics.ConvertBt601YCbCrToRgb(y, cb, cr, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b);
                r.StoreUnsafe(ref first0, (uint)x);
                g.StoreUnsafe(ref first1, (uint)x);
                b.StoreUnsafe(ref first2, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offset = Vector128.Create(LumaOffset);

            for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> cb = Vector128.LoadUnsafe(ref first0, (uint)x);
                Vector128<float> y = Vector128.LoadUnsafe(ref first1, (uint)x) + offset;
                Vector128<float> cr = Vector128.LoadUnsafe(ref first2, (uint)x);
                ColorNumerics.ConvertBt601YCbCrToRgb(y, cb, cr, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b);
                r.StoreUnsafe(ref first0, (uint)x);
                g.StoreUnsafe(ref first1, (uint)x);
                b.StoreUnsafe(ref first2, (uint)x);
            }
        }

        for (; x < width; x++)
        {
            float cb = row0[x];
            float y = row1[x] + LumaOffset;
            float cr = row2[x];
            ColorNumerics.ConvertBt601YCbCrToRgb(y, cb, cr, out float r, out float g, out float b);
            row0[x] = r;
            row1[x] = g;
            row2[x] = b;
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < 3 ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;
}
