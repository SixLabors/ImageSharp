// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// SIMD-based conversion from Y'Cb'Cr to RGB pixel buffers.
/// </summary>
internal sealed class YCbCrStage(Configuration configuration) : RenderPipelineStageBase(configuration)
{
    /// <inheritdoc />
    public override string Name => "YCbCr";

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        // Vectors for conversion, defined by the ITU
        Vector<float> c128 = Vector.Create(128.0f / 255);
        Vector<float> crcr = Vector.Create(1.402f);
        Vector<float> cgcb = Vector.Create(-0.114f * 1.772f / 0.587f);
        Vector<float> cgcr = Vector.Create(-0.299f * 1.402f / 0.587f);
        Vector<float> cbcb = Vector.Create(1.772f);

        Span<float> row0 = this.GetInputRow(inputRows, 0, 0);
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0);
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0);

        // Using refs for better performance
        ref float row0Ref = ref MemoryMarshal.GetReference(row0);
        ref float row1Ref = ref MemoryMarshal.GetReference(row1);
        ref float row2Ref = ref MemoryMarshal.GetReference(row2);

        for (int x = 0; x < width; x += Vector<float>.Count)
        {
            // Y'Cb'Cr input vectors
            Vector<float> yVec = Vector.LoadUnsafe(ref Unsafe.Add(ref row1Ref, x)) + c128;
            Vector<float> cbVec = Vector.LoadUnsafe(ref Unsafe.Add(ref row0Ref, x));
            Vector<float> crVec = Vector.LoadUnsafe(ref Unsafe.Add(ref row2Ref, x));

            // RGB output vectors
            Vector<float> rVec = (crcr * crVec) + yVec;
            Vector<float> gVec = (cgcr * crVec) + ((cgcb * cbVec) + yVec);
            Vector<float> bVec = (cbcb * cbVec) + yVec;

            // Copying to the output...
            rVec.StoreUnsafe(ref Unsafe.Add(ref row0Ref, x));
            gVec.StoreUnsafe(ref Unsafe.Add(ref row1Ref, x));
            bVec.StoreUnsafe(ref Unsafe.Add(ref row2Ref, x));
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel) =>
        channel < 3
            ? RenderPipelineChannelMode.InPlace
            : RenderPipelineChannelMode.Ignored;
}
