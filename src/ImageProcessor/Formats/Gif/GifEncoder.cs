
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
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");

            // Write the header.
            // File Header signature and version.
            this.WriteString(stream, "GIF");
            this.WriteString(stream, "89a");

            GifLogicalScreenDescriptor descriptor = new GifLogicalScreenDescriptor
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                GlobalColorTableFlag = true,
                GlobalColorTableSize = this.Quality
            };

            this.WriteGlobalLogicalScreenDescriptor(stream, descriptor);



            throw new System.NotImplementedException();
        }

        private void WriteGlobalLogicalScreenDescriptor(Stream stream, GifLogicalScreenDescriptor descriptor)
        {
            this.WriteShort(stream, descriptor.Width);
            this.WriteShort(stream, descriptor.Width);
            int size = descriptor.GlobalColorTableSize;
            int bitdepth = this.GetBitsNeededForColorDepth(size) - 1;
            int packed = 0x80 | // 1   : Global color table flag = 1 (GCT used)
                         0x70 | // 2-4 : color resolution
                         0x00 | // 5   : GCT sort flag = 0
                         bitdepth; // 6-8 : GCT size assume 1:1

            this.WriteByte(stream, packed);
            this.WriteByte(stream, descriptor.BackgroundColorIndex); // Background Color Index
            this.WriteByte(stream, descriptor.PixelAspectRatio); // Pixel aspect ratio

            // Write the global color table.
            this.WriteColorTable(stream, size);
        }

        private void WriteColorTable(Stream stream, int size)
        {

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
        /// Writes a short to the given stream.
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
