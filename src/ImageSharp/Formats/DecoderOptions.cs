// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides general configuration options for decoding image formats.
/// </summary>
public sealed class DecoderOptions
{
    private static readonly Lazy<DecoderOptions> LazyOptions = new(() => new());

    private uint maxFrames = int.MaxValue;

    /// <summary>
    /// Gets the shared default general decoder options instance.
    /// </summary>
    internal static DecoderOptions Default { get; } = LazyOptions.Value;

    /// <summary>
    /// Gets a custom configuration instance to be used by the image processing pipeline.
    /// </summary>
    public Configuration Configuration { get; internal set; } = Configuration.Default;

    /// <summary>
    /// Gets the target size to decode the image into.
    /// </summary>
    public Size? TargetSize { get; init; }

    /// <summary>
    /// Gets the sampler to use when resizing during decoding.
    /// </summary>
    public IResampler Sampler { get; init; } = KnownResamplers.Box;

    /// <summary>
    /// Gets a value indicating whether to ignore encoded metadata when decoding.
    /// </summary>
    public bool SkipMetadata { get; init; }

    /// <summary>
    /// Gets the maximum number of image frames to decode, inclusive.
    /// </summary>
    public uint MaxFrames { get => this.maxFrames; init => this.maxFrames = Math.Clamp(value, 1, int.MaxValue); }
}
