// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
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
internal sealed class GifEncoderCore : IImageEncoderInternals
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
    private GifColorTableMode? colorTableMode;

    /// <summary>
    /// The number of bits requires to store the color palette.
    /// </summary>
    private int bitDepth;

    /// <summary>
    /// The pixel sampling strategy for global quantization.
    /// </summary>
    private readonly IPixelSamplingStrategy pixelSamplingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
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

        ImageMetadata metadata = image.Metadata;
        GifMetadata gifMetadata = metadata.GetGifMetadata();
        this.colorTableMode ??= gifMetadata.ColorTableMode;
        bool useGlobalTable = this.colorTableMode == GifColorTableMode.Global;

        // Quantize the image returning a palette.
        IndexedImageFrame<TPixel>? quantized;

        if (this.quantizer is null)
        {
            // Is this a gif with color information. If so use that, otherwise use octree.
            if (gifMetadata.ColorTableMode == GifColorTableMode.Global && gifMetadata.GlobalColorTable?.Length > 0)
            {
                // We avoid dithering by default to preserve the original colors.
                this.quantizer = new PaletteQuantizer(gifMetadata.GlobalColorTable.Value, new() { Dither = null });
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

        // Get the number of bits.
        this.bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);

        // Write the header.
        WriteHeader(stream);

        // Write the LSD.
        image.Frames.RootFrame.Metadata.TryGetGifMetadata(out GifFrameMetadata? frameMetadata);
        int index = GetTransparentIndex(quantized, frameMetadata);
        if (index == -1)
        {
            index = gifMetadata.BackgroundColor;
        }

        this.WriteLogicalScreenDescriptor(metadata, image.Width, image.Height, index, useGlobalTable, stream);

        if (useGlobalTable)
        {
            this.WriteColorTable(quantized, stream);
        }

        if (!this.skipMetadata)
        {
            // Write the comments.
            this.WriteComments(gifMetadata, stream);

            // Write application extensions.
            XmpProfile? xmpProfile = image.Metadata.XmpProfile ?? image.Frames.RootFrame.Metadata.XmpProfile;
            this.WriteApplicationExtensions(stream, image.Frames.Count, gifMetadata.RepeatCount, xmpProfile);
        }

        this.EncodeFrames(stream, image, quantized, quantized.Palette.ToArray());

        stream.WriteByte(GifConstants.EndIntroducer);
    }

    private void EncodeFrames<TPixel>(
        Stream stream,
        Image<TPixel> image,
        IndexedImageFrame<TPixel> quantized,
        ReadOnlyMemory<TPixel> palette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PaletteQuantizer<TPixel> paletteQuantizer = default;
        bool hasPaletteQuantizer = false;

        // Create a buffer to store de-duplicated pixel indices for encoding.
        // These are used when the color table is global but we must always allocate since we don't know
        // in advance whether the frames will use a local palette.
        Buffer2D<byte> indices = this.memoryAllocator.Allocate2D<byte>(image.Width, image.Height);

        // Store the first frame as a reference for de-duplication comparison.
        IndexedImageFrame<TPixel> previousQuantized = quantized;
        for (int i = 0; i < image.Frames.Count; i++)
        {
            // Gather the metadata for this frame.
            ImageFrame<TPixel> frame = image.Frames[i];
            ImageFrameMetadata metadata = frame.Metadata;
            bool hasMetadata = metadata.TryGetGifMetadata(out GifFrameMetadata? frameMetadata);
            bool useLocal = this.colorTableMode == GifColorTableMode.Local || (hasMetadata && frameMetadata!.ColorTableMode == GifColorTableMode.Local);

            if (!useLocal && !hasPaletteQuantizer && i > 0)
            {
                // The palette quantizer can reuse the same pixel map across multiple frames
                // since the palette is unchanging. This allows a reduction of memory usage across
                // multi frame gifs using a global palette.
                hasPaletteQuantizer = true;
                paletteQuantizer = new(this.configuration, this.quantizer!.Options, palette);
            }

            this.EncodeFrame(stream, frame, i, useLocal, frameMetadata, indices, ref previousQuantized, ref quantized!, ref paletteQuantizer);

            // Clean up for the next run.
            if (quantized != previousQuantized)
            {
                quantized.Dispose();
            }
        }

        previousQuantized.Dispose();
        indices.Dispose();

        if (hasPaletteQuantizer)
        {
            paletteQuantizer.Dispose();
        }
    }

    private void EncodeFrame<TPixel>(
        Stream stream,
        ImageFrame<TPixel> frame,
        int frameIndex,
        bool useLocal,
        GifFrameMetadata? metadata,
        Buffer2D<byte> indices,
        ref IndexedImageFrame<TPixel> previousQuantized,
        ref IndexedImageFrame<TPixel> quantized,
        ref PaletteQuantizer<TPixel> globalPaletteQuantizer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The first frame has already been quantized so we do not need to do so again.
        int transparencyIndex = -1;
        if (frameIndex > 0)
        {
            if (useLocal)
            {
                // Reassign using the current frame and details.
                if (metadata?.LocalColorTable?.Length > 0)
                {
                    // We can use the color data from the decoded metadata here.
                    // We avoid dithering by default to preserve the original colors.
                    PaletteQuantizer localQuantizer = new(metadata.LocalColorTable.Value, new() { Dither = null });
                    using IQuantizer<TPixel> frameQuantizer = localQuantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, localQuantizer.Options);
                    quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds());
                }
                else
                {
                    // We must quantize the frame to generate a local color table.
                    IQuantizer localQuantizer = this.hasQuantizer ? this.quantizer! : KnownQuantizers.Octree;
                    using IQuantizer<TPixel> frameQuantizer = localQuantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, localQuantizer.Options);
                    quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds());
                }
            }
            else
            {
                // Quantize the image using the global palette.
                quantized = globalPaletteQuantizer.QuantizeFrame(frame, frame.Bounds());
                transparencyIndex = GetTransparentIndex(quantized, metadata);

                // De-duplicate pixels comparing to the previous frame.
                // Only global is supported for now as the color palettes as the operation required to compare
                // and offset the index lookups is too expensive for local palettes.
                DeDuplicatePixels(previousQuantized, quantized, indices, transparencyIndex);
            }

            this.bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
        }
        else
        {
            transparencyIndex = GetTransparentIndex(quantized, metadata);
        }

        this.WriteGraphicalControlExtension(metadata, transparencyIndex, stream);
        this.WriteImageDescriptor(frame, useLocal, stream);

        if (useLocal)
        {
            this.WriteColorTable(quantized, stream);
        }

        // Assign the correct buffer to compress.
        // If we are using a local palette or it's the first run then we want to use the quantized frame.
        Buffer2D<byte> buffer = useLocal || frameIndex == 0 ? ((IPixelSource)quantized).PixelBuffer : indices;
        this.WriteImageData(buffer, stream);

        // Swap the buffers.
        (quantized, previousQuantized) = (previousQuantized, quantized);
    }

    private static void DeDuplicatePixels<TPixel>(
        IndexedImageFrame<TPixel> background,
        IndexedImageFrame<TPixel> source,
        Buffer2D<byte> indices,
        int transparencyIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TODO: This should be the background color if not transparent.
        byte replacementIndex = unchecked((byte)transparencyIndex);
        for (int y = 0; y < background.Height; y++)
        {
            ref byte backgroundRowBase = ref MemoryMarshal.GetReference(background.DangerousGetRowSpan(y));
            ref byte sourceRowBase = ref MemoryMarshal.GetReference(source.DangerousGetRowSpan(y));
            ref byte indicesRowBase = ref MemoryMarshal.GetReference(indices.DangerousGetRowSpan(y));

            uint x = 0;
            if (Avx2.IsSupported)
            {
                int remaining = background.Width;
                Vector256<byte> transparentVector = Vector256.Create(replacementIndex);
                while (remaining >= Vector256<byte>.Count)
                {
                    Vector256<byte> b = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref backgroundRowBase, x));
                    Vector256<byte> s = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.Add(ref sourceRowBase, x));
                    Vector256<byte> m = Avx2.CompareEqual(b, s);
                    Vector256<byte> i = Avx2.BlendVariable(s, transparentVector, m);

                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref indicesRowBase, x), i);

                    x += (uint)Vector256<byte>.Count;
                    remaining -= Vector256<byte>.Count;
                }
            }
            else if (Sse2.IsSupported)
            {
                int remaining = background.Width;
                Vector128<byte> transparentVector = Vector128.Create(replacementIndex);
                while (remaining >= Vector128<byte>.Count)
                {
                    Vector128<byte> b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref backgroundRowBase, x));
                    Vector128<byte> s = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sourceRowBase, x));
                    Vector128<byte> m = Sse2.CompareEqual(b, s);
                    Vector128<byte> i = SimdUtils.HwIntrinsics.BlendVariable(s, transparentVector, m);

                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref indicesRowBase, x), i);

                    x += (uint)Vector128<byte>.Count;
                    remaining -= Vector128<byte>.Count;
                }
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                int remaining = background.Width;
                Vector128<byte> transparentVector = Vector128.Create(replacementIndex);
                while (remaining >= Vector128<byte>.Count)
                {
                    Vector128<byte> b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref backgroundRowBase, x));
                    Vector128<byte> s = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref sourceRowBase, x));
                    Vector128<byte> m = AdvSimd.CompareEqual(b, s);
                    Vector128<byte> i = SimdUtils.HwIntrinsics.BlendVariable(s, transparentVector, m);

                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref indicesRowBase, x), i);

                    x += (uint)Vector128<byte>.Count;
                    remaining -= Vector128<byte>.Count;
                }
            }

            for (; x < (uint)background.Width; x++)
            {
                byte b = Unsafe.Add(ref backgroundRowBase, x);
                byte s = Unsafe.Add(ref sourceRowBase, x);
                ref byte i = ref Unsafe.Add(ref indicesRowBase, x);
                i = (b == s) ? replacementIndex : s;
            }
        }
    }

    /// <summary>
    /// Returns the index of the most transparent color in the palette.
    /// </summary>
    /// <param name="quantized">The current quantized frame.</param>
    /// <param name="metadata">The current gif frame metadata.</param>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>
    /// The <see cref="int"/>.
    /// </returns>
    private static int GetTransparentIndex<TPixel>(IndexedImageFrame<TPixel> quantized, GifFrameMetadata? metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Transparent pixels are much more likely to be found at the end of a palette.
        int index = -1;
        ReadOnlySpan<TPixel> paletteSpan = quantized.Palette.Span;

        using IMemoryOwner<Rgba32> rgbaOwner = quantized.Configuration.MemoryAllocator.Allocate<Rgba32>(paletteSpan.Length);
        Span<Rgba32> rgbaSpan = rgbaOwner.GetSpan();
        PixelOperations<TPixel>.Instance.ToRgba32(quantized.Configuration, paletteSpan, rgbaSpan);
        ref Rgba32 rgbaSpanRef = ref MemoryMarshal.GetReference(rgbaSpan);

        for (int i = rgbaSpan.Length - 1; i >= 0; i--)
        {
            if (Unsafe.Add(ref rgbaSpanRef, (uint)i).Equals(default))
            {
                index = i;
            }
        }

        if (metadata?.HasTransparency == true && index == -1)
        {
            index = metadata.TransparencyIndex;
        }

        return index;
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
    /// <param name="stream">The stream to write to.</param>
    private void WriteLogicalScreenDescriptor(
        ImageMetadata metadata,
        int width,
        int height,
        int backgroundIndex,
        bool useGlobalTable,
        Stream stream)
    {
        byte packedValue = GifLogicalScreenDescriptor.GetPackedValue(useGlobalTable, this.bitDepth - 1, false, this.bitDepth - 1);

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
            backgroundColorIndex: unchecked((byte)backgroundIndex),
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
    /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteGraphicalControlExtension(GifFrameMetadata? metadata, int transparencyIndex, Stream stream)
    {
        bool hasTransparency;
        if (metadata is null)
        {
            metadata = new();
            hasTransparency = transparencyIndex > -1;
        }
        else
        {
            hasTransparency = metadata.HasTransparency;
        }

        byte packedValue = GifGraphicControlExtension.GetPackedValue(
            disposalMethod: metadata!.DisposalMethod,
            transparencyFlag: hasTransparency);

        GifGraphicControlExtension extension = new(
            packed: packedValue,
            delayTime: (ushort)metadata.FrameDelay,
            transparencyIndex: hasTransparency ? unchecked((byte)transparencyIndex) : byte.MinValue);

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
        Span<byte> extensionBuffer = stackalloc byte[0];    // workaround compiler limitation
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
    /// Writes the image descriptor to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to be encoded.</param>
    /// <param name="hasColorTable">Whether to use the global color table.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteImageDescriptor<TPixel>(ImageFrame<TPixel> image, bool hasColorTable, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte packedValue = GifImageDescriptor.GetPackedValue(
            localColorTableFlag: hasColorTable,
            interfaceFlag: false,
            sortFlag: false,
            localColorTableSize: this.bitDepth - 1);

        GifImageDescriptor descriptor = new(
            left: 0,
            top: 0,
            width: (ushort)image.Width,
            height: (ushort)image.Height,
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
    /// <param name="stream">The stream to write to.</param>
    private void WriteColorTable<TPixel>(IndexedImageFrame<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The maximum number of colors for the bit depth
        int colorTableLength = ColorNumerics.GetColorCountForBitDepth(this.bitDepth) * Unsafe.SizeOf<Rgb24>();

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
    /// <param name="indices">The <see cref="Buffer2D{Byte}"/> containing indexed pixels.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteImageData(Buffer2D<byte> indices, Stream stream)
    {
        using LzwEncoder encoder = new(this.memoryAllocator, (byte)this.bitDepth);
        encoder.Encode(indices, stream);
    }
}
