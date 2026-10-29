// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>Converts reconstructed XYB rows to linear RGB or the scaled-XYB output representation.</summary>
internal class XybStage : RenderPipelineStageBase
{
    /// <summary>The inverse transform prepared before processing rows.</summary>
    private readonly JxlOpsinParameters opsinParameters;

    /// <summary>Whether the requested output retains XYB components instead of reconstructing RGB.</summary>
    private readonly bool outputIsXYB;

    /// <summary>Creates the in-place row stage with the selected output transform.</summary>
    /// <param name="configuration">The pipeline configuration.</param>
    /// <param name="outputEncodingInfo">The selected output encoding and prepared inverse transform.</param>
    public XybStage(Configuration configuration, JxlOutputEncodingInfo outputEncodingInfo)
        : base(configuration)
    {
        this.opsinParameters = outputEncodingInfo.OpsinParameters;
        this.outputIsXYB = outputEncodingInfo.ColorEncoding.ColorSpace == JxlColorSpace.Xyb;
    }

    /// <inheritdoc />
    public override string Name => "XYB";

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < 3 ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;

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
        if (xExtraLeft != 0 || xExtraRight != 0)
        {
            throw new InvalidOperationException("The XYB stage does not consume horizontal border samples.");
        }

        // This point-wise transform needs no halo. Logical slices exclude the pipeline prefix and all right-border padding.
        Span<float> row0 = this.GetInputRow(inputRows, 0, 0)[..width];
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0)[..width];
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0)[..width];

        if (this.outputIsXYB)
        {
            ScaleXyb(row0, row1, row2);
        }
        else
        {
            JxlXybDecoder.ConvertXybToRgb(row0, row1, row2, row0, row1, row2, this.opsinParameters);
        }
    }

    /// <summary>Applies native output offsets and scales to the three logical XYB rows in place.</summary>
    /// <param name="rowX">The X row.</param>
    /// <param name="rowY">The corresponding Y row.</param>
    /// <param name="rowB">The corresponding B row, replaced by scaled B minus Y.</param>
    private static void ScaleXyb(Span<float> rowX, Span<float> rowY, Span<float> rowB)
    {
        ReadOnlySpan<float> scales = JxlOpsinConstants.ScaledXybScale;
        ReadOnlySpan<float> offsets = JxlOpsinConstants.ScaledXybOffset;
        ref float firstX = ref MemoryMarshal.GetReference(rowX);
        ref float firstY = ref MemoryMarshal.GetReference(rowY);
        ref float firstB = ref MemoryMarshal.GetReference(rowB);
        int x = 0;

        // Each lane is one pixel. B minus Y uses the original Y, so load all components before any in-place store.
        // Full registers never cross the logical row end; narrower vectors and scalar pixels complete the remainder.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> scaleX = Vector512.Create(scales[0]);
            Vector512<float> scaleY = Vector512.Create(scales[1]);
            Vector512<float> scaleB = Vector512.Create(scales[2]);
            Vector512<float> offsetX = Vector512.Create(offsets[0]);
            Vector512<float> offsetY = Vector512.Create(offsets[1]);
            Vector512<float> offsetB = Vector512.Create(offsets[2]);
            for (; x <= rowX.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> inputX = Vector512.LoadUnsafe(ref firstX, (uint)x);
                Vector512<float> inputY = Vector512.LoadUnsafe(ref firstY, (uint)x);
                Vector512<float> inputB = Vector512.LoadUnsafe(ref firstB, (uint)x);
                ((inputX + offsetX) * scaleX).StoreUnsafe(ref firstX, (uint)x);
                ((inputY + offsetY) * scaleY).StoreUnsafe(ref firstY, (uint)x);
                (((inputB - inputY) + offsetB) * scaleB).StoreUnsafe(ref firstB, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> scaleX = Vector256.Create(scales[0]);
            Vector256<float> scaleY = Vector256.Create(scales[1]);
            Vector256<float> scaleB = Vector256.Create(scales[2]);
            Vector256<float> offsetX = Vector256.Create(offsets[0]);
            Vector256<float> offsetY = Vector256.Create(offsets[1]);
            Vector256<float> offsetB = Vector256.Create(offsets[2]);
            for (; x <= rowX.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> inputX = Vector256.LoadUnsafe(ref firstX, (uint)x);
                Vector256<float> inputY = Vector256.LoadUnsafe(ref firstY, (uint)x);
                Vector256<float> inputB = Vector256.LoadUnsafe(ref firstB, (uint)x);
                ((inputX + offsetX) * scaleX).StoreUnsafe(ref firstX, (uint)x);
                ((inputY + offsetY) * scaleY).StoreUnsafe(ref firstY, (uint)x);
                (((inputB - inputY) + offsetB) * scaleB).StoreUnsafe(ref firstB, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> scaleX = Vector128.Create(scales[0]);
            Vector128<float> scaleY = Vector128.Create(scales[1]);
            Vector128<float> scaleB = Vector128.Create(scales[2]);
            Vector128<float> offsetX = Vector128.Create(offsets[0]);
            Vector128<float> offsetY = Vector128.Create(offsets[1]);
            Vector128<float> offsetB = Vector128.Create(offsets[2]);
            for (; x <= rowX.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> inputX = Vector128.LoadUnsafe(ref firstX, (uint)x);
                Vector128<float> inputY = Vector128.LoadUnsafe(ref firstY, (uint)x);
                Vector128<float> inputB = Vector128.LoadUnsafe(ref firstB, (uint)x);
                ((inputX + offsetX) * scaleX).StoreUnsafe(ref firstX, (uint)x);
                ((inputY + offsetY) * scaleY).StoreUnsafe(ref firstY, (uint)x);
                (((inputB - inputY) + offsetB) * scaleB).StoreUnsafe(ref firstB, (uint)x);
            }
        }

        for (; x < rowX.Length; x++)
        {
            float inputX = rowX[x];
            float inputY = rowY[x];
            float inputB = rowB[x];
            rowX[x] = (inputX + offsets[0]) * scales[0];
            rowY[x] = (inputY + offsets[1]) * scales[1];
            rowB[x] = ((inputB - inputY) + offsets[2]) * scales[2];
        }
    }
}
