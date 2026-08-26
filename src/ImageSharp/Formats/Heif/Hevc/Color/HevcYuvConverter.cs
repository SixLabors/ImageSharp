// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Color;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Formats.Heif.Color.HeifColorConverterBase;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc.Color;

/// <summary>
/// Converts between reconstructed HEVC component planes and packed ImageSharp pixels.
/// </summary>
internal static partial class HevcYuvConverter
{
    /// <summary>
    /// The largest value represented by an eight-bit packed RGB component.
    /// </summary>
    private const float ByteMaximum = byte.MaxValue;

    /// <summary>
    /// The largest value represented by a 16-bit packed RGB component.
    /// </summary>
    private const float UShortMaximum = ushort.MaxValue;

    /// <summary>
    /// Gets the horizontal offset in half-luma-sample units for each HEVC 4:2:0 chroma-location code.
    /// </summary>
    private static ReadOnlySpan<byte> ChromaLocationX => [0, 1, 0, 1, 0, 1];

    /// <summary>
    /// Gets the vertical offset in half-luma-sample units for each HEVC 4:2:0 chroma-location code.
    /// </summary>
    private static ReadOnlySpan<byte> ChromaLocationY => [1, 1, 0, 0, 2, 2];

    /// <summary>
    /// Converts reconstructed HEVC component planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="picture">The reconstructed HEVC picture.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="colorProfile">The effective H.273 color description.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    public static void ConvertToRgb<TPixel>(
        Configuration configuration,
        HevcPictureBuffer picture,
        ImageFrame<TPixel> image,
        CicpProfile colorProfile,
        HevcChromaSampleLocation chromaSampleLocation,
        int sourceX = 0,
        int sourceY = 0)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(picture, colorProfile, out HeifColorConversionMode mode);

        // The common full-range eight-bit path remains in the integer sample domain. The codec adapter exposes
        // only native rows; shared HEIF code owns coefficient arithmetic, SIMD traversal, pooling, and packing.
        if (HeifYuv420ToRgb8Converter.IsSupported(
            picture.GetSubsamplingX(HevcPlane.Cb),
            picture.GetSubsamplingY(HevcPlane.Cb),
            picture.BitDepthLuma,
            picture.BitDepthChroma,
            colorProfile.FullRange,
            colorProfile.MatrixCoefficients,
            mode))
        {
            HevcYuv420ToRgb8Source source = new(picture);
            HeifYuv420ToRgb8Converter.Convert(configuration, source, image, in parameters, sourceX, sourceY);
            return;
        }

        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, picture.ChromaFormat == 0);
        YuvToRgbRowConverter<TPixel> converter = new(configuration, picture, image, colorConverter, chromaSampleLocation, sourceX, sourceY);
        using IMemoryOwner<float> scratchOwner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
        Span<float> scratch = scratchOwner.GetSpan();

        if (converter.UsesBytePacking)
        {
            for (int y = 0; y < image.Height; y++)
            {
                converter.Convert(y, scratch);
            }

            return;
        }

        for (int y = 0; y < image.Height; y++)
        {
            converter.Convert(y, scratch);
        }
    }

    /// <summary>
    /// Converts packed pixels to the configured HEVC component planes.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="picture">The destination HEVC picture.</param>
    /// <param name="colorProfile">The H.273 color description to encode.</param>
    /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
    public static void ConvertFromRgb<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        HevcPictureBuffer picture,
        CicpProfile colorProfile,
        HevcChromaSampleLocation chromaSampleLocation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifColorConversionParameters parameters = GetConversionParameters(picture, colorProfile, out HeifColorConversionMode mode);
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, picture.ChromaFormat == 0);
        RgbToYuvRowConverter<TPixel> converter = new(configuration, picture, image, colorConverter, chromaSampleLocation, in parameters);
        using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(converter.ComponentBufferLength);
        Span<float> components = componentOwner.GetSpan();

        if (converter.UsesByteInput)
        {
            converter.Convert(Span<Rgb48>.Empty, components);
            return;
        }

        using IMemoryOwner<Rgb48> packedOwner = configuration.MemoryAllocator.Allocate<Rgb48>(image.Width);
        converter.Convert(packedOwner.GetSpan()[..image.Width], components);
    }

    /// <summary>
    /// Resolves the shared H.273 conversion parameters for an HEVC picture.
    /// </summary>
    /// <param name="picture">The picture defining component precision and sampling.</param>
    /// <param name="colorProfile">The effective H.273 color description.</param>
    /// <param name="mode">The resolved color conversion operation.</param>
    /// <returns>The immutable scalar and SIMD conversion parameters.</returns>
    private static HeifColorConversionParameters GetConversionParameters(
        HevcPictureBuffer picture,
        CicpProfile colorProfile,
        out HeifColorConversionMode mode)
        => HeifColorConversionParameters.Create(
            colorProfile.ColorPrimaries,
            colorProfile.TransferCharacteristics,
            colorProfile.MatrixCoefficients,
            colorProfile.FullRange,
            picture.BitDepthLuma,
            picture.ChromaFormat == 0 ? picture.BitDepthLuma : picture.BitDepthChroma,
            picture.ChromaFormat == 0,
            picture.ChromaFormat == 3,
            out mode);

    /// <summary>
    /// Resolves the two chroma rows and quarter-sample weight surrounding a luma row.
    /// </summary>
    /// <param name="coordinate">The luma row coordinate.</param>
    /// <param name="subsampling">The vertical chroma subsampling shift.</param>
    /// <param name="position">The chroma offset in half-luma-sample units.</param>
    /// <param name="maximum">The last available chroma row.</param>
    /// <param name="lower">The lower chroma row.</param>
    /// <param name="upper">The upper chroma row.</param>
    /// <param name="upperWeight">The upper-row weight with a denominator of four.</param>
    private static void GetChromaCoordinates(
        int coordinate,
        int subsampling,
        int position,
        int maximum,
        out int lower,
        out int upper,
        out int upperWeight)
    {
        if (subsampling == 0)
        {
            lower = coordinate;
            upper = coordinate;
            upperWeight = 0;
            return;
        }

        // Multiplying the source coordinate by four keeps all six HEVC 4:2:0 locations integral. Floor division
        // is required above the top edge because the first centered sample lies after the first luma row.
        int quarterCoordinate = (coordinate * 2) - position;
        int unclampedLower = quarterCoordinate >= 0 ? quarterCoordinate >> 2 : -((-quarterCoordinate + 3) >> 2);
        int fraction = quarterCoordinate - (unclampedLower * 4);
        lower = Numerics.Clamp(unclampedLower, 0, maximum);
        upper = Numerics.Clamp(unclampedLower + 1, 0, maximum);
        upperWeight = lower == upper ? 0 : fraction;
    }

    /// <summary>
    /// Resolves the luma-relative chroma offsets for the selected plane layout.
    /// </summary>
    /// <param name="picture">The HEVC picture layout.</param>
    /// <param name="chromaSampleLocation">The signaled 4:2:0 location.</param>
    /// <param name="horizontalPosition">The horizontal offset in half-luma-sample units.</param>
    /// <param name="verticalPosition">The vertical offset in half-luma-sample units.</param>
    private static void GetChromaPosition(
        HevcPictureBuffer picture,
        HevcChromaSampleLocation chromaSampleLocation,
        out int horizontalPosition,
        out int verticalPosition)
    {
        if (picture.ChromaFormat != 1 || picture.SeparateColorPlane)
        {
            horizontalPosition = 0;
            verticalPosition = 0;
            return;
        }

        int location = (int)chromaSampleLocation;
        horizontalPosition = ChromaLocationX[location];
        verticalPosition = ChromaLocationY[location];
    }

    /// <summary>
    /// Converts one reconstructed HEVC row using pooled planar and packed-pixel storage.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    private readonly struct YuvToRgbRowConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The configuration used for packed-pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The reconstructed HEVC picture.
        /// </summary>
        private readonly HevcPictureBuffer picture;

        /// <summary>
        /// The destination image frame.
        /// </summary>
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// The selected H.273 color converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The horizontal chroma offset in half-luma-sample units.
        /// </summary>
        private readonly int horizontalPosition;

        /// <summary>
        /// The vertical chroma offset in half-luma-sample units.
        /// </summary>
        private readonly int verticalPosition;

        /// <summary>
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int subY;

        /// <summary>
        /// The horizontal luma-sample offset of the output window.
        /// </summary>
        private readonly int sourceX;

        /// <summary>
        /// The vertical luma-sample offset of the output window.
        /// </summary>
        private readonly int sourceY;

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbRowConverter{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="picture">The reconstructed HEVC picture.</param>
        /// <param name="image">The destination image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
        /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
        /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
        public YuvToRgbRowConverter(
            Configuration configuration,
            HevcPictureBuffer picture,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter,
            HevcChromaSampleLocation chromaSampleLocation,
            int sourceX,
            int sourceY)
        {
            this.configuration = configuration;
            this.picture = picture;
            this.image = image;
            this.colorConverter = colorConverter;
            this.subX = picture.GetSubsamplingX(HevcPlane.Cb);
            this.subY = picture.GetSubsamplingY(HevcPlane.Cb);
            this.sourceX = sourceX;
            this.sourceY = sourceY;
            GetChromaPosition(picture, chromaSampleLocation, out this.horizontalPosition, out this.verticalPosition);
        }

        /// <summary>
        /// Gets a value indicating whether the destination uses JPEG-compatible eight-bit RGB plane packing.
        /// </summary>
        public bool UsesBytePacking => this.picture.BitDepthLuma == 8 && (this.picture.ChromaFormat == 0 || this.picture.BitDepthChroma == 8);

        /// <summary>
        /// Gets the number of float elements required by the reusable row buffer.
        /// </summary>
        public int BufferLength
        {
            get
            {
                int componentLength = this.image.Width * 3;
                if (this.picture.ChromaFormat != 0 && this.subX != 0)
                {
                    // Cropped output can begin between subsampled chroma positions. Reconstructing one complete
                    // coded-width row preserves the edge interpolation before selecting the visible window.
                    componentLength += this.picture.Width + (this.picture.GetWidth(HevcPlane.Cb) * 2);
                }

                int packedRowCount = this.UsesBytePacking ? 1 : 2;
                return componentLength + (this.image.Width * packedRowCount);
            }
        }

        /// <summary>
        /// Converts one reconstructed row to packed pixels.
        /// </summary>
        /// <param name="y">The zero-based luma row.</param>
        /// <param name="scratch">The reusable pooled row buffer.</param>
        public void Convert(int y, Span<float> scratch)
        {
            int width = this.image.Width;
            Span<float> red = scratch[..width];
            Span<float> green = scratch.Slice(width, width);
            Span<float> blue = scratch.Slice(width * 2, width);
            int sourceY = y + this.sourceY;
            ReadOnlySpan<ushort> luma = this.picture.GetRowSpan(HevcPlane.Y, sourceY).Slice(this.sourceX, width);
            ConvertSamplesToFloat<ushort, HeifUShortSampleLoader>(luma, red);

            int packedOffset = width * 3;
            if (this.picture.ChromaFormat != 0)
            {
                int chromaHeight = this.picture.GetHeight(HevcPlane.Cb);
                GetChromaCoordinates(sourceY, this.subY, this.verticalPosition, chromaHeight - 1, out int y0, out int y1, out int y1Weight);
                ReadOnlySpan<ushort> cb0 = this.picture.GetRowSpan(HevcPlane.Cb, y0);
                ReadOnlySpan<ushort> cb1 = this.picture.GetRowSpan(HevcPlane.Cb, y1);
                ReadOnlySpan<ushort> cr0 = this.picture.GetRowSpan(HevcPlane.Cr, y0);
                ReadOnlySpan<ushort> cr1 = this.picture.GetRowSpan(HevcPlane.Cr, y1);
                if (this.subX == 0)
                {
                    ConvertSamplesToFloat<ushort, HeifUShortSampleLoader>(cb0.Slice(this.sourceX, width), green);
                    ConvertSamplesToFloat<ushort, HeifUShortSampleLoader>(cr0.Slice(this.sourceX, width), blue);
                }
                else
                {
                    int chromaWidth = this.picture.GetWidth(HevcPlane.Cb);
                    Span<float> reconstructed = scratch.Slice(packedOffset, this.picture.Width);
                    Span<float> chroma0 = scratch.Slice(packedOffset + this.picture.Width, chromaWidth);
                    Span<float> chroma1 = scratch.Slice(packedOffset + this.picture.Width + chromaWidth, chromaWidth);
                    bool isCenteredX = this.horizontalPosition == 1;

                    ReconstructChromaRow<ushort, HeifUShortSampleLoader>(
                        cb0,
                        cb1,
                        y1Weight,
                        this.subX,
                        isCenteredX,
                        reconstructed,
                        chroma0,
                        chroma1);

                    reconstructed.Slice(this.sourceX, width).CopyTo(green);
                    ReconstructChromaRow<ushort, HeifUShortSampleLoader>(
                        cr0,
                        cr1,
                        y1Weight,
                        this.subX,
                        isCenteredX,
                        reconstructed,
                        chroma0,
                        chroma1);

                    reconstructed.Slice(this.sourceX, width).CopyTo(blue);
                    packedOffset += this.picture.Width + (chromaWidth * 2);
                }
            }

            this.colorConverter.ConvertToRgbInPlace(red, green, blue);
            Span<TPixel> destination = this.image.PixelBuffer.DangerousGetRowSpan(y);
            Span<float> packedStorage = scratch[packedOffset..];
            if (this.UsesBytePacking)
            {
                // This is JPEG's planar packing contract. Existing pixel-specific SIMD packers therefore own the
                // final RGB-to-TPixel conversion instead of a HEVC-specific per-pixel loop.
                Span<byte> byteStorage = MemoryMarshal.AsBytes(packedStorage)[..(width * 3)];
                Span<byte> redBytes = byteStorage[..width];
                Span<byte> greenBytes = byteStorage.Slice(width, width);
                Span<byte> blueBytes = byteStorage.Slice(width * 2, width);
                SimdUtils.NormalizedFloatToByteSaturate(red, redBytes);
                SimdUtils.NormalizedFloatToByteSaturate(green, greenBytes);
                SimdUtils.NormalizedFloatToByteSaturate(blue, blueBytes);

                PixelOperations<TPixel>.Instance.PackFromRgbPlanes(redBytes, greenBytes, blueBytes, destination);

                return;
            }

            Span<Rgba64> packed = MemoryMarshal.Cast<float, Rgba64>(packedStorage)[..width];
            PackRgba64(red, green, blue, packed);
            PixelOperations<TPixel>.Instance.FromRgba64(this.configuration, packed, destination);
        }
    }

    /// <summary>
    /// Converts packed image rows to HEVC component planes using pooled planar storage.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    private readonly struct RgbToYuvRowConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The configuration used for packed-pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The destination HEVC picture.
        /// </summary>
        private readonly HevcPictureBuffer picture;

        /// <summary>
        /// The source image frame.
        /// </summary>
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// The selected H.273 color converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The largest encoded luma sample value.
        /// </summary>
        private readonly float lumaMaximum;

        /// <summary>
        /// The largest encoded chroma sample value.
        /// </summary>
        private readonly float chromaMaximum;

        /// <summary>
        /// The horizontal chroma offset in half-luma-sample units.
        /// </summary>
        private readonly int horizontalPosition;

        /// <summary>
        /// The vertical chroma offset in half-luma-sample units.
        /// </summary>
        private readonly int verticalPosition;

        /// <summary>
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int subY;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbToYuvRowConverter{TPixel}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="picture">The destination HEVC picture.</param>
        /// <param name="image">The source image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="chromaSampleLocation">The progressive-frame 4:2:0 chroma sample location.</param>
        /// <param name="parameters">The resolved H.273 component ranges.</param>
        public RgbToYuvRowConverter(
            Configuration configuration,
            HevcPictureBuffer picture,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter,
            HevcChromaSampleLocation chromaSampleLocation,
            in HeifColorConversionParameters parameters)
        {
            this.configuration = configuration;
            this.picture = picture;
            this.image = image;
            this.colorConverter = colorConverter;
            this.lumaMaximum = parameters.LumaSampleMaximum;
            this.chromaMaximum = parameters.ChromaSampleMaximum;
            this.subX = picture.GetSubsamplingX(HevcPlane.Cb);
            this.subY = picture.GetSubsamplingY(HevcPlane.Cb);
            GetChromaPosition(picture, chromaSampleLocation, out this.horizontalPosition, out this.verticalPosition);
        }

        /// <summary>
        /// Gets a value indicating whether source pixels use JPEG-compatible eight-bit RGB plane unpacking.
        /// </summary>
        public bool UsesByteInput => this.picture.BitDepthLuma == 8 && (this.picture.ChromaFormat == 0 || this.picture.BitDepthChroma == 8);

        /// <summary>
        /// Gets the number of float elements required by the reusable component buffer.
        /// </summary>
        public int ComponentBufferLength => this.image.Width * (this.subY == 0 ? 3 : 6);

        /// <summary>
        /// Converts every packed source row to the destination component planes.
        /// </summary>
        /// <param name="packed">The reusable high-bit-depth RGB staging row.</param>
        /// <param name="components">The reusable planar component buffer.</param>
        public void Convert(Span<Rgb48> packed, Span<float> components)
        {
            int width = this.image.Width;
            for (int y = 0; y < this.image.Height; y++)
            {
                int slot = this.subY == 0 ? 0 : y & 1;
                int offset = slot * width * 3;
                Span<float> luma = components.Slice(offset, width);
                Span<float> chromaBlue = components.Slice(offset + width, width);
                Span<float> chromaRed = components.Slice(offset + (width * 2), width);
                this.ConvertSourceRow(y, packed, luma, chromaBlue, chromaRed);
                WriteSamples<ushort, HeifUShortSampleStorer>(
                    luma,
                    this.picture.GetRowSpan(HevcPlane.Y, y),
                    this.colorConverter.LumaScale,
                    this.colorConverter.LumaBias,
                    this.lumaMaximum);

                if (this.picture.ChromaFormat == 0)
                {
                    continue;
                }

                if (this.subY == 0)
                {
                    this.WriteChromaRows(y, chromaBlue, chromaRed, Span<float>.Empty, Span<float>.Empty, 0F);
                    continue;
                }

                bool isLastRow = y == this.image.Height - 1;
                if (this.verticalPosition == 0 && (y & 1) == 0)
                {
                    this.WriteChromaRows(y >> 1, chromaBlue, chromaRed, Span<float>.Empty, Span<float>.Empty, 0F);
                }
                else if (this.verticalPosition == 1 && ((y & 1) != 0 || isLastRow))
                {
                    if ((y & 1) != 0)
                    {
                        Span<float> previousBlue = components.Slice(width, width);
                        Span<float> previousRed = components.Slice(width * 2, width);
                        this.WriteChromaRows(y >> 1, previousBlue, previousRed, chromaBlue, chromaRed, 0.5F);
                    }
                    else
                    {
                        this.WriteChromaRows(y >> 1, chromaBlue, chromaRed, Span<float>.Empty, Span<float>.Empty, 0F);
                    }
                }
                else if (this.verticalPosition == 2 && ((y & 1) != 0 || isLastRow))
                {
                    this.WriteChromaRows(y >> 1, chromaBlue, chromaRed, Span<float>.Empty, Span<float>.Empty, 0F);
                }
            }
        }

        /// <summary>
        /// Converts one packed source row into normalized planar components.
        /// </summary>
        /// <param name="y">The source row index.</param>
        /// <param name="packed">The high-bit-depth RGB staging row.</param>
        /// <param name="luma">The destination luma or first component values.</param>
        /// <param name="chromaBlue">The destination blue-difference or second component values.</param>
        /// <param name="chromaRed">The destination red-difference or third component values.</param>
        private void ConvertSourceRow(int y, Span<Rgb48> packed, Span<float> luma, Span<float> chromaBlue, Span<float> chromaRed)
        {
            ReadOnlySpan<TPixel> source = this.image.PixelBuffer.DangerousGetRowSpan(y);
            if (this.UsesByteInput)
            {
                // JPEG's planar unpack contract reaches the existing pixel-specific SIMD implementation before
                // the closed H.273 operator transforms the rows in place.
                PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(luma, chromaBlue, chromaRed, source);
                this.colorConverter.ConvertFromRgbInPlace(luma, chromaBlue, chromaRed, ByteMaximum);
                return;
            }

            PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, source, packed);
            DeinterleaveRgb48(packed, luma, chromaBlue, chromaRed);
            this.colorConverter.ConvertFromRgbInPlace(luma, chromaBlue, chromaRed, UShortMaximum);
        }

        /// <summary>
        /// Filters and writes one pair of chroma component rows.
        /// </summary>
        /// <param name="destinationY">The destination chroma row.</param>
        /// <param name="blue0">The first blue-difference source row.</param>
        /// <param name="red0">The first red-difference source row.</param>
        /// <param name="blue1">The optional second blue-difference source row.</param>
        /// <param name="red1">The optional second red-difference source row.</param>
        /// <param name="row1Weight">The second-row contribution.</param>
        private void WriteChromaRows(
            int destinationY,
            ReadOnlySpan<float> blue0,
            ReadOnlySpan<float> red0,
            ReadOnlySpan<float> blue1,
            ReadOnlySpan<float> red1,
            float row1Weight)
        {
            Span<ushort> blueDestination = this.picture.GetRowSpan(HevcPlane.Cb, destinationY);
            Span<ushort> redDestination = this.picture.GetRowSpan(HevcPlane.Cr, destinationY);
            if (this.subX == 0)
            {
                WriteSamples<ushort, HeifUShortSampleStorer>(
                    blue0,
                    blueDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                WriteSamples<ushort, HeifUShortSampleStorer>(
                    red0,
                    redDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                return;
            }

            bool isCenteredX = this.horizontalPosition == 1;
            WriteSubsampledSamples<ushort, HeifUShortSampleStorer>(
                blue0,
                blue1,
                blueDestination,
                isCenteredX,
                row1Weight,
                this.colorConverter.ChromaScale,
                this.colorConverter.ChromaBias,
                this.chromaMaximum);

            WriteSubsampledSamples<ushort, HeifUShortSampleStorer>(
                red0,
                red1,
                redDestination,
                isCenteredX,
                row1Weight,
                this.colorConverter.ChromaScale,
                this.colorConverter.ChromaBias,
                this.chromaMaximum);
        }
    }
}
