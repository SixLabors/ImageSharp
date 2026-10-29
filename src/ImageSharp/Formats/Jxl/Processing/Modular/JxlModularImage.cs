// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

/// <summary>
/// Owns the modular channels for one image and the transform metadata applied to them.
/// </summary>
internal sealed class JxlModularImage : IDisposable
{
    /// <summary>
    /// The consumable inverse-transform stack. Parsed header lists retain their own entries.
    /// </summary>
    private List<JxlTransform> transforms = [];

    /// <summary>
    /// Initializes image metadata without allocating channel samples.
    /// </summary>
    /// <param name="width">The image width in samples.</param>
    /// <param name="height">The image height in rows.</param>
    /// <param name="bitDepth">The encoded sample precision.</param>
    public JxlModularImage(int width, int height, int bitDepth)
    {
        this.Width = width;
        this.Height = height;
        this.BitDepth = bitDepth;
        this.MetaChannels = 0;
        this.IsInvalid = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlModularImage"/> class,
    /// representing an empty image with 0x0 width/height and a bit-depth
    /// of 8 bits.
    /// </summary>
    public JxlModularImage()
        : this(0, 0, 8)
    {
    }

    /// <summary>
    /// Gets the owned channels. Removed or replaced channels must be released by the operation that removes them.
    /// </summary>
    public List<JxlModularChannel> Channels { get; } = [];

    /// <summary>
    /// Gets the inverse-transform stack, or copies parsed transform metadata without changing the source list or its descriptors.
    /// </summary>
    public List<JxlTransform> Transforms
    {
        get => this.transforms;
        set
        {
            // MetaApply can expand an empty squeeze list using this image's channel shapes. The parsed header must retain its encoded defaults.
            List<JxlTransform> replacement = new(value.Count);
            foreach (JxlTransform transform in value)
            {
                replacement.Add(transform.DeepClone());
            }

            // Publish only the complete copy. The source can refer to the current stack; copying must not clear it before reading its entries.
            this.transforms = replacement;
        }
    }

    /// <summary>
    /// Gets or sets the total number of metachannels in this image.
    /// </summary>
    public int MetaChannels { get; set; }

    /// <summary>
    /// Gets or sets the bit depth used in this image.
    /// </summary>
    public int BitDepth { get; set; }

    /// <summary>
    /// Gets or sets modular image width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets modular image height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the modular image has an error.
    /// </summary>
    public bool IsInvalid { get; set; }

    /// <summary>
    /// Gets a value indicating whether no channel contains samples.
    /// </summary>
    public bool IsEmpty => !this.Channels.Any(x => x.Width > 0 && x.Height > 0);

    /// <summary>
    /// Applies the recorded inverse transforms to this image's owned channels.
    /// </summary>
    /// <param name="configuration">The configuration used by transforms that allocate output samples.</param>
    /// <param name="wpHeader">The weighted-prediction settings.</param>
    public void UndoTransforms(Configuration configuration, JxlModularHeader wpHeader)
    {
        while (this.Transforms.Count > 0)
        {
            // Forward transforms compose in list order. Their inverse must consume the final transform first.
            JxlTransform transform = this.Transforms[^1];
            transform.Inverse(configuration, this, wpHeader);
            this.Transforms.RemoveAt(this.Transforms.Count - 1);
        }
    }

    /// <summary>
    /// Allocates image channels and releases partial allocations if construction fails.
    /// </summary>
    /// <param name="configuration">The configuration used for channel sample allocations.</param>
    /// <param name="width">The image width in samples.</param>
    /// <param name="height">The image height in rows.</param>
    /// <param name="bitDepth">The encoded sample precision.</param>
    /// <param name="channels">The number of owned channels.</param>
    /// <returns>The image that owns every completed channel allocation.</returns>
    public static JxlModularImage Create(Configuration configuration, int width, int height, int bitDepth, int channels)
    {
        JxlModularImage result = new(width, height, bitDepth);

        // Reserve references before allocating samples so List.Add cannot strand a completed channel if list growth fails.
        result.Channels.EnsureCapacity(channels);

        try
        {
            for (int i = 0; i < channels; i++)
            {
                JxlModularChannel c = new(configuration, width, height, 0, 0)
                {
                    Component = i
                };

                result.Channels.Add(c);
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Releases all owned channel samples and clears the existing channel list for reuse.
    /// </summary>
    public void Dispose()
    {
        foreach (JxlModularChannel channel in this.Channels)
        {
            channel.Dispose();
        }

        this.Channels.Clear();
        this.Transforms.Clear();
    }
}
