// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
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
    private ScratchBuffer buffer;   // mutable struct, don't make readonly

    /// <summary>
    /// The global color table.
    /// </summary>
    private IMemoryOwner<byte>? globalColorTable;

    /// <summary>
    /// The current local color table.
    /// </summary>
    private IMemoryOwner<byte>? currentLocalColorTable;

    /// <summary>
    /// Gets the size in bytes of the current local color table.
    /// </summary>
    private int currentLocalColorTableSize;

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
        uint frameCount = 0;
        Image<TPixel>? image = null;
        ImageFrame<TPixel>? previousFrame = null;
        GifDisposalMethod? previousDisposalMethod = null;
        bool globalColorTableUsed = false;
        Color backgroundColor = Color.Transparent;

        try
        {
            this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

            // Loop though the respective gif parts and read the data.
            int nextFlag = stream.ReadByte();
            while (nextFlag != GifConstants.Terminator)
            {
                if (nextFlag == GifConstants.ImageLabel)
                {
                    if (previousFrame != null && ++frameCount == this.maxFrames)
                    {
                        break;
                    }

                    globalColorTableUsed |= this.ReadFrame(stream, ref image, ref previousFrame, ref previousDisposalMethod, ref backgroundColor);

                    // Reset per-frame state.
                    this.imageDescriptor = default;
                    this.graphicsControlExtension = default;
                }
                else if (nextFlag == GifConstants.ExtensionIntroducer)
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
                        case GifConstants.PlainTextLabel:
                            SkipBlock(stream); // Not supported by any known decoder.
                            break;
                    }
                }
                else if (nextFlag == GifConstants.EndIntroducer)
                {
                    break;
                }

                nextFlag = stream.ReadByte();
                if (nextFlag == -1)
                {
                    break;
                }
            }

            // We cannot always trust the global GIF palette has actually been used.
            // https://github.com/SixLabors/ImageSharp/issues/2866
            if (!globalColorTableUsed)
            {
                this.gifMetadata.ColorTableMode = GifColorTableMode.Local;
            }
        }
        finally
        {
            this.globalColorTable?.Dispose();
            this.currentLocalColorTable?.Dispose();
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
        uint frameCount = 0;
        ImageFrameMetadata? previousFrame = null;
        List<ImageFrameMetadata> framesMetadata = new();
        try
        {
            this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

            // Loop though the respective gif parts and read the data.
            int nextFlag = stream.ReadByte();
            while (nextFlag != GifConstants.Terminator)
            {
                if (nextFlag == GifConstants.ImageLabel)
                {
                    if (previousFrame != null && ++frameCount == this.maxFrames)
                    {
                        break;
                    }

                    this.ReadFrameMetadata(stream, framesMetadata, ref previousFrame);

                    // Reset per-frame state.
                    this.imageDescriptor = default;
                    this.graphicsControlExtension = default;
                }
                else if (nextFlag == GifConstants.ExtensionIntroducer)
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
                        case GifConstants.PlainTextLabel:
                            SkipBlock(stream); // Not supported by any known decoder.
                            break;
                    }
                }
                else if (nextFlag == GifConstants.EndIntroducer)
                {
                    break;
                }

                nextFlag = stream.ReadByte();
                if (nextFlag == -1)
                {
                    break;
                }
            }
        }
        finally
        {
            this.globalColorTable?.Dispose();
            this.currentLocalColorTable?.Dispose();
        }

        if (this.logicalScreenDescriptor.Width == 0 && this.logicalScreenDescriptor.Height == 0)
        {
            GifThrowHelper.ThrowNoHeader();
        }

        return new ImageInfo(
            new PixelTypeInfo(this.logicalScreenDescriptor.BitsPerPixel),
            new(this.logicalScreenDescriptor.Width, this.logicalScreenDescriptor.Height),
            this.metadata,
            framesMetadata);
    }

    /// <summary>
    /// Reads the graphic control extension.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadGraphicalControlExtension(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer.Span, 0, 6);
        if (bytesRead != 6)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Not enough data to read the graphic control extension");
        }

        this.graphicsControlExtension = GifGraphicControlExtension.Parse(this.buffer.Span);
    }

    /// <summary>
    /// Reads the image descriptor.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadImageDescriptor(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer.Span, 0, 9);
        if (bytesRead != 9)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Not enough data to read the image descriptor");
        }

        this.imageDescriptor = GifImageDescriptor.Parse(this.buffer.Span);
        if (this.imageDescriptor.Height == 0 || this.imageDescriptor.Width == 0)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Width or height should not be 0");
        }

        this.Dimensions = new(this.imageDescriptor.Width, this.imageDescriptor.Height);
    }

    /// <summary>
    /// Reads the logical screen descriptor.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadLogicalScreenDescriptor(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer.Span, 0, 7);
        if (bytesRead != 7)
        {
            GifThrowHelper.ThrowInvalidImageContentException("Not enough data to read the logical screen descriptor");
        }

        this.logicalScreenDescriptor = GifLogicalScreenDescriptor.Parse(this.buffer.Span);
    }

    /// <summary>
    /// Reads the application extension block parsing any animation or XMP information
    /// if present.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    private void ReadApplicationExtension(BufferedReadStream stream)
    {
        int appLength = stream.ReadByte();

        // If the length is 11 then it's a valid extension and most likely
        // a NETSCAPE, XMP or ANIMEXTS extension. We want the loop count from this.
        long position = stream.Position;
        if (appLength == GifConstants.ApplicationBlockSize)
        {
            stream.Read(this.buffer.Span, 0, GifConstants.ApplicationBlockSize);
            bool isXmp = this.buffer.Span.StartsWith(GifConstants.XmpApplicationIdentificationBytes);
            if (isXmp && !this.skipMetadata)
            {
                GifXmpApplicationExtension extension = GifXmpApplicationExtension.Read(stream, this.memoryAllocator);
                if (extension.Data.Length > 0)
                {
                    this.metadata!.XmpProfile = new XmpProfile(extension.Data);
                }
                else
                {
                    // Reset the stream position and continue.
                    stream.Position = position;
                    SkipBlock(stream, appLength);
                }

                return;
            }

            int subBlockSize = stream.ReadByte();

            // TODO: There's also a NETSCAPE buffer extension.
            // http://www.vurdalakov.net/misc/gif/netscape-buffering-application-extension
            if (subBlockSize == GifConstants.NetscapeLoopingSubBlockSize)
            {
                stream.Read(this.buffer.Span, 0, GifConstants.NetscapeLoopingSubBlockSize);
                this.gifMetadata!.RepeatCount = GifNetscapeLoopingApplicationExtension.Parse(this.buffer.Span[1..]).RepeatCount;
                stream.Skip(1); // Skip the terminator.
                return;
            }

            // Could be something else not supported yet.
            // Skip the subblock and terminator.
            SkipBlock(stream, subBlockSize);

            return;
        }

        SkipBlock(stream, appLength); // Not supported by any known decoder.
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
            if (length > GifConstants.MaxCommentSubBlockLength)
            {
                GifThrowHelper.ThrowInvalidImageContentException($"Gif comment length '{length}' exceeds max '{GifConstants.MaxCommentSubBlockLength}' of a comment data block");
            }

            if (length == -1)
            {
                GifThrowHelper.ThrowInvalidImageContentException("Unexpected end of stream while reading gif comment");
            }

            if (this.skipMetadata)
            {
                stream.Seek(length, SeekOrigin.Current);
                continue;
            }

            using IMemoryOwner<byte> commentsBuffer = this.memoryAllocator.Allocate<byte>(length);
            Span<byte> commentsSpan = commentsBuffer.GetSpan();

            stream.Read(commentsSpan);
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
    /// <param name="previousDisposalMethod">The previous disposal method.</param>
    /// <param name="backgroundColor">The background color.</param>
    private bool ReadFrame<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref ImageFrame<TPixel>? previousFrame,
        ref GifDisposalMethod? previousDisposalMethod,
        ref Color backgroundColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.ReadImageDescriptor(stream);

        // Determine the color table for this frame. If there is a local one, use it otherwise use the global color table.
        bool hasLocalColorTable = this.imageDescriptor.LocalColorTableFlag;

        if (hasLocalColorTable)
        {
            // Read and store the local color table. We allocate the maximum possible size and slice to match.
            int length = this.currentLocalColorTableSize = this.imageDescriptor.LocalColorTableSize * 3;
            this.currentLocalColorTable ??= this.configuration.MemoryAllocator.Allocate<byte>(768, AllocationOptions.Clean);
            stream.Read(this.currentLocalColorTable.GetSpan()[..length]);
        }

        Span<byte> rawColorTable = default;
        if (hasLocalColorTable)
        {
            rawColorTable = this.currentLocalColorTable!.GetSpan()[..this.currentLocalColorTableSize];
        }
        else if (this.globalColorTable != null)
        {
            rawColorTable = this.globalColorTable.GetSpan();
        }

        ReadOnlySpan<Rgb24> colorTable = MemoryMarshal.Cast<byte, Rgb24>(rawColorTable);

        // First frame
        if (image is null)
        {
            if (this.backgroundColorIndex < colorTable.Length)
            {
                backgroundColor = colorTable[this.backgroundColorIndex];
            }
            else
            {
                backgroundColor = Color.Transparent;
            }

            if (this.graphicsControlExtension.TransparencyFlag)
            {
                backgroundColor = backgroundColor.WithAlpha(0);
            }
        }

        this.ReadFrameColors(stream, ref image, ref previousFrame, ref previousDisposalMethod, colorTable, this.imageDescriptor, backgroundColor.ToPixel<TPixel>());

        // Update from newly decoded frame.
        if (this.graphicsControlExtension.DisposalMethod != GifDisposalMethod.RestoreToPrevious)
        {
            if (this.backgroundColorIndex < colorTable.Length)
            {
                backgroundColor = colorTable[this.backgroundColorIndex];
            }
            else
            {
                backgroundColor = Color.Transparent;
            }

            // TODO: I don't understand why this is always set to alpha of zero.
            // This should be dependent on the transparency flag of the graphics
            // control extension. ImageMagick does the same.
            // if (this.graphicsControlExtension.TransparencyFlag)
            {
                backgroundColor = backgroundColor.WithAlpha(0);
            }
        }

        // Skip any remaining blocks
        SkipBlock(stream);

        return !hasLocalColorTable;
    }

    /// <summary>
    /// Reads the frames colors, mapping indices to colors.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="previousDisposalMethod">The previous disposal method.</param>
    /// <param name="colorTable">The color table containing the available colors.</param>
    /// <param name="descriptor">The <see cref="GifImageDescriptor"/></param>
    /// <param name="backgroundPixel">The background color pixel.</param>
    private void ReadFrameColors<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref ImageFrame<TPixel>? previousFrame,
        ref GifDisposalMethod? previousDisposalMethod,
        ReadOnlySpan<Rgb24> colorTable,
        in GifImageDescriptor descriptor,
        TPixel backgroundPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int imageWidth = this.logicalScreenDescriptor.Width;
        int imageHeight = this.logicalScreenDescriptor.Height;
        bool transFlag = this.graphicsControlExtension.TransparencyFlag;
        GifDisposalMethod disposalMethod = this.graphicsControlExtension.DisposalMethod;
        ImageFrame<TPixel> currentFrame;
        ImageFrame<TPixel>? restoreFrame = null;

        if (previousFrame is null && previousDisposalMethod is null)
        {
            image = transFlag
                ? new Image<TPixel>(this.configuration, imageWidth, imageHeight, this.metadata)
                : new Image<TPixel>(this.configuration, imageWidth, imageHeight, backgroundPixel, this.metadata);

            this.SetFrameMetadata(image.Frames.RootFrame.Metadata);
            currentFrame = image.Frames.RootFrame;
        }
        else
        {
            if (previousFrame != null)
            {
                currentFrame = image!.Frames.AddFrame(previousFrame);
            }
            else
            {
                currentFrame = image!.Frames.CreateFrame(backgroundPixel);
            }

            this.SetFrameMetadata(currentFrame.Metadata);

            if (this.graphicsControlExtension.DisposalMethod == GifDisposalMethod.RestoreToPrevious)
            {
                restoreFrame = previousFrame;
            }

            if (previousDisposalMethod == GifDisposalMethod.RestoreToBackground)
            {
                this.RestoreToBackground(currentFrame, backgroundPixel, transFlag);
            }
        }

        if (this.graphicsControlExtension.DisposalMethod == GifDisposalMethod.RestoreToPrevious)
        {
            previousFrame = restoreFrame;
        }
        else
        {
            previousFrame = currentFrame;
        }

        previousDisposalMethod = disposalMethod;

        if (disposalMethod == GifDisposalMethod.RestoreToBackground)
        {
            this.restoreArea = Rectangle.Intersect(image.Bounds, new(descriptor.Left, descriptor.Top, descriptor.Width, descriptor.Height));
        }

        if (colorTable.Length == 0)
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
        ref byte indicesRowRef = ref MemoryMarshal.GetReference(indicesRow);

        int minCodeSize = stream.ReadByte();
        if (LzwDecoder.IsValidMinCodeSize(minCodeSize))
        {
            using LzwDecoder lzwDecoder = new(this.configuration.MemoryAllocator, stream, minCodeSize);

            for (int y = descriptorTop; y < descriptorBottom && y < imageHeight; y++)
            {
                // Check if this image is interlaced.
                int writeY; // the target y offset to write to
                if (descriptor.InterlaceFlag)
                {
                    // If so then we read lines at predetermined offsets.
                    // When an entire image height worth of offset lines has been read we consider this a pass.
                    // With each pass the number of offset lines changes and the starting line changes.
                    if (interlaceY >= descriptor.Height)
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

                    writeY = Math.Min(interlaceY + descriptor.Top, image.Height);
                    interlaceY += interlaceIncrement;
                }
                else
                {
                    writeY = y;
                }

                lzwDecoder.DecodePixelRow(indicesRow);
                ref TPixel rowRef = ref MemoryMarshal.GetReference(currentFrame.PixelBuffer.DangerousGetRowSpan(writeY));

                if (!transFlag)
                {
                    // #403 The left + width value can be larger than the image width
                    for (int x = descriptorLeft; x < descriptorRight && x < imageWidth; x++)
                    {
                        int index = Numerics.Clamp(Unsafe.Add(ref indicesRowRef, (uint)(x - descriptorLeft)), 0, colorTableMaxIdx);
                        ref TPixel pixel = ref Unsafe.Add(ref rowRef, (uint)x);
                        Rgb24 rgb = colorTable[index];
                        pixel.FromRgb24(rgb);
                    }
                }
                else
                {
                    for (int x = descriptorLeft; x < descriptorRight && x < imageWidth; x++)
                    {
                        int index = Unsafe.Add(ref indicesRowRef, (uint)(x - descriptorLeft));

                        // Treat any out of bounds values as transparent.
                        if (index > colorTableMaxIdx || index == transIndex)
                        {
                            continue;
                        }

                        ref TPixel pixel = ref Unsafe.Add(ref rowRef, (uint)x);
                        Rgb24 rgb = colorTable[index];
                        pixel.FromRgb24(rgb);
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
    /// <param name="previousFrame">The previous frame metadata.</param>
    private void ReadFrameMetadata(BufferedReadStream stream, List<ImageFrameMetadata> frameMetadata, ref ImageFrameMetadata? previousFrame)
    {
        this.ReadImageDescriptor(stream);

        // Skip the color table for this frame if local.
        if (this.imageDescriptor.LocalColorTableFlag)
        {
            // Read and store the local color table. We allocate the maximum possible size and slice to match.
            int length = this.currentLocalColorTableSize = this.imageDescriptor.LocalColorTableSize * 3;
            this.currentLocalColorTable ??= this.configuration.MemoryAllocator.Allocate<byte>(768, AllocationOptions.Clean);
            stream.Read(this.currentLocalColorTable.GetSpan()[..length]);
        }
        else
        {
            this.currentLocalColorTable = null;
            this.currentLocalColorTableSize = 0;
        }

        // Skip the frame indices. Pixels length + mincode size.
        // The gif format does not tell us the length of the compressed data beforehand.
        int minCodeSize = stream.ReadByte();
        if (LzwDecoder.IsValidMinCodeSize(minCodeSize))
        {
            using LzwDecoder lzwDecoder = new(this.configuration.MemoryAllocator, stream, minCodeSize);
            lzwDecoder.SkipIndices(this.imageDescriptor.Width * this.imageDescriptor.Height);
        }

        ImageFrameMetadata currentFrame = new();
        frameMetadata.Add(currentFrame);
        this.SetFrameMetadata(currentFrame);
        previousFrame = currentFrame;

        // Skip any remaining blocks
        SkipBlock(stream);
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

        Rectangle interest = Rectangle.Intersect(frame.Bounds(), this.restoreArea.Value);
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
            gifMeta.ColorTableMode = GifColorTableMode.Global;
        }

        if (this.imageDescriptor.LocalColorTableFlag
            && this.imageDescriptor.LocalColorTableSize > 0)
        {
            GifFrameMetadata gifMeta = metadata.GetGifMetadata();
            gifMeta.ColorTableMode = GifColorTableMode.Local;

            Color[] colorTable = new Color[this.imageDescriptor.LocalColorTableSize];
            ReadOnlySpan<Rgb24> rgbTable = MemoryMarshal.Cast<byte, Rgb24>(this.currentLocalColorTable!.GetSpan()[..this.currentLocalColorTableSize]);
            for (int i = 0; i < colorTable.Length; i++)
            {
                colorTable[i] = new Color(rgbTable[i]);
            }

            gifMeta.LocalColorTable = colorTable;
        }

        // Graphics control extensions is optional.
        if (this.graphicsControlExtension != default)
        {
            GifFrameMetadata gifMeta = metadata.GetGifMetadata();
            gifMeta.HasTransparency = this.graphicsControlExtension.TransparencyFlag;
            gifMeta.TransparencyIndex = this.graphicsControlExtension.TransparencyIndex;
            gifMeta.FrameDelay = this.graphicsControlExtension.DelayTime;
            gifMeta.DisposalMethod = this.graphicsControlExtension.DisposalMethod;
        }
    }

    /// <summary>
    /// Reads the logical screen descriptor and global color table blocks
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
        ? GifColorTableMode.Global
        : GifColorTableMode.Local;

        if (this.logicalScreenDescriptor.GlobalColorTableFlag)
        {
            int globalColorTableLength = this.logicalScreenDescriptor.GlobalColorTableSize * 3;
            if (globalColorTableLength > 0)
            {
                this.globalColorTable = this.memoryAllocator.Allocate<byte>(globalColorTableLength, AllocationOptions.Clean);

                // Read the global color table data from the stream and preserve it in the gif metadata
                Span<byte> globalColorTableSpan = this.globalColorTable.GetSpan();
                stream.Read(globalColorTableSpan);

                Color[] colorTable = new Color[this.logicalScreenDescriptor.GlobalColorTableSize];
                ReadOnlySpan<Rgb24> rgbTable = MemoryMarshal.Cast<byte, Rgb24>(globalColorTableSpan);
                for (int i = 0; i < colorTable.Length; i++)
                {
                    colorTable[i] = new Color(rgbTable[i]);
                }

                this.gifMetadata.GlobalColorTable = colorTable;
            }
        }

        byte index = this.logicalScreenDescriptor.BackgroundColorIndex;
        this.backgroundColorIndex = index;
        this.gifMetadata.BackgroundColorIndex = index;
    }

    private unsafe struct ScratchBuffer
    {
        private const int Size = 16;
        private fixed byte scratch[Size];

        public Span<byte> Span => MemoryMarshal.CreateSpan(ref this.scratch[0], Size);
    }
}
