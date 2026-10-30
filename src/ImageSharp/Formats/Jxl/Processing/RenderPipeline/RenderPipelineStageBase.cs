// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Base class for a render pipeline stage.
/// </summary>
[DebuggerDisplay($"{{{nameof(Name)}}}")]
internal abstract class RenderPipelineStageBase(Configuration configuration) : IDisposable
{
    public const int RenderPipelineXOffset = 32;

    /// <summary>
    /// Gets or sets the configuration for this render pipeline stage.
    /// </summary>
    public RenderPipelineStageConfiguration Settings { get; set; }

    /// <summary>
    /// Gets a value indicating whether this stage is initialized and is therefore
    /// ready to use.
    /// </summary>
    public virtual bool IsInitialized => true;

    /// <summary>
    /// Gets a value indicating whether, from this stage on, the pipeline will operate
    /// on an image rather than the frame-sized buffer. Only one stage in the pipeline
    /// should return true, and it should implement <see cref="ProcessPaddingRow(Buffer2D{Memory{float}}, int, int, int)"/>.
    /// </summary>
    public virtual bool SwitchToImageDimensions => false;

    /// <summary>
    /// Gets a friendly name representing this stage.
    /// </summary>
    public virtual string Name => "(invalid pipeline stage)";

    /// <summary>
    /// If any unmanaged or pooled memory is present by the derived stage, releases
    /// memory used by that.
    /// </summary>
    public virtual void Dispose()
    {
    }

    public virtual void ProcessRow(
        Buffer2D<Memory<float>> inputRows,
        Buffer2D<Memory<float>> outputRows,
        int xExtraLeft,
        int xExtraRight,
        int width,
        int xPos,
        int yPos)
    {
    }

    /// <summary>
    /// Represents how each channel will be processed.
    /// </summary>
    /// <param name="channel">Desired channel.</param>
    /// <returns>Mode specifying how the specified channel will be processed.</returns>
    public virtual RenderPipelineChannelMode GetChannelMode(int channel)
        => RenderPipelineChannelMode.Ignored;

    public virtual void SetInputSizes(Span<Size> inputSizes)
    {
    }

    /// <summary>
    /// Borrows an input row, including its remaining right-border storage.
    /// </summary>
    /// <param name="inputRows">The input row table, indexed by channel and vertical position.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="rowOffset">The vertical offset from the current row, in rows.</param>
    /// <returns>The row storage without the unused left prefix.</returns>
    public Span<float> GetInputRow(Buffer2D<Memory<float>> inputRows, int channelIndex, int rowOffset)
        => inputRows[channelIndex, this.Settings.BorderY + rowOffset].Span[RenderPipelineXOffset..];

    /// <summary>
    /// Borrows an input row, including its remaining right-border storage.
    /// </summary>
    /// <param name="inputRows">The input row table, indexed by channel and vertical position.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="rowOffset">The vertical offset from the current row, in rows.</param>
    /// <param name="xExtraLeft">The number of left-border samples to include.</param>
    /// <returns>The row storage without the unused left prefix.</returns>
    public Span<float> GetInputRow(Buffer2D<Memory<float>> inputRows, int channelIndex, int rowOffset, int xExtraLeft)
        => inputRows[channelIndex, this.Settings.BorderY + rowOffset].Span[(RenderPipelineXOffset - xExtraLeft)..];

    /// <summary>
    /// Borrows an input row, including its remaining right-border storage.
    /// </summary>
    /// <param name="inputRows">The input row table, indexed by channel and vertical position.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="rowOffset">The vertical offset from the current row, in rows.</param>
    /// <returns>The row storage without the unused left prefix.</returns>
    public Memory<float> GetInputRowMemory(Buffer2D<Memory<float>> inputRows, int channelIndex, int rowOffset)
        => inputRows[channelIndex, this.Settings.BorderY + rowOffset][RenderPipelineXOffset..];

    /// <summary>
    /// Borrows an input row, including its remaining right-border storage.
    /// </summary>
    /// <param name="inputRows">The input row table, indexed by channel and vertical position.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="rowOffset">The vertical offset from the current row, in rows.</param>
    /// <param name="xExtraLeft">The number of left-border samples to include.</param>
    /// <returns>The row storage without the unused left prefix.</returns>
    public Memory<float> GetInputRowMemory(Buffer2D<Memory<float>> inputRows, int channelIndex, int rowOffset, int xExtraLeft)
        => inputRows[channelIndex, this.Settings.BorderY + rowOffset][(RenderPipelineXOffset - xExtraLeft)..];

    /// <summary>
    /// Borrows an output row without the unused left prefix.
    /// </summary>
    /// <param name="outputRows">The output row table, indexed by channel and vertical position.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="rowOffset">The row index within the output table.</param>
    /// <returns>The output samples and remaining right-border storage.</returns>
    public static Span<float> GetOutputRow(Buffer2D<Memory<float>> outputRows, int channelIndex, int rowOffset)
        => outputRows[channelIndex, rowOffset].Span[RenderPipelineXOffset..];

    public virtual void GetImageDimensions(out int width, out int height, out Point frameOrigin)
    {
        width = 0;
        height = 0;
        frameOrigin = default;
    }

    public virtual void ProcessPaddingRow(Buffer2D<Memory<float>> outputRows, int width, int xPos, int yPos)
    {
    }

    protected Configuration GetConfiguration() => configuration;
}
