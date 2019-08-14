// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Performs the tiff decoding operation.
    /// </summary>
    internal class TiffDecoderCore : ITiffDecoderCoreOptions, IDisposable
    {
        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private readonly bool ignoreMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        private TiffDecoderCore(Configuration configuration, ITiffDecoderOptions options)
        {
            options = options ?? new TiffDecoder();

            this.configuration = configuration ?? Configuration.Default;
            this.ignoreMetadata = options.IgnoreMetadata;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public TiffDecoderCore(Stream stream, Configuration configuration, ITiffDecoderOptions options)
            : this(configuration, options)
        {
            this.Stream = TiffStreamFactory.CreateBySignature(stream);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public TiffDecoderCore(TiffByteOrder byteOrder, Stream stream, Configuration configuration, ITiffDecoderOptions options)
            : this(configuration, options)
        {
            this.Stream = TiffStreamFactory.Create(byteOrder, stream);
        }

        /// <summary>
        /// Gets or sets the number of bits for each sample of the pixel format used to encode the image.
        /// </summary>
        public uint[] BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the lookup table for RGB palette colored images.
        /// </summary>
        public uint[] ColorMap { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation implementation to use when decoding the image.
        /// </summary>
        public TiffColorType ColorType { get; set; }

        /// <summary>
        /// Gets or sets the compression implementation to use when decoding the image.
        /// </summary>
        public TiffCompressionType CompressionType { get; set; }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public TiffStream Stream { get; }

        /// <summary>
        /// Gets or sets the planar configuration type to use when decoding the image.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/>  and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var header = TiffHeader.Read(this.Stream);
            TiffIfd[] ifds = TiffFileFormatReader.ReadIfds(header, this.Stream);
            TiffIfd firstIfd = ifds[0];
            Image<TPixel> image = this.DecodeImage<TPixel>(firstIfd);

            return image;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // nothing
        }

        /// <summary>
        /// Decodes the image data from a specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ifd">The IFD to read the image from.</param>
        /// <returns>The decoded image.</returns>
        private Image<TPixel> DecodeImage<TPixel>(TiffIfd ifd)
            where TPixel : struct, IPixel<TPixel>
        {
            TiffIfdEntriesContainer entries = ifd.Entries;
            int width = (int)entries.Width;
            int height = (int)entries.Height;

            var image = new Image<TPixel>(this.configuration, width, height);

            TiffDecoderHelpers.FillMetadata(image.Metadata, entries, this.ignoreMetadata);
            TiffDecoderHelpers.ReadFormatOptions(this, entries);

            int rowsPerStrip = (int)entries.RowsPerStrip;
            uint[] stripOffsets = entries.StripOffsets;
            uint[] stripByteCounts = entries.StripByteCounts;

            this.DecodeImageStrips(image, rowsPerStrip, stripOffsets, stripByteCounts);

            return image;
        }

        /// <summary>
        /// Calculates the size (in bytes) for a pixel buffer using the determined color format.
        /// </summary>
        /// <param name="width">The width for the desired pixel buffer.</param>
        /// <param name="height">The height for the desired pixel buffer.</param>
        /// <param name="plane">The index of the plane for planar image configuration (or zero for chunky).</param>
        /// <returns>The size (in bytes) of the required pixel buffer.</returns>
        private int CalculateImageBufferSize(int width, int height, int plane)
        {
            uint bitsPerPixel = 0;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
            {
                for (int i = 0; i < this.BitsPerSample.Length; i++)
                {
                    bitsPerPixel += this.BitsPerSample[i];
                }
            }
            else
            {
                bitsPerPixel = this.BitsPerSample[plane];
            }

            int bytesPerRow = ((width * (int)bitsPerPixel) + 7) / 8;
            return bytesPerRow * height;
        }

        /// <summary>
        /// Decodes the image data for strip encoded data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to decode data into.</param>
        /// <param name="rowsPerStrip">The number of rows per strip of data.</param>
        /// <param name="stripOffsets">An array of byte offsets to each strip in the image.</param>
        /// <param name="stripByteCounts">An array of the size of each strip (in bytes).</param>
        private void DecodeImageStrips<TPixel>(Image<TPixel> image, int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts)
            where TPixel : struct, IPixel<TPixel>
        {
            int stripsPerPixel = this.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? 1 : this.BitsPerSample.Length;
            int stripsPerPlane = stripOffsets.Length / stripsPerPixel;

            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            byte[][] stripBytes = new byte[stripsPerPixel][];

            for (int stripIndex = 0; stripIndex < stripBytes.Length; stripIndex++)
            {
                int uncompressedStripSize = this.CalculateImageBufferSize(image.Width, rowsPerStrip, stripIndex);
                stripBytes[stripIndex] = ArrayPool<byte>.Shared.Rent(uncompressedStripSize);
            }

            try
            {
                TiffColorDecoder<TPixel> chunkyDecoder = null;
                RgbPlanarTiffColor<TPixel> planarDecoder = null;
                if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                {
                    chunkyDecoder = TiffColorDecoderFactory<TPixel>.Create(this.ColorType, this.BitsPerSample, this.ColorMap);
                }
                else
                {
                    planarDecoder = TiffColorDecoderFactory<TPixel>.CreatePlanar(this.ColorType, this.BitsPerSample, this.ColorMap);
                }

                for (int i = 0; i < stripsPerPlane; i++)
                {
                    int stripHeight = i < stripsPerPlane - 1 || image.Height % rowsPerStrip == 0 ? rowsPerStrip : image.Height % rowsPerStrip;

                    for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                    {
                        int stripIndex = (i * stripsPerPixel) + planeIndex;
                        CompressionFactory.DecompressImageBlock(this.Stream.InputStream, this.CompressionType, stripOffsets[stripIndex], stripByteCounts[stripIndex], stripBytes[planeIndex]);
                    }

                    if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                    {
                        chunkyDecoder.Decode(stripBytes[0], pixels, 0, rowsPerStrip * i, image.Width, stripHeight);
                    }
                    else
                    {
                        planarDecoder.Decode(stripBytes, pixels, 0, rowsPerStrip * i, image.Width, stripHeight);
                    }
                }
            }
            finally
            {
                for (int stripIndex = 0; stripIndex < stripBytes.Length; stripIndex++)
                {
                    ArrayPool<byte>.Shared.Return(stripBytes[stripIndex]);
                }
            }
        }
    }
}
