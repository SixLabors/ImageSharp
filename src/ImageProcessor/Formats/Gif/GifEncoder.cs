// <copyright file="GifEncoder.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Image encoder for writing image data to a stream in gif format.
    /// </summary>
    public class GifEncoder : IImageEncoder
    {
        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>For gifs the value ranges from 1 to 256.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets the default file extension for this encoder.
        /// </summary>
        public string Extension => "GIF";

        /// <summary>
        /// Returns a value indicating whether the <see cref="IImageDecoder"/> supports the specified
        /// file header.
        /// </summary>
        /// <param name="extension">The <see cref="string"/> containing the file extension.</param>
        /// <returns>
        /// True if the decoder supports the file extension; otherwise, false.
        /// </returns>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, "extension");

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;
            return extension.Equals("GIF", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="imageBase">The <see cref="ImageBase"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode(ImageBase imageBase, Stream stream)
        {
            Guard.NotNull(imageBase, nameof(imageBase));
            Guard.NotNull(stream, nameof(stream));

            Image image = (Image)imageBase;

            // Write the header.
            // File Header signature and version.
            this.WriteString(stream, GifConstants.FileType);
            this.WriteString(stream, GifConstants.FileVersion);

            // Calculate the quality.
            int quality = this.Quality > 0 ? this.Quality : imageBase.Quality;
            quality = quality > 0 ? quality.Clamp(1, 256) : 256;

            // Get the number of bits.
            int bitDepth = this.GetBitsNeededForColorDepth(quality);

            // Write the LSD and check to see if we need a global color table.
            // Always true just now.
            bool globalColor = this.WriteGlobalLogicalScreenDescriptor(image, stream, bitDepth);
            QuantizedImage quantized = this.WriteColorTable(imageBase, stream, quality, bitDepth);

            this.WriteGraphicalControlExtension(imageBase, stream);
            this.WriteImageDescriptor(quantized, quality, stream);

            if (image.Frames.Any())
            {
                this.WriteApplicationExtension(stream, image.RepeatCount, image.Frames.Count);
                foreach (ImageFrame frame in image.Frames)
                {
                    this.WriteGraphicalControlExtension(frame, stream);
                    this.WriteFrameImageDescriptor(frame, stream);
                }
            }

            // TODO: Write Comments extension etc
            this.WriteByte(stream, GifConstants.EndIntroducer);
        }

        /// <summary>
        /// Writes the logical screen descriptor to the stream.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="bitDepth">The bit depth.</param>
        /// <returns>The <see cref="GifLogicalScreenDescriptor"/></returns>
        private bool WriteGlobalLogicalScreenDescriptor(Image image, Stream stream, int bitDepth)
        {
            GifLogicalScreenDescriptor descriptor = new GifLogicalScreenDescriptor
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                GlobalColorTableFlag = true,
                GlobalColorTableSize = bitDepth
            };

            this.WriteShort(stream, descriptor.Width);
            this.WriteShort(stream, descriptor.Height);

            int packed = 0x80 | // 1   : Global color table flag = 1 (GCT used)
                         bitDepth - 1 | // 2-4 : color resolution
                         0x00 | // 5   : GCT sort flag = 0
                         bitDepth - 1; // 6-8 : GCT size TODO: Check this.

            this.WriteByte(stream, packed);
            this.WriteByte(stream, descriptor.BackgroundColorIndex); // Background Color Index
            this.WriteByte(stream, descriptor.PixelAspectRatio); // Pixel aspect ratio. Assume 1:1

            return descriptor.GlobalColorTableFlag;
        }

        /// <summary>
        /// Writes the color table to the stream.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to encode.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="quality">The quality (number of colors) to encode the image to.</param>
        /// <param name="bitDepth">The bit depth.</param>
        /// <returns>The <see cref="QuantizedImage"/></returns>
        private QuantizedImage WriteColorTable(ImageBase image, Stream stream, int quality, int bitDepth)
        {
            // Quantize the image returning a pallete.
            IQuantizer quantizer = new OctreeQuantizer(quality.Clamp(1, 255), bitDepth);
            QuantizedImage quantizedImage = quantizer.Quantize(image);

            // Grab the pallete and write it to the stream.
            Bgra[] pallete = quantizedImage.Palette;
            int pixelCount = pallete.Length;

            // Get max colors for bit depth.
            int colorTableLength = (int)Math.Pow(2, bitDepth) * 3;
            byte[] colorTable = new byte[colorTableLength];

            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 3;
                Bgra color = pallete[i];
                colorTable[offset + 2] = color.B;
                colorTable[offset + 1] = color.G;
                colorTable[offset + 0] = color.R;
            }

            stream.Write(colorTable, 0, colorTableLength);

            return quantizedImage;
        }

        /// <summary>
        /// Writes the graphics control extension to the stream.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to encode.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteGraphicalControlExtension(ImageBase image, Stream stream)
        {
            // Calculate the quality.
            int quality = this.Quality > 0 ? this.Quality : image.Quality;
            quality = quality > 0 ? quality.Clamp(1, 256) : 256;

            // TODO: Check transparency logic.
            bool hasTransparent = quality > 1;
            DisposalMethod disposalMethod = hasTransparent
                ? DisposalMethod.RestoreToBackground
                : DisposalMethod.Unspecified;

            GifGraphicsControlExtension extension = new GifGraphicsControlExtension()
            {
                DisposalMethod = disposalMethod,
                TransparencyFlag = hasTransparent,
                TransparencyIndex = quality - 1, // Quantizer sets last index as transparent.
                DelayTime = image.FrameDelay
            };

            this.WriteByte(stream, GifConstants.ExtensionIntroducer);
            this.WriteByte(stream, GifConstants.GraphicControlLabel);
            this.WriteByte(stream, 4); // Size

            int packed = 0 | // 1-3 : Reserved
                         (int)extension.DisposalMethod << 2 | // 4-6 : Disposal
                         0 | // 7 : User input - 0 = none
                         (extension.TransparencyFlag ? 1 : 0); // 8: Has transparent.

            this.WriteByte(stream, packed);
            this.WriteShort(stream, extension.DelayTime);
            this.WriteByte(stream, extension.TransparencyIndex);
            this.WriteByte(stream, GifConstants.Terminator);
        }

        /// <summary>
        /// Writes the application exstension to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="repeatCount">The animated image repeat count.</param>
        /// <param name="frames">Th number of image frames.</param>
        private void WriteApplicationExtension(Stream stream, ushort repeatCount, int frames)
        {
            // Application Extension Header
            if (repeatCount != 1 && frames > 0)
            {
                // 0 means loop indefinitely. count is set as play n + 1 times.
                // TODO: Check this as the correct value might be pulled from the decoder.
                repeatCount = (ushort)Math.Max(0, repeatCount - 1);
                this.WriteByte(stream, GifConstants.ExtensionIntroducer); // NETSCAPE2.0
                this.WriteByte(stream, GifConstants.ApplicationExtensionLabel);
                this.WriteByte(stream, GifConstants.ApplicationBlockSize);

                this.WriteString(stream, GifConstants.ApplicationIdentification);
                this.WriteByte(stream, 3); // Application block length
                this.WriteByte(stream, 1); // Data sub-block index (always 1)
                this.WriteShort(stream, repeatCount); // Repeat count for images.

                this.WriteByte(stream, GifConstants.Terminator); // Terminator
            }
        }

        /// <summary>
        /// Writes the image descriptor to the stream.
        /// </summary>
        /// <param name="image">The <see cref="QuantizedImage"/> containing indexed pixels.</param>
        /// <param name="quality">The quality (number of colors) to encode the image to.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteImageDescriptor(QuantizedImage image, int quality, Stream stream)
        {
            this.WriteByte(stream, GifConstants.ImageDescriptorLabel); // 2c
            // TODO: Can we capture this?
            this.WriteShort(stream, 0); // Left position
            this.WriteShort(stream, 0); // Top position
            this.WriteShort(stream, image.Width);
            this.WriteShort(stream, image.Height);

            // Calculate the quality.
            int bitDepth = this.GetBitsNeededForColorDepth(quality);

            // No LCT use GCT.
            this.WriteByte(stream, 0);

            // Write the image data.
            this.WriteImageData(image, stream, bitDepth);
        }

        /// <summary>
        /// Writes the image descriptor to the stream.
        /// </summary>
        /// <param name="image">The <see cref="ImageBase"/> to be encoded.</param>
        /// <param name="stream">The stream to write to.</param>
        private void WriteFrameImageDescriptor(ImageBase image, Stream stream)
        {
            this.WriteByte(stream, GifConstants.ImageDescriptorLabel); // 2c
            // TODO: Can we capture this?
            this.WriteShort(stream, 0); // Left position
            this.WriteShort(stream, 0); // Top position
            this.WriteShort(stream, image.Width);
            this.WriteShort(stream, image.Height);

            // Calculate the quality.
            int quality = this.Quality > 0 ? this.Quality : image.Quality;
            quality = quality > 0 ? quality.Clamp(1, 256) : 256;
            int bitDepth = this.GetBitsNeededForColorDepth(quality);

            int packed = 0x80 | // 1: Local color table flag = 1 (LCT used)
                         0x00 | // 2: Interlace flag 0
                         0x00 | // 3: Sort flag 0
                         0 | // 4-5: Reserved
                         bitDepth - 1;

            this.WriteByte(stream, packed);

            // Now immediately follow with the color table.
            QuantizedImage quantized = this.WriteColorTable(image, stream, quality, bitDepth);
            this.WriteImageData(quantized, stream, bitDepth);
        }

        /// <summary>
        /// Writes the image pixel data to the stream.
        /// </summary>
        /// <param name="image">The <see cref="QuantizedImage"/> containing indexed pixels.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="bitDepth">The bit depth of the image.</param>
        private void WriteImageData(QuantizedImage image, Stream stream, int bitDepth)
        {
            byte[] indexedPixels = image.Pixels;

            LzwEncoder encoder = new LzwEncoder(indexedPixels, (byte)bitDepth);
            encoder.Encode(stream);

            this.WriteByte(stream, GifConstants.Terminator);
        }

        /// <summary>
        /// Writes a short to the given stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private void WriteShort(Stream stream, int value)
        {
            // Leave only one significant byte.
            stream.WriteByte(Convert.ToByte(value & 0xff));
            stream.WriteByte(Convert.ToByte((value >> 8) & 0xff));
        }

        /// <summary>
        /// Writes a byte to the given stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private void WriteByte(Stream stream, int value)
        {
            stream.WriteByte(Convert.ToByte(value));
        }

        /// <summary>
        /// Writes a string to the given stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="value">The value to write.</param>
        private void WriteString(Stream stream, string value)
        {
            char[] chars = value.ToCharArray();
            foreach (char c in chars)
            {
                stream.WriteByte((byte)c);
            }
        }

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <para>The number of colors.</para>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        private int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }
    }
}
