// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Implements the GIF encoding protocol.
    /// </summary>
    internal sealed class GifEncoderCore
    {
        /// <summary>
        /// Used for allocating memory during procesing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// A reusable buffer used to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[20];

        /// <summary>
        /// The text encoding used to write comments.
        /// </summary>
        private readonly Encoding textEncoding;

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
        /// Gif specific meta data.
        /// </summary>
        private GifMetaData gifMetaData;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="options">The options for the encoder.</param>
        public GifEncoderCore(MemoryAllocator memoryAllocator, IGifEncoderOptions options)
        {
            this.memoryAllocator = memoryAllocator;
            this.textEncoding = options.TextEncoding ?? GifConstants.DefaultEncoding;
            this.quantizer = options.Quantizer;
            this.colorTableMode = options.ColorTableMode;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            ImageMetaData metaData = image.MetaData;
            this.gifMetaData = metaData.GetFormatMetaData(GifFormat.Instance);
            this.colorTableMode = this.colorTableMode ?? this.gifMetaData.ColorTableMode;
            bool useGlobalTable = this.colorTableMode.Equals(GifColorTableMode.Global);

            // Quantize the image returning a palette.
            QuantizedFrame<TPixel> quantized =
                this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(image.Frames.RootFrame);

            // Get the number of bits.
            this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(quantized.Palette.Length).Clamp(1, 8);

            // Write the header.
            this.WriteHeader(stream);

            // Write the LSD.
            int index = this.GetTransparentIndex(quantized);
            this.WriteLogicalScreenDescriptor(metaData, image.Width, image.Height, index, useGlobalTable, stream);

            if (useGlobalTable)
            {
                this.WriteColorTable(quantized, stream);
            }

            // Write the comments.
            this.WriteComments(metaData, stream);

            // Write application extension to allow additional frames.
            if (image.Frames.Count > 1)
            {
                this.WriteApplicationExtension(stream, this.gifMetaData.RepeatCount);
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
            quantized?.Dispose();
            quantized = null;

            // TODO: Write extension etc
            stream.WriteByte(GifConstants.EndIntroducer);
        }

        private void EncodeGlobal<TPixel>(Image<TPixel> image, QuantizedFrame<TPixel> quantized, int transparencyIndex, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var palleteQuantizer = new PaletteQuantizer(this.quantizer.Diffuser);

            for (int i = 0; i < image.Frames.Count; i++)
            {
                ImageFrame<TPixel> frame = image.Frames[i];
                ImageFrameMetaData metaData = frame.MetaData;
                GifFrameMetaData frameMetaData = metaData.GetFormatMetaData(GifFormat.Instance);
                this.WriteGraphicalControlExtension(frameMetaData, transparencyIndex, stream);
                this.WriteImageDescriptor(frame, false, stream);

                if (i == 0)
                {
                    this.WriteImageData(quantized, stream);
                }
                else
                {
                    using (QuantizedFrame<TPixel> paletteQuantized
                        = palleteQuantizer.CreateFrameQuantizer(() => quantized.Palette).QuantizeFrame(frame))
                    {
                        this.WriteImageData(paletteQuantized, stream);
                    }
                }
            }
        }

        private void EncodeLocal<TPixel>(Image<TPixel> image, QuantizedFrame<TPixel> quantized, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            ImageFrame<TPixel> previousFrame = null;
            GifFrameMetaData previousMeta = null;
            foreach (ImageFrame<TPixel> frame in image.Frames)
            {
                ImageFrameMetaData metaData = frame.MetaData;
                GifFrameMetaData frameMetaData = metaData.GetFormatMetaData(GifFormat.Instance);
                if (quantized is null)
                {
                    // Allow each frame to be encoded at whatever color depth the frame designates if set.
                    if (previousFrame != null
                        && previousMeta.ColorTableLength != frameMetaData.ColorTableLength
                        && frameMetaData.ColorTableLength > 0)
                    {
                        quantized = this.quantizer.CreateFrameQuantizer<TPixel>(frameMetaData.ColorTableLength).QuantizeFrame(frame);
                    }
                    else
                    {
                        quantized = this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(frame);
                    }
                }

                this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(quantized.Palette.Length).Clamp(1, 8);
                this.WriteGraphicalControlExtension(frameMetaData, this.GetTransparentIndex(quantized), stream);
                this.WriteImageDescriptor(frame, true, stream);
                this.WriteColorTable(quantized, stream);
                this.WriteImageData(quantized, stream);

                quantized?.Dispose();
                quantized = null; // So next frame can regenerate it
                previousFrame = frame;
                previousMeta = frameMetaData;
            }
        }

        /// <summary>
        /// Returns the index of the most transparent color in the palette.
        /// </summary>
        /// <param name="quantized">
        /// The quantized.
        /// </param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private int GetTransparentIndex<TPixel>(QuantizedFrame<TPixel> quantized)
            where TPixel : struct, IPixel<TPixel>
        {
            // Transparent pixels are much more likely to be found at the end of a palette
            int index = -1;
            Rgba32 trans = default;

            ref TPixel paletteRef = ref MemoryMarshal.GetReference(quantized.Palette.AsSpan());
            for (int i = quantized.Palette.Length - 1; i >= 0; i--)
            {
                ref TPixel entry = ref Unsafe.Add(ref paletteRef, i);
                entry.ToRgba32(ref trans);
                if (trans.Equals(default))
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
        private void WriteHeader(Stream stream) => stream.Write(GifConstants.MagicNumber, 0, GifConstants.MagicNumber.Length);

        /// <summary>
        /// Writes the logical screen descriptor to the stream.
        /// </summary>
        /// <param name="metaData">The image metadata.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <param name="transparencyIndex">The transparency index to set the default background index to.</param>
        /// <param name="useGlobalTable">Whether to use a global or local color table.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteLogicalScreenDescriptor(
            ImageMetaData metaData,
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

            if (metaData.ResolutionUnits == PixelResolutionUnit.AspectRatio)
            {
                double hr = metaData.HorizontalResolution;
                double vr = metaData.VerticalResolution;
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
        private void WriteComments(ImageMetaData metadata, Stream stream)
        {
            if (!metadata.TryGetProperty(GifConstants.Comments, out ImageProperty property) || string.IsNullOrEmpty(property.Value))
            {
                return;
            }

            byte[] comments = this.textEncoding.GetBytes(property.Value);

            int count = Math.Min(comments.Length, 255);

            this.buffer[0] = GifConstants.ExtensionIntroducer;
            this.buffer[1] = GifConstants.CommentLabel;
            this.buffer[2] = (byte)count;

            stream.Write(this.buffer, 0, 3);
            stream.Write(comments, 0, count);
            stream.WriteByte(GifConstants.Terminator);
        }

        /// <summary>
        /// Writes the graphics control extension to the stream.
        /// </summary>
        /// <param name="metaData">The metadata of the image or frame.</param>
        /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteGraphicalControlExtension(GifFrameMetaData metaData, int transparencyIndex, Stream stream)
        {
            byte packedValue = GifGraphicControlExtension.GetPackedValue(
                disposalMethod: metaData.DisposalMethod,
                transparencyFlag: transparencyIndex > -1);

            var extension = new GifGraphicControlExtension(
                packed: packedValue,
                delayTime: (ushort)metaData.FrameDelay,
                transparencyIndex: unchecked((byte)transparencyIndex));

            this.WriteExtension(extension, stream);
        }

        /// <summary>
        /// Writes the provided extension to the stream.
        /// </summary>
        /// <param name="extension">The extension to write to the stream.</param>
        /// <param name="stream">The stream to write to.</param>
        public void WriteExtension(IGifExtension extension, Stream stream)
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
            where TPixel : struct, IPixel<TPixel>
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
        private void WriteColorTable<TPixel>(QuantizedFrame<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            int pixelCount = image.Palette.Length;

            // The maximium number of colors for the bit depth
            int colorTableLength = ImageMaths.GetColorCountForBitDepth(this.bitDepth) * 3;
            Rgb24 rgb = default;

            using (IManagedByteBuffer colorTable = this.memoryAllocator.AllocateManagedByteBuffer(colorTableLength))
            {
                ref TPixel paletteRef = ref MemoryMarshal.GetReference(image.Palette.AsSpan());
                ref Rgb24 rgb24Ref = ref Unsafe.As<byte, Rgb24>(ref MemoryMarshal.GetReference(colorTable.GetSpan()));
                for (int i = 0; i < pixelCount; i++)
                {
                    ref TPixel entry = ref Unsafe.Add(ref paletteRef, i);
                    entry.ToRgb24(ref rgb);
                    Unsafe.Add(ref rgb24Ref, i) = rgb;
                }

                // Write the palette to the stream
                stream.Write(colorTable.Array, 0, colorTableLength);
            }
        }

        /// <summary>
        /// Writes the image pixel data to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="QuantizedFrame{TPixel}"/> containing indexed pixels.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteImageData<TPixel>(QuantizedFrame<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var encoder = new LzwEncoder(this.memoryAllocator, (byte)this.bitDepth))
            {
                encoder.Encode(image.GetPixelSpan(), stream);
            }
        }
    }
}