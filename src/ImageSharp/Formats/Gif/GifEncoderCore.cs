// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Quantization;

// TODO: This is causing more GC collections than I'm happy with.
// This is likely due to the number of short writes to the stream we are doing.
// We should investigate reducing them since we know the length of the byte array we require for multiple parts.
namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Performs the gif encoding operation.
    /// </summary>
    internal sealed class GifEncoderCore
    {
        private readonly MemoryManager memoryManager;

        /// <summary>
        /// The temp buffer used to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// Gets the TextEncoding
        /// </summary>
        private readonly Encoding textEncoding;

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private readonly bool ignoreMetadata;

        /// <summary>
        /// The number of bits requires to store the image palette.
        /// </summary>
        private int bitDepth;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="options">The options for the encoder.</param>
        public GifEncoderCore(MemoryManager memoryManager, IGifEncoderOptions options)
        {
            this.memoryManager = memoryManager;
            this.textEncoding = options.TextEncoding ?? GifConstants.DefaultEncoding;
            this.quantizer = options.Quantizer;
            this.ignoreMetadata = options.IgnoreMetadata;
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

            // Quantize the image returning a palette.
            QuantizedFrame<TPixel> quantized = this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(image.Frames.RootFrame);

            // Get the number of bits.
            this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(quantized.Palette.Length).Clamp(1, 8);

            int index = this.GetTransparentIndex(quantized);

            // Write the header.
            this.WriteHeader(stream);

            // Write the LSD. We'll use local color tables for now.
            this.WriteLogicalScreenDescriptor(image, stream, index);

            // Write the first frame.
            this.WriteComments(image, stream);

            // Write additional frames.
            if (image.Frames.Count > 1)
            {
                this.WriteApplicationExtension(stream, image.MetaData.RepeatCount);
            }

            foreach (ImageFrame<TPixel> frame in image.Frames)
            {
                if (quantized == null)
                {
                    quantized = this.quantizer.CreateFrameQuantizer<TPixel>().QuantizeFrame(frame);
                }

                this.WriteGraphicalControlExtension(frame.MetaData, stream, this.GetTransparentIndex(quantized));
                this.WriteImageDescriptor(frame, stream);
                this.WriteColorTable(quantized, stream);
                this.WriteImageData(quantized, stream);

                quantized = null; // So next frame can regenerate it
            }

            // TODO: Write extension etc
            stream.WriteByte(GifConstants.EndIntroducer);
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
            var trans = default(Rgba32);
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
        private void WriteHeader(Stream stream)
        {
            stream.Write(GifConstants.MagicNumber, 0, GifConstants.MagicNumber.Length);
        }

        /// <summary>
        /// Writes the logical screen descriptor to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="transparencyIndex">The transparency index to set the default background index to.</param>
        private void WriteLogicalScreenDescriptor<TPixel>(Image<TPixel> image, Stream stream, int transparencyIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            byte packedValue = GifLogicalScreenDescriptor.GetPackedValue(false, this.bitDepth - 1, false, this.bitDepth - 1);

            var descriptor = new GifLogicalScreenDescriptor(
                width: (ushort)image.Width,
                height: (ushort)image.Height,
                packed: packedValue,
                backgroundColorIndex: unchecked((byte)transparencyIndex));

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
                this.buffer[0] = GifConstants.ExtensionIntroducer;
                this.buffer[1] = GifConstants.ApplicationExtensionLabel;
                this.buffer[2] = GifConstants.ApplicationBlockSize;

                stream.Write(this.buffer, 0, 3);

                stream.Write(GifConstants.ApplicationIdentificationBytes, 0, 11); // NETSCAPE2.0

                this.buffer[0] = 3; // Application block length
                this.buffer[1] = 1; // Data sub-block index (always 1)

                // 0 means loop indefinitely. Count is set as play n + 1 times.
                repeatCount = (ushort)Math.Max(0, repeatCount - 1);

                BinaryPrimitives.WriteUInt16LittleEndian(this.buffer.AsSpan(2, 2), repeatCount); // Repeat count for images.

                this.buffer[4] = GifConstants.Terminator; // Terminator

                stream.Write(this.buffer, 0, 5);
            }
        }

        /// <summary>
        /// Writes the image comments to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to be encoded.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteComments<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.ignoreMetadata)
            {
                return;
            }

            ImageProperty property = image.MetaData.Properties.FirstOrDefault(p => p.Name == GifConstants.Comments);
            if (property == null || string.IsNullOrEmpty(property.Value))
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
        /// <param name="stream">The stream to write to.</param>
        /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
        private void WriteGraphicalControlExtension(ImageFrameMetaData metaData, Stream stream, int transparencyIndex)
        {
            byte packedValue = GifGraphicsControlExtension.GetPackedValue(
                disposalMethod: metaData.DisposalMethod,
                transparencyFlag: transparencyIndex > -1);

            var extension = new GifGraphicsControlExtension(
                packed: packedValue,
                transparencyIndex: unchecked((byte)transparencyIndex),
                delayTime: (ushort)metaData.FrameDelay);

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

            stream.Write(this.buffer, 0, 8);
        }

        /// <summary>
        /// Writes the image descriptor to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to be encoded.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteImageDescriptor<TPixel>(ImageFrame<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            byte packedValue = GifImageDescriptor.GetPackedValue(
                localColorTableFlag: true,
                interfaceFlag: false,
                sortFlag: false,
                localColorTableSize: this.bitDepth); // Note: we subtract 1 from the colorTableSize writing

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
            // Grab the palette and write it to the stream.
            int pixelCount = image.Palette.Length;

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, this.bitDepth) * 3;
            var rgb = default(Rgb24);
            using (IManagedByteBuffer colorTable = this.memoryManager.AllocateManagedByteBuffer(colorTableLength))
            {
                ref TPixel paletteRef = ref MemoryMarshal.GetReference(image.Palette.AsSpan());
                ref Rgb24 rgb24Ref = ref Unsafe.As<byte, Rgb24>(ref MemoryMarshal.GetReference(colorTable.Span));

                for (int i = 0; i < pixelCount; i++)
                {
                    ref TPixel entry = ref Unsafe.Add(ref paletteRef, i);
                    entry.ToRgb24(ref rgb);
                    Unsafe.Add(ref rgb24Ref, i) = rgb;
                }

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
            using (var encoder = new LzwEncoder(this.memoryManager, image.Pixels, (byte)this.bitDepth))
            {
                encoder.Encode(stream);
            }
        }
    }
}