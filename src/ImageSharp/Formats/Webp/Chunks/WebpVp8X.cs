// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Chunks;

internal readonly struct WebpVp8X
{
    public WebpVp8X(bool hasAnimation, bool hasXmp, bool hasExif, bool hasAlpha, bool hasIcc, uint width, uint height)
    {
        this.HasAnimation = hasAnimation;
        this.HasXmp = hasXmp;
        this.HasExif = hasExif;
        this.HasAlpha = hasAlpha;
        this.HasIcc = hasIcc;
        this.Width = width;
        this.Height = height;
    }

    /// <summary>
    /// Gets a value indicating whether this is an animated image. Data in 'ANIM' and 'ANMF' Chunks should be used to control the animation.
    /// </summary>
    public bool HasAnimation { get; }

    /// <summary>
    /// Gets a value indicating whether the file contains XMP metadata.
    /// </summary>
    public bool HasXmp { get; }

    /// <summary>
    /// Gets a value indicating whether the file contains Exif metadata.
    /// </summary>
    public bool HasExif { get; }

    /// <summary>
    /// Gets a value indicating whether any of the frames of the image contain transparency information ("alpha").
    /// </summary>
    public bool HasAlpha { get; }

    /// <summary>
    /// Gets a value indicating whether the file contains an 'ICCP' Chunk.
    /// </summary>
    public bool HasIcc { get; }

    /// <summary>
    /// Gets width of the canvas in pixels. (uint24)
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets height of the canvas in pixels. (uint24)
    /// </summary>
    public uint Height { get; }

    public void Validate(uint maxDimension, ulong maxCanvasPixels)
    {
        if (this.Width > maxDimension || this.Height > maxDimension)
        {
            WebpThrowHelper.ThrowInvalidImageDimensions($"Image width or height exceeds maximum allowed dimension of {maxDimension}");
        }

        // The spec states that the product of Canvas Width and Canvas Height MUST be at most 2^32 - 1.
        if (this.Width * this.Height > maxCanvasPixels)
        {
            WebpThrowHelper.ThrowInvalidImageDimensions("The product of image width and height MUST be at most 2^32 - 1");
        }
    }

    public void WriteTo(Stream stream)
    {
        byte flags = 0;

        if (this.HasAnimation)
        {
            // Set animated flag.
            flags |= 2;
        }

        if (this.HasXmp)
        {
            // Set xmp bit.
            flags |= 4;
        }

        if (this.HasExif)
        {
            // Set exif bit.
            flags |= 8;
        }

        if (this.HasAlpha)
        {
            // Set alpha bit.
            flags |= 16;
        }

        if (this.HasIcc)
        {
            // Set icc flag.
            flags |= 32;
        }

        long pos = RiffHelper.BeginWriteChunk(stream, (uint)WebpChunkType.Vp8X);

        stream.WriteByte(flags);
        stream.Position += 3; // Reserved bytes
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Width - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Height - 1);

        RiffHelper.EndWriteChunk(stream, pos);
    }
}
