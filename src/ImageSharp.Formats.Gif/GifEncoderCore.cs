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
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>For gifs the value ranges from 1 to 256.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = 128;

        /// <summary>
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TColor}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TColor>(Image<TColor> image, Stream stream)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            if (this.Quantizer == null)
            {
                this.Quantizer = new OctreeQuantizer<TColor>();
            }

            // Do not use IDisposable pattern here as we want to preserve the stream.
            EndianBinaryWriter writer = new EndianBinaryWriter(Endianness.LittleEndian, stream);

            // Ensure that quality can be set but has a fallback.
            int quality = this.Quality > 0 ? this.Quality : image.Quality;
            this.Quality = quality > 0 ? quality.Clamp(1, 256) : 256;

            // Get the number of bits.
            this.bitDepth = ImageMaths.GetBitsNeededForColorDepth(this.Quality);

            // Quantize the image returning a palette.
            QuantizedImage<TColor> quantized = ((IQuantizer<TColor>)this.Quantizer).Quantize(image, this.Quality);

            int index = this.GetTransparentIndex(quantized);

            // Write the header.
            this.WriteHeader(writer);

            // Write the LSD. We'll use local color tables for now.
            this.WriteLogicalScreenDescriptor(image, writer, index);

            // Write the first frame.
            this.WriteGraphicalControlExtension(image, writer, index);
            this.WriteImageDescriptor(image, writer);
            this.WriteColorTable(quantized, writer);
            this.WriteImageData(quantized, writer);

            // Write additional frames.
            if (image.Frames.Any())
            {
                this.WriteApplicationExtension(writer, image.RepeatCount, image.Frames.Count);

                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 0; i < image.Frames.Count; i++)
                {
                    ImageFrame<TColor> frame = image.Frames[i];
                    QuantizedImage<TColor> quantizedFrame = ((IQuantizer<TColor>)this.Quantizer).Quantize(frame, this.Quality);

                    this.WriteGraphicalControlExtension(frame, writer, this.GetTransparentIndex(quantizedFrame));
                    this.WriteImageDescriptor(frame, writer);
                    this.WriteColorTable(quantizedFrame, writer);
                    this.WriteImageData(quantizedFrame, writer);
                }
            }

            // TODO: Write Comments extension etc
            writer.Write(GifConstants.EndIntroducer);
        }

        /// <summary>
        /// Returns the index of the most transparent color in the palette.
        /// </summary>
        /// <param name="quantized">
        /// The quantized.
        /// </param>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        private int GetTransparentIndex<TColor>(QuantizedImage<TColor> quantized)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // Find the lowest alpha value and make it the transparent index.
            int index = 255;
            byte alpha = 255;
            bool hasEmpty = false;

            // Some images may have more than one quantized pixel returned with an alpha value of zero
            // so we should always ignore if we have empty pixels present.
            for (int i = 0; i < quantized.Palette.Length; i++)
            {
                quantized.Palette[i].ToXyzwBytes(this.buffer, 0);

                if (!hasEmpty)
                {
                    if (this.buffer[0] == 0 && this.buffer[1] == 0 && this.buffer[2] == 0 && this.buffer[3] == 0)
                    {
                        alpha = this.buffer[3];
                        index = i;
                        hasEmpty = true;
                    }

                    if (this.buffer[3] < alpha)
                    {
                        alpha = this.buffer[3];
                        index = i;
                    }
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
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The image to encode.</param>
        /// <param name="writer">The writer to write to the stream with.</param>
        /// <param name="tranparencyIndex">The transparency index to set the default background index to.</param>
        private void WriteLogicalScreenDescriptor<TColor>(Image<TColor> image, EndianBinaryWriter writer, int tranparencyIndex)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            GifLogicalScreenDescriptor descriptor = new GifLogicalScreenDescriptor
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                GlobalColorTableFlag = false, // Always false for now.
                GlobalColorTableSize = this.bitDepth - 1,
                BackgroundColorIndex = (byte)tranparencyIndex
            };

            writer.Write((ushort)descriptor.Width);
            writer.Write((ushort)descriptor.Height);

            PackedField field = default(PackedField);
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
                repeatCount = (ushort)(Math.Max((ushort)0, repeatCount) - 1);

                writer.Write(repeatCount); // Repeat count for images.

                writer.Write(GifConstants.Terminator); // Terminator
            }
        }

        /// <summary>
        /// Writes the graphics control extension to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to encode.</param>
        /// <param name="writer">The stream to write to.</param>
        /// <param name="transparencyIndex">The index of the color in the color palette to make transparent.</param>
        private void WriteGraphicalControlExtension<TColor>(ImageBase<TColor> image, EndianBinaryWriter writer, int transparencyIndex)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // TODO: Check transparency logic.
            bool hasTransparent = transparencyIndex < 255;
            DisposalMethod disposalMethod = hasTransparent
                ? DisposalMethod.RestoreToBackground
                : DisposalMethod.Unspecified;

            GifGraphicsControlExtension extension = new GifGraphicsControlExtension()
            {
                DisposalMethod = disposalMethod,
                TransparencyFlag = hasTransparent,
                TransparencyIndex = transparencyIndex,
                DelayTime = image.FrameDelay
            };

            // Write the intro.
            this.buffer[0] = GifConstants.ExtensionIntroducer;
            this.buffer[1] = GifConstants.GraphicControlLabel;
            this.buffer[2] = 4;
            writer.Write(this.buffer, 0, 3);

            PackedField field = default(PackedField);
            field.SetBits(3, 3, (int)extension.DisposalMethod); // 1-3 : Reserved, 4-6 : Disposal

            // TODO: Allow this as an option.
            field.SetBit(6, false); // 7 : User input - 0 = none
            field.SetBit(7, extension.TransparencyFlag); // 8: Has transparent.

            writer.Write(field.Byte);
            writer.Write((ushort)extension.DelayTime);
            writer.Write((byte)extension.TransparencyIndex);
            writer.Write(GifConstants.Terminator);
        }

        /// <summary>
        /// Writes the image descriptor to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to be encoded.</param>
        /// <param name="writer">The stream to write to.</param>
        private void WriteImageDescriptor<TColor>(ImageBase<TColor> image, EndianBinaryWriter writer)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            writer.Write(GifConstants.ImageDescriptorLabel); // 2c

            // TODO: Can we capture this?
            writer.Write((ushort)0); // Left position
            writer.Write((ushort)0); // Top position
            writer.Write((ushort)image.Width);
            writer.Write((ushort)image.Height);

            PackedField field = default(PackedField);
            field.SetBit(0, true); // 1: Local color table flag = 1 (LCT used)
            field.SetBit(1, false); // 2: Interlace flag 0
            field.SetBit(2, false); // 3: Sort flag 0
            field.SetBits(5, 3, this.bitDepth - 1); // 4-5: Reserved, 6-8 : LCT size. 2^(N+1)

            writer.Write(field.Byte);
        }

        /// <summary>
        /// Writes the color table to the stream.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to encode.</param>
        /// <param name="writer">The writer to write to the stream with.</param>
        private void WriteColorTable<TColor>(QuantizedImage<TColor> image, EndianBinaryWriter writer)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
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
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="QuantizedImage{TColor}"/> containing indexed pixels.</param>
        /// <param name="writer">The stream to write to.</param>
        private void WriteImageData<TColor>(QuantizedImage<TColor> image, EndianBinaryWriter writer)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (LzwEncoder encoder = new LzwEncoder(image.Pixels, (byte)this.bitDepth))
            {
                encoder.Encode(writer.BaseStream);
            }
        }
    }
}