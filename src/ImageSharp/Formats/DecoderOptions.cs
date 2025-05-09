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

    // Used by the FileProvider in the unit tests to set the configuration on the fly.
#pragma warning disable IDE0032 // Use auto property
    private Configuration configuration = Configuration.Default;
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Gets the shared default general decoder options instance.
    /// Used internally to reduce allocations for default decoding operations.
    /// </summary>
    internal static DecoderOptions Default { get; } = LazyOptions.Value;

    /// <summary>
    /// Gets a custom configuration instance to be used by the image processing pipeline.
    /// </summary>
#pragma warning disable IDE0032 // Use auto property
#pragma warning disable RCS1085 // Use auto-implemented property.
    public Configuration Configuration { get => this.configuration; init => this.configuration = value; }
#pragma warning restore RCS1085 // Use auto-implemented property.
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Gets the target size to decode the image into. Scaling should use an operation equivalent to <see cref="ResizeMode.Max"/>.
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

    /// <summary>
    /// Gets the segment error handling strategy to use during decoding.
    /// </summary>
    public SegmentIntegrityHandling SegmentIntegrityHandling { get; init; } = SegmentIntegrityHandling.IgnoreNonCritical;

    /// <summary>
    /// Gets a value that controls how ICC profiles are handled during decode.
    /// TODO: Implement this.
    /// </summary>
    internal ColorProfileHandling ColorProfileHandling { get; init; }

    internal void SetConfiguration(Configuration configuration) => this.configuration = configuration;
}
