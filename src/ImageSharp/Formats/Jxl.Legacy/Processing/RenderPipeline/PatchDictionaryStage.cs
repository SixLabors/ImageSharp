// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Applies reference-frame patches to planar rows using reusable blend storage.
/// </summary>
/// <param name="configuration">The configuration that owns sample storage.</param>
/// <param name="patches">The decoded patch dictionary.</param>
/// <param name="extraChannelInfos">The extra-channel types and alpha representations.</param>
internal sealed class PatchDictionaryStage(Configuration configuration, JxlPatchDictionary patches, List<JxlExtraChannelInfo> extraChannelInfos)
    : RenderPipelineStageBase(configuration)
{
    // These tables contain borrowed memory, not owned sample buffers. Allocate them once for the fixed channel count.
    private readonly Memory<float>[] inputRowTable = new Memory<float>[3 + extraChannelInfos.Count];
    private readonly Memory<float>[] foregroundRowTable = new Memory<float>[3 + extraChannelInfos.Count];

    // One allocator-owned row per channel protects original alpha while results are prepared.
    // The codec is sequential. Every patch and subsequent row can reuse these same rows.
    private JxlPlane<float>? blendStorage;

    // The interval index supplies an exact maximum. Rent the index buffer once for this sequential stage.
    private IMemoryOwner<int>? patchIndices;

    /// <inheritdoc />
    public override string Name => "Patches";

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
        if (patches.MaximumPatchesPerRow == 0)
        {
            return;
        }

        this.patchIndices ??= this.GetConfiguration().MemoryAllocator.Allocate<int>(patches.MaximumPatchesPerRow);
        Span<int> indices = this.patchIndices.GetSpan();
        int rowWidth = width + xExtraLeft + xExtraRight;
        JxlPlane<float>? storage = this.blendStorage;

        if (storage is null || storage.XSize < rowWidth)
        {
            // Grow before releasing the previous allocation so allocation failure does not lose the owned buffer.
            storage = new JxlPlane<float>(this.GetConfiguration(), rowWidth, this.inputRowTable.Length);
            this.blendStorage?.Dispose();
            this.blendStorage = storage;
        }

        for (int channel = 0; channel < this.inputRowTable.Length; channel++)
        {
            this.inputRowTable[channel] = this.GetInputRowMemory(inputRows, channel, 0, xExtraLeft);
        }

        if (!patches.AddOneRow(this.inputRowTable, this.foregroundRowTable, storage, indices, yPos, xPos - xExtraLeft, rowWidth, extraChannelInfos))
        {
            throw new InvalidImageContentException("Invalid JPEG XL patch row.");
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < this.inputRowTable.Length ? RenderPipelineChannelMode.InPlace : RenderPipelineChannelMode.Ignored;

    /// <inheritdoc />
    public override void Dispose()
    {
        this.blendStorage?.Dispose();
        this.blendStorage = null;
        this.patchIndices?.Dispose();
        this.patchIndices = null;

        // References belong to the pipeline and reference frames. Release the borrowed views without disposing their owners.
        Array.Clear(this.inputRowTable);
        Array.Clear(this.foregroundRowTable);
    }
}
