// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
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
    internal class TiffDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        private readonly bool ignoreMetadata;

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

            this.Configuration = configuration ?? Configuration.Default;
            this.ignoreMetadata = options.IgnoreMetadata;
            this.memoryAllocator = this.Configuration.MemoryAllocator;
        }

        /// <summary>
        /// Gets or sets the number of bits per component of the pixel format used to decode the image.
        /// </summary>
        public TiffBitsPerSample BitsPerSample { get; set; }

        /// <summary>
        /// Gets or sets the bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; set; }

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
        /// Gets or sets the Fax specific compression options.
        /// </summary>
        public FaxCompressionOptions FaxCompressionOptions { get; set; }

        /// <summary>
        /// Gets or sets the planar configuration type to use when decoding the image.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets or sets the horizontal predictor.
        /// </summary>
        public TiffPredictor Predictor { get; set; }

        /// <inheritdoc/>
        public Configuration Configuration { get; }

        /// <inheritdoc/>
        public Size Dimensions { get; private set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.inputStream = stream;
            var reader = new DirectoryReader(stream);

            IEnumerable<ExifProfile> directories = reader.Read();

            var frames = new List<ImageFrame<TPixel>>();
            foreach (ExifProfile ifd in directories)
            {
                ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd);
                frames.Add(frame);
            }

            ImageMetadata metadata = TiffDecoderMetadataCreator.Create(frames, this.ignoreMetadata, reader.ByteOrder);

            // TODO: Tiff frames can have different sizes
            ImageFrame<TPixel> root = frames[0];
            this.Dimensions = root.Size();
            foreach (ImageFrame<TPixel> frame in frames)
            {
                if (frame.Size() != root.Size())
                {
                    TiffThrowHelper.ThrowNotSupported("Images with different sizes are not supported");
                }
            }

            return new Image<TPixel>(this.Configuration, metadata, frames);
        }

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.inputStream = stream;
            var reader = new DirectoryReader(stream);
            IEnumerable<ExifProfile> directories = reader.Read();

            ExifProfile rootFrameExifProfile = directories.First();
            var rootMetadata = TiffFrameMetadata.Parse(rootFrameExifProfile);

            ImageMetadata metadata = TiffDecoderMetadataCreator.Create(reader.ByteOrder, rootFrameExifProfile);
            int width = GetImageWidth(rootFrameExifProfile);
            int height = GetImageHeight(rootFrameExifProfile);

            return new ImageInfo(new PixelTypeInfo((int)rootMetadata.BitsPerPixel), width, height, metadata);
        }

        /// <summary>
        /// Decodes the image data from a specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="tags">The IFD tags.</param>
        /// <returns>
        /// The tiff frame.
        /// </returns>
        private ImageFrame<TPixel> DecodeFrame<TPixel>(ExifProfile tags)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ImageFrameMetadata imageFrameMetaData = this.ignoreMetadata ?
                new ImageFrameMetadata() :
                new ImageFrameMetadata { ExifProfile = tags, XmpProfile = tags.GetValue(ExifTag.XMP)?.Value };

            TiffFrameMetadata tiffFrameMetaData = imageFrameMetaData.GetTiffMetadata();
            TiffFrameMetadata.Parse(tiffFrameMetaData, tags);

            this.VerifyAndParse(tags, tiffFrameMetaData);

            int width = GetImageWidth(tags);
            int height = GetImageHeight(tags);
            var frame = new ImageFrame<TPixel>(this.Configuration, width, height, imageFrameMetaData);

            int rowsPerStrip = tags.GetValue(ExifTag.RowsPerStrip) != null ? (int)tags.GetValue(ExifTag.RowsPerStrip).Value : TiffConstants.RowsPerStripInfinity;
            Number[] stripOffsets = tags.GetValue(ExifTag.StripOffsets)?.Value;
            Number[] stripByteCounts = tags.GetValue(ExifTag.StripByteCounts)?.Value;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
            {
                this.DecodeStripsPlanar(frame, rowsPerStrip, stripOffsets, stripByteCounts);
            }
            else
            {
                this.DecodeStripsChunky(frame, rowsPerStrip, stripOffsets, stripByteCounts);
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
            int bitsPerPixel;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
            {
                DebugGuard.IsTrue(plane == -1, "Expected Chunky planar.");
                bitsPerPixel = this.BitsPerPixel;
            }
            else
            {
                bitsPerPixel = this.BitsPerSample.Bits()[plane];
            }

            int bytesPerRow = ((width * bitsPerPixel) + 7) / 8;
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
        private void DecodeStripsPlanar<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, Number[] stripOffsets, Number[] stripByteCounts)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int stripsPerPixel = this.BitsPerSample.Bits().Length;
            int stripsPerPlane = stripOffsets.Length / stripsPerPixel;
            int bitsPerPixel = this.BitsPerPixel;

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            var stripBuffers = new IManagedByteBuffer[stripsPerPixel];

            try
            {
                for (int stripIndex = 0; stripIndex < stripBuffers.Length; stripIndex++)
                {
                    int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip, stripIndex);
                    stripBuffers[stripIndex] = this.memoryAllocator.AllocateManagedByteBuffer(uncompressedStripSize);
                }

                using TiffBaseDecompressor decompressor = TiffDecompressorsFactory.Create(this.CompressionType, this.memoryAllocator, this.PhotometricInterpretation, frame.Width, bitsPerPixel, this.Predictor, this.FaxCompressionOptions);

                RgbPlanarTiffColor<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.CreatePlanar(this.ColorType, this.BitsPerSample.Bits(), this.ColorMap);

                for (int i = 0; i < stripsPerPlane; i++)
                {
                    int stripHeight = i < stripsPerPlane - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;

                    for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                    {
                        int stripIndex = (i * stripsPerPixel) + planeIndex;

                        decompressor.Decompress(this.inputStream, (uint)stripOffsets[stripIndex], (uint)stripByteCounts[stripIndex], stripBuffers[planeIndex].GetSpan());
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

        private void DecodeStripsChunky<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, Number[] stripOffsets, Number[] stripByteCounts)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            // If the rowsPerStrip has the default value, which is effectively infinity. That is, the entire image is one strip.
            if (rowsPerStrip == TiffConstants.RowsPerStripInfinity)
            {
                rowsPerStrip = frame.Height;
            }

            int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip);
            int bitsPerPixel = this.BitsPerPixel;

            using IManagedByteBuffer stripBuffer = this.memoryAllocator.AllocateManagedByteBuffer(uncompressedStripSize, AllocationOptions.Clean);

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            using TiffBaseDecompressor decompressor = TiffDecompressorsFactory.Create(this.CompressionType, this.memoryAllocator, this.PhotometricInterpretation, frame.Width, bitsPerPixel, this.Predictor, this.FaxCompressionOptions);

            TiffBaseColorDecoder<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.Create(this.ColorType, this.BitsPerSample.Bits(), this.ColorMap);

            for (int stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
            {
                int stripHeight = stripIndex < stripOffsets.Length - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;
                int top = rowsPerStrip * stripIndex;
                if (top + stripHeight > frame.Height)
                {
                    // Make sure we ignore any strips that are not needed for the image (if too many are present)
                    break;
                }

                decompressor.Decompress(this.inputStream, (uint)stripOffsets[stripIndex], (uint)stripByteCounts[stripIndex], stripBuffer.GetSpan());

                colorDecoder.Decode(stripBuffer.GetSpan(), pixels, 0, top, frame.Width, stripHeight);
            }
        }

        /// <summary>
        /// Gets the width of the image frame.
        /// </summary>
        /// <param name="exifProfile">The image frame exif profile.</param>
        /// <returns>The image width.</returns>
        private static int GetImageWidth(ExifProfile exifProfile)
        {
            IExifValue<Number> width = exifProfile.GetValue(ExifTag.ImageWidth);
            if (width == null)
            {
                TiffThrowHelper.ThrowImageFormatException("The TIFF image frame is missing the ImageWidth");
            }

            return (int)width.Value;
        }

        /// <summary>
        /// Gets the height of the image frame.
        /// </summary>
        /// <param name="exifProfile">The image frame exif profile.</param>
        /// <returns>The image height.</returns>
        private static int GetImageHeight(ExifProfile exifProfile)
        {
            IExifValue<Number> height = exifProfile.GetValue(ExifTag.ImageLength);
            if (height == null)
            {
                TiffThrowHelper.ThrowImageFormatException("The TIFF image frame is missing the ImageLength");
            }

            return (int)height.Value;
        }
    }
}
