// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Formats.Heif.Components.HeifColorConverterBase;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Converts between native HEIF component planes and packed ImageSharp pixels.
/// </summary>
internal static class HeifPlanarColorConverter
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
    /// Converts native unsigned 16-bit component storage to packed pixels and selects eligible exact integer kernels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The native component-plane buffer.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    public static void ConvertToRgb<TPixel, TBuffer>(
        Configuration configuration,
        TBuffer buffer,
        ImageFrame<TPixel> image,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode,
        int sourceX = 0,
        int sourceY = 0)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<ushort>
    {
        if (HeifYuv420ToRgb8Converter.IsSupported(
            buffer.ChromaSubsamplingX,
            buffer.ChromaSubsamplingY,
            buffer.LumaBitDepth,
            buffer.ChromaBitDepth,
            parameters.IsFullRange,
            parameters.MatrixCoefficients,
            mode))
        {
            // The fixed-point operator preserves the exact code-value rounding used by the verified eight-bit
            // presentation path. Selection belongs here so no codec can acquire a private color-conversion route.
            HeifYuv420ToRgb8Converter.Convert(configuration, buffer, image, in parameters, sourceX, sourceY);
            return;
        }

        ConvertToRgb<TPixel, TBuffer, ushort, HeifUShortSampleLoader>(
            configuration,
            buffer,
            image,
            in parameters,
            mode,
            sourceX,
            sourceY);
    }

    /// <summary>
    /// Converts native component planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The native component-plane buffer.</param>
    /// <param name="image">The destination image frame.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    public static void ConvertToRgb<TPixel, TBuffer, TSample, TLoader>(
        Configuration configuration,
        TBuffer buffer,
        ImageFrame<TPixel> image,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode,
        int sourceX = 0,
        int sourceY = 0)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleLoader<TSample>
    {
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, buffer.IsMonochrome);
        YuvToRgbRowConverter<TPixel, TBuffer, TSample, TLoader> converter = new(
            configuration,
            buffer,
            image,
            colorConverter,
            sourceX,
            sourceY);

        using IMemoryOwner<float> scratchOwner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
        Span<float> scratch = scratchOwner.GetSpan();
        for (int y = 0; y < image.Height; y++)
        {
            converter.Convert(y, scratch);
        }
    }

    /// <summary>
    /// Converts packed pixels to native component planes.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="buffer">The destination component-plane buffer.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    public static void ConvertFromRgb<TPixel, TBuffer, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        TBuffer buffer,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleStorer<TSample>
    {
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, buffer.IsMonochrome);
        RgbToYuvRowConverter<TPixel, TBuffer, TSample, TStorer> converter = new(
            configuration,
            buffer,
            image,
            colorConverter,
            in parameters);

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

        // Multiplication by two expresses the luma coordinate in half-sample units, while division by four
        // addresses the subsampled plane. Floor division is required before the first centered chroma sample.
        int quarterCoordinate = (coordinate * 2) - position;
        int unclampedLower = quarterCoordinate >= 0 ? quarterCoordinate >> 2 : -((-quarterCoordinate + 3) >> 2);
        int fraction = quarterCoordinate - (unclampedLower * 4);
        lower = Numerics.Clamp(unclampedLower, 0, maximum);
        upper = Numerics.Clamp(unclampedLower + 1, 0, maximum);
        upperWeight = lower == upper ? 0 : fraction;
    }

    /// <summary>
    /// Converts one native component row using pooled planar and packed-pixel storage.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    private struct YuvToRgbRowConverter<TPixel, TBuffer, TSample, TLoader>
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleLoader<TSample>
    {
        /// <summary>
        /// The configuration used for packed-pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The codec adapter exposing the reconstructed component planes.
        /// </summary>
        private TBuffer buffer;

        /// <summary>
        /// The destination image frame.
        /// </summary>
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// The selected H.273 color converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The horizontal luma-sample offset of the output window.
        /// </summary>
        private readonly int sourceX;

        /// <summary>
        /// The vertical luma-sample offset of the output window.
        /// </summary>
        private readonly int sourceY;

        /// <summary>
        /// The full coded luma-plane width.
        /// </summary>
        private readonly int bufferWidth;

        /// <summary>
        /// The chroma-plane width in samples.
        /// </summary>
        private readonly int chromaWidth;

        /// <summary>
        /// The chroma-plane height in samples.
        /// </summary>
        private readonly int chromaHeight;

        /// <summary>
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subsamplingX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int subsamplingY;

        /// <summary>
        /// The horizontal chroma position in half-luma-sample units.
        /// </summary>
        private readonly int chromaPositionX;

        /// <summary>
        /// The vertical chroma position in half-luma-sample units.
        /// </summary>
        private readonly int chromaPositionY;

        /// <summary>
        /// Whether the buffer contains only the luma plane.
        /// </summary>
        private readonly bool isMonochrome;

        /// <summary>
        /// Whether the destination uses JPEG-compatible eight-bit RGB plane packing.
        /// </summary>
        private readonly bool usesBytePacking;

        /// <summary>
        /// Whether chroma reconstruction must retain the complete coded row before selecting the output window.
        /// </summary>
        private readonly bool reconstructCompleteRow;

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbRowConverter{TPixel, TBuffer, TSample, TLoader}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="buffer">The codec adapter exposing the reconstructed component planes.</param>
        /// <param name="image">The destination image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
        /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
        public YuvToRgbRowConverter(
            Configuration configuration,
            TBuffer buffer,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter,
            int sourceX,
            int sourceY)
        {
            this.configuration = configuration;
            this.buffer = buffer;
            this.image = image;
            this.colorConverter = colorConverter;
            this.sourceX = sourceX;
            this.sourceY = sourceY;
            this.bufferWidth = buffer.Width;
            this.subsamplingX = buffer.ChromaSubsamplingX;
            this.subsamplingY = buffer.ChromaSubsamplingY;
            this.chromaPositionX = buffer.ChromaPositionX;
            this.chromaPositionY = buffer.ChromaPositionY;
            this.isMonochrome = buffer.IsMonochrome;
            this.usesBytePacking = buffer.LumaBitDepth == 8 && (buffer.IsMonochrome || buffer.ChromaBitDepth == 8);
            this.chromaWidth = (buffer.Width + (1 << this.subsamplingX) - 1) >> this.subsamplingX;
            this.chromaHeight = (buffer.Height + (1 << this.subsamplingY) - 1) >> this.subsamplingY;
            this.reconstructCompleteRow = sourceX != 0 || image.Width != buffer.Width;
        }

        /// <summary>
        /// Gets a value indicating whether the destination uses JPEG-compatible eight-bit RGB plane packing.
        /// </summary>
        public readonly bool UsesBytePacking => this.usesBytePacking;

        /// <summary>
        /// Gets the number of float elements required by the reusable row buffer.
        /// </summary>
        public readonly int BufferLength
        {
            get
            {
                int componentLength = this.image.Width * 3;
                if (!this.isMonochrome && this.subsamplingX != 0)
                {
                    // Full-image conversion reconstructs directly into the component rows. Cropped conversion
                    // retains the complete coded row so interpolation phase is preserved at the window boundary.
                    componentLength += (this.chromaWidth * 2) + (this.reconstructCompleteRow ? this.bufferWidth : 0);
                }

                int packedRowCount = this.UsesBytePacking ? 1 : 2;
                return componentLength + (this.image.Width * packedRowCount);
            }
        }

        /// <summary>
        /// Converts one reconstructed row to packed pixels.
        /// </summary>
        /// <param name="y">The zero-based output row.</param>
        /// <param name="scratch">The reusable pooled row buffer.</param>
        public void Convert(int y, Span<float> scratch)
        {
            int width = this.image.Width;
            Span<float> red = scratch[..width];
            Span<float> green = scratch.Slice(width, width);
            Span<float> blue = scratch.Slice(width * 2, width);
            int sourceY = y + this.sourceY;
            ReadOnlySpan<TSample> luma = this.buffer.GetLumaRowSpan(sourceY).Slice(this.sourceX, width);
            ConvertSamplesToFloat<TSample, TLoader>(luma, red);

            int packedOffset = width * 3;
            if (!this.isMonochrome)
            {
                GetChromaCoordinates(
                    sourceY,
                    this.subsamplingY,
                    this.chromaPositionY,
                    this.chromaHeight - 1,
                    out int y0,
                    out int y1,
                    out int y1Weight);

                ReadOnlySpan<TSample> cb0 = this.buffer.GetChromaBlueRowSpan(y0);
                ReadOnlySpan<TSample> cb1 = this.buffer.GetChromaBlueRowSpan(y1);
                ReadOnlySpan<TSample> cr0 = this.buffer.GetChromaRedRowSpan(y0);
                ReadOnlySpan<TSample> cr1 = this.buffer.GetChromaRedRowSpan(y1);
                if (this.subsamplingX == 0)
                {
                    ConvertSamplesToFloat<TSample, TLoader>(cb0.Slice(this.sourceX, width), green);
                    ConvertSamplesToFloat<TSample, TLoader>(cr0.Slice(this.sourceX, width), blue);
                }
                else
                {
                    Span<float> chroma0 = scratch.Slice(packedOffset, this.chromaWidth);
                    Span<float> chroma1 = scratch.Slice(packedOffset + this.chromaWidth, this.chromaWidth);
                    Span<float> reconstructed = this.reconstructCompleteRow
                        ? scratch.Slice(packedOffset + (this.chromaWidth * 2), this.bufferWidth)
                        : green;

                    bool isCenteredX = this.chromaPositionX == 1;

                    ReconstructChromaRow<TSample, TLoader>(
                        cb0,
                        cb1,
                        y1Weight,
                        this.subsamplingX,
                        isCenteredX,
                        reconstructed,
                        chroma0,
                        chroma1);

                    if (this.reconstructCompleteRow)
                    {
                        reconstructed.Slice(this.sourceX, width).CopyTo(green);
                    }

                    reconstructed = this.reconstructCompleteRow ? reconstructed : blue;
                    ReconstructChromaRow<TSample, TLoader>(
                        cr0,
                        cr1,
                        y1Weight,
                        this.subsamplingX,
                        isCenteredX,
                        reconstructed,
                        chroma0,
                        chroma1);

                    if (this.reconstructCompleteRow)
                    {
                        reconstructed.Slice(this.sourceX, width).CopyTo(blue);
                    }

                    packedOffset += (this.chromaWidth * 2) + (this.reconstructCompleteRow ? this.bufferWidth : 0);
                }
            }

            this.colorConverter.ConvertToRgbInPlace(red, green, blue);
            Span<TPixel> destination = this.image.PixelBuffer.DangerousGetRowSpan(y);
            Span<float> packedStorage = scratch[packedOffset..];
            if (this.UsesBytePacking)
            {
                // This is JPEG's planar packing contract. Existing pixel-specific SIMD packers therefore own the
                // final RGB-to-TPixel conversion rather than a HEIF-codec-specific per-pixel implementation.
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
    /// Converts packed image rows to native component planes using pooled planar storage.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    private struct RgbToYuvRowConverter<TPixel, TBuffer, TSample, TStorer>
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleStorer<TSample>
    {
        /// <summary>
        /// The configuration used for packed-pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The codec adapter exposing the destination component planes.
        /// </summary>
        private TBuffer buffer;

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
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subsamplingX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int subsamplingY;

        /// <summary>
        /// The horizontal chroma position in half-luma-sample units.
        /// </summary>
        private readonly int chromaPositionX;

        /// <summary>
        /// The vertical chroma position in half-luma-sample units.
        /// </summary>
        private readonly int chromaPositionY;

        /// <summary>
        /// Whether the destination buffer contains only the luma plane.
        /// </summary>
        private readonly bool isMonochrome;

        /// <summary>
        /// Whether source pixels use JPEG-compatible eight-bit RGB plane unpacking.
        /// </summary>
        private readonly bool usesByteInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbToYuvRowConverter{TPixel, TBuffer, TSample, TStorer}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="buffer">The codec adapter exposing the destination component planes.</param>
        /// <param name="image">The source image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="parameters">The resolved H.273 component ranges.</param>
        public RgbToYuvRowConverter(
            Configuration configuration,
            TBuffer buffer,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter,
            in HeifColorConversionParameters parameters)
        {
            this.configuration = configuration;
            this.buffer = buffer;
            this.image = image;
            this.colorConverter = colorConverter;
            this.lumaMaximum = parameters.LumaSampleMaximum;
            this.chromaMaximum = parameters.ChromaSampleMaximum;
            this.subsamplingX = buffer.ChromaSubsamplingX;
            this.subsamplingY = buffer.ChromaSubsamplingY;
            this.chromaPositionX = buffer.ChromaPositionX;
            this.chromaPositionY = buffer.ChromaPositionY;
            this.isMonochrome = buffer.IsMonochrome;
            this.usesByteInput = buffer.LumaBitDepth == 8 && (buffer.IsMonochrome || buffer.ChromaBitDepth == 8);
        }

        /// <summary>
        /// Gets a value indicating whether source pixels use JPEG-compatible eight-bit RGB plane unpacking.
        /// </summary>
        public readonly bool UsesByteInput => this.usesByteInput;

        /// <summary>
        /// Gets the number of float elements required by the reusable component buffer.
        /// </summary>
        public readonly int ComponentBufferLength => this.image.Width * (this.subsamplingY == 0 || this.isMonochrome ? 3 : 6);

        /// <summary>
        /// Converts every packed source row to the destination component planes.
        /// </summary>
        /// <param name="packed">The reusable high-bit-depth RGB staging row.</param>
        /// <param name="components">The reusable planar component buffer.</param>
        public void Convert(Span<Rgb48> packed, Span<float> components)
        {
            int width = this.image.Width;
            Span<float> luma0 = components[..width];
            Span<float> blue0 = components.Slice(width, width);
            Span<float> red0 = components.Slice(width * 2, width);
            if (this.subsamplingY == 0)
            {
                for (int y = 0; y < this.image.Height; y++)
                {
                    this.ConvertSourceRow(y, packed, luma0, blue0, red0);
                    WriteSamples<TSample, TStorer>(
                        luma0,
                        this.buffer.GetLumaRowSpan(y),
                        this.colorConverter.LumaScale,
                        this.colorConverter.LumaBias,
                        this.lumaMaximum);

                    if (!this.isMonochrome)
                    {
                        this.WriteChromaRows(y, blue0, red0, Span<float>.Empty, Span<float>.Empty, 0F);
                    }
                }

                return;
            }

            Span<float> luma1 = this.isMonochrome ? luma0 : components.Slice(width * 3, width);
            Span<float> blue1 = this.isMonochrome ? blue0 : components.Slice(width * 4, width);
            Span<float> red1 = this.isMonochrome ? red0 : components.Slice(width * 5, width);
            int chromaHeight = (this.image.Height + 1) >> 1;
            for (int destinationY = 0; destinationY < chromaHeight; destinationY++)
            {
                // A vertically subsampled chroma row is owned by one two-row luma cell. Processing that cell as a
                // unit removes per-row state and lets the selected chroma position choose or average the two rows.
                int sourceY = destinationY << 1;
                this.ConvertSourceRow(sourceY, packed, luma0, blue0, red0);
                WriteSamples<TSample, TStorer>(
                    luma0,
                    this.buffer.GetLumaRowSpan(sourceY),
                    this.colorConverter.LumaScale,
                    this.colorConverter.LumaBias,
                    this.lumaMaximum);

                bool hasSecondRow = sourceY + 1 < this.image.Height;
                if (hasSecondRow)
                {
                    this.ConvertSourceRow(sourceY + 1, packed, luma1, blue1, red1);
                    WriteSamples<TSample, TStorer>(
                        luma1,
                        this.buffer.GetLumaRowSpan(sourceY + 1),
                        this.colorConverter.LumaScale,
                        this.colorConverter.LumaBias,
                        this.lumaMaximum);
                }

                if (this.isMonochrome)
                {
                    continue;
                }

                if (this.chromaPositionY == 0 || !hasSecondRow)
                {
                    this.WriteChromaRows(destinationY, blue0, red0, Span<float>.Empty, Span<float>.Empty, 0F);
                }
                else if (this.chromaPositionY == 1)
                {
                    this.WriteChromaRows(destinationY, blue0, red0, blue1, red1, 0.5F);
                }
                else
                {
                    this.WriteChromaRows(destinationY, blue1, red1, Span<float>.Empty, Span<float>.Empty, 0F);
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
                // the closed H.273 operator transforms the component rows in place.
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
            Span<TSample> blueDestination = this.buffer.GetChromaBlueRowSpan(destinationY);
            Span<TSample> redDestination = this.buffer.GetChromaRedRowSpan(destinationY);
            if (this.subsamplingX == 0)
            {
                WriteSamples<TSample, TStorer>(
                    blue0,
                    blueDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                WriteSamples<TSample, TStorer>(
                    red0,
                    redDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                return;
            }

            bool isCenteredX = this.chromaPositionX == 1;
            WriteSubsampledSamples<TSample, TStorer>(
                blue0,
                blue1,
                blueDestination,
                isCenteredX,
                row1Weight,
                this.colorConverter.ChromaScale,
                this.colorConverter.ChromaBias,
                this.chromaMaximum);

            WriteSubsampledSamples<TSample, TStorer>(
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
