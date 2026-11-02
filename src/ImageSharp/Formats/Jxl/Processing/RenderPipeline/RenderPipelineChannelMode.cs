// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Specifies how does a render pipeline stage apply to channels.
/// </summary>
internal enum RenderPipelineChannelMode : byte
{
    /// <summary>
    /// Channel is not modified.
    /// </summary>
    Ignored,

    /// <summary>
    /// Channel is in-place.
    /// </summary>
    InPlace,

    /// <summary>
    /// Channel is modified and written to a new buffer.
    /// </summary>
    InOut,

    /// <summary>
    /// Read-only channel.
    /// </summary>
    Input
}
