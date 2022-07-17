// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
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
        /// Gets the decoding mode for multi-frame images
        /// </summary>
        private readonly FrameDecodingMode decodingMode;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private BufferedReadStream inputStream;

        /// <summary>
        /// Indicates the byte order of the stream.
        /// </summary>
        private ByteOrder byteOrder;

        /// <summary>
        /// Indicating whether is BigTiff format.
        /// </summary>
        private bool isBigTiff;

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
            this.decodingMode = options.DecodingMode;
            this.memoryAllocator = this.Configuration.MemoryAllocator;
        }

        /// <summary>
        /// Gets or sets the bits per sample.
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
        /// Gets or sets the reference black and white for decoding YCbCr pixel data.
        /// </summary>
        public Rational[] ReferenceBlackAndWhite { get; set; }

        /// <summary>
        /// Gets or sets the YCbCr coefficients.
        /// </summary>
        public Rational[] YcbcrCoefficients { get; set; }

        /// <summary>
        /// Gets or sets the YCbCr sub sampling.
        /// </summary>
        public ushort[] YcbcrSubSampling { get; set; }

        /// <summary>
        /// Gets or sets the compression used, when the image was encoded.
        /// </summary>
        public TiffDecoderCompressionType CompressionType { get; set; }

        /// <summary>
        /// Gets or sets the Fax specific compression options.
        /// </summary>
        public FaxCompressionOptions FaxCompressionOptions { get; set; }

        /// <summary>
        /// Gets or sets the the logical order of bits within a byte.
        /// </summary>
        public TiffFillOrder FillOrder { get; set; }

        /// <summary>
        /// Gets or sets the extra samples type.
        /// </summary>
        public TiffExtraSampleType? ExtraSamplesType { get; set; }

        /// <summary>
        /// Gets or sets the JPEG tables when jpeg compression is used.
        /// </summary>
        public byte[] JpegTables { get; set; }

        /// <summary>
        /// Gets or sets the planar configuration type to use when decoding the image.
        /// </summary>
        public TiffPlanarConfiguration PlanarConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the photometric interpretation.
        /// </summary>
        public TiffPhotometricInterpretation PhotometricInterpretation { get; set; }

        /// <summary>
        /// Gets or sets the sample format.
        /// </summary>
        public TiffSampleFormat SampleFormat { get; set; }

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
            var frames = new List<ImageFrame<TPixel>>();
            try
            {
                this.inputStream = stream;
                var reader = new DirectoryReader(stream, this.Configuration.MemoryAllocator);

                IEnumerable<ExifProfile> directories = reader.Read();
                this.byteOrder = reader.ByteOrder;
                this.isBigTiff = reader.IsBigTiff;

                foreach (ExifProfile ifd in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(ifd, cancellationToken);
                    frames.Add(frame);

                    if (this.decodingMode is FrameDecodingMode.First)
                    {
                        break;
                    }
                }

                ImageMetadata metadata = TiffDecoderMetadataCreator.Create(frames, this.ignoreMetadata, reader.ByteOrder, reader.IsBigTiff);

                // TODO: Tiff frames can have different sizes.
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
            catch
            {
                foreach (ImageFrame<TPixel> f in frames)
                {
                    f.Dispose();
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            this.inputStream = stream;
            var reader = new DirectoryReader(stream, this.Configuration.MemoryAllocator);
            IEnumerable<ExifProfile> directories = reader.Read();

            ExifProfile rootFrameExifProfile = directories.First();
            var rootMetadata = TiffFrameMetadata.Parse(rootFrameExifProfile);

            ImageMetadata metadata = TiffDecoderMetadataCreator.Create(reader.ByteOrder, reader.IsBigTiff, rootFrameExifProfile);
            int width = GetImageWidth(rootFrameExifProfile);
            int height = GetImageHeight(rootFrameExifProfile);

            return new ImageInfo(new PixelTypeInfo((int)rootMetadata.BitsPerPixel), width, height, metadata);
        }

        /// <summary>
        /// Decodes the image data from a specified IFD.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="tags">The IFD tags.</param>
        /// <param name="cancellationToken">The token to monitor cancellation.</param>
        /// <returns>The tiff frame.</returns>
        private ImageFrame<TPixel> DecodeFrame<TPixel>(ExifProfile tags, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var imageFrameMetaData = new ImageFrameMetadata();
            if (!this.ignoreMetadata)
            {
                imageFrameMetaData.ExifProfile = tags;
            }

            TiffFrameMetadata tiffFrameMetaData = imageFrameMetaData.GetTiffMetadata();
            TiffFrameMetadata.Parse(tiffFrameMetaData, tags);

            this.VerifyAndParse(tags, tiffFrameMetaData);

            int width = GetImageWidth(tags);
            int height = GetImageHeight(tags);
            var frame = new ImageFrame<TPixel>(this.Configuration, width, height, imageFrameMetaData);

            int rowsPerStrip = tags.GetValue(ExifTag.RowsPerStrip) != null ? (int)tags.GetValue(ExifTag.RowsPerStrip).Value : TiffConstants.RowsPerStripInfinity;

            var stripOffsetsArray = (Array)tags.GetValueInternal(ExifTag.StripOffsets).GetValue();
            var stripByteCountsArray = (Array)tags.GetValueInternal(ExifTag.StripByteCounts).GetValue();

            using IMemoryOwner<ulong> stripOffsetsMemory = this.ConvertNumbers(stripOffsetsArray, out Span<ulong> stripOffsets);
            using IMemoryOwner<ulong> stripByteCountsMemory = this.ConvertNumbers(stripByteCountsArray, out Span<ulong> stripByteCounts);

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Planar)
            {
                this.DecodeStripsPlanar(
                    frame,
                    rowsPerStrip,
                    stripOffsets,
                    stripByteCounts,
                    cancellationToken);
            }
            else
            {
                this.DecodeStripsChunky(
                    frame,
                    rowsPerStrip,
                    stripOffsets,
                    stripByteCounts,
                    cancellationToken);
            }

            return frame;
        }

        private IMemoryOwner<ulong> ConvertNumbers(Array array, out Span<ulong> span)
        {
            if (array is Number[] numbers)
            {
                IMemoryOwner<ulong> memory = this.memoryAllocator.Allocate<ulong>(numbers.Length);
                span = memory.GetSpan();
                for (int i = 0; i < numbers.Length; i++)
                {
                    span[i] = (uint)numbers[i];
                }

                return memory;
            }

            DebugGuard.IsTrue(array is ulong[], $"Expected {nameof(UInt64)} array.");
            span = (ulong[])array;
            return null;
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
            DebugGuard.MustBeLessThanOrEqualTo(plane, 3, nameof(plane));

            int bitsPerPixel = 0;

            if (this.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
            {
                DebugGuard.IsTrue(plane == -1, "Expected Chunky planar.");
                bitsPerPixel = this.BitsPerPixel;
            }
            else
            {
                switch (plane)
                {
                    case 0:
                        bitsPerPixel = this.BitsPerSample.Channel0;
                        break;
                    case 1:
                        bitsPerPixel = this.BitsPerSample.Channel1;
                        break;
                    case 2:
                        bitsPerPixel = this.BitsPerSample.Channel2;
                        break;
                    case 3:
                        bitsPerPixel = this.BitsPerSample.Channel2;
                        break;
                    default:
                        TiffThrowHelper.ThrowNotSupported("More then 4 color channels are not supported");
                        break;
                }
            }

            int bytesPerRow = ((width * bitsPerPixel) + 7) / 8;
            return bytesPerRow * height;
        }

        /// <summary>
        /// Decodes the image data for planar encoded pixel data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frame">The image frame to decode data into.</param>
        /// <param name="rowsPerStrip">The number of rows per strip of data.</param>
        /// <param name="stripOffsets">An array of byte offsets to each strip in the image.</param>
        /// <param name="stripByteCounts">An array of the size of each strip (in bytes).</param>
        /// <param name="cancellationToken">The token to monitor cancellation.</param>
        private void DecodeStripsPlanar<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, Span<ulong> stripOffsets, Span<ulong> stripByteCounts, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int stripsPerPixel = this.BitsPerSample.Channels;
            int stripsPerPlane = stripOffsets.Length / stripsPerPixel;
            int bitsPerPixel = this.BitsPerPixel;

            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            var stripBuffers = new IMemoryOwner<byte>[stripsPerPixel];

            try
            {
                for (int stripIndex = 0; stripIndex < stripBuffers.Length; stripIndex++)
                {
                    int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip, stripIndex);
                    stripBuffers[stripIndex] = this.memoryAllocator.Allocate<byte>(uncompressedStripSize);
                }

                using TiffBaseDecompressor decompressor = TiffDecompressorsFactory.Create(
                    this.Configuration,
                    this.CompressionType,
                    this.memoryAllocator,
                    this.PhotometricInterpretation,
                    frame.Width,
                    bitsPerPixel,
                    this.ColorType,
                    this.Predictor,
                    this.FaxCompressionOptions,
                    this.JpegTables,
                    this.FillOrder,
                    this.byteOrder);

                TiffBasePlanarColorDecoder<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.CreatePlanar(
                    this.ColorType,
                    this.BitsPerSample,
                    this.ExtraSamplesType,
                    this.ColorMap,
                    this.ReferenceBlackAndWhite,
                    this.YcbcrCoefficients,
                    this.YcbcrSubSampling,
                    this.byteOrder);

                for (int i = 0; i < stripsPerPlane; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int stripHeight = i < stripsPerPlane - 1 || frame.Height % rowsPerStrip == 0 ? rowsPerStrip : frame.Height % rowsPerStrip;

                    int stripIndex = i;
                    for (int planeIndex = 0; planeIndex < stripsPerPixel; planeIndex++)
                    {
                        decompressor.Decompress(
                            this.inputStream,
                            stripOffsets[stripIndex],
                            stripByteCounts[stripIndex],
                            stripHeight,
                            stripBuffers[planeIndex].GetSpan());

                        stripIndex += stripsPerPlane;
                    }

                    colorDecoder.Decode(stripBuffers, pixels, 0, rowsPerStrip * i, frame.Width, stripHeight);
                }
            }
            finally
            {
                foreach (IMemoryOwner<byte> buf in stripBuffers)
                {
                    buf?.Dispose();
                }
            }
        }

        /// <summary>
        /// Decodes the image data for chunky encoded pixel data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frame">The image frame to decode data into.</param>
        /// <param name="rowsPerStrip">The rows per strip.</param>
        /// <param name="stripOffsets">The strip offsets.</param>
        /// <param name="stripByteCounts">The strip byte counts.</param>
        /// <param name="cancellationToken">The token to monitor cancellation.</param>
        private void DecodeStripsChunky<TPixel>(ImageFrame<TPixel> frame, int rowsPerStrip, Span<ulong> stripOffsets, Span<ulong> stripByteCounts, CancellationToken cancellationToken)
           where TPixel : unmanaged, IPixel<TPixel>
        {
            // If the rowsPerStrip has the default value, which is effectively infinity. That is, the entire image is one strip.
            if (rowsPerStrip == TiffConstants.RowsPerStripInfinity)
            {
                rowsPerStrip = frame.Height;
            }

            int uncompressedStripSize = this.CalculateStripBufferSize(frame.Width, rowsPerStrip);
            int bitsPerPixel = this.BitsPerPixel;

            using IMemoryOwner<byte> stripBuffer = this.memoryAllocator.Allocate<byte>(uncompressedStripSize, AllocationOptions.Clean);
            Span<byte> stripBufferSpan = stripBuffer.GetSpan();
            Buffer2D<TPixel> pixels = frame.PixelBuffer;

            using TiffBaseDecompressor decompressor = TiffDecompressorsFactory.Create(
                this.Configuration,
                this.CompressionType,
                this.memoryAllocator,
                this.PhotometricInterpretation,
                frame.Width,
                bitsPerPixel,
                this.ColorType,
                this.Predictor,
                this.FaxCompressionOptions,
                this.JpegTables,
                this.FillOrder,
                this.byteOrder);

            TiffBaseColorDecoder<TPixel> colorDecoder = TiffColorDecoderFactory<TPixel>.Create(
                this.Configuration,
                this.memoryAllocator,
                this.ColorType,
                this.BitsPerSample,
                this.ExtraSamplesType,
                this.ColorMap,
                this.ReferenceBlackAndWhite,
                this.YcbcrCoefficients,
                this.YcbcrSubSampling,
                this.byteOrder);

            for (int stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int stripHeight = stripIndex < stripOffsets.Length - 1 || frame.Height % rowsPerStrip == 0
                    ? rowsPerStrip
                    : frame.Height % rowsPerStrip;

                int top = rowsPerStrip * stripIndex;
                if (top + stripHeight > frame.Height)
                {
                    // Make sure we ignore any strips that are not needed for the image (if too many are present).
                    break;
                }

                decompressor.Decompress(
                    this.inputStream,
                    stripOffsets[stripIndex],
                    stripByteCounts[stripIndex],
                    stripHeight,
                    stripBufferSpan);

                colorDecoder.Decode(stripBufferSpan, pixels, 0, top, frame.Width, stripHeight);
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

            DebugGuard.MustBeLessThanOrEqualTo((ulong)width.Value, (ulong)int.MaxValue, nameof(ExifTag.ImageWidth));

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
