// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Gif
{
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
        /// A reusable buffer used to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[20];

        /// <summary>
        /// The quantizer used to generate the color palette.
        /// </summary>
        private readonly IQuantizer quantizer;

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
        private IPixelSamplingStrategy pixelSamplingStrategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="options">The options for the encoder.</param>
        public GifEncoderCore(Configuration configuration, IGifEncoderOptions options)
        {
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.quantizer = options.Quantizer;
            this.colorTableMode = options.ColorTableMode;
            this.pixelSamplingStrategy = options.GlobalPixelSamplingStrategy;
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
            IndexedImageFrame<TPixel> quantized;

            using (IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration))
            {
                if (useGlobalTable)
                {
                    frameQuantizer.BuildPalette(this.pixelSamplingStrategy, image);
                    quantized = frameQuantizer.QuantizeFrame(image.Frames.RootFrame, image.Bounds());
                }
                else
                {
                    quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds());
                }
            }

            // Get the number of bits.
            this.bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);

            // Write the header.
            this.WriteHeader(stream);

            // Write the LSD.
            int index = this.GetTransparentIndex(quantized);
            this.WriteLogicalScreenDescriptor(metadata, image.Width, image.Height, index, useGlobalTable, stream);

            if (useGlobalTable)
            {
                this.WriteColorTable(quantized, stream);
            }

            // Write the comments.
            this.WriteComments(gifMetadata, stream);

            // Write application extension to allow additional frames.
            if (image.Frames.Count > 1)
            {
                this.WriteApplicationExtension(stream, gifMetadata.RepeatCount);
            }

            if (useGlobalTable)
            {
                this.EncodeGlobal(image, quantized, index, stream);
            }
            else
            {
                this.EncodeLocal(image, quantized, stream);
            }

            // Clean up.
            quantized.Dispose();

            // TODO: Write extension etc
            stream.WriteByte(GifConstants.EndIntroducer);
        }

        private void EncodeGlobal<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, int transparencyIndex, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // The palette quantizer can reuse the same pixel map across multiple frames
            // since the palette is unchanging. This allows a reduction of memory usage across
            // multi frame gifs using a global palette.
            EuclideanPixelMap<TPixel> pixelMap = default;
            bool pixelMapSet = false;
            for (int i = 0; i < image.Frames.Count; i++)
            {
                ImageFrame<TPixel> frame = image.Frames[i];
                ImageFrameMetadata metadata = frame.Metadata;
                GifFrameMetadata frameMetadata = metadata.GetGifMetadata();
                this.WriteGraphicalControlExtension(frameMetadata, transparencyIndex, stream);
                this.WriteImageDescriptor(frame, false, stream);

                if (i == 0)
                {
                    this.WriteImageData(quantized, stream);
                }
                else
                {
                    if (!pixelMapSet)
                    {
                        pixelMapSet = true;
                        pixelMap = new EuclideanPixelMap<TPixel>(this.configuration, quantized.Palette);
                    }

                    using var paletteFrameQuantizer = new PaletteQuantizer<TPixel>(this.configuration, this.quantizer.Options, pixelMap);
                    using IndexedImageFrame<TPixel> paletteQuantized = paletteFrameQuantizer.QuantizeFrame(frame, frame.Bounds());
                    this.WriteImageData(paletteQuantized, stream);
                }
            }
        }

        private void EncodeLocal<TPixel>(Image<TPixel> image, IndexedImageFrame<TPixel> quantized, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ImageFrame<TPixel> previousFrame = null;
            GifFrameMetadata previousMeta = null;
            for (int i = 0; i < image.Frames.Count; i++)
            {
                ImageFrame<TPixel> frame = image.Frames[i];
                ImageFrameMetadata metadata = frame.Metadata;
                GifFrameMetadata frameMetadata = metadata.GetGifMetadata();
                if (quantized is null)
                {
                    // Allow each frame to be encoded at whatever color depth the frame designates if set.
                    if (previousFrame != null && previousMeta.ColorTableLength != frameMetadata.ColorTableLength
                                              && frameMetadata.ColorTableLength > 0)
                    {
                        var options = new QuantizerOptions
                        {
                            Dither = this.quantizer.Options.Dither,
                            DitherScale = this.quantizer.Options.DitherScale,
                            MaxColors = frameMetadata.ColorTableLength
                        };

                        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, options);
                        quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds());
                    }
                    else
                    {
                        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration);
                        quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(frame, frame.Bounds());
                    }
                }

                this.bitDepth = ColorNumerics.GetBitsNeededForColorDepth(quantized.Palette.Length);
                this.WriteGraphicalControlExtension(frameMetadata, this.GetTransparentIndex(quantized), stream);
                this.WriteImageDescriptor(frame, true, stream);
                this.WriteColorTable(quantized, stream);
                this.WriteImageData(quantized, stream);

                quantized.Dispose();
                quantized = null; // So next frame can regenerate it
                previousFrame = frame;
                previousMeta = frameMetadata;
            }
        }

        /// <summary>
        /// Returns the index of the most transparent color in the palette.
        /// </summary>
        /// <param name="quantized">The quantized frame.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private int GetTransparentIndex<TPixel>(IndexedImageFrame<TPixel> quantized)
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
                if (Unsafe.Add(ref rgbaSpanRef, i).Equals(default))
                {
                    index = i;
                }
            }

            return index;
        }

        /// <summary>
        /// Writes the file header signature and version to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteHeader(Stream stream) => stream.Write(GifConstants.MagicNumber);

        /// <summary>
        /// Writes the logical screen descriptor to the stream.
        /// </summary>
        /// <param name="metadata">The image metadata.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="transparencyIndex">The transparency index to set the default background index to.</param>
        /// <param name="useGlobalTable">Whether to use a global or local color table.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteLogicalScreenDescriptor(
            ImageMetadata metadata,
            int width,
            int height,
            int transparencyIndex,
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
                        ratio = (byte)(((1 / vr) * 64) - 15);
                    }
                }
            }

            var descriptor = new GifLogicalScreenDescriptor(
                width: (ushort)width,
                height: (ushort)height,
                packed: packedValue,
                backgroundColorIndex: unchecked((byte)transparencyIndex),
                ratio);

            descriptor.WriteTo(this.buffer);

            stream.Write(this.buffer, 0, GifLogicalScreenDescriptor.Size);
        }

        /// <summary>
        /// Writes the application extension to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="repeatCount">The animated image repeat count.</param>
        private void WriteApplicationExtension(Stream stream, ushort repeatCount)
        {
            // Application Extension Header
            if (repeatCount != 1)
            {
                var loopingExtension = new GifNetscapeLoopingApplicationExtension(repeatCount);
                this.WriteExtension(loopingExtension, stream);
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

            for (var i = 0; i < metadata.Comments.Count; i++)
            {
                string comment = metadata.Comments[i];
                this.buffer[0] = GifConstants.ExtensionIntroducer;
                this.buffer[1] = GifConstants.CommentLabel;
                stream.Write(this.buffer, 0, 2);

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
        /// Writes the graphics control extension to the stream.
        /// </summary>
        /// <param name="metadata">The metadata of the image or frame.</param>
        /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteGraphicalControlExtension(GifFrameMetadata metadata, int transparencyIndex, Stream stream)
        {
            byte packedValue = GifGraphicControlExtension.GetPackedValue(
                disposalMethod: metadata.DisposalMethod,
                transparencyFlag: transparencyIndex > -1);

            var extension = new GifGraphicControlExtension(
                packed: packedValue,
                delayTime: (ushort)metadata.FrameDelay,
                transparencyIndex: unchecked((byte)transparencyIndex));

            this.WriteExtension(extension, stream);
        }

        /// <summary>
        /// Writes the provided extension to the stream.
        /// </summary>
        /// <param name="extension">The extension to write to the stream.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteExtension<TGifExtension>(TGifExtension extension, Stream stream)
            where TGifExtension : struct, IGifExtension
        {
            this.buffer[0] = GifConstants.ExtensionIntroducer;
            this.buffer[1] = extension.Label;

            int extensionSize = extension.WriteTo(this.buffer.AsSpan(2));

            this.buffer[extensionSize + 2] = GifConstants.Terminator;

            stream.Write(this.buffer, 0, extensionSize + 3);
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

            var descriptor = new GifImageDescriptor(
                left: 0,
                top: 0,
                width: (ushort)image.Width,
                height: (ushort)image.Height,
                packed: packedValue);

            descriptor.WriteTo(this.buffer);

            stream.Write(this.buffer, 0, GifImageDescriptor.Size);
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

            using IManagedByteBuffer colorTable = this.memoryAllocator.AllocateManagedByteBuffer(colorTableLength, AllocationOptions.Clean);
            PixelOperations<TPixel>.Instance.ToRgb24Bytes(
                this.configuration,
                image.Palette.Span,
                colorTable.GetSpan(),
                image.Palette.Length);

            stream.Write(colorTable.Array, 0, colorTableLength);
        }

        /// <summary>
        /// Writes the image pixel data to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="IndexedImageFrame{TPixel}"/> containing indexed pixels.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteImageData<TPixel>(IndexedImageFrame<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var encoder = new LzwEncoder(this.memoryAllocator, (byte)this.bitDepth);
            encoder.Encode(((IPixelSource)image).PixelBuffer, stream);
        }
    }
}
