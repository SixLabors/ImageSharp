// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class HorizontalChromaUpsamplingStage : RenderPipelineStageBase
{
    private readonly int channel;

    public HorizontalChromaUpsamplingStage(Configuration configuration, int channel)
        : base(configuration)
    {
        this.Settings = RenderPipelineStageConfiguration.CreateShiftX(shift: 1, border: 1);
        this.channel = channel;
    }

    public override string Name => "HChromaUps";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector<float>.Count);
        int xEnd = width + xExtraRight;

        Vector<float> threeFour = Vector.Create(0.75f); // 3/4
        Vector<float> oneFour = Vector.Create(0.25f); // 1/4
        Span<float> rowIn = this.GetInputRow(inputRows, this.channel, 0);
        Span<float> rowOut = GetOutputRow(outputRows, this.channel, 0);

        ref float rRowIn = ref MemoryMarshal.GetReference(rowIn);
        ref float rRowOut = ref MemoryMarshal.GetReference(rowOut);

        for (int x = xStart; x < xEnd; x += Vector<float>.Count)
        {
            Vector<float> current = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowIn, x)) * threeFour;
            Vector<float> prev = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowIn, x - 1));
            Vector<float> next = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowIn, x + 1));

            Vector<float> left = (oneFour * prev) + current;
            Vector<float> right = (oneFour * next) + current;

            JxlSimdUtils.StoreInterleaved(left, right, ref Unsafe.Add(ref rRowOut, x * 2));
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => channel == this.channel ? RenderPipelineChannelMode.InOut : RenderPipelineChannelMode.Ignored;
}
