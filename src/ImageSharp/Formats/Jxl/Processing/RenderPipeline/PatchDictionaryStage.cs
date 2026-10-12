// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal sealed class PatchDictionaryStage(Configuration configuration, JxlPatchDictionary patches, List<JxlExtraChannelInfo> extraChannelInfos)
    : RenderPipelineStageBase(configuration)
{
    /// <inheritdoc />
    public override string Name => "Patches";

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int channels = 3 + extraChannelInfos.Count;

        Span<Memory<float>> rowPtrs = new Memory<float>[channels];

        for (int i = 0; i < channels; i++)
        {
            rowPtrs[i] = this.GetInputRowMemory(inputRows, i, 0, xExtraLeft);
        }

        return patches.AddOneRow(rowPtrs, yPos, xPos - xExtraLeft, width + xExtraLeft + xExtraRight, extraChannelInfos);
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
    {
        int numChannels = 3 + extraChannelInfos.Count;
        return channel < numChannels
            ? RenderPipelineChannelMode.InPlace
            : RenderPipelineChannelMode.Ignored;
    }
}
