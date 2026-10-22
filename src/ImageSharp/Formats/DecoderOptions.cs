// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides general configuration options for decoding image formats.
/// </summary>
public sealed class DecoderOptions
{
    private static readonly Lazy<DecoderOptions> LazyOptions = new(() => new DecoderOptions(Configuration.Default));

    private uint maxFrames = int.MaxValue;

    // Used by the FileProvider in the unit tests to set the configuration on the fly.
#pragma warning disable IDE0032 // Use auto property
    private Configuration configuration;
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Initializes a new instance of the <see cref="DecoderOptions"/> class with the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    public DecoderOptions(Configuration configuration)
    {
        Guard.NotNull(configuration, nameof(configuration));

        // Keep default configuration selection in the default entry points. Explicit options must not retain every codec.
        this.configuration = configuration;
    }

    /// <summary>
    /// Gets the shared default general decoder options instance.
    /// Used internally to reduce allocations for default decoding operations.
    /// </summary>
    internal static DecoderOptions Default { get; } = LazyOptions.Value;

    /// <summary>
    /// Gets the configuration supplied when these options were created.
    /// </summary>
#pragma warning disable IDE0032 // Use auto property
#pragma warning disable RCS1085 // Use auto-implemented property.
    public Configuration Configuration => this.configuration;
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
    /// Gets a value indicating whether to ignore optional encoded metadata when decoding.
    /// ICC profiles remain available for color conversion.
    /// </summary>
    public bool SkipMetadata { get; init; }

    /// <summary>
    /// Gets the maximum number of image frames to decode, inclusive.
    /// </summary>
    public uint MaxFrames { get => this.maxFrames; init => this.maxFrames = Math.Clamp(value, 1, int.MaxValue); }

    /// <summary>
    /// Gets the segment error handling strategy to use during decoding.
    /// </summary>
    public SegmentIntegrityHandling SegmentIntegrityHandling { get; init; } = SegmentIntegrityHandling.IgnoreAncillary;

    /// <summary>
    /// Gets a value that controls ICC color conversion and profile retention during decoding and identification.
    /// </summary>
    public ColorProfileHandling ColorProfileHandling { get; init; }

    internal void SetConfiguration(Configuration configuration) => this.configuration = configuration;

    internal bool CanRemoveIccProfile(IccProfile? profile)
    {
        if (profile is null)
        {
            return false;
        }

        if (this.ColorProfileHandling == ColorProfileHandling.Convert)
        {
            return true;
        }

        return this.ColorProfileHandling == ColorProfileHandling.Compact && profile.IsCanonicalSrgbMatrixTrc();
    }
}
