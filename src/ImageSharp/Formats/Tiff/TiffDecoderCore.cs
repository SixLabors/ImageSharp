// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Performs the tiff decoding operation.
    /// </summary>
    internal class TiffDecoderCore : IImageDecoderInternals, IDisposable
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
        /// Gets the input stream.
        /// </summary>
        public TiffStream Stream { get; }

        /// <summary>
        /// Gets or sets the number of bits for each sample of the pixel format used to encode the image.
        /// </summary>
        public ushort[] BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the lookup table for RGB palette colored images.
        /// </summary>
        public ushort[] ColorMap { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation implementation to use when decoding the image.
        /// </summary>
        public TiffColorType ColorType { get; set; }

        /// <summary>
        /// Gets or sets the compression implementation to use when decoding the image.
        /// </summary>
        public TiffCompressionType CompressionType { get; set; }

        /// <summary>
        /// Gets or sets the planar configuration type to use when decoding the image.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <inheritdoc/>
        public Configuration Configuration => this.configuration;

        /// <inheritdoc/>
        public Size Dimensions { get; private set; }

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/>  and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var reader = new DirectoryReader(this.Stream);
            IEnumerable<IExifValue[]> directories = reader.Read();

            var frames = new List<ImageFrame<TPixel>>();
            foreach (IExifValue[] ifd in directories)
            {
                ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd);
                frames.Add(frame);
            }

            ImageMetadata metadata = frames.CreateMetadata(this.ignoreMetadata, this.Stream.ByteOrder);

            // todo: tiff frames can have different sizes
            {
                var root = frames.First();
                this.Dimensions = root.Size();
                foreach (var frame in frames)
                {
                    if (frame.Size() != root.Size())
                    {
                        throw new NotSupportedException("Images with different sizes are not supported");
                    }
                }
            }

            var image = new Image<TPixel>(this.configuration, metadata, frames);

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
        /// <param name="tags">The IFD tags.</param>
        private ImageFrame<TPixel> DecodeFrame<TPixel>(IExifValue[] tags)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var coreMetadata = new ImageFrameMetadata();
            TiffFrameMetadata metadata = coreMetadata.GetTiffMetadata();
            metadata.Tags = tags;

            this.VerifyAndParseOptions(metadata);

            int width = (int)metadata.Width;
            int height = (int)metadata.Height;
            var frame = new ImageFrame<TPixel>(this.configuration, width, height, coreMetadata);

            int rowsPerStrip = (int)metadata.RowsPerStrip;
            uint[] stripOffsets = metadata.StripOffsets;
            uint[] stripByteCounts = metadata.StripByteCounts;

            this.DecodeImageStrips(frame, rowsPerStrip, stripOffsets, stripByteCounts);

            return frame;
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
        /// <param name="frame">The image frame to decode data into.</param>
        /// <param name="rowsPerStrip">The number of rows per strip of data.</param>
        /// <param name="stripOffsets">An array of byte offsets to each strip in the image.</param>
        /// <param name="stripByteCounts">An array of the size of each strip (in bytes).</param>
        private void DecodeImageStrips<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int stripsPerPixel = this.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? 1 : this.BitsPerSample.Length;
            int stripsPerPlane = stripOffsets.Length / stripsPerPixel;

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            byte[][] stripBytes = new byte[stripsPerPixel][];

            for (int stripIndex = 0; stripIndex < stripBytes.Length; stripIndex++)
            {
                int uncompressedStripSize = this.CalculateImageBufferSize(frame.Width, rowsPerStrip, stripIndex);
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
                    int stripHeight = i < stripsPerPlane - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;

                    for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                    {
                        int stripIndex = (i * stripsPerPixel) + planeIndex;
                        CompressionFactory.DecompressImageBlock(this.Stream.InputStream, this.CompressionType, stripOffsets[stripIndex], stripByteCounts[stripIndex], stripBytes[planeIndex]);
                    }

                    if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                    {
                        chunkyDecoder.Decode(stripBytes[0], pixels, 0, rowsPerStrip * i, frame.Width, stripHeight);
                    }
                    else
                    {
                        planarDecoder.Decode(stripBytes, pixels, 0, rowsPerStrip * i, frame.Width, stripHeight);
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

        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            throw new NotImplementedException();
        }

        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
