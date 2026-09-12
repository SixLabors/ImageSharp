// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

/// <summary>
/// An image bundle.
/// </summary>
internal sealed class JxlImageBundle
{
    /// <summary>
    /// Image data for additional channels.
    /// </summary>
    private List<JxlImageF> extraChannels = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlImageBundle"/> class.
    /// </summary>
    public JxlImageBundle()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlImageBundle"/> class with the specified image metadata.
    /// </summary>
    /// <param name="metadata">Initial image metadata.</param>
    public JxlImageBundle(JxlImageMetadata? metadata) => this.Metadata = metadata;

    /// <summary>
    /// Gets or sets the optional name of the bundle.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the blend mode. Default is Blend.
    /// </summary>
    public JxlBlendMode BlendMode { get; set; } = JxlBlendMode.Blend;

    /// <summary>
    /// Gets or sets a value indicating whether blending should be done. (Default: false)
    /// </summary>
    public bool Blend { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this a reference frame.
    /// </summary>
    public bool UseForNextFrame { get; set; }

    /// <summary>
    /// Gets or sets the duration for animation.
    /// </summary>
    public uint Duration { get; set; }

    /// <summary>
    /// Gets or sets the timecode for animation.
    /// </summary>
    public uint Timecode { get; set; }

    /// <summary>
    /// Gets or sets the frame origin.
    /// </summary>
    public Point Origin { get; set; }

    /// <summary>
    /// Gets or sets the chroma subsampling for Y'Cb'Cr images.
    /// </summary>
    public JxlYCbCrChromaSubsampling? ChromaSubsampling { get; set; }

    /// <summary>
    /// Gets or sets the color transform mode for this image, the default is None.
    /// </summary>
    public JxlColorTransform ColorTransform { get; set; } = JxlColorTransform.None;

    /// <summary>
    /// Gets or sets the JPEG data if the input image was converted to JPEG XL from a JPEG.
    /// </summary>
    public JpegData? JpegData { get; set; }

    /// <summary>
    /// Gets a value indicating whether returns the image does or will represent quantized DCT-8 coefficients
    /// stored in the 8x8 pixel regions.
    /// </summary>
    public bool IsJpeg => this.JpegData is not null;

    /// <summary>
    /// Gets or sets the number of bytes that were actually read.
    /// </summary>
    public long DecodedBytes { get; set; }

    /// <summary>
    /// Gets a value indicating whether the black extra channel is present.
    /// </summary>
    public bool ContainsBlack => this.Metadata?.FindExtraChannel(JxlExtraChannel.Black) is not null;

    /// <summary>
    /// Gets a value indicating whether the alpha extra channel is present.
    /// </summary>
    public bool ContainsAlpha => this.Metadata?.FindExtraChannel(JxlExtraChannel.Alpha) is not null;

    /// <summary>
    /// Gets a value indicating whether the alpha channel is premultiplied.
    /// </summary>
    public bool IsAlphaPremultiplied => this.Metadata?.FindExtraChannel(JxlExtraChannel.Alpha)?.AlphaAssociated == true;

    /// <summary>
    /// Gets a value indicating whether the color encoding specifies Gray.
    /// </summary>
    public bool IsGray => this.CurrentColorEncoding?.IsGray == true;

    /// <summary>
    /// Gets a value indicating whether the color encoding specifies sRGB.
    /// </summary>
    public bool IsSrgb => this.CurrentColorEncoding?.IsSrgb == true;

    /// <summary>
    /// Gets a value indicating whether the color encoding specifies linear sRGB.
    /// </summary>
    public bool IsLinearSrgb => this.CurrentColorEncoding?.IsLinearSrgb == true;

    /// <summary>
    /// Gets the current color encoding for this image.
    /// </summary>
    public JxlColorEncoding? CurrentColorEncoding { get; private set; }

    /// <summary>
    /// Gets the image metadata for this image bundle.
    /// </summary>
    public JxlImageMetadata? Metadata { get; }

    /// <summary>
    /// Gets the color data.
    /// </summary>
    public JxlImage3F? Color { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the color data is present and usable.
    /// </summary>
    public bool HasColor => this.Color?.XSize != 0;

    /// <summary>
    /// Gets the width.
    /// </summary>
    public int XSize
    {
        get
        {
            if (this.IsJpeg)
            {
                return this.JpegData!.Width;
            }

            if (this.Color?.XSize != 0)
            {
                return this.Color!.XSize;
            }

            return this.extraChannels?.Count > 0 ? 0 : this.extraChannels![0].XSize;
        }
    }

    /// <summary>
    /// Gets the height.
    /// </summary>
    public int YSize
    {
        get
        {
            if (this.IsJpeg)
            {
                return this.JpegData!.Height;
            }

            if (this.Color?.YSize != 0)
            {
                return this.Color!.YSize;
            }

            return this.extraChannels?.Count > 0 ? 0 : this.extraChannels![0].YSize;
        }
    }

    /// <summary>
    /// Gets the black extra channel.
    /// </summary>
    public JxlImageF? Black
    {
        get
        {
            if (!this.ContainsBlack || this.Metadata is null)
            {
                return null;
            }

            int ec = this.Metadata!.FindExtraChannel(JxlExtraChannel.Black) - this.Metadata.ExtraChannelInfo.Data;
            return this.extraChannels[ec];
        }
    }

    /// <summary>
    /// Gets the alpha extra channel.
    /// </summary>
    public JxlImageF? Alpha
    {
        get
        {
            if (!this.ContainsAlpha || this.Metadata is null)
            {
                return null;
            }

            int ec = this.Metadata!.FindExtraChannel(JxlExtraChannel.Alpha) - this.Metadata.ExtraChannelInfo.Data;
            return this.extraChannels[ec];
        }
    }

    /// <summary>
    /// Gets the oriented X size.
    /// </summary>
    public int OrientedXSize => this.Metadata?.Orientation > 4 ? this.YSize : this.XSize;

    /// <summary>
    /// Gets the oriented Y size.
    /// </summary>
    public int OrientedYSize => this.Metadata?.Orientation > 4 ? this.XSize : this.YSize;

    /// <summary>
    /// Gets the real bit depth.
    /// </summary>
    public uint RealBitDepth => this.Metadata!.BitDepth!.BitsPerSample;

    /// <summary>
    /// Returns false if the width or height is 0 and any extra channel
    /// does not match this image bundle's width or height; returns true if
    /// the sizes are otherwise correct.
    /// </summary>
    /// <returns>
    /// True if the sizes are correct. False if they aren't.
    /// </returns>
    public bool VerifySizes()
    {
        if (this.ContainsExtraChannels())
        {
            int xs = this.XSize;
            int ys = this.YSize;

            if (xs == 0 || ys == 0)
            {
                return false;
            }

            foreach (JxlImageF ec in this.extraChannels)
            {
                if (ec.XSize != xs || ec.YSize != ys)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Overrides the color encoding for this image bundle.
    /// </summary>
    /// <param name="encoding">The new color encoding.</param>
    public void OverrideProfile(JxlColorEncoding encoding) => this.CurrentColorEncoding = encoding;

    /// <summary>
    /// If the color data is present, assigns it to <paramref name="color"/> and returns true,
    /// otherwise returns false and assigns null.
    /// </summary>
    /// <param name="color">Output color data.</param>
    /// <returns>True if color isn't null.</returns>
    public bool TryGetColor(out JxlImage3F? color)
    {
        color = null;
        if (this.Color is not null)
        {
            color = this.Color;
        }

        return color is not null;
    }

    /// <summary>
    /// Removes the color data, replacing it with a new Image3F with 0 as width and height.
    /// </summary>
    public void RemoveColor() => this.Color = new JxlImage3F();

    /// <summary>
    /// Removes all extra channels, if any.
    /// </summary>
    public void ClearExtraChannels() => this.extraChannels.Clear();

    /// <summary>
    /// Returns true if there is at least 1 extra channel.
    /// </summary>
    /// <returns>Boolean indicating if extra channels are present.</returns>
    public bool ContainsExtraChannels() => this.extraChannels.Count > 0;

    /// <summary>
    /// Returns an enumerable for extra channels.
    /// </summary>
    /// <returns>Extra channels enumerable.</returns>
    public IEnumerable<JxlImageF> EnumerateExtraChannels() => this.extraChannels;

    /// <summary>
    /// Sets the extra channels.
    /// </summary>
    /// <param name="extraChannels">The extra channels.</param>
    /// <returns>
    /// True if each plane had width and height greater than 0 and sizes are correct
    /// after changing the extra channels; false otherwise.
    /// </returns>
    public bool TrySetExtraChannels(List<JxlImageF> extraChannels)
    {
        foreach (JxlImageF plane in extraChannels)
        {
            if (plane.XSize == 0 || plane.YSize == 0)
            {
                return false;
            }
        }

        this.extraChannels = extraChannels;

        return this.VerifySizes();
    }

    /// <summary>
    /// Attempts to set the alpha channel.
    /// </summary>
    /// <param name="alpha">The alpha channel to set.</param>
    /// <returns>True if it was set successfully; false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the corresponding extra channel has incorrect info.</exception>
    public bool TrySetAlpha(JxlImageF alpha)
    {
        if (this.Metadata is null)
        {
            return false;
        }

        JxlExtraChannelInfo? eci = this.Metadata!.FindExtraChannel(JxlExtraChannel.Alpha);

        if (eci is null)
        {
            return false;
        }

        if (alpha.XSize == 0 || alpha.YSize == 0)
        {
            return false;
        }

        int eciIndex = this.Metadata.ExtraChannelInfo.Data;

        if (eciIndex != this.extraChannels.Count)
        {
            throw new InvalidOperationException("The SetAlpha parameter is incorrect");
        }

        this.extraChannels.Add(alpha);

        return this.VerifySizes();
    }

    /// <summary>
    /// Ensures that the metadata of this image is valid.
    /// </summary>
    /// <returns>True if metadata is correct. False if it isn't.</returns>
    /// <exception cref="InvalidOperationException">Rare.</exception>
    public bool VerifyMetadata()
    {
        if (this.CurrentColorEncoding?.Icc?.IsEmpty == true)
        {
            return false;
        }

        if (this.Metadata?.ColorEncoding?.IsGray != this.IsGray)
        {
            return false;
        }

        if (this.Metadata?.HasAlpha == true)
        {
            JxlImageF? img = this.Alpha;
            if (img?.XSize == 0)
            {
                throw new InvalidOperationException("Alpha should not have width equal to 0");
            }
        }

        int alphaBits = this.Metadata?.AlphaBits ?? 0;

        if (alphaBits > 32)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Updates the bundle from the sepcified image.
    /// </summary>
    /// <param name="color">Color data.</param>
    /// <param name="current">Current color encoding.</param>
    /// <returns>True if setting the image succeeded; otherwise false.</returns>
    public bool SetFromImage(JxlImage3F color, JxlColorEncoding current)
    {
        if (color.XSize == 0 || color.YSize == 0)
        {
            return false;
        }

        if (this.Metadata?.ColorEncoding?.IsGray == this.IsGray)
        {
            return false;
        }

        this.Color = color;
        this.CurrentColorEncoding = current;

        return this.VerifySizes();
    }

    /// <summary>
    /// Shrinks this image and all of its extra channels to the specified
    /// width and height.
    /// </summary>
    /// <param name="width">The desired width.</param>
    /// <param name="height">The desired height.</param>
    /// <returns>
    /// If this bundle color data or any of the extra channels
    /// happens to have a smaller width or height than the specified
    /// width or height, that is considered expanding, which will immediately
    /// return false. If this method returns true, all colors and
    /// extra channels have successfully been shrunk.
    /// </returns>
    public bool ShrinkTo(int width, int height)
    {
        if (this.HasColor)
        {
            if (this.Color?.ShrinkTo(width, height) != true)
            {
                return false;
            }
        }

        foreach (JxlImageF extraChannel in this.extraChannels)
        {
            if (!extraChannel.ShrinkTo(width, height))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Copies this image bundle to a new bundle.
    /// </summary>
    /// <param name="configuration">
    /// A configuration with a memory allocator.
    /// </param>
    /// <returns>
    /// A new copy of this image bundle.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if some extra channels cannot be copied.
    /// </exception>
    public JxlImageBundle Copy(Configuration configuration)
    {
        JxlImageBundle copy = new(this.Metadata);

        if (this.Color is not null)
        {
            copy.Color = new JxlImage3F(configuration, this.Color.XSize, this.Color.YSize);
        }

        copy.CurrentColorEncoding = this.CurrentColorEncoding;
        copy.JpegData = this.JpegData;
        copy.ColorTransform = this.ColorTransform;
        copy.ChromaSubsampling = this.ChromaSubsampling;

        foreach (JxlImageF plane in this.extraChannels)
        {
            JxlImageF ec = new(configuration, plane.XSize, plane.YSize);
            if (!JxlImageOperations.CopyImage(plane, ec))
            {
                throw new InvalidOperationException("Cannot copy extra channel");
            }

            copy.extraChannels.Add(ec);
        }

        return copy;
    }
}
