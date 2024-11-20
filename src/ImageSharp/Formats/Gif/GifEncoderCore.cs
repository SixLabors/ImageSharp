// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Implements the GIF encoding protocol.
/// </summary>
internal sealed class GifEncoderCore
{
    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// Configuration bound to the encoding operation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Whether to skip metadata during encode.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// The quantizer used to generate the color palette.
    /// </summary>
    private IQuantizer? quantizer;

    /// <summary>
    /// Whether the quantizer was supplied via options.
    /// </summary>
    private readonly bool hasQuantizer;

    /// <summary>
    /// The color table mode: Global or local.
    /// </summary>
    private FrameColorTableMode? colorTableMode;

    /// <summary>
    /// The pixel sampling strategy for global quantization.
    /// </summary>
    private readonly IPixelSamplingStrategy pixelSamplingStrategy;

    /// <summary>
    /// The default background color of the canvas when animating.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when a frame disposal mode is <see cref="FrameDisposalMode.RestoreToBackground"/>.
    /// </summary>
    private readonly Color? backgroundColor;

    /// <summary>
    /// The number of times any animation is repeated.
    /// </summary>
    private readonly ushort? repeatCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="encoder">The encoder with options.</param>
    public GifEncoderCore(Configuration configuration, GifEncoder encoder)
    {
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
        this.skipMetadata = encoder.SkipMetadata;
        this.quantizer = encoder.Quantizer;
        this.hasQuantizer = encoder.Quantizer is not null;
        this.colorTableMode = encoder.ColorTableMode;
        this.pixelSamplingStrategy = encoder.PixelSamplingStrategy;
        this.backgroundColor = encoder.BackgroundColor;
        this.repeatCount = encoder.RepeatCount;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        GifMetadata gifMetadata = image.Metadata.CloneGifMetadata();
        this.colorTableMode ??= gifMetadata.ColorTableMode;
        bool useGlobalTable = this.colorTableMode == FrameColorTableMode.Global;

        // Quantize the first image frame returning a palette.
        IndexedImageFrame<TPixel>? quantized = null;

        // Work out if there is an explicit transparent index set for the frame. We use that to ensure the
        // correct value is set for the background index when quantizing.
        GifFrameMetadata frameMetadata = GetGifFrameMetadata(image.Frames.RootFrame, -1);

        if (this.quantizer is null)
        {
            // Is this a gif with color information. If so use that, otherwise use octree.
            if (gifMetadata.ColorTableMode == FrameColorTableMode.Global && gifMetadata.GlobalColorTable?.Length > 0)
            {
                // We avoid dithering by default to preserve the original colors.
                int transparencyIndex = GetTransparentIndex(quantized, frameMetadata);
                if (transparencyIndex >= 0 || gifMetadata.GlobalColorTable.Value.Length < 256)
                {
                    this.quantizer = new PaletteQuantizer(gifMetadata.GlobalColorTable.Value, new() { Dither = null }, transparencyIndex);
                }
                else
                {
                    this.quantizer = KnownQuantizers.Octree;
                }
            }
            else
            {
                this.quantizer = KnownQuantizers.Octree;
            }
        }

        using (IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration))
        {
            if (useGlobalTable)
            {
                frameQuantizer.BuildPalette(this.pixelSamplingStrategy, image);
                quantized = frameQuantizer.QuantizeFrame(image.Frames.RootFrame, image.Bounds);
            }
            else
            {
                frameQuantizer.BuildPalette(this.pixelSamplingStrategy, image.Frames.RootFrame);
                quantized = frameQuantizer.QuantizeFrame(image.Frames.RootFrame, image.Bounds);
            }
        }

        // Write the header.
        WriteHeader(stream);

        // Write the LSD.
        int derivedTransparencyIndex = GetTransparentIndex(quantized, null);
        if (derivedTransparencyIndex >= 0)
        {
            frameMetadata.HasTransparency = true;
            frameMetadata.TransparencyIndex = ClampIndex(derivedTransparencyIndex);
        }

        if (!TryGetBackgroundIndex(quantized, this.backgroundColor, out byte backgroundIndex))
        {
            backgroundIndex = derivedTransparencyIndex >= 0
               ? frameMetadata.TransparencyIndex
               : gifMetadata.BackgroundColorIndex;
        }

        // Get the number of bits.
        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
        this.WriteLogicalScreenDescriptor(image.Metadata, image.Width, image.Height, backgroundIndex, useGlobalTable, bitDepth, stream);

