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
    private const int RenderPipelineXOffset = 32;

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

    public Span<float> GetInputRow(Buffer2D<Memory<float>> inputRows, int c, int offset)
        => inputRows[c, this.Settings.BorderY + offset].Span[RenderPipelineXOffset..];

    public Span<float> GetInputRow(Buffer2D<Memory<float>> inputRows, int c, int offset, int xExtraLeft)
        => inputRows[c, this.Settings.BorderY + offset].Span[(RenderPipelineXOffset - xExtraLeft)..];

    public Memory<float> GetInputRowMemory(Buffer2D<Memory<float>> inputRows, int c, int offset)
        => inputRows[c, this.Settings.BorderY + offset][RenderPipelineXOffset..];

    public Memory<float> GetInputRowMemory(Buffer2D<Memory<float>> inputRows, int c, int offset, int xExtraLeft)
        => inputRows[c, this.Settings.BorderY + offset][(RenderPipelineXOffset - xExtraLeft)..];

    public static Span<float> GetOutputRow(Buffer2D<Memory<float>> outputRows, int c, int offset)
        => outputRows[c, offset].Span[RenderPipelineXOffset..];

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
