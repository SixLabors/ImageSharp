// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Chunks;

internal readonly struct WebpVp8X : IEquatable<WebpVp8X>
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

    public static bool operator ==(WebpVp8X left, WebpVp8X right) => left.Equals(right);

    public static bool operator !=(WebpVp8X left, WebpVp8X right) => !(left == right);

    public override bool Equals(object? obj) => obj is WebpVp8X x && this.Equals(x);

    public bool Equals(WebpVp8X other)
        => this.HasAnimation == other.HasAnimation
        && this.HasXmp == other.HasXmp
        && this.HasExif == other.HasExif
        && this.HasAlpha == other.HasAlpha
        && this.HasIcc == other.HasIcc
        && this.Width == other.Width
        && this.Height == other.Height;

    public override int GetHashCode()
        => HashCode.Combine(this.HasAnimation, this.HasXmp, this.HasExif, this.HasAlpha, this.HasIcc, this.Width, this.Height);

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

    public WebpVp8X WithAlpha(bool hasAlpha)
        => new(this.HasAnimation, this.HasXmp, this.HasExif, hasAlpha, this.HasIcc, this.Width, this.Height);

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

        Span<byte> reserved = stackalloc byte[3];
        stream.Write(reserved);

        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Width - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Height - 1);

        RiffHelper.EndWriteChunk(stream, pos, 2);
    }
}