        if (useGlobalTable)
        {
            this.WriteColorTable(quantized, bitDepth, stream);
        }

        if (!this.skipMetadata)
        {
            // Write the comments.
            this.WriteComments(gifMetadata, stream);

            // Write application extensions.
            XmpProfile? xmpProfile = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            this.WriteApplicationExtensions(stream, image.Frames.Count, this.repeatCount ?? gifMetadata.RepeatCount, xmpProfile);
        }

        this.EncodeFirstFrame(stream, frameMetadata, quantized);

        // Capture the global palette for reuse on subsequent frames and cleanup the quantized frame.
        TPixel[] globalPalette = image.Frames.Count == 1 ? [] : quantized.Palette.ToArray();

        this.EncodeAdditionalFrames(
            stream,
            image,
            globalPalette,
            derivedTransparencyIndex,
            frameMetadata.DisposalMode,
            cancellationToken);

        stream.WriteByte(GifConstants.EndIntroducer);

        quantized?.Dispose();
    }

    private static GifFrameMetadata GetGifFrameMetadata<TPixel>(ImageFrame<TPixel> frame, int transparencyIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        GifFrameMetadata metadata = frame.Metadata.CloneGifMetadata();
        if (metadata.ColorTableMode == FrameColorTableMode.Global && transparencyIndex > -1)
        {
            metadata.HasTransparency = true;
            metadata.TransparencyIndex = ClampIndex(transparencyIndex);
        }

        return metadata;
    }

    private void EncodeAdditionalFrames<TPixel>(
        Stream stream,
        Image<TPixel> image,
        ReadOnlyMemory<TPixel> globalPalette,
        int globalTransparencyIndex,
        FrameDisposalMode previousDisposalMode,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image.Frames.Count == 1)
        {
            return;
        }

        PaletteQuantizer<TPixel> paletteQuantizer = default;
        bool hasPaletteQuantizer = false;

        // Store the first frame as a reference for de-duplication comparison.
        ImageFrame<TPixel> previousFrame = image.Frames.RootFrame;

        // This frame is reused to store de-duplicated pixel buffers.
        using ImageFrame<TPixel> encodingFrame = new(previousFrame.Configuration, previousFrame.Size);

        for (int i = 1; i < image.Frames.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (hasPaletteQuantizer)
                {
                    paletteQuantizer.Dispose();
                }

                return;
            }

            // Gather the metadata for this frame.
            ImageFrame<TPixel> currentFrame = image.Frames[i];
            ImageFrame<TPixel>? nextFrame = i < image.Frames.Count - 1 ? image.Frames[i + 1] : null;
            GifFrameMetadata gifMetadata = GetGifFrameMetadata(currentFrame, globalTransparencyIndex);
            bool useLocal = this.colorTableMode == FrameColorTableMode.Local || (gifMetadata.ColorTableMode == FrameColorTableMode.Local);

            if (!useLocal && !hasPaletteQuantizer && i > 0)
            {
                // The palette quantizer can reuse the same global pixel map across multiple frames since the palette is unchanging.
                // This allows a reduction of memory usage across multi-frame gifs using a global palette
                // and also allows use to reuse the cache from previous runs.
                int transparencyIndex = gifMetadata.HasTransparency ? gifMetadata.TransparencyIndex : -1;
                paletteQuantizer = new(this.configuration, this.quantizer!.Options, globalPalette, transparencyIndex);
                hasPaletteQuantizer = true;
            }

            this.EncodeAdditionalFrame(
                stream,
                previousFrame,
                currentFrame,
                nextFrame,
                encodingFrame,
                useLocal,
                gifMetadata,
                paletteQuantizer,
                previousDisposalMode);

            previousFrame = currentFrame;
            previousDisposalMode = gifMetadata.DisposalMode;
        }

        if (hasPaletteQuantizer)
        {
            paletteQuantizer.Dispose();
        }
    }

    private void EncodeFirstFrame<TPixel>(
        Stream stream,
        GifFrameMetadata metadata,
        IndexedImageFrame<TPixel> quantized)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.WriteGraphicalControlExtension(metadata, stream);

        Buffer2D<byte> indices = ((IPixelSource)quantized).PixelBuffer;
        Rectangle interest = indices.FullRectangle();
        bool useLocal = this.colorTableMode == FrameColorTableMode.Local || (metadata.ColorTableMode == FrameColorTableMode.Local);
        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);

        this.WriteImageDescriptor(interest, useLocal, bitDepth, stream);

        if (useLocal)
        {
            this.WriteColorTable(quantized, bitDepth, stream);
        }

        this.WriteImageData(indices, stream, quantized.Palette.Length, metadata.TransparencyIndex);
    }

    private void EncodeAdditionalFrame<TPixel>(
        Stream stream,
        ImageFrame<TPixel> previousFrame,
        ImageFrame<TPixel> currentFrame,
        ImageFrame<TPixel>? nextFrame,
        ImageFrame<TPixel> encodingFrame,
        bool useLocal,
        GifFrameMetadata metadata,
        PaletteQuantizer<TPixel> globalPaletteQuantizer,
        FrameDisposalMode previousDisposalMode)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Capture any explicit transparency index from the metadata.
        // We use it to determine the value to use to replace duplicate pixels.
        int transparencyIndex = metadata.HasTransparency ? metadata.TransparencyIndex : -1;

        ImageFrame<TPixel>? previous = previousDisposalMode == FrameDisposalMode.RestoreToBackground ? null : previousFrame;

        Color background = metadata.DisposalMode == FrameDisposalMode.RestoreToBackground
            ? this.backgroundColor ?? Color.Transparent
            : Color.Transparent;

        // Deduplicate and quantize the frame capturing only required parts.
        (bool difference, Rectangle bounds) =
            AnimationUtilities.DeDuplicatePixels(
                this.configuration,
                previous,
                currentFrame,
                nextFrame,
                encodingFrame,
                background,
                true);

        using IndexedImageFrame<TPixel> quantized = this.QuantizeAdditionalFrameAndUpdateMetadata(
                encodingFrame,
                bounds,
                metadata,
                useLocal,
                globalPaletteQuantizer,
                difference,
                transparencyIndex);

        this.WriteGraphicalControlExtension(metadata, stream);

        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
        this.WriteImageDescriptor(bounds, useLocal, bitDepth, stream);

        if (useLocal)
        {
            this.WriteColorTable(quantized, bitDepth, stream);
        }

        Buffer2D<byte> indices = ((IPixelSource)quantized).PixelBuffer;
        this.WriteImageData(indices, stream, quantized.Palette.Length, metadata.TransparencyIndex);
    }

    private IndexedImageFrame<TPixel> QuantizeAdditionalFrameAndUpdateMetadata<TPixel>(
        ImageFrame<TPixel> encodingFrame,
        Rectangle bounds,
        GifFrameMetadata metadata,
        bool useLocal,
        PaletteQuantizer<TPixel> globalPaletteQuantizer,
        bool hasDuplicates,
        int transparencyIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IndexedImageFrame<TPixel> quantized;
        if (useLocal)
        {
            // Reassign using the current frame and details.
            if (metadata.LocalColorTable?.Length > 0)
            {
                // We can use the color data from the decoded metadata here.
                // We avoid dithering by default to preserve the original colors.
                ReadOnlyMemory<Color> palette = metadata.LocalColorTable.Value;
                if (hasDuplicates && !metadata.HasTransparency)
                {
                    // Duplicates were captured but the metadata does not have transparency.
                    metadata.HasTransparency = true;

                    if (palette.Length < 256)
                    {
                        // We can use the existing palette and set the transparent index as the length.
                        // decoders will ignore this value.
                        transparencyIndex = palette.Length;
                        metadata.TransparencyIndex = ClampIndex(transparencyIndex);

                        PaletteQuantizer quantizer = new(palette, new() { Dither = null }, transparencyIndex);
                        using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                        quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, bounds);
                    }
                    else
                    {
                        // We must quantize the frame to generate a local color table.
                        IQuantizer quantizer = this.hasQuantizer ? this.quantizer! : KnownQuantizers.Octree;
                        using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                        quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, bounds);

                        // The transparency index derived by the quantizer will differ from the index
                        // within the metadata. We need to update the metadata to reflect this.
                        int derivedTransparencyIndex = GetTransparentIndex(quantized, null);
                        metadata.TransparencyIndex = ClampIndex(derivedTransparencyIndex);
                    }
                }
                else
                {
                    // Just use the local palette.
                    PaletteQuantizer quantizer = new(palette, new() { Dither = null }, transparencyIndex);
                    using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                    quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, bounds);
                }
            }
            else
            {
                // We must quantize the frame to generate a local color table.
                IQuantizer quantizer = this.hasQuantizer ? this.quantizer! : KnownQuantizers.Octree;
                using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, bounds);

                // The transparency index derived by the quantizer might differ from the index
                // within the metadata. We need to update the metadata to reflect this.
                int derivedTransparencyIndex = GetTransparentIndex(quantized, null);
                if (derivedTransparencyIndex < 0)
                {
                    // If no index is found set to the palette length, this trick allows us to fake transparency without an explicit index.
                    derivedTransparencyIndex = quantized.Palette.Length;
                }

                metadata.TransparencyIndex = ClampIndex(derivedTransparencyIndex);

                if (hasDuplicates)
                {
                    metadata.HasTransparency = true;
                }
            }
        }
        else
        {
            // Quantize the image using the global palette.
            // Individual frames, though using the shared palette, can use a different transparent index to represent transparency.

            // A difference was captured but the metadata does not have transparency.
            if (hasDuplicates && !metadata.HasTransparency)
            {
                metadata.HasTransparency = true;
                transparencyIndex = globalPaletteQuantizer.Palette.Length;
                metadata.TransparencyIndex = ClampIndex(transparencyIndex);
            }

            globalPaletteQuantizer.SetTransparentIndex(transparencyIndex);
            quantized = globalPaletteQuantizer.QuantizeFrame(encodingFrame, bounds);
        }

        return quantized;
    }

    private static byte ClampIndex(int value) => (byte)Numerics.Clamp(value, byte.MinValue, byte.MaxValue);

    /// <summary>
    /// Returns the index of the transparent color in the palette.
    /// </summary>
    /// <param name="quantized">The current quantized frame.</param>
    /// <param name="metadata">The current gif frame metadata.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>The <see cref="int"/>.</returns>
    private static int GetTransparentIndex<TPixel>(IndexedImageFrame<TPixel>? quantized, GifFrameMetadata? metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (metadata?.HasTransparency == true)
        {
            return metadata.TransparencyIndex;
        }

        int index = -1;
        if (quantized != null)
        {
            TPixel transparentPixel = TPixel.FromScaledVector4(Vector4.Zero);
            ReadOnlySpan<TPixel> palette = quantized.Palette.Span;

            // Transparent pixels are much more likely to be found at the end of a palette.
            for (int i = palette.Length - 1; i >= 0; i--)
            {
                if (palette[i].Equals(transparentPixel))
                {
                    index = i;
                }
            }
        }

        return index;
    }

    /// <summary>
    /// Returns the index of the background color in the palette.
    /// </summary>
    /// <param name="quantized">The current quantized frame.</param>
    /// <param name="background">The background color to match.</param>
    /// <param name="index">The index in the palette of the background color.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>The <see cref="bool"/>.</returns>
    private static bool TryGetBackgroundIndex<TPixel>(
        IndexedImageFrame<TPixel>? quantized,
        Color? background,
        out byte index)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int match = -1;
        if (quantized != null && background.HasValue)
        {
            TPixel backgroundPixel = background.Value.ToPixel<TPixel>();
            ReadOnlySpan<TPixel> palette = quantized.Palette.Span;
            for (int i = 0; i < palette.Length; i++)
            {
                if (!backgroundPixel.Equals(palette[i]))
                {
                    continue;
                }

                match = i;
                break;
            }
        }

        if (match >= 0)
        {
            index = (byte)Numerics.Clamp(match, 0, 255);
            return true;
        }

        index = 0;
        return false;
    }

    /// <summary>
    /// Writes the file header signature and version to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(Stream stream) => stream.Write(GifConstants.MagicNumber);

    /// <summary>
    /// Writes the logical screen descriptor to the stream.
    /// </summary>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="backgroundIndex">The index to set the default background index to.</param>
    /// <param name="useGlobalTable">Whether to use a global or local color table.</param>
    /// <param name="bitDepth">The bit depth of the color palette.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteLogicalScreenDescriptor(
        ImageMetadata metadata,
        int width,
        int height,
        byte backgroundIndex,
        bool useGlobalTable,
        int bitDepth,
        Stream stream)
    {
        byte packedValue = GifLogicalScreenDescriptor.GetPackedValue(useGlobalTable, bitDepth - 1, false, bitDepth - 1);

        // The Pixel Aspect Ratio is defined to be the quotient of the pixel's
        // width over its height.  The value range in this field allows
        // specification of the widest pixel of 4:1 to the tallest pixel of
        // 1:4 in increments of 1/64th.
        //
        // Values :        0 -   No aspect ratio information is given.
        //            1..255 -   Value used in the computation.
        //
        // Aspect Ratio = (Pixel Aspect Ratio + 15) / 64
        byte ratio = 0;

        if (metadata.ResolutionUnits == PixelResolutionUnit.AspectRatio)
        {
            double hr = metadata.HorizontalResolution;
            double vr = metadata.VerticalResolution;
            if (hr != vr)
            {
                if (hr > vr)
                {
                    ratio = (byte)((hr * 64) - 15);
                }
                else
                {
                    ratio = (byte)((1 / vr * 64) - 15);
                }
            }
        }

        GifLogicalScreenDescriptor descriptor = new(
            width: (ushort)width,
            height: (ushort)height,
            packed: packedValue,
            backgroundColorIndex: backgroundIndex,
            ratio);

        Span<byte> buffer = stackalloc byte[20];
        descriptor.WriteTo(buffer);

        stream.Write(buffer, 0, GifLogicalScreenDescriptor.Size);
    }

    /// <summary>
    /// Writes the application extension to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="frameCount">The frame count fo this image.</param>
    /// <param name="repeatCount">The animated image repeat count.</param>
    /// <param name="xmpProfile">The XMP metadata profile. Null if profile is not to be written.</param>
    private void WriteApplicationExtensions(Stream stream, int frameCount, ushort repeatCount, XmpProfile? xmpProfile)
    {
        // Application Extension: Loop repeat count.
        if (frameCount > 1 && repeatCount != 1)
        {
            GifNetscapeLoopingApplicationExtension loopingExtension = new(repeatCount);
            this.WriteExtension(loopingExtension, stream);
        }

        // Application Extension: XMP Profile.
        if (xmpProfile != null)
        {
            GifXmpApplicationExtension xmpExtension = new(xmpProfile.Data!);
            this.WriteExtension(xmpExtension, stream);
        }
    }

    /// <summary>
    /// Writes the image comments to the stream.
    /// </summary>
    /// <param name="metadata">The metadata to be extract the comment data.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteComments(GifMetadata metadata, Stream stream)
    {
        if (metadata.Comments.Count == 0)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[2];

        for (int i = 0; i < metadata.Comments.Count; i++)
        {
            string comment = metadata.Comments[i];
            buffer[1] = GifConstants.CommentLabel;
            buffer[0] = GifConstants.ExtensionIntroducer;
            stream.Write(buffer);

            // Comment will be stored in chunks of 255 bytes, if it exceeds this size.
            ReadOnlySpan<char> commentSpan = comment.AsSpan();
            int idx = 0;
            for (;
                idx <= comment.Length - GifConstants.MaxCommentSubBlockLength;
                idx += GifConstants.MaxCommentSubBlockLength)
            {
                WriteCommentSubBlock(stream, commentSpan, idx, GifConstants.MaxCommentSubBlockLength);
            }

            // Write the length bytes, if any, to another sub block.
            if (idx < comment.Length)
            {
                int remaining = comment.Length - idx;
                WriteCommentSubBlock(stream, commentSpan, idx, remaining);
            }

            stream.WriteByte(GifConstants.Terminator);
        }
    }

    /// <summary>
    /// Writes a comment sub-block to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="commentSpan">Comment as a Span.</param>
    /// <param name="idx">Current start index.</param>
    /// <param name="length">The length of the string to write. Should not exceed 255 bytes.</param>
    private static void WriteCommentSubBlock(Stream stream, ReadOnlySpan<char> commentSpan, int idx, int length)
    {
        string subComment = commentSpan.Slice(idx, length).ToString();
        byte[] subCommentBytes = GifConstants.Encoding.GetBytes(subComment);
        stream.WriteByte((byte)length);
        stream.Write(subCommentBytes, 0, length);
    }

    /// <summary>
    /// Writes the optional graphics control extension to the stream.
    /// </summary>
    /// <param name="metadata">The metadata of the image or frame.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteGraphicalControlExtension(GifFrameMetadata metadata, Stream stream)
    {
        bool hasTransparency = metadata.HasTransparency;

        byte packedValue = GifGraphicControlExtension.GetPackedValue(
            disposalMode: metadata.DisposalMode,
            transparencyFlag: hasTransparency);

        GifGraphicControlExtension extension = new(
            packed: packedValue,
            delayTime: (ushort)metadata.FrameDelay,
            transparencyIndex: hasTransparency ? metadata.TransparencyIndex : byte.MinValue);

        this.WriteExtension(extension, stream);
    }

    /// <summary>
    /// Writes the provided extension to the stream.
    /// </summary>
    /// <typeparam name="TGifExtension">The type of gif extension.</typeparam>
    /// <param name="extension">The extension to write to the stream.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteExtension<TGifExtension>(TGifExtension extension, Stream stream)
        where TGifExtension : struct, IGifExtension
    {
        int extensionSize = extension.ContentLength;

        if (extensionSize == 0)
        {
            return;
        }

        IMemoryOwner<byte>? owner = null;
        scoped Span<byte> extensionBuffer = []; // workaround compiler limitation
        if (extensionSize > 128)
        {
            owner = this.memoryAllocator.Allocate<byte>(extensionSize + 3);
            extensionBuffer = owner.GetSpan();
        }
        else
        {
            extensionBuffer = stackalloc byte[extensionSize + 3];
        }

        extensionBuffer[0] = GifConstants.ExtensionIntroducer;
        extensionBuffer[1] = extension.Label;

        extension.WriteTo(extensionBuffer[2..]);

        extensionBuffer[extensionSize + 2] = GifConstants.Terminator;

        stream.Write(extensionBuffer, 0, extensionSize + 3);
        owner?.Dispose();
    }

    /// <summary>
    /// Writes the image frame descriptor to the stream.
    /// </summary>
    /// <param name="rectangle">The frame location and size.</param>
    /// <param name="hasColorTable">Whether to use the global color table.</param>
    /// <param name="bitDepth">The bit depth of the color palette.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteImageDescriptor(Rectangle rectangle, bool hasColorTable, int bitDepth, Stream stream)
    {
        byte packedValue = GifImageDescriptor.GetPackedValue(
            localColorTableFlag: hasColorTable,
            interfaceFlag: false,
            sortFlag: false,
            localColorTableSize: bitDepth - 1);

        GifImageDescriptor descriptor = new(
            left: (ushort)rectangle.X,
            top: (ushort)rectangle.Y,
            width: (ushort)rectangle.Width,
            height: (ushort)rectangle.Height,
            packed: packedValue);

        Span<byte> buffer = stackalloc byte[20];
        descriptor.WriteTo(buffer);

        stream.Write(buffer, 0, GifImageDescriptor.Size);
    }

    /// <summary>
    /// Writes the color table to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode.</param>
    /// <param name="bitDepth">The bit depth of the color palette.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteColorTable<TPixel>(IndexedImageFrame<TPixel> image, int bitDepth, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The maximum number of colors for the bit depth
        int colorTableLength = ColorNumerics.GetColorCountForBitDepth(bitDepth) * Unsafe.SizeOf<Rgb24>();

        using IMemoryOwner<byte> colorTable = this.memoryAllocator.Allocate<byte>(colorTableLength, AllocationOptions.Clean);
        Span<byte> colorTableSpan = colorTable.GetSpan();

        PixelOperations<TPixel>.Instance.ToRgb24Bytes(
            this.configuration,
            image.Palette.Span,
            colorTableSpan,
            image.Palette.Length);

        stream.Write(colorTableSpan);
    }

    /// <summary>
    /// Writes the image pixel data to the stream.
    /// </summary>
    /// <param name="indices">The <see cref="Buffer2DRegion{Byte}"/> containing indexed pixels.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="paletteLength">The length of the frame color palette.</param>
    /// <param name="transparencyIndex">The index of the color used to represent transparency.</param>
    private void WriteImageData(Buffer2D<byte> indices, Stream stream, int paletteLength, int transparencyIndex)
    {
        // Pad the bit depth when required for encoding the image data.
        // This is a common trick which allows to use out of range indexes for transparency and avoid allocating a larger color palette
        // as decoders skip indexes that are out of range.
        int padding = transparencyIndex >= paletteLength
            ? 1
            : 0;

        using LzwEncoder encoder = new(this.memoryAllocator, ColorNumerics.GetBitsNeededForColorDepth(paletteLength + padding));
        encoder.Encode(indices, stream);
    }
}
