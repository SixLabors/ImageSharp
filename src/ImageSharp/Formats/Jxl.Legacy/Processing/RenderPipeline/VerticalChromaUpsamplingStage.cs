// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class VerticalChromaUpsamplingStage : RenderPipelineStageBase
{
    private readonly int channel;

    public VerticalChromaUpsamplingStage(Configuration configuration, int channel)
        : base(configuration)
    {
        this.Settings = RenderPipelineStageConfiguration.CreateShiftY(shift: 1, border: 1);
        this.channel = channel;
    }

    public override string Name => "VChromaUps";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector<float>.Count);
        int xEnd = width + xExtraRight;

        Vector<float> threeFour = Vector.Create(0.75f); // 3/4
        Vector<float> oneFour = Vector.Create(0.25f); // 1/4

        Span<float> rowTop = this.GetInputRow(inputRows, this.channel, -1);
        Span<float> rowMid = this.GetInputRow(inputRows, this.channel, 0);
        Span<float> rowBot = this.GetInputRow(inputRows, this.channel, 1);

        Span<float> rowOut0 = GetOutputRow(outputRows, this.channel, 0);
        Span<float> rowOut1 = GetOutputRow(outputRows, this.channel, 1);

        ref float rRowTop = ref MemoryMarshal.GetReference(rowTop);
        ref float rRowMid = ref MemoryMarshal.GetReference(rowMid);
        ref float rRowBot = ref MemoryMarshal.GetReference(rowBot);

        for (int x = xStart; x < xEnd; x += Vector<float>.Count)
        {
            Vector<float> vtop = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowTop, x));
            Vector<float> vmid = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowMid, x));
            Vector<float> vbot = Vector.LoadUnsafe(ref Unsafe.Add(ref rRowBot, x));
            Vector<float> vmidscale = vmid * threeFour;

            ((vtop * oneFour) + vmidscale).CopyTo(rowOut0[x..]);
            ((vbot * oneFour) + vmidscale).CopyTo(rowOut1[x..]);
        }
    }

    public override RenderPipelineChannelMode GetChannelMode(int channel) => channel == this.channel ? RenderPipelineChannelMode.InOut : RenderPipelineChannelMode.Ignored;
}
