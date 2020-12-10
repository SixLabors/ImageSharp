// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Streams;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// Performs the tiff decoding operation.
    /// </summary>
    internal class TiffDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// The global configuration
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private readonly bool ignoreMetadata;

        /// <summary>
        /// The image metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// The tiff specific metadata.
        /// </summary>
        private TiffMetadata tiffMetaData;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream inputStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public TiffDecoderCore(Configuration configuration, ITiffDecoderOptions options)
        {
            options ??= new TiffDecoder();

            this.configuration = configuration ?? Configuration.Default;
            this.ignoreMetadata = options.IgnoreMetadata;
            this.memoryAllocator = this.configuration.MemoryAllocator;
        }

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
        /// Gets or sets the compression used, when the image was encoded.
        /// </summary>
        public TiffDecoderCompressionType CompressionType { get; set; }

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

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.inputStream = stream;
            TiffStream tiffStream = CreateStream(stream);
            var reader = new DirectoryReader(tiffStream);

            IEnumerable<IExifValue[]> directories = reader.Read();

            var frames = new List<ImageFrame<TPixel>>();
            var framesMetadata = new List<TiffFrameMetadata>();
            foreach (IExifValue[] ifd in directories)
            {
                ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd, out TiffFrameMetadata frameMetadata);
                frames.Add(frame);
                framesMetadata.Add(frameMetadata);
            }

            this.metadata = framesMetadata.CreateMetadata(this.ignoreMetadata, tiffStream.ByteOrder);
            this.SetTiffFormatMetaData(framesMetadata, tiffStream.ByteOrder);

            // todo: tiff frames can have different sizes
            {
                ImageFrame<TPixel> root = frames.First();
                this.Dimensions = root.Size();
                foreach (ImageFrame<TPixel> frame in frames)
                {
                    if (frame.Size() != root.Size())
                    {
                        TiffThrowHelper.ThrowNotSupported("Images with different sizes are not supported");
                    }
                }
            }

            var image = new Image<TPixel>(this.configuration, this.metadata, frames);

            return image;
        }

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.inputStream = stream;
            TiffStream tiffStream = CreateStream(stream);
            var reader = new DirectoryReader(tiffStream);

            IEnumerable<IExifValue[]> directories = reader.Read();

            var framesMetadata = new List<TiffFrameMetadata>();
            foreach (IExifValue[] ifd in directories)
            {
                var meta = new TiffFrameMetadata();
                meta.FrameTags.AddRange(ifd);
                framesMetadata.Add(meta);
            }

            this.SetTiffFormatMetaData(framesMetadata, tiffStream.ByteOrder);

            TiffFrameMetadata root = framesMetadata.First();
            int bitsPerPixel = 0;
            foreach (var bits in root.BitsPerSample)
            {
                bitsPerPixel += bits;
            }

            return new ImageInfo(new PixelTypeInfo(bitsPerPixel), (int)root.Width, (int)root.Height, this.metadata);
        }

        private void SetTiffFormatMetaData(List<TiffFrameMetadata> framesMetadata, ByteOrder byteOrder)
        {
            this.metadata = framesMetadata.CreateMetadata(this.ignoreMetadata, byteOrder);
            this.tiffMetaData = this.metadata.GetTiffMetadata();
        }

        private static TiffStream CreateStream(Stream stream)
        {
            ByteOrder byteOrder = ReadByteOrder(stream);
            if (byteOrder == ByteOrder.BigEndian)
            {
                return new TiffBigEndianStream(stream);
            }
            else if (byteOrder == ByteOrder.LittleEndian)
            {
                return new TiffLittleEndianStream(stream);
            }

            throw TiffThrowHelper.InvalidHeader();
        }

        private static ByteOrder ReadByteOrder(Stream stream)
        {
            var headerBytes = new byte[2];
            stream.Read(headerBytes, 0, 2);
            if (headerBytes[0] == TiffConstants.ByteOrderLittleEndian && headerBytes[1] == TiffConstants.ByteOrderLittleEndian)
            {
                return ByteOrder.LittleEndian;
            }
            else if (headerBytes[0] == TiffConstants.ByteOrderBigEndian && headerBytes[1] == TiffConstants.ByteOrderBigEndian)
            {
                return ByteOrder.BigEndian;
            }

            throw TiffThrowHelper.InvalidHeader();
        }

        /// <summary>
        /// Decodes the image data from a specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="tags">The IFD tags.</param>
        /// <param name="frameMetaData">The frame metadata.</param>
        /// <returns>
        /// The tiff frame.
        /// </returns>
        private ImageFrame<TPixel> DecodeFrame<TPixel>(IExifValue[] tags, out TiffFrameMetadata frameMetaData)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var coreMetadata = new ImageFrameMetadata();
            frameMetaData = coreMetadata.GetTiffMetadata();
            frameMetaData.FrameTags.AddRange(tags);
            TiffFrameMetadata tiffFormatMetaData = coreMetadata.GetFormatMetadata(TiffFormat.Instance);
            TiffPredictor predictor = tiffFormatMetaData.Predictor;

            this.VerifyAndParseOptions(frameMetaData);

            int width = (int)frameMetaData.Width;
            int height = (int)frameMetaData.Height;
            var frame = new ImageFrame<TPixel>(this.configuration, width, height, coreMetadata);

            int rowsPerStrip = (int)frameMetaData.RowsPerStrip;
            uint[] stripOffsets = frameMetaData.StripOffsets;
            uint[] stripByteCounts = frameMetaData.StripByteCounts;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
            {
                this.DecodeStripsPlanar(frame, rowsPerStrip, stripOffsets, stripByteCounts, width, predictor);
            }
            else
            {
                this.DecodeStripsChunky(frame, rowsPerStrip, stripOffsets, stripByteCounts, width, predictor);
            }

            return frame;
        }

        /// <summary>
        /// Calculates the size (in bytes) for a pixel buffer using the determined color format.
        /// </summary>
        /// <param name="width">The width for the desired pixel buffer.</param>
        /// <param name="height">The height for the desired pixel buffer.</param>
        /// <param name="plane">The index of the plane for planar image configuration (or zero for chunky).</param>
        /// <returns>The size (in bytes) of the required pixel buffer.</returns>
        private int CalculateStripBufferSize(int width, int height, int plane = -1)
        {
            uint bitsPerPixel = 0;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
            {
                DebugGuard.IsTrue(plane == -1, "Excepted Chunky planar.");
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
            int stripBytes = bytesPerRow * height;

            return stripBytes;
        }

        /// <summary>
        /// Decodes the image data for strip encoded data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frame">The image frame to decode data into.</param>
        /// <param name="rowsPerStrip">The number of rows per strip of data.</param>
        /// <param name="stripOffsets">An array of byte offsets to each strip in the image.</param>
        /// <param name="stripByteCounts">An array of the size of each strip (in bytes).</param>
        /// <param name="width">The image width.</param>
        /// <param name="predictor">The tiff predictor used.</param>
        private void DecodeStripsPlanar<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts, int width, TiffPredictor predictor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int stripsPerPixel = this.BitsPerSample.Length;
            int stripsPerPlane = stripOffsets.Length / stripsPerPixel;
            int bitsPerPixel = 0;
            foreach (var bits in this.BitsPerSample)
            {
                bitsPerPixel += bits;
            }

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            var stripBuffers = new IManagedByteBuffer[stripsPerPixel];

            try
            {
                for (int stripIndex = 0; stripIndex < stripBuffers.Length; stripIndex++)
                {
                    int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip, stripIndex);
                    stripBuffers[stripIndex] = this.memoryAllocator.AllocateManagedByteBuffer(uncompressedStripSize);
                }

                TiffBaseCompression decompressor = TiffCompressionFactory.Create(this.CompressionType, this.memoryAllocator, this.PhotometricInterpretation, width, bitsPerPixel, predictor);

                RgbPlanarTiffColor<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.CreatePlanar(this.ColorType, this.BitsPerSample, this.ColorMap);

                for (int i = 0; i < stripsPerPlane; i++)
                {
                    int stripHeight = i < stripsPerPlane - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;

                    for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                    {
                        int stripIndex = (i * stripsPerPixel) + planeIndex;

                        decompressor.Decompress(this.inputStream, stripOffsets[stripIndex], stripByteCounts[stripIndex], stripBuffers[planeIndex].GetSpan());
                    }

                    colorDecoder.Decode(stripBuffers, pixels, 0, rowsPerStrip * i, frame.Width, stripHeight);
                }
            }
            finally
            {
                foreach (IManagedByteBuffer buf in stripBuffers)
                {
                    buf?.Dispose();
                }
            }
        }

        private void DecodeStripsChunky<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts, int width, TiffPredictor predictor)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip);
            int bitsPerPixel = 0;
            foreach (var bits in this.BitsPerSample)
            {
                bitsPerPixel += bits;
            }

            using IManagedByteBuffer stripBuffer = this.memoryAllocator.AllocateManagedByteBuffer(uncompressedStripSize, AllocationOptions.Clean);

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            TiffBaseCompression decompressor = TiffCompressionFactory.Create(this.CompressionType, this.memoryAllocator, this.PhotometricInterpretation, width, bitsPerPixel, predictor);

            TiffBaseColorDecoder<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.Create(this.ColorType, this.BitsPerSample, this.ColorMap);

            for (int stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
            {
                int stripHeight = stripIndex < stripOffsets.Length - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;

                decompressor.Decompress(this.inputStream, stripOffsets[stripIndex], stripByteCounts[stripIndex], stripBuffer.GetSpan());

                colorDecoder.Decode(stripBuffer.GetSpan(), pixels, 0, rowsPerStrip * stripIndex, frame.Width, stripHeight);
            }
        }
    }
}
