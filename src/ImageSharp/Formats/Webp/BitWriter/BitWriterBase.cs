// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Chunks;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.BitWriter;

internal abstract class BitWriterBase
{
    private const uint MaxDimension = 16777215;

    private const ulong MaxCanvasPixels = 4294967295ul;

    /// <summary>
    /// Buffer to write to.
    /// </summary>
    private byte[] buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
    /// </summary>
    /// <param name="expectedSize">The expected size in bytes.</param>
    protected BitWriterBase(int expectedSize) => this.buffer = new byte[expectedSize];

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
    /// Used internally for cloning.
    /// </summary>
    /// <param name="buffer">The byte buffer.</param>
    private protected BitWriterBase(byte[] buffer) => this.buffer = buffer;

    public byte[] Buffer => this.buffer;

    /// <summary>
    /// Gets the number of bytes of the encoded image data.
    /// </summary>
    /// <returns>The number of bytes of the image data.</returns>
    public abstract int NumBytes { get; }

    /// <summary>
    /// Writes the encoded bytes of the image to the stream. Call Finish() before this.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteToStream(Stream stream) => stream.Write(this.Buffer.AsSpan(0, this.NumBytes));

    /// <summary>
    /// Writes the encoded bytes of the image to the given buffer. Call Finish() before this.
    /// </summary>
    /// <param name="dest">The destination buffer.</param>
    public void WriteToBuffer(Span<byte> dest) => this.Buffer.AsSpan(0, this.NumBytes).CopyTo(dest);

    /// <summary>
    /// Resizes the buffer to write to.
    /// </summary>
    /// <param name="extraSize">The extra size in bytes needed.</param>
    public abstract void BitWriterResize(int extraSize);

    /// <summary>
    /// Flush leftover bits.
    /// </summary>
    public abstract void Finish();

    protected void ResizeBuffer(int maxBytes, int sizeRequired)
    {
        int newSize = (3 * maxBytes) >> 1;
        if (newSize < sizeRequired)
        {
            newSize = sizeRequired;
        }

        // Make new size multiple of 1k.
        newSize = ((newSize >> 10) + 1) << 10;
        Array.Resize(ref this.buffer, newSize);
    }

    /// <summary>
    /// Write the trunks before data trunk.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="exifProfile">The exif profile.</param>
    /// <param name="xmpProfile">The XMP profile.</param>
    /// <param name="iccProfile">The color profile.</param>
    /// <param name="hasAlpha">Flag indicating, if a alpha channel is present.</param>
    /// <param name="hasAnimation">Flag indicating, if an animation parameter is present.</param>
    /// <returns>A <see cref="WebpVp8X"/> or a default instance.</returns>
    public static WebpVp8X WriteTrunksBeforeData(
        Stream stream,
        uint width,
        uint height,
        ExifProfile? exifProfile,
        XmpProfile? xmpProfile,
        IccProfile? iccProfile,
        bool hasAlpha,
        bool hasAnimation)
    {
        // Write file size later
        RiffHelper.BeginWriteRiff(stream, WebpConstants.WebpFormTypeFourCc);

        // Write VP8X, header if necessary.
        WebpVp8X vp8x = default;
        bool isVp8X = exifProfile != null || xmpProfile != null || iccProfile != null || hasAlpha || hasAnimation;
        if (isVp8X)
        {
            vp8x = WriteVp8XHeader(stream, exifProfile, xmpProfile, iccProfile, width, height, hasAlpha, hasAnimation);

            if (iccProfile != null)
            {
                RiffHelper.WriteChunk(stream, (uint)WebpChunkType.Iccp, iccProfile.ToByteArray());
            }
        }

        return vp8x;
    }

    /// <summary>
    /// Writes the encoded image to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public abstract void WriteEncodedImageToStream(Stream stream);

    /// <summary>
    /// Write the trunks after data trunk.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="vp8x">The VP8X chunk.</param>
    /// <param name="updateVp8x">Whether to update the chunk.</param>
    /// <param name="initialPosition">The initial position of the stream before encoding.</param>
    /// <param name="exifProfile">The EXIF profile.</param>
    /// <param name="xmpProfile">The XMP profile.</param>
    public static void WriteTrunksAfterData(
        Stream stream,
        in WebpVp8X vp8x,
        bool updateVp8x,
        long initialPosition,
        ExifProfile? exifProfile,
        XmpProfile? xmpProfile)
    {
        if (exifProfile != null)
        {
            RiffHelper.WriteChunk(stream, (uint)WebpChunkType.Exif, exifProfile.ToByteArray());
        }

        if (xmpProfile != null)
        {
            RiffHelper.WriteChunk(stream, (uint)WebpChunkType.Xmp, xmpProfile.Data);
        }

        RiffHelper.EndWriteVp8X(stream, in vp8x, updateVp8x, initialPosition);
    }

    /// <summary>
    /// Writes the animation parameter(<see cref="WebpChunkType.AnimationParameter"/>) to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="background">
    /// The default background color of the canvas in [Blue, Green, Red, Alpha] byte order.
    /// This color MAY be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is 1.
    /// </param>
    /// <param name="loopCount">The number of times to loop the animation. If it is 0, this means infinitely.</param>
    public static void WriteAnimationParameter(Stream stream, Color background, ushort loopCount)
    {
        WebpAnimationParameter chunk = new(background.ToPixel<Bgra32>().PackedValue, loopCount);
        chunk.WriteTo(stream);
    }

    /// <summary>
    /// Writes the alpha chunk to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="dataBytes">The alpha channel data bytes.</param>
    /// <param name="alphaDataIsCompressed">Indicates, if the alpha channel data is compressed.</param>
    public static void WriteAlphaChunk(Stream stream, Span<byte> dataBytes, bool alphaDataIsCompressed)
    {
        long pos = RiffHelper.BeginWriteChunk(stream, (uint)WebpChunkType.Alpha);
        byte flags = 0;
        if (alphaDataIsCompressed)
        {
            // TODO: Filtering and preprocessing
            flags = 1;
        }

        stream.WriteByte(flags);
        stream.Write(dataBytes);
        RiffHelper.EndWriteChunk(stream, pos, 2);
    }

    /// <summary>
    /// Writes a VP8X header to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="exifProfile">An EXIF profile or null, if it does not exist.</param>
    /// <param name="xmpProfile">An XMP profile or null, if it does not exist.</param>
    /// <param name="iccProfile">The color profile.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="hasAlpha">Flag indicating, if a alpha channel is present.</param>
    /// <param name="hasAnimation">Flag indicating, if an animation parameter is present.</param>
    protected static WebpVp8X WriteVp8XHeader(Stream stream, ExifProfile? exifProfile, XmpProfile? xmpProfile, IccProfile? iccProfile, uint width, uint height, bool hasAlpha, bool hasAnimation)
    {
        WebpVp8X chunk = new(hasAnimation, xmpProfile != null, exifProfile != null, hasAlpha, iccProfile != null, width, height);

        chunk.Validate(MaxDimension, MaxCanvasPixels);

        chunk.WriteTo(stream);

        return chunk;
    }
}
