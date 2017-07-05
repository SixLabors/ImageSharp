// <copyright file="GifEncoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ImageSharp.PixelFormats;

    using IO;
    using Quantizers;

    /// <summary>
    /// Performs the gif encoding operation.
    /// </summary>
    internal sealed class GifEncoderCore
    {
        /// <summary>
        /// The temp buffer used to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// The number of bits requires to store the image palette.
        /// </summary>
        private int bitDepth;

        /// <summary>
        /// Whether the current image has multiple frames.
        /// </summary>
        private bool hasFrames;

        /// <summary>
        /// Gets the TextEncoding
        /// </summary>
        private Encoding textEncoding;

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        private IQuantizer quantizer;

        /// <summary>
        /// Gets or sets the threshold.
        /// </summary>
        private byte threshold;

        /// <summary>
        /// Gets or sets the size of the color palette to use.
        /// </summary>
        private int paletteSize;

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private bool ignoreMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The options for the encoder.</param>
        public GifEncoderCore(IGifEncoderOptions options)
        {
            this.textEncoding = options.TextEncoding ?? GifConstants.DefaultEncoding;

            this.quantizer = options.Quantizer;
            this.threshold = options.Threshold;
            this.paletteSize = options.PaletteSize;
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

            this.quantizer = this.quantizer ?? new OctreeQuantizer<TPixel>();

            // Do not use IDisposable pattern here as we want to preserve the stream.
            var writer = new EndianBinaryWriter(Endianness.LittleEndian, stream);

            // Ensure that pallete size  can be set but has a fallback.
            int paletteSize = this.paletteSize;
            paletteSize = paletteSize > 0 ? paletteSize.Clamp(1, 256) : 256;

            // Get the number of bits.
            this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(paletteSize);

            this.hasFrames = image.Frames.Any();

            // Dithering when animating gifs is a bad idea as we introduce pixel tearing across frames.
            var ditheredQuantizer = (IQuantizer<TPixel>)this.quantizer;
            ditheredQuantizer.Dither = !this.hasFrames;

            // Quantize the image returning a palette.
            QuantizedImage<TPixel> quantized = ditheredQuantizer.Quantize(image, paletteSize);

            int index = this.GetTransparentIndex(quantized);

            // Write the header.
            this.WriteHeader(writer);

            // Write the LSD. We'll use local color tables for now.
            this.WriteLogicalScreenDescriptor(image, writer, index);

            // Write the first frame.
            this.WriteGraphicalControlExtension(image.MetaData, writer, index);
            this.WriteComments(image, writer);
            this.WriteImageDescriptor(image, writer);
            this.WriteColorTable(quantized, writer);
            this.WriteImageData(quantized, writer);

            // Write additional frames.
            if (this.hasFrames)
            {
                this.WriteApplicationExtension(writer, image.MetaData.RepeatCount, image.Frames.Count);

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 0; i < image.Frames.Count; i++)
                {
                    ImageFrame<TPixel> frame = image.Frames[i];
                    QuantizedImage<TPixel> quantizedFrame = ditheredQuantizer.Quantize(frame, paletteSize);

                    this.WriteGraphicalControlExtension(frame.MetaData, writer, this.GetTransparentIndex(quantizedFrame));
                    this.WriteImageDescriptor(frame, writer);
                    this.WriteColorTable(quantizedFrame, writer);
                    this.WriteImageData(quantizedFrame, writer);
                }
            }

            // TODO: Write extension etc
            writer.Write(GifConstants.EndIntroducer);
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
        private int GetTransparentIndex<TPixel>(QuantizedImage<TPixel> quantized)
            where TPixel : struct, IPixel<TPixel>
        {
            // Transparent pixels are much more likely to be found at the end of a palette
            int index = -1;
            for (int i = quantized.Palette.Length - 1; i >= 0; i--)
            {
                quantized.Palette[i].ToXyzwBytes(this.buffer, 0);

                if (this.buffer[3] > 0)
                {
                    continue;
                }
                else
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Writes the file header signature and version to the stream.
        /// </summary>
        /// <param name="writer">The writer to write to the stream with.</param>
        private void WriteHeader(EndianBinaryWriter writer)
        {
            writer.Write((GifConstants.FileType + GifConstants.FileVersion).ToCharArray());
        }

        /// <summary>
        /// Writes the logical screen descriptor to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="writer">The writer to write to the stream with.</param>
        /// <param name="transparencyIndex">The transparency index to set the default background index to.</param>
        private void WriteLogicalScreenDescriptor<TPixel>(Image<TPixel> image, EndianBinaryWriter writer, int transparencyIndex)
            where TPixel : struct, IPixel<TPixel>
        {
            var descriptor = new GifLogicalScreenDescriptor
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                GlobalColorTableFlag = false, // TODO: Always false for now.
                GlobalColorTableSize = this.bitDepth - 1,
                BackgroundColorIndex = unchecked((byte)transparencyIndex)
            };

            writer.Write((ushort)descriptor.Width);
            writer.Write((ushort)descriptor.Height);

            var field = default(PackedField);
            field.SetBit(0, descriptor.GlobalColorTableFlag);  // 1   : Global color table flag = 1 || 0 (GCT used/ not used)
            field.SetBits(1, 3, descriptor.GlobalColorTableSize); // 2-4 : color resolution
            field.SetBit(4, false); // 5   : GCT sort flag = 0
            field.SetBits(5, 3, descriptor.GlobalColorTableSize); // 6-8 : GCT size. 2^(N+1)

            // Reduce the number of writes
            this.buffer[0] = field.Byte;
            this.buffer[1] = descriptor.BackgroundColorIndex; // Background Color Index
            this.buffer[2] = descriptor.PixelAspectRatio; // Pixel aspect ratio. Assume 1:1

            writer.Write(this.buffer, 0, 3);
        }

        /// <summary>
        /// Writes the application extension to the stream.
        /// </summary>
        /// <param name="writer">The writer to write to the stream with.</param>
        /// <param name="repeatCount">The animated image repeat count.</param>
        /// <param name="frames">The number of image frames.</param>
        private void WriteApplicationExtension(EndianBinaryWriter writer, ushort repeatCount, int frames)
        {
            // Application Extension Header
            if (repeatCount != 1 && frames > 0)
            {
                this.buffer[0] = GifConstants.ExtensionIntroducer;
                this.buffer[1] = GifConstants.ApplicationExtensionLabel;
                this.buffer[2] = GifConstants.ApplicationBlockSize;

                writer.Write(this.buffer, 0, 3);

                writer.Write(GifConstants.ApplicationIdentification.ToCharArray()); // NETSCAPE2.0
                writer.Write((byte)3); // Application block length
                writer.Write((byte)1); // Data sub-block index (always 1)

                // 0 means loop indefinitely. Count is set as play n + 1 times.
                repeatCount = (ushort)Math.Max(0, repeatCount - 1);
                writer.Write(repeatCount); // Repeat count for images.

                writer.Write(GifConstants.Terminator); // Terminator
            }
        }

        /// <summary>
        /// Writes the image comments to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TPixel}"/> to be encoded.</param>
        /// <param name="writer">The stream to write to.</param>
        private void WriteComments<TPixel>(Image<TPixel> image, EndianBinaryWriter writer)
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

            writer.Write(this.buffer, 0, 3);
            writer.Write(comments, 0, count);
            writer.Write(GifConstants.Terminator);
        }

        /// <summary>
        /// Writes the graphics control extension to the stream.
        /// </summary>
        /// <param name="metaData">The metadata of the image or frame.</param>
        /// <param name="writer">The stream to write to.</param>
        /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
        private void WriteGraphicalControlExtension(IMetaData metaData, EndianBinaryWriter writer, int transparencyIndex)
        {
            var extension = new GifGraphicsControlExtension
            {
                DisposalMethod = metaData.DisposalMethod,
                TransparencyFlag = transparencyIndex > -1,
                TransparencyIndex = unchecked((byte)transparencyIndex),
                DelayTime = metaData.FrameDelay
            };

            // Write the intro.
            this.buffer[0] = GifConstants.ExtensionIntroducer;
            this.buffer[1] = GifConstants.GraphicControlLabel;
            this.buffer[2] = 4;
            writer.Write(this.buffer, 0, 3);

            var field = default(PackedField);
            field.SetBits(3, 3, (int)extension.DisposalMethod); // 1-3 : Reserved, 4-6 : Disposal

            // TODO: Allow this as an option.
            field.SetBit(6, false); // 7 : User input - 0 = none
            field.SetBit(7, extension.TransparencyFlag); // 8: Has transparent.

            writer.Write(field.Byte);
            writer.Write((ushort)extension.DelayTime);
            writer.Write(extension.TransparencyIndex);
            writer.Write(GifConstants.Terminator);
        }

        /// <summary>
        /// Writes the image descriptor to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TPixel}"/> to be encoded.</param>
        /// <param name="writer">The stream to write to.</param>
        private void WriteImageDescriptor<TPixel>(ImageBase<TPixel> image, EndianBinaryWriter writer)
            where TPixel : struct, IPixel<TPixel>
        {
            writer.Write(GifConstants.ImageDescriptorLabel); // 2c

            // TODO: Can we capture this?
            writer.Write((ushort)0); // Left position
            writer.Write((ushort)0); // Top position
            writer.Write((ushort)image.Width);
            writer.Write((ushort)image.Height);

            var field = default(PackedField);
            field.SetBit(0, true); // 1: Local color table flag = 1 (LCT used)
            field.SetBit(1, false); // 2: Interlace flag 0
            field.SetBit(2, false); // 3: Sort flag 0
            field.SetBits(5, 3, this.bitDepth - 1); // 4-5: Reserved, 6-8 : LCT size. 2^(N+1)

            writer.Write(field.Byte);
        }

        /// <summary>
        /// Writes the color table to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TPixel}"/> to encode.</param>
        /// <param name="writer">The writer to write to the stream with.</param>
        private void WriteColorTable<TPixel>(QuantizedImage<TPixel> image, EndianBinaryWriter writer)
            where TPixel : struct, IPixel<TPixel>
        {
            // Grab the palette and write it to the stream.
            int pixelCount = image.Palette.Length;

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, this.bitDepth) * 3;
            byte[] colorTable = ArrayPool<byte>.Shared.Rent(colorTableLength);

            try
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int offset = i * 3;
                    image.Palette[i].ToXyzBytes(this.buffer, 0);
                    colorTable[offset] = this.buffer[0];
                    colorTable[offset + 1] = this.buffer[1];
                    colorTable[offset + 2] = this.buffer[2];
                }

                writer.Write(colorTable, 0, colorTableLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(colorTable);
            }
        }

        /// <summary>
        /// Writes the image pixel data to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="QuantizedImage{TPixel}"/> containing indexed pixels.</param>
        /// <param name="writer">The stream to write to.</param>
        private void WriteImageData<TPixel>(QuantizedImage<TPixel> image, EndianBinaryWriter writer)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var encoder = new LzwEncoder(image.Pixels, (byte)this.bitDepth))
            {
                encoder.Encode(writer.BaseStream);
            }
        }
    }
}