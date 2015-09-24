
namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    public class GifEncoder : IImageEncoder
    {
        /// <summary>
        /// The quality.
        /// </summary>
        private int quality = 256;

        private Image image;

        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>For gifs the value ranges from 1 to 256.</remarks>
        public int Quality
        {
            get
            {
                return this.quality;
            }

            set
            {
                this.quality = value.Clamp(1, 256);
            }
        }

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
        /// <param name="image">The <see cref="ImageBase"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode(ImageBase image, Stream stream)
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.image = (Image)image;

            // Write the header.
            // File Header signature and version.
            this.WriteString(stream, "GIF");
            this.WriteString(stream, "89a");

            int bitdepth = this.GetBitsNeededForColorDepth(this.Quality) - 1;

            this.WriteGlobalLogicalScreenDescriptor(stream, bitdepth);
           
            foreach (ImageFrame frame in this.image.Frames)
            {
                this.WriteColorTable(stream, bitdepth);
                this.WriteGraphicalControlExtension(stream);
            }

            throw new System.NotImplementedException();
        }

        private void WriteGlobalLogicalScreenDescriptor(Stream stream, int bitDepth)
        {
            IImageDecoder decoder = ((Image)this.image).Decoder;
            GifLogicalScreenDescriptor descriptor;

            // Try and grab an existing descriptor.
            if (decoder.GetType() == typeof(GifDecoder))
            {
                // Ensure the dimensions etc are up to date.
                descriptor = ((GifDecoder)decoder).CoreDecoder.LogicalScreenDescriptor;
                descriptor.Width = (short)this.image.Width;
                descriptor.Height = (short)this.image.Height;
                descriptor.GlobalColorTableSize = this.Quality;
            }
            else
            {
                descriptor = new GifLogicalScreenDescriptor
                {
                    Width = (short)this.image.Width,
                    Height = (short)this.image.Height,
                    GlobalColorTableFlag = true,
                    GlobalColorTableSize = this.Quality
                };
            }

            this.WriteShort(stream, descriptor.Width);
            this.WriteShort(stream, descriptor.Width);

            int packed = 0x80 | // 1   : Global color table flag = 1 (GCT used)
                         0x70 | // 2-4 : color resolution
                         0x00 | // 5   : GCT sort flag = 0
                         bitDepth; // 6-8 : GCT size assume 1:1

            this.WriteByte(stream, packed);
            this.WriteByte(stream, descriptor.BackgroundColorIndex); // Background Color Index
            this.WriteByte(stream, descriptor.PixelAspectRatio); // Pixel aspect ratio
        }

        private void WriteColorTable(Stream stream, int bitDepth)
        {
            // Quantize the image returning a pallete.
            IQuantizer quantizer = new OctreeQuantizer(Math.Max(1, this.quality - 1), bitDepth);
            QuantizedImage quantizedImage = quantizer.Quantize(this.image);
            this.image = quantizedImage.ToImage();

            // Grab the pallete and write it to the stream.
            Bgra[] pallete = quantizedImage.Palette;
            int pixelCount = pallete.Length;
            int colorTableLength = pixelCount * 3;
            byte[] colorTable = new byte[colorTableLength];

            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                Bgra color = pallete[i];
                colorTable[offset + 0] = color.B;
                colorTable[offset + 1] = color.G;
                colorTable[offset + 2] = color.R;
            }

            stream.Write(colorTable, 0, colorTableLength);
        }

        private void WriteGraphicalControlExtension(Stream stream)
        {
            Image i = ((Image)this.image);
            IImageDecoder decoder = i.Decoder;
            GifGraphicsControlExtension extension;

            // Try and grab an existing descriptor.
            // TODO: Check whether we need to.
            if (decoder.GetType() == typeof(GifDecoder))
            {
                // Ensure the dimensions etc are up to date.
                extension = ((GifDecoder)decoder).CoreDecoder.GraphicsControlExtension;
                extension.TransparencyFlag = this.Quality > 1;
                extension.TransparencyIndex = this.Quality - 1;
                extension.DelayTime = i.FrameDelay;
            }
            else
            {
                bool hasTransparent = this.Quality > 1;
                DisposalMethod disposalMethod = hasTransparent
                    ? DisposalMethod.RestoreToBackground
                    : DisposalMethod.Unspecified;

                extension = new GifGraphicsControlExtension()
                {
                    DisposalMethod = disposalMethod,
                    TransparencyFlag = hasTransparent,
                    TransparencyIndex = this.Quality - 1, // Quantizer set last as transparent.
                    DelayTime = i.FrameDelay
                };
            }

            this.WriteByte(stream, GifConstants.ExtensionIntroducer);
            this.WriteByte(stream, GifConstants.GraphicControlLabel);
            this.WriteByte(stream, 4); // Size

            int packed = 0 | // 1-3 : Reserved
                         (int)extension.DisposalMethod | // 4-6 : Disposal
                         0 | // 7 : User input - 0 = none
                         extension.TransparencyIndex;

            this.WriteByte(stream, packed);
            this.WriteShort(stream, extension.DelayTime);
            this.WriteByte(stream, GifConstants.Terminator);
        }



        private void WriteApplicationExtension(Stream stream)
        {
            // TODO: Implement
            throw new NotImplementedException();
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
        private int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }
    }
}
