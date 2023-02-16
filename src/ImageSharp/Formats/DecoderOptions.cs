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
#pragma warning disable SA1401 // Fields should be private
    internal Configuration BackingConfiguration = Configuration.Default;
#pragma warning restore SA1401 // Fields should be private

    /// <summary>
    /// Gets the shared default general decoder options instance.
    /// Used internally to reduce allocations for default decoding operations.
    /// </summary>
    internal static DecoderOptions Default { get; } = LazyOptions.Value;

    /// <summary>
    /// Gets a custom configuration instance to be used by the image processing pipeline.
    /// </summary>
    public Configuration Configuration { get => this.BackingConfiguration; init => this.BackingConfiguration = value; }

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
}

internal static class DecoderOptionsExtensions
{
    public static void SetConfiguration(this DecoderOptions options, Configuration configuration)
        => options.BackingConfiguration = configuration;
}
