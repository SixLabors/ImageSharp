// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Performs the gif decoding operation.
/// </summary>
internal sealed class GifDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// The temp buffer used to reduce allocations.
    /// </summary>
    private InlineArray16<byte> buffer; // mutable struct, don't make readonly

    /// <summary>
    /// The global color table.
    /// </summary>
    private Color[]? globalColorTable;

    /// <summary>
    /// The current local color table.
    /// </summary>
    private Color[]? currentLocalColorTable;

    /// <summary>
    /// Converts encoded RGB palette entries with the image-level source profile.
    /// </summary>
    private ColorProfileConverter? colorProfileConverter;

    /// <summary>
    /// The area to restore.
    /// </summary>
    private Rectangle? restoreArea;

    /// <summary>
    /// The logical screen descriptor.
    /// </summary>
    private GifLogicalScreenDescriptor logicalScreenDescriptor;

    /// <summary>
    /// The graphics control extension.
    /// </summary>
    private GifGraphicControlExtension graphicsControlExtension;

    /// <summary>
    /// The image descriptor.
    /// </summary>
    private GifImageDescriptor imageDescriptor;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The maximum number of frames to decode. Inclusive.
    /// </summary>
    private readonly uint maxFrames;

    /// <summary>
    /// Whether to skip metadata during decode.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// The abstract metadata.
    /// </summary>
    private ImageMetadata? metadata;

    /// <summary>
    /// The gif specific metadata.
    /// </summary>
    private GifMetadata? gifMetadata;

    /// <summary>
    /// The background color index.
    /// </summary>
    private byte backgroundColorIndex;

    /// <summary>
    /// A value that tells if the decoder added a frame. The integrity mode uses it to decide if an image data error throws.
    /// </summary>
    private bool hasDecodedFrame;

    /// <summary>
    /// Initializes a new instance of the <see cref="GifDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public GifDecoderCore(DecoderOptions options)
        : base(options)
    {
        this.configuration = options.Configuration;
        this.skipMetadata = options.SkipMetadata;
        this.maxFrames = options.MaxFrames;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        Image<TPixel>? image = null;
        ImageFrame<TPixel>? previousFrame = null;
        FrameDisposalMode? previousDisposalMode = null;
        bool globalColorTableUsed = false;
        Color backgroundColor = Color.Transparent;

        try
        {
            this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

            while (this.TryReadToImageDescriptor(stream))
            {
                // The decoder counts the frames that it adds. A frame that the decoder skips does not count.
                if (image is not null && image.Frames.Count == this.maxFrames)
                {
                    break;
                }

                // The conversion changes the global color table in place, so it runs one time, before the first frame. When the image does not exist, each
                // check that skips a frame throws. Thus the image exists after the first frame.
                if (image is null)
                {
                    this.ConvertGlobalColorTable<TPixel>();
                }

                globalColorTableUsed |= this.ReadFrame(stream, ref image, ref previousFrame, ref previousDisposalMode, ref backgroundColor);
            }

            // We cannot always trust the global GIF palette has actually been used.
            // https://github.com/SixLabors/ImageSharp/issues/2866
            if (!globalColorTableUsed)
            {
                this.gifMetadata.ColorTableMode = FrameColorTableMode.Local;
            }
        }
        catch
        {
            // The caller owns the image only after decoding succeeds. Release partial frames on failure.
            image?.Dispose();
            throw;
        }

        if (image is null)
        {
            GifThrowHelper.ThrowNoData();
        }

        return image;
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        List<ImageFrameMetadata> framesMetadata = [];
        bool globalColorTableUsed = false;

        this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

        while (this.TryReadToImageDescriptor(stream))
        {
            // Identify counts the frames that it adds. A frame that Identify skips does not count.
            if (framesMetadata.Count == this.maxFrames)
            {
                break;
            }

            if (framesMetadata.Count == 0)
            {
                // Identify has no destination pixel format. RgbaVector keeps the converted colors as unassociated floating-point values.
                this.ConvertGlobalColorTable<RgbaVector>();
            }

            globalColorTableUsed |= this.ReadFrameMetadata(stream, framesMetadata);
        }

        // We cannot always trust the global GIF palette has actually been used.
        // https://github.com/SixLabors/ImageSharp/issues/2866
        if (!globalColorTableUsed)
        {
            this.gifMetadata.ColorTableMode = FrameColorTableMode.Local;
        }

        // Ignoring a malformed ancillary extension must not let identify succeed for a file
        // that never contained any readable image frame data.
        if (framesMetadata.Count == 0)
        {
            GifThrowHelper.ThrowNoData();
        }

        return new ImageInfo(
            new Size(this.logicalScreenDescriptor.Width, this.logicalScreenDescriptor.Height),
            this.metadata,
            framesMetadata);
    }

    /// <summary>
    /// Reads the blocks before the next image descriptor.
    /// </summary>
    /// <remarks>
    /// The method reads the extensions and skips each byte that does not start a block. A zero byte, the trailer or the end of the stream ends the data.
    /// </remarks>
    /// <param name="stream">The <see cref="BufferedReadStream"/> that contains the image data.</param>
    /// <returns>
    /// <see langword="true"/> if the method read an image separator, and the image descriptor follows. Otherwise <see langword="false"/>.
    /// </returns>
    private bool TryReadToImageDescriptor(BufferedReadStream stream)
    {
        // The scope of a graphic control extension is the next graphic rendering block (GIF89a section 12). Thus the descriptor and the extension of the
        // previous frame do not apply to the next frame.
        this.imageDescriptor = default;
        this.graphicsControlExtension = default;

        int nextFlag = stream.ReadByte();
        while (nextFlag != GifConstants.Terminator && nextFlag != GifConstants.EndIntroducer && nextFlag != -1)
        {
            if (nextFlag == GifConstants.ImageLabel)
            {
                return true;
            }

            if (nextFlag == GifConstants.ExtensionIntroducer)
            {
                switch (stream.ReadByte())
                {
                    case GifConstants.GraphicControlLabel:
                        this.ReadGraphicalControlExtension(stream);
                        break;
                    case GifConstants.CommentLabel:
                        this.ReadComments(stream);
                        break;
                    case GifConstants.ApplicationExtensionLabel:
                        this.ReadApplicationExtension(stream);
                        break;
                    default:
                        // The decoder does not use a plain text extension (GIF89a section 25) or an extension with an unknown label. Each extension holds data
                        // sub-blocks, so the decoder skips them through the block terminator (GIF89a sections 12 and 15). At the end of
                        // the stream, the label is -1, and the skip reads nothing.
                        SkipBlock(stream);
                        break;
                }
            }

            nextFlag = stream.ReadByte();
        }

        return false;
    }

    /// <summary>
    /// Reads the graphic control extension.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadGraphicalControlExtension(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 6);
        if (bytesRead != 6)
        {
            // The data ends inside the extension. The extension controls the next frame, so the error is an image data error. If the mode ignores the error,
            // the decoder keeps the frames that it decoded. The stream is at its end, so the decode loop stops.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(this.hasDecodedFrame, "Not enough data to read the graphic control extension");
            return;
        }

        this.graphicsControlExtension = GifGraphicControlExtension.Parse(this.buffer);
    }

    /// <summary>
    /// Reads the image descriptor.
    /// </summary>
    /// <remarks>
    /// A descriptor that the data does not hold, or a later descriptor without pixels, is damaged image data. If the mode ignores the error, the method returns
    /// <see langword="false"/>, and the caller does not add a frame. The decoder keeps the frames that it decoded before the damaged frame.
    /// </remarks>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <returns><see langword="true"/> if the frame has pixels to decode. Otherwise <see langword="false"/>.</returns>
    private bool TryReadImageDescriptor(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 9);
        if (bytesRead != 9)
        {
            // The stream is at its end, so the decode loop stops after this frame.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(this.hasDecodedFrame, "Not enough data to read the image descriptor");
            return false;
        }

        this.imageDescriptor = GifImageDescriptor.Parse(this.buffer);
        if (this.imageDescriptor.Height == 0 || this.imageDescriptor.Width == 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(this.hasDecodedFrame, "Width or height should not be 0");

            // The structure of the frame is complete, so the decoder skips the frame and continues with the next block. The local color table, the LZW minimum
            // code size byte and the data sub-blocks follow the descriptor. See GIF89a sections 20 to 22 (https://www.w3.org/Graphics/GIF/spec-gif89a.txt).
            if (this.imageDescriptor.LocalColorTableFlag)
            {
                stream.Skip(this.imageDescriptor.LocalColorTableSize * 3);
            }

            SkipBlock(stream, 1);
            return false;
        }

        this.Dimensions = new Size(this.imageDescriptor.Width, this.imageDescriptor.Height);
        return true;
    }

    /// <summary>
    /// Reads the logical screen descriptor.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadLogicalScreenDescriptor(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 7);
        if (bytesRead != 7)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Not enough data to read the logical screen descriptor");
        }

        this.logicalScreenDescriptor = GifLogicalScreenDescriptor.Parse(this.buffer);
        if (this.logicalScreenDescriptor.Width == 0 || this.logicalScreenDescriptor.Height == 0)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Width and height must be greater than 0.");
        }
    }

    /// <summary>
    /// Reads the application extension block parsing ICC, animation or XMP information
    /// if present.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadApplicationExtension(BufferedReadStream stream)
    {
        int appLength = stream.ReadByte();
        if (appLength == -1)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif application extension");
            return;
        }

        if (appLength != GifConstants.ApplicationBlockSize)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError($"Gif application extension length '{appLength}' is invalid");
            SkipBlock(stream, appLength);
            return;
        }

        // The identifier and authentication code occupy the 11-byte application header.
        long position = stream.Position;
        int bytesRead = stream.Read(this.buffer, 0, GifConstants.ApplicationBlockSize);
        if (bytesRead != GifConstants.ApplicationBlockSize)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif application extension");
            return;
        }

        bool isXmp = ((ReadOnlySpan<byte>)this.buffer).StartsWith(GifConstants.XmpApplicationIdentificationBytes);
        bool isIcc = ((ReadOnlySpan<byte>)this.buffer).StartsWith(GifConstants.IccApplicationIdentificationBytes);
        if (isIcc)
        {
            if (this.metadata!.IccProfile is null)
            {
                this.ReadIccApplicationExtension(stream);
            }
            else
            {
                SkipBlock(stream);
            }

            return;
        }

        if (isXmp)
        {
            this.ReadXmpApplicationExtension(stream, position, appLength);
            return;
        }

        int subBlockSize = stream.ReadByte();
        if (subBlockSize == -1)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif application extension");
            return;
        }

        // TODO: There's also a NETSCAPE buffer extension.
        // http://www.vurdalakov.net/misc/gif/netscape-buffering-application-extension
        if (subBlockSize == GifConstants.NetscapeLoopingSubBlockSize)
        {
            this.ReadNetscapeApplicationExtensionData(stream);
            return;
        }

        // Could be something else not supported yet.
        // Skip the subblock and terminator.
        SkipBlock(stream, subBlockSize);
    }

    /// <summary>
    /// Reads the complete ICC payload from ordinary GIF application data subblocks.
    /// </summary>
    /// <param name="stream">The stream positioned at the first data subblock.</param>
    private void ReadIccApplicationExtension(BufferedReadStream stream)
    {
        // Keep GIF subblock framing here while the shared reader owns ICC size and validity checks.
        IccReader reader = new();

        int length = stream.ReadByte();
        try
        {
            while (length > 0)
            {
                if (!reader.TryReadSegment(stream, length))
                {
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif ICC profile");
                    return;
                }

                length = stream.ReadByte();
            }

            if (length < 0)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif ICC profile");
                return;
            }

            this.metadata!.IccProfile = reader.ToProfile(this.Options.SegmentIntegrityHandling.IgnoresAncillaryErrors());
        }
        catch (InvalidIccProfileException ex)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Invalid gif ICC profile", ex);

            // The reader rejects an oversized segment before it reads the segment. The segment and the remaining sub-blocks then stay in the stream, so the
            // decoder skips them through the block terminator. If the reader rejects the complete profile, the loop read the block terminator before, and
            // length is 0.
            if (length > 0)
            {
                SkipBlock(stream, length);
            }
        }
    }

    /// <summary>
    /// Reads the GIF XMP application extension.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="applicationPosition">The stream position where the application identifier begins.</param>
    /// <param name="appLength">The application block length.</param>
    private void ReadXmpApplicationExtension(BufferedReadStream stream, long applicationPosition, int appLength)
    {
        if (!this.skipMetadata
            && GifXmpApplicationExtension.TryReadProfile(stream, this.memoryAllocator, this.Options.SegmentIntegrityHandling, out XmpProfile? profile))
        {
            this.metadata!.XmpProfile = profile;
            return;
        }

        // The decoder skips the packet, or the packet is not valid. The XMP packet and its trailer form data sub-blocks. Thus the decoder reads the extension
        // again from the application identifier and skips it through the block terminator.
        stream.Position = applicationPosition;
        SkipBlock(stream, appLength);
    }

    /// <summary>
    /// Reads the GIF NETSCAPE looping application extension data.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadNetscapeApplicationExtensionData(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, GifConstants.NetscapeLoopingSubBlockSize);
        if (bytesRead != GifConstants.NetscapeLoopingSubBlockSize)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif application extension");
            return;
        }

        this.gifMetadata!.RepeatCount = GifNetscapeLoopingApplicationExtension.Parse(this.buffer[1..]).RepeatCount;

        if (stream.ReadByte() == -1)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif application extension");
        }
    }

    /// <summary>
    /// Skips over a block or reads its terminator.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="blockSize">The length of the block to skip.</param>
    private static void SkipBlock(BufferedReadStream stream, int blockSize = 0)
    {
        if (blockSize > 0)
        {
            stream.Skip(blockSize);
        }

        int flag;

        while ((flag = stream.ReadByte()) > 0)
        {
            stream.Skip(flag);
        }
    }

    /// <summary>
    /// Reads the gif comments.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadComments(BufferedReadStream stream)
    {
        int length;

        StringBuilder stringBuilder = new();
        while ((length = stream.ReadByte()) != 0)
        {
            if (length == -1)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif comment");
                return;
            }

            if (this.skipMetadata)
            {
                stream.Seek(length, SeekOrigin.Current);
                continue;
            }

            using IMemoryOwner<byte> commentsBuffer = this.memoryAllocator.Allocate<byte>(length);
            Span<byte> commentsSpan = commentsBuffer.GetSpan();

            int bytesRead = stream.Read(commentsSpan);
            if (bytesRead != length)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("Unexpected end of stream while reading gif comment");
                return;
            }

            string commentPart = GifConstants.Encoding.GetString(commentsSpan);
            stringBuilder.Append(commentPart);
        }

        if (stringBuilder.Length > 0)
        {
            this.gifMetadata!.Comments.Add(stringBuilder.ToString());
        }
    }

    /// <summary>
    /// Reads an individual gif frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="previousDisposalMode">The previous frame disposal mode.</param>
    /// <param name="backgroundColor">The background color.</param>
    /// <returns>
    /// <see langword="true"/> if the decoder does not skip the frame, and the frame uses the global color table. Otherwise <see langword="false"/>.
    /// </returns>
    private bool ReadFrame<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref ImageFrame<TPixel>? previousFrame,
        ref FrameDisposalMode? previousDisposalMode,
        ref Color backgroundColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.TryReadImageDescriptor(stream))
        {
            return false;
        }

        // Determine the color table for this frame. If there is a local one, use it otherwise use the global color table.
        bool hasLocalColorTable = this.imageDescriptor.LocalColorTableFlag;
        Color[]? colorTable = this.globalColorTable;
        if (hasLocalColorTable)
        {
            colorTable = this.ReadLocalColorTable<TPixel>(stream);
            if (colorTable is null)
            {
                return false;
            }

            this.currentLocalColorTable = colorTable;
        }

        ReadOnlySpan<Color> colors = colorTable;

        // First frame
        if (image is null)
        {
            // GIF89a section 18(c)(vii) defines Background Color Index against the global table,
            // even when this frame uses a local table. Without a global table the index is ignored;
            // retain the decoder's transparent fallback when no valid background entry exists.
            if (this.globalColorTable is not null && this.backgroundColorIndex < this.globalColorTable.Length)
            {
                backgroundColor = this.globalColorTable[this.backgroundColorIndex];
            }
            else
            {
                backgroundColor = Color.Transparent;
            }

            // We zero the alpha only when this frame declares transparency so that
            // frames with a transparent index coalesce over a transparent canvas rather than
            // baking the LSD background as a matte. When the flag is not set, this frame will
            // write an opaque color for every addressed pixel; keeping the LSD background
            // opaque here allows ReadFrameColors to show that background in uncovered areas
            // for non-transparent GIFs that rely on it. We still do not prefill the canvas here.
            if (this.graphicsControlExtension.TransparencyFlag)
            {
                backgroundColor = backgroundColor.WithAlpha(0);
            }
        }

        // ReadFrameColors throws for damaged LZW data unless the integrity mode ignores image data errors.
        // In that mode, the frame keeps its decoded rows. The LZW decoder stops at the end of the data and leaves
        // a block terminator in the stream. SkipBlock below then reads the remaining sub-blocks and the terminator.
        // Thus, the next block stays readable.
        this.ReadFrameColors(stream, ref image, ref previousFrame, ref previousDisposalMode, colors, backgroundColor.ToPixel<TPixel>());

        // Update from newly decoded frame.
        FrameDisposalMode disposalMethod = this.graphicsControlExtension.DisposalMethod;
        if (disposalMethod != FrameDisposalMode.RestoreToPrevious)
        {
            // Transparent disposal retains RGB from the active frame table, distinct from the LSD background.
            backgroundColor = this.backgroundColorIndex < colors.Length
                ? colors[this.backgroundColorIndex].WithAlpha(0)
                : Color.Transparent;
        }

        // Skip any remaining blocks
        SkipBlock(stream);

        return !hasLocalColorTable;
    }

    /// <summary>
    /// Configures ICC conversion for GIF palettes and converts the global color table and its background color.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format for converted colors.</typeparam>
    private void ConvertGlobalColorTable<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!IccProfile.TryGetIccProfileForColorConversion(this.metadata!.IccProfile, out IccProfile? sourceProfile))
        {
            return;
        }

        ColorConversionOptions options = new()
        {
            SourceIccProfile = sourceProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
            MemoryAllocator = this.memoryAllocator
        };

        this.colorProfileConverter = ColorProfileConverter.CreateIccForDecoding(options);

        // The global table was read before the ICC extension; metadata and frames share this converted array.
        if (this.colorProfileConverter is not null && this.globalColorTable is not null)
        {
            this.colorProfileConverter.Convert<TPixel>(this.globalColorTable);

            if (this.backgroundColorIndex < this.globalColorTable.Length)
            {
                this.gifMetadata!.BackgroundColor = this.globalColorTable[this.backgroundColorIndex];
            }
        }
    }

    /// <summary>
    /// Reads and converts the current frame's local color table for pixels and frame metadata.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format for converted colors.</typeparam>
    /// <param name="stream">The stream containing the local color table.</param>
    /// <returns>
    /// The local color table, or <see langword="null"/> if the data ends inside the table. The method returns <see langword="null"/> only when the decoder
    /// decoded a frame before this frame and the mode ignores image data errors.
    /// </returns>
    private Color[]? ReadLocalColorTable<TPixel>(BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The descriptor derives the entry count from three packed bits (2..256), so RGB table data is bounded to 6..768 bytes.
        int length = this.imageDescriptor.LocalColorTableSize * 3;

        // GIF bounds a local table to 256 RGB entries, so typed stack storage covers it without a pooled buffer.
        InlineArray256<Rgb24> colorTableStorage = default;
        Span<Rgb24> rgbTable = colorTableStorage[..this.imageDescriptor.LocalColorTableSize];

        if (stream.Read(MemoryMarshal.AsBytes(rgbTable)) != length)
        {
            // The table controls the pixels of the frame, so the error is an image data error. If the mode ignores the error, the decoder keeps the frames that
            // it decoded before this frame. The stream is at its end, so the decode loop stops.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(this.hasDecodedFrame, "Not enough data to read the local color table");
            return null;
        }

        // The same Color array supplies frame metadata and decoded pixel indices.
        Color[] colorTable = new Color[this.imageDescriptor.LocalColorTableSize];
        Color.FromPixel(rgbTable, colorTable);

        if (this.colorProfileConverter is not null)
        {
            this.colorProfileConverter.Convert<TPixel>(colorTable);
        }

        return colorTable;
    }

    /// <summary>
    /// Reads the frames colors, mapping indices to colors.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="previousDisposalMode">The previous frame disposal mode.</param>
    /// <param name="colorTable">The color table containing the available colors.</param>
    /// <param name="backgroundPixel">The background color pixel.</param>
    private void ReadFrameColors<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref ImageFrame<TPixel>? previousFrame,
        ref FrameDisposalMode? previousDisposalMode,
        ReadOnlySpan<Color> colorTable,
        TPixel backgroundPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        GifImageDescriptor descriptor = this.imageDescriptor;
        int imageWidth = this.logicalScreenDescriptor.Width;
        int imageHeight = this.logicalScreenDescriptor.Height;
        bool useTransparency = this.graphicsControlExtension.TransparencyFlag;
        bool useBackground;
        FrameDisposalMode disposalMethod = this.graphicsControlExtension.DisposalMethod;
        ImageFrame<TPixel> currentFrame;
        ImageFrame<TPixel>? restoreFrame = null;

        // GIF89a section 22. The LZW minimum code size follows the color table, also in a frame without a color table. The decoder reads and checks the value
        // before it creates the frame. If the mode ignores a missing or invalid value, the frame keeps its initial pixels. Then the caller skips the data
        // sub-blocks.
        bool hasMinCodeSize = LzwDecoder.TryReadMinCodeSize(stream, this.Options.SegmentIntegrityHandling, out int minCodeSize);

        if (image is null)
        {
            // First frame: prefill with LSD background iff a GCT exists (policy: HonorBackgroundColor).
            useBackground =
                this.logicalScreenDescriptor.GlobalColorTableFlag
                && disposalMethod == FrameDisposalMode.RestoreToBackground;

            image = useBackground
                ? new Image<TPixel>(this.configuration, imageWidth, imageHeight, backgroundPixel, this.metadata)
                : new Image<TPixel>(this.configuration, imageWidth, imageHeight, this.metadata);

            this.hasDecodedFrame = true;
            this.SetFrameMetadata(image.Frames.RootFrame.Metadata);
            currentFrame = image.Frames.RootFrame;
        }
        else
        {
            // Subsequent frames: use LSD background iff previous disposal was RestoreToBackground and a GCT exists.
            useBackground =
                this.logicalScreenDescriptor.GlobalColorTableFlag
                && previousDisposalMode == FrameDisposalMode.RestoreToBackground;

            if (previousFrame != null)
            {
                currentFrame = image.Frames.AddFrame(previousFrame);
            }
            else if (useBackground)
            {
                currentFrame = image.Frames.CreateFrame(backgroundPixel);
            }
            else
            {
                currentFrame = image.Frames.CreateFrame();
            }

            this.SetFrameMetadata(currentFrame.Metadata);

            if (disposalMethod == FrameDisposalMode.RestoreToPrevious)
            {
                restoreFrame = previousFrame;
            }

            if (previousDisposalMode == FrameDisposalMode.RestoreToBackground)
            {
                this.RestoreToBackground(currentFrame, backgroundPixel, !useBackground);
            }
        }

        if (disposalMethod == FrameDisposalMode.RestoreToPrevious)
        {
            previousFrame = restoreFrame;
        }
        else
        {
            previousFrame = currentFrame;
        }

        previousDisposalMode = disposalMethod;

        if (disposalMethod == FrameDisposalMode.RestoreToBackground)
        {
            this.restoreArea = Rectangle.Intersect(image.Bounds, new Rectangle(descriptor.Left, descriptor.Top, descriptor.Width, descriptor.Height));
        }

        // A frame without a color table has no colors for its indices. A frame without a valid LZW minimum code size has no indices. In both cases, the decoder
        // does not decode the indices.
        if (colorTable.Length == 0 || !hasMinCodeSize)
        {
            return;
        }

        int interlacePass = 0; // The interlace pass
        int interlaceIncrement = 8; // The interlacing line increment
        int interlaceY = 0; // The current interlaced line
        int descriptorTop = descriptor.Top;
        int descriptorBottom = descriptorTop + descriptor.Height;
        int descriptorLeft = descriptor.Left;
        int descriptorRight = descriptorLeft + descriptor.Width;
        byte transIndex = this.graphicsControlExtension.TransparencyIndex;
        int colorTableMaxIdx = colorTable.Length - 1;

        // For a properly encoded gif the descriptor dimensions will never exceed the logical screen dimensions.
        // However we have images that exceed this that can be decoded by other libraries. #1530
        using IMemoryOwner<byte> indicesRowOwner = this.memoryAllocator.Allocate<byte>(descriptor.Width);
        Span<byte> indicesRow = indicesRowOwner.Memory.Span;

        // A frame that starts at or beyond the right or bottom edge of the logical screen has nothing to draw.
        // Its image data is then left to the caller, which skips the remaining data sub-blocks.
        if (descriptorTop < imageHeight && descriptorLeft < imageWidth)
        {
            using LzwDecoder lzwDecoder = new(this.configuration.MemoryAllocator, stream, minCodeSize, this.Options.SegmentIntegrityHandling);

            for (int y = descriptorTop; y < descriptorBottom; y++)
            {
                // Check if this image is interlaced.
                int writeY; // the target y offset to write to
                if (descriptor.InterlaceFlag)
                {
                    // If so then we read lines at predetermined offsets.
                    // When an entire image height worth of offset lines has been read we consider this a pass.
                    // With each pass the number of offset lines changes and the starting line changes.
                    // A frame shorter than 5 lines has passes without any lines, so keep advancing until a
                    // pass has a line left. The four passes visit every frame line exactly once and the loop
                    // runs once per frame line, so a pass with a line left always exists.
                    while (interlaceY >= descriptor.Height)
                    {
                        interlacePass++;
                        switch (interlacePass)
                        {
                            case 1:
                                interlaceY = 4;
                                break;
                            case 2:
                                interlaceY = 2;
                                interlaceIncrement = 4;
                                break;
                            case 3:
                                interlaceY = 1;
                                interlaceIncrement = 2;
                                break;
                        }
                    }

                    writeY = interlaceY + descriptorTop;
                    interlaceY += interlaceIncrement;
                }
                else
                {
                    writeY = y;
                }

                // For a properly encoded gif the frame never extends below the logical screen. When it does
                // the lines below the screen are dropped. Lines only move down within a pass, so once a line
                // falls below the screen the rest of the pass is hidden too. A sequential frame is a single
                // pass, so decoding can stop there. Before the last interlace pass, later passes still start
                // at frame line 1, 2 or 4 and can reach the screen again unless it shows only frame line 0.
                // The hidden lines in between must still be decoded and discarded to reach those lines.
                if (writeY >= imageHeight)
                {
                    if (!descriptor.InterlaceFlag || interlacePass == 3 || imageHeight - descriptorTop < 2)
                    {
                        break;
                    }

                    if (!lzwDecoder.SkipIndices(descriptor.Width))
                    {
                        return;
                    }

                    continue;
                }

                if (!lzwDecoder.DecodePixelRow(indicesRow))
                {
                    // The decoder cannot decode more LZW data. The decoded rows stay.
                    return;
                }

                // #403 The left + width value can be larger than the image width
                int maxX = Math.Min(descriptorRight, imageWidth);
                Span<TPixel> row = currentFrame.PixelBuffer.DangerousGetRowSpan(writeY);

                // Take the descriptorLeft..maxX slice of the row, so the loop can be simplified.
                row = row[descriptorLeft..maxX];

                if (!useTransparency)
                {
                    for (int x = 0; x < row.Length; x++)
                    {
                        int index = indicesRow[x];

                        // Treat any out of bounds values as background.
                        if (index > colorTableMaxIdx)
                        {
                            index = Math.Clamp(index, 0, colorTableMaxIdx);
                        }

                        row[x] = colorTable[index].ToPixel<TPixel>();
                    }
                }
                else
                {
                    for (int x = 0; x < row.Length; x++)
                    {
                        int index = indicesRow[x];

                        // Treat any out of bounds values as transparent.
                        // We explicitly set the pixel to transparent rather than alter the inbound
                        // color palette.
                        if (index > colorTableMaxIdx || index == transIndex)
                        {
                            continue;
                        }

                        row[x] = colorTable[index].ToPixel<TPixel>();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reads the frames metadata.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="frameMetadata">The collection of frame metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the decoder does not skip the frame, and the frame uses the global color table. Otherwise <see langword="false"/>.
    /// </returns>
    private bool ReadFrameMetadata(BufferedReadStream stream, List<ImageFrameMetadata> frameMetadata)
    {
        if (!this.TryReadImageDescriptor(stream))
        {
            return false;
        }

        // Retain the frame's local table while skipping its compressed pixel indices.
        if (this.imageDescriptor.LocalColorTableFlag)
        {
            this.currentLocalColorTable = this.ReadLocalColorTable<RgbaVector>(stream);
            if (this.currentLocalColorTable is null)
            {
                return false;
            }
        }
        else
        {
            this.currentLocalColorTable = null;
        }

        // Skip the frame indices. Pixels length + mincode size.
        // The gif format does not tell us the length of the compressed data beforehand.
        // Identify still reads LZW codes. It uses the same image data integrity policy and sub-block skipping as
        // full decoding.
        if (LzwDecoder.TryReadMinCodeSize(stream, this.Options.SegmentIntegrityHandling, out int minCodeSize))
        {
            using LzwDecoder lzwDecoder = new(this.configuration.MemoryAllocator, stream, minCodeSize, this.Options.SegmentIntegrityHandling);
            lzwDecoder.SkipIndices(this.imageDescriptor.Width * this.imageDescriptor.Height);
        }

        ImageFrameMetadata currentFrame = new();
        frameMetadata.Add(currentFrame);
        this.hasDecodedFrame = true;
        this.SetFrameMetadata(currentFrame);

        // Skip any remaining blocks
        SkipBlock(stream);

        return !this.imageDescriptor.LocalColorTableFlag;
    }

    /// <summary>
    /// Restores the current frame area to the background.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The frame.</param>
    /// <param name="background">The background color.</param>
    /// <param name="transparent">Whether the background is transparent.</param>
    private void RestoreToBackground<TPixel>(ImageFrame<TPixel> frame, TPixel background, bool transparent)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.restoreArea is null)
        {
            return;
        }

        Rectangle interest = Rectangle.Intersect(frame.Bounds, this.restoreArea.Value);
        Buffer2DRegion<TPixel> pixelRegion = frame.PixelBuffer.GetRegion(interest);
        if (transparent)
        {
            pixelRegion.Clear();
        }
        else
        {
            pixelRegion.Fill(background);
        }

        this.restoreArea = null;
    }

    /// <summary>
    /// Sets the metadata for the image frame.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFrameMetadata(ImageFrameMetadata metadata)
    {
        // Frames can either use the global table or their own local table.
        if (this.logicalScreenDescriptor.GlobalColorTableFlag
            && this.logicalScreenDescriptor.GlobalColorTableSize > 0)
        {
            GifFrameMetadata gifMeta = metadata.GetGifMetadata();
            gifMeta.ColorTableMode = FrameColorTableMode.Global;
        }

        if (this.imageDescriptor.LocalColorTableFlag
            && this.imageDescriptor.LocalColorTableSize > 0)
        {
            GifFrameMetadata gifMeta = metadata.GetGifMetadata();
            gifMeta.ColorTableMode = FrameColorTableMode.Local;

            gifMeta.LocalColorTable = this.currentLocalColorTable;
        }

        // Graphics control extensions is optional.
        if (this.graphicsControlExtension != default)
        {
            GifFrameMetadata gifMeta = metadata.GetGifMetadata();
            gifMeta.HasTransparency = this.graphicsControlExtension.TransparencyFlag;
            gifMeta.TransparencyIndex = this.graphicsControlExtension.TransparencyIndex;
            gifMeta.FrameDelay = this.graphicsControlExtension.DelayTime;
            gifMeta.DisposalMode = this.graphicsControlExtension.DisposalMethod;
        }
    }

    /// <summary>
    /// Reads the logical screen descriptor and global color table blocks.
    /// </summary>
    /// <param name="stream">The stream containing image data. </param>
    [MemberNotNull(nameof(metadata))]
    [MemberNotNull(nameof(gifMetadata))]
    private void ReadLogicalScreenDescriptorAndGlobalColorTable(BufferedReadStream stream)
    {
        // Skip the identifier
        stream.Skip(6);
        this.ReadLogicalScreenDescriptor(stream);

        ImageMetadata meta = new();

        // The Pixel Aspect Ratio is defined to be the quotient of the pixel's
        // width over its height.  The value range in this field allows
        // specification of the widest pixel of 4:1 to the tallest pixel of
        // 1:4 in increments of 1/64th.
        //
        // Values :        0 -   No aspect ratio information is given.
        //            1..255 -   Value used in the computation.
        //
        // Aspect Ratio = (Pixel Aspect Ratio + 15) / 64
        if (this.logicalScreenDescriptor.PixelAspectRatio > 0)
        {
            meta.ResolutionUnits = PixelResolutionUnit.AspectRatio;
            float ratio = (this.logicalScreenDescriptor.PixelAspectRatio + 15) / 64F;

            if (ratio > 1)
            {
                meta.HorizontalResolution = ratio;
                meta.VerticalResolution = 1;
            }
            else
            {
                meta.VerticalResolution = 1 / ratio;
                meta.HorizontalResolution = 1;
            }
        }

        this.metadata = meta;
        this.gifMetadata = meta.GetGifMetadata();
        this.gifMetadata.ColorTableMode = this.logicalScreenDescriptor.GlobalColorTableFlag
            ? FrameColorTableMode.Global
            : FrameColorTableMode.Local;

        int globalColorTableLength = this.logicalScreenDescriptor.GlobalColorTableFlag
            ? this.logicalScreenDescriptor.GlobalColorTableSize * 3
            : 0;

        if (this.logicalScreenDescriptor.GlobalColorTableFlag)
        {
            if (globalColorTableLength > 0)
            {
                // A GIF palette contains at most 256 RGB entries, so typed stack storage covers the full table.
                InlineArray256<Rgb24> colorTableStorage = default;
                Span<Rgb24> rgbTable = colorTableStorage[..this.logicalScreenDescriptor.GlobalColorTableSize];

                if (stream.Read(MemoryMarshal.AsBytes(rgbTable)) != globalColorTableLength)
                {
                    GifThrowHelper.ThrowInvalidImageContentException("Not enough data to read the global color table");
                }

                // Retain encoded entries until the first frame, after application extensions can supply an ICC profile.
                this.globalColorTable = new Color[this.logicalScreenDescriptor.GlobalColorTableSize];
                Color.FromPixel(rgbTable, this.globalColorTable);

                this.gifMetadata.GlobalColorTable = this.globalColorTable;
            }
        }

        byte index = this.logicalScreenDescriptor.BackgroundColorIndex;
        this.backgroundColorIndex = index;
        ReadOnlyMemory<Color>? globalColorTable = this.gifMetadata.GlobalColorTable;
        if (globalColorTable.HasValue && index < globalColorTable.Value.Length)
        {
            this.gifMetadata.BackgroundColor = globalColorTable.Value.Span[index];
        }
    }
}
