// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

internal sealed class JxlModularImage : IDisposable
{
    public JxlModularImage(int width, int height, int bitDepth)
    {
        this.Width = width;
        this.Height = height;
        this.BitDepth = bitDepth;
        this.MetaChannels = 0;
        this.IsInvalid = false;
    }

    public JxlModularImage()
        : this(0, 0, 8)
    {
    }

    public List<JxlModularChannel> Channels { get; set; } = [];

    public List<JxlTransform> Transforms { get; set; } = [];

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

    public bool IsEmpty => this.Channels.Any(x => x.Width > 0 && x.Height > 0);

    public void UndoTransforms(Configuration configuration, JxlModularHeader wpHeader)
    {
        while (this.Transforms.Count > 0)
        {
            JxlTransform transform = this.Transforms.First();
            transform.Inverse(configuration, this, wpHeader);
            this.Transforms.RemoveAt(0);
        }
    }

    public static JxlModularImage Create(Configuration configuration, int width, int height, int bitDepth, int channels)
    {
        JxlModularImage result = new(width, height, bitDepth);

        for (int i = 0; i < channels; i++)
        {
            JxlModularChannel c = new(configuration, width, height, 0, 0);

            result.Channels.Add(c);
            result.Channels[^1].Component = i;
        }

        return result;
    }

    public void Dispose()
    {
        foreach (JxlModularChannel channel in this.Channels)
        {
            channel.Dispose();
        }

        this.Channels = [];
    }
}
