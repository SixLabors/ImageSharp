// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
    /// The pixel sampling strategy for global quantization.
    /// </summary>
    private readonly IPixelSamplingStrategy pixelSamplingStrategy;

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

        // Quantize the first image frame returning a palette.
        IndexedImageFrame<TPixel>? quantized = null;

        // Work out if there is an explicit transparent index set for the frame. We use that to ensure the
        // correct value is set for the background index when quantizing.
        image.Frames.RootFrame.Metadata.TryGetGifMetadata(out GifFrameMetadata? frameMetadata);
        int transparencyIndex = GetTransparentIndex(quantized, frameMetadata);

        if (this.quantizer is null)
        {
            // Is this a gif with color information. If so use that, otherwise use octree.
            if (gifMetadata.ColorTableMode == GifColorTableMode.Global && gifMetadata.GlobalColorTable?.Length > 0)
            {
                // We avoid dithering by default to preserve the original colors.
                this.quantizer = new PaletteQuantizer(gifMetadata.GlobalColorTable.Value, new() { Dither = null }, transparencyIndex);
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
        transparencyIndex = GetTransparentIndex(quantized, frameMetadata);
        byte backgroundIndex = unchecked((byte)transparencyIndex);
        if (transparencyIndex == -1)
        {
            backgroundIndex = gifMetadata.BackgroundColor;
        }

        // Get the number of bits.
        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
        this.WriteLogicalScreenDescriptor(metadata, image.Width, image.Height, backgroundIndex, useGlobalTable, bitDepth, stream);

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
            this.WriteApplicationExtensions(stream, image.Frames.Count, gifMetadata.RepeatCount, xmpProfile);
        }

        this.EncodeFirstFrame(stream, frameMetadata, quantized, transparencyIndex);

        // Capture the global palette for reuse on subsequent frames and cleanup the quantized frame.
        TPixel[] globalPalette = image.Frames.Count == 1 ? Array.Empty<TPixel>() : quantized.Palette.ToArray();

        quantized.Dispose();

        this.EncodeAdditionalFrames(stream, image, globalPalette);

        stream.WriteByte(GifConstants.EndIntroducer);
    }

    private void EncodeAdditionalFrames<TPixel>(
        Stream stream,
        Image<TPixel> image,
        ReadOnlyMemory<TPixel> globalPalette)
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
        // This is more expensive memory-wise than de-duplicating indexed buffer but allows us to deduplicate
        // frames using both local and global palettes.
        using ImageFrame<TPixel> encodingFrame = new(previousFrame.GetConfiguration(), previousFrame.Size());

        for (int i = 1; i < image.Frames.Count; i++)
        {
            // Gather the metadata for this frame.
            ImageFrame<TPixel> currentFrame = image.Frames[i];
            ImageFrameMetadata metadata = currentFrame.Metadata;
            metadata.TryGetGifMetadata(out GifFrameMetadata? gifMetadata);
            bool useLocal = this.colorTableMode == GifColorTableMode.Local || (gifMetadata?.ColorTableMode == GifColorTableMode.Local);

            if (!useLocal && !hasPaletteQuantizer && i > 0)
            {
                // The palette quantizer can reuse the same global pixel map across multiple frames since the palette is unchanging.
                // This allows a reduction of memory usage across multi-frame gifs using a global palette
                // and also allows use to reuse the cache from previous runs.
                int transparencyIndex = gifMetadata?.HasTransparency == true ? gifMetadata.TransparencyIndex : -1;
                paletteQuantizer = new(this.configuration, this.quantizer!.Options, globalPalette, transparencyIndex);
                hasPaletteQuantizer = true;
            }

            this.EncodeAdditionalFrame(
                stream,
                previousFrame,
                currentFrame,
                encodingFrame,
                useLocal,
                gifMetadata,
                paletteQuantizer);

            previousFrame = currentFrame;
        }

        if (hasPaletteQuantizer)
        {
            paletteQuantizer.Dispose();
        }
    }

    private void EncodeFirstFrame<TPixel>(
        Stream stream,
        GifFrameMetadata? metadata,
        IndexedImageFrame<TPixel> quantized,
        int transparencyIndex)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.WriteGraphicalControlExtension(metadata, transparencyIndex, stream);

        Buffer2DRegion<byte> region = ((IPixelSource)quantized).PixelBuffer.GetRegion();
        bool useLocal = this.colorTableMode == GifColorTableMode.Local || (metadata?.ColorTableMode == GifColorTableMode.Local);
        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);

        this.WriteImageDescriptor(region.Rectangle, useLocal, bitDepth, stream);

        if (useLocal)
        {
            this.WriteColorTable(quantized, bitDepth, stream);
        }

        this.WriteImageData(region, stream, quantized.Palette.Length, transparencyIndex);
    }

    private void EncodeAdditionalFrame<TPixel>(
        Stream stream,
        ImageFrame<TPixel> previousFrame,
        ImageFrame<TPixel> currentFrame,
        ImageFrame<TPixel> encodingFrame,
        bool useLocal,
        GifFrameMetadata? metadata,
        PaletteQuantizer<TPixel> globalPaletteQuantizer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Capture any explicit transparency index from the metadata.
        // We use it to determine the value to use to replace duplicate pixels.
        int transparencyIndex = metadata?.HasTransparency == true ? metadata.TransparencyIndex : -1;
        Vector4 replacement = Vector4.Zero;
        if (transparencyIndex >= 0)
        {
            if (useLocal)
            {
                if (metadata?.LocalColorTable?.Length > 0)
                {
                    ReadOnlySpan<Color> palette = metadata.LocalColorTable.Value.Span;
                    if (transparencyIndex < palette.Length)
                    {
                        replacement = palette[transparencyIndex].ToScaledVector4();
                    }
                }
            }
            else
            {
                ReadOnlySpan<TPixel> palette = globalPaletteQuantizer.Palette.Span;
                if (transparencyIndex < palette.Length)
                {
                    replacement = palette[transparencyIndex].ToScaledVector4();
                }
            }
        }

        this.DeDuplicatePixels(previousFrame, currentFrame, encodingFrame, replacement);

        IndexedImageFrame<TPixel> quantized;
        if (useLocal)
        {
            // Reassign using the current frame and details.
            if (metadata?.LocalColorTable?.Length > 0)
            {
                // We can use the color data from the decoded metadata here.
                // We avoid dithering by default to preserve the original colors.
                ReadOnlyMemory<Color> palette = metadata.LocalColorTable.Value;
                PaletteQuantizer quantizer = new(palette, new() { Dither = null }, transparencyIndex);
                using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, encodingFrame.Bounds());
            }
            else
            {
                // We must quantize the frame to generate a local color table.
                IQuantizer quantizer = this.hasQuantizer ? this.quantizer! : KnownQuantizers.Octree;
                using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, quantizer.Options);
                quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(encodingFrame, encodingFrame.Bounds());
            }
        }
        else
        {
            // Quantize the image using the global palette.
            // Individual frames, though using the shared palette, can use a different transparent index to represent transparency.
            globalPaletteQuantizer.SetTransparentIndex(transparencyIndex);
            quantized = globalPaletteQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds());
        }

        // Recalculate the transparency index as depending on the quantizer used could have a new value.
        transparencyIndex = GetTransparentIndex(quantized, metadata);

        // Trim down the buffer to the minimum size required.
        // Buffer2DRegion<byte> region = ((IPixelSource)quantized).PixelBuffer.GetRegion();
        Buffer2DRegion<byte> region = TrimTransparentPixels(((IPixelSource)quantized).PixelBuffer, transparencyIndex);

        this.WriteGraphicalControlExtension(metadata, transparencyIndex, stream);

        int bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
        this.WriteImageDescriptor(region.Rectangle, useLocal, bitDepth, stream);

        if (useLocal)
        {
            this.WriteColorTable(quantized, bitDepth, stream);
        }

        this.WriteImageData(region, stream, quantized.Palette.Length, transparencyIndex);
    }

    private void DeDuplicatePixels<TPixel>(
        ImageFrame<TPixel> backgroundFrame,
        ImageFrame<TPixel> sourceFrame,
        ImageFrame<TPixel> resultFrame,
        Vector4 replacement)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IMemoryOwner<Vector4> buffers = this.memoryAllocator.Allocate<Vector4>(backgroundFrame.Width * 3);
        Span<Vector4> background = buffers.GetSpan()[..backgroundFrame.Width];
        Span<Vector4> source = buffers.GetSpan()[backgroundFrame.Width..];
        Span<Vector4> result = buffers.GetSpan()[(backgroundFrame.Width * 2)..];

        // TODO: This algorithm is greedy and will always replace matching colors, however, theoretically, if the proceeding color
        // is the same, but not replaced, you would actually be better of not replacing it since longer runs compress better.
        // This would require a more complex algorithm.
        for (int y = 0; y < backgroundFrame.Height; y++)
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, backgroundFrame.DangerousGetPixelRowMemory(y).Span, background, PixelConversionModifiers.Scale);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceFrame.DangerousGetPixelRowMemory(y).Span, source, PixelConversionModifiers.Scale);

            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(background));
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector256<float> resultBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(result));

            uint x = 0;
            int remaining = background.Length;
            if (Avx2.IsSupported && remaining >= 2)
            {
                Vector256<float> replacement256 = Vector256.Create(replacement.X, replacement.Y, replacement.Z, replacement.W, replacement.X, replacement.Y, replacement.Z, replacement.W);

                while (remaining >= 2)
                {
                    Vector256<float> b = Unsafe.Add(ref backgroundBase, x);
                    Vector256<float> s = Unsafe.Add(ref sourceBase, x);

                    Vector256<int> m = Avx.CompareEqual(b, s).AsInt32();

                    m = Avx2.HorizontalAdd(m, m);
                    m = Avx2.HorizontalAdd(m, m);
                    m = Avx2.CompareEqual(m, Vector256.Create(-4));

                    Unsafe.Add(ref resultBase, x) = Avx.BlendVariable(s, replacement256, m.AsSingle());

                    x++;
                    remaining -= 2;
                }
            }

            for (int i = remaining; i >= 0; i--)
            {
                x = (uint)i;
                Vector4 b = Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref backgroundBase), x);
                Vector4 s = Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref sourceBase), x);
                ref Vector4 r = ref Unsafe.Add(ref Unsafe.As<Vector256<float>, Vector4>(ref resultBase), x);
                r = (b == s) ? replacement : s;
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, result, resultFrame.DangerousGetPixelRowMemory(y).Span, PixelConversionModifiers.Scale);
        }
    }

    private static Buffer2DRegion<byte> TrimTransparentPixels(Buffer2D<byte> buffer, int transparencyIndex)
    {
        if (transparencyIndex < 0)
        {
            return buffer.GetRegion();
        }

        byte trimmableIndex = unchecked((byte)transparencyIndex);

        int top = int.MinValue;
        int bottom = int.MaxValue;
        int left = int.MaxValue;
        int right = int.MinValue;

        // Run through th buffer in a single pass. Use variables to track the min/max values.
        int minY = -1;
        bool isTransparentRow = true;
        for (int y = 0; y < buffer.Height; y++)
        {
            isTransparentRow = true;
            Span<byte> rowSpan = buffer.DangerousGetRowSpan(y);

            // TODO: It may be possible to optimize this inner loop using SIMD.
            for (int x = 0; x < rowSpan.Length; x++)
            {
                if (rowSpan[x] != trimmableIndex)
                {
                    isTransparentRow = false;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }

            if (!isTransparentRow)
            {
                if (y == 0)
                {
                    // First row is opaque.
                    // Capture to prevent over assignment when a match is found below.
                    top = 0;
                }

                // The minimum top bounds have already been captured.
                // Increment the bottom to include the current opaque row.
                if (minY < 0 && top != 0)
                {
                    // Increment to the first opaque row.
                    top++;
                }

                minY = top;
                bottom = y;
            }
            else
            {
                // We've yet to hit an opaque row. Capture the top position.
                if (minY < 0)
                {
                    top = Math.Max(top, y);
                }

                bottom = Math.Min(bottom, y);
            }
        }

        if (left == int.MaxValue)
        {
            left = 0;
        }

        if (right == int.MinValue)
        {
            right = buffer.Width;
        }

        if (top == bottom || left == right)
        {
            // The entire image is transparent.
            return buffer.GetRegion();
        }

        if (!isTransparentRow)
        {
            // Last row is opaque.
            bottom = buffer.Height;
        }

        return buffer.GetRegion(Rectangle.FromLTRB(left, top, Math.Min(right + 1, buffer.Width), Math.Min(bottom + 1, buffer.Height)));
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
            TPixel transparentPixel = default;
            transparentPixel.FromScaledVector4(Vector4.Zero);
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
    /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
    /// <param name="stream">The stream to write to.</param>
    private void WriteGraphicalControlExtension(GifFrameMetadata? metadata, int transparencyIndex, Stream stream)
    {
        GifFrameMetadata? data = metadata;
        bool hasTransparency;
        if (metadata is null)
        {
            data = new();
            hasTransparency = transparencyIndex >= 0;
        }
        else
        {
            hasTransparency = metadata.HasTransparency;
        }

        byte packedValue = GifGraphicControlExtension.GetPackedValue(
            disposalMethod: data!.DisposalMethod,
            transparencyFlag: hasTransparency);

        GifGraphicControlExtension extension = new(
            packed: packedValue,
            delayTime: (ushort)data.FrameDelay,
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
        Span<byte> extensionBuffer = stackalloc byte[0]; // workaround compiler limitation
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
    private void WriteImageData(Buffer2DRegion<byte> indices, Stream stream, int paletteLength, int transparencyIndex)
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
