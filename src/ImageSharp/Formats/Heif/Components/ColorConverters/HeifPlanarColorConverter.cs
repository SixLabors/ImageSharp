// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Converts between native HEIF component planes and packed ImageSharp pixels.
/// </summary>
internal static class HeifPlanarColorConverter
{
    /// <summary>
    /// The number of planar color components retained for each source row.
    /// </summary>
    private const int ColorComponentCount = 3;

    /// <summary>
    /// The number of source rows consumed together by vertically subsampled chroma.
    /// </summary>
    private const int VerticallySubsampledRowCount = 2;

    /// <summary>
    /// The largest value represented by an eight-bit packed RGB component.
    /// </summary>
    private const float ByteMaximum = byte.MaxValue;

    /// <summary>
    /// The largest value represented by a 16-bit packed RGB component.
    /// </summary>
    private const float UShortMaximum = ushort.MaxValue;

    /// <summary>
    /// Converts a region of native unsigned 16-bit component storage to packed pixels and selects eligible exact integer kernels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The native component-plane buffer.</param>
    /// <param name="destination">The exact destination pixel region.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceSize">The extent of the source region before rotation and mirroring.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="profile">The source profile selected for conversion, or null to preserve source colors.</param>
    /// <param name="alpha">The matching normalized auxiliary rows, or null for opaque pixels.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
    public static void ConvertToRgb<TPixel, TBuffer>(
        Configuration configuration,
        TBuffer buffer,
        Buffer2DRegion<TPixel> destination,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode,
        int sourceX,
        int sourceY,
        Size sourceSize,
        HeifPixelTransform transform,
        IccProfile? profile,
        HeifAlphaRowSource? alpha,
        bool premultiplied,
        HeifChromaUpsampling chromaUpsampling)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<ushort>
    {
        if (chromaUpsampling != HeifChromaUpsampling.Bilinear && profile is null && alpha is null && HeifYuvToRgb8Converter.SupportsFixedPointConversion(
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
            HeifYuvToRgb8Converter.ConvertFixedPoint(configuration, buffer, destination, in parameters, sourceX, sourceY, sourceSize, transform);
            return;
        }

        ConvertToRgb<TPixel, TBuffer, ushort, HeifUShortSampleConverter>(
            configuration,
            buffer,
            destination,
            in parameters,
            mode,
            sourceX,
            sourceY,
            sourceSize,
            transform,
            profile,
            alpha,
            premultiplied,
            chromaUpsampling);
    }

    /// <summary>
    /// Converts a region of native component planes to packed pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="buffer">The native component-plane buffer.</param>
    /// <param name="destination">The exact destination pixel region.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    /// <param name="sourceX">The horizontal luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceY">The vertical luma-sample offset of the first converted pixel.</param>
    /// <param name="sourceSize">The extent of the source region before rotation and mirroring.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="profile">The source profile selected for conversion, or null to preserve source colors.</param>
    /// <param name="alpha">The matching normalized auxiliary rows, or null for opaque pixels.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
    public static void ConvertToRgb<TPixel, TBuffer, TSample, TLoader>(
        Configuration configuration,
        TBuffer buffer,
        Buffer2DRegion<TPixel> destination,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode,
        int sourceX,
        int sourceY,
        Size sourceSize,
        HeifPixelTransform transform,
        IccProfile? profile,
        HeifAlphaRowSource? alpha,
        bool premultiplied,
        HeifChromaUpsampling chromaUpsampling)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, buffer.IsMonochrome);
        ColorProfileConverter? profileConverter = profile is null
            ? null
            : new ColorProfileConverter(new ColorConversionOptions
            {
                MemoryAllocator = configuration.MemoryAllocator,
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
            });

        YuvToRgbRowConverter<TPixel, TBuffer, TSample, TLoader> converter = new(
            configuration,
            buffer,
            sourceSize.Width,
            colorConverter,
            profileConverter,
            alpha,
            premultiplied,
            sourceX,
            sourceY,
            chromaUpsampling);

        using IMemoryOwner<float> scratchOwner = configuration.MemoryAllocator.Allocate<float>(converter.BufferLength);
        Span<float> scratch = scratchOwner.GetSpan();
        Matrix3x2 matrix = transform.GetMatrix(sourceSize);
        Point origin = HeifPixelTransform.Transform(0, 0, matrix);
        Size columnStep = new((int)matrix.M11, (int)matrix.M12);
        Size rowStep = new((int)matrix.M21, (int)matrix.M22);

        // The decoder's region bounds already include crop and tile placement. Resolve orientation once;
        // each row advances by an integer basis vector and packing writes directly into that region.
        for (int y = 0; y < sourceSize.Height; y++)
        {
            converter.Convert(y, scratch, destination, origin, columnStep);
            origin += rowStep;
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
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
        ConvertFromRgb<TPixel, TBuffer, TSample, TStorer>(
            configuration,
            image,
            sourceRectangle,
            buffer,
            in parameters,
            mode);
    }

    /// <summary>
    /// Converts a rectangular packed-pixel region to native component planes.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for allocation and pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="sourceRectangle">The source region mapped to the complete destination buffer.</param>
    /// <param name="buffer">The destination component-plane buffer.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="mode">The resolved H.273 conversion mode.</param>
    public static void ConvertFromRgb<TPixel, TBuffer, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        TBuffer buffer,
        in HeifColorConversionParameters parameters,
        HeifColorConversionMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        HeifColorConverterBase colorConverter = HeifColorConverterBase.Create(mode, in parameters, buffer.IsMonochrome);
        RgbToYuvRowConverter<TPixel, TBuffer, TSample, TStorer> converter = new(
            configuration,
            buffer,
            image,
            sourceRectangle,
            colorConverter,
            in parameters);

        using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(converter.ComponentBufferLength);
        Span<float> components = componentOwner.GetSpan();
        if (converter.UsesByteInput)
        {
            converter.Convert(Span<Rgb48>.Empty, components);
            return;
        }

        using IMemoryOwner<Rgb48> packedOwner = configuration.MemoryAllocator.Allocate<Rgb48>(sourceRectangle.Width);
        converter.Convert(packedOwner.GetSpan()[..sourceRectangle.Width], components);
    }

    /// <summary>
    /// Converts a rectangular packed-pixel region using a retained color converter and caller-owned row storage.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="image">The source image frame.</param>
    /// <param name="sourceRectangle">The source region mapped to the complete destination buffer.</param>
    /// <param name="buffer">The destination component-plane buffer.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="colorConverter">The retained converter matching <paramref name="parameters"/>.</param>
    /// <param name="packed">The reusable high-bit-depth packed RGB row, or an empty span for eight-bit input.</param>
    /// <param name="components">The reusable planar component rows.</param>
    public static void ConvertFromRgb<TPixel, TBuffer, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Rectangle sourceRectangle,
        TBuffer buffer,
        in HeifColorConversionParameters parameters,
        HeifColorConverterBase colorConverter,
        Span<Rgb48> packed,
        Span<float> components)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        RgbToYuvRowConverter<TPixel, TBuffer, TSample, TStorer> converter = new(
            configuration,
            buffer,
            image,
            sourceRectangle,
            colorConverter,
            in parameters);

        converter.Convert(packed, components);
    }

    /// <summary>
    /// Gets the planar float storage required to convert one row, or one vertically subsampled row pair.
    /// </summary>
    /// <param name="width">The source-row width.</param>
    /// <param name="isMonochrome">Whether only luma is written.</param>
    /// <param name="subsamplingY">The vertical chroma-subsampling shift.</param>
    /// <returns>The required number of float elements.</returns>
    public static int GetRgbToYuvComponentBufferLength(int width, bool isMonochrome, int subsamplingY)
        => width * ColorComponentCount * (subsamplingY == 0 || isMonochrome ? 1 : VerticallySubsampledRowCount);

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
        where TLoader : struct, IHeifSampleConverter<TSample>
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
        /// The exact destination pixel region.
        /// </summary>
        private readonly int width;

        /// <summary>
        /// The selected H.273 color converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The profile transform reused by every component row in the region.
        /// </summary>
        private readonly ColorProfileConverter? profileConverter;

        private readonly HeifAlphaRowSource? alpha;
        private readonly bool premultiplied;

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
        /// Whether the destination uses eight-bit RGB plane packing.
        /// </summary>
        private readonly bool usesBytePacking;

        /// <summary>
        /// Whether chroma reconstruction must retain the complete coded row before selecting the output window.
        /// </summary>
        private readonly bool reconstructCompleteRow;

        /// <summary>
        /// Whether subsampled chroma is reconstructed with bilinear interpolation.
        /// </summary>
        private readonly bool useBilinear;

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbRowConverter{TPixel, TBuffer, TSample, TLoader}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="buffer">The codec adapter exposing the reconstructed component planes.</param>
        /// <param name="width">The source row width.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="profileConverter">The profile transform for this region, or null to preserve source colors.</param>
        /// <param name="alpha">The auxiliary rows, or null for opaque pixels.</param>
        /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
        /// <param name="sourceX">The horizontal luma-sample offset of the output window.</param>
        /// <param name="sourceY">The vertical luma-sample offset of the output window.</param>
        /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
        public YuvToRgbRowConverter(
            Configuration configuration,
            TBuffer buffer,
            int width,
            HeifColorConverterBase colorConverter,
            ColorProfileConverter? profileConverter,
            HeifAlphaRowSource? alpha,
            bool premultiplied,
            int sourceX,
            int sourceY,
            HeifChromaUpsampling chromaUpsampling)
        {
            this.configuration = configuration;
            this.buffer = buffer;
            this.width = width;
            this.colorConverter = colorConverter;
            this.profileConverter = profileConverter;
            this.alpha = alpha;
            this.premultiplied = premultiplied;
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
            this.reconstructCompleteRow = sourceX != 0 || width != buffer.Width;
            this.useBilinear = chromaUpsampling == HeifChromaUpsampling.Bilinear
                || (chromaUpsampling == HeifChromaUpsampling.Auto && buffer.ChromaBitDepth > 8);
        }

        /// <summary>
        /// Gets a value indicating whether the destination uses eight-bit RGB plane packing.
        /// </summary>
        public readonly bool UsesBytePacking => this.usesBytePacking;

        /// <summary>
        /// Gets the number of float elements required by the reusable row buffer.
        /// </summary>
        public readonly int BufferLength
        {
            get
            {
                int componentLength = this.width * 3;
                if (this.useBilinear && !this.isMonochrome && this.subsamplingX != 0)
                {
                    // Full-destination conversion reconstructs directly into the component rows. Cropped conversion
                    // retains the complete coded row so interpolation phase is preserved at the window boundary.
                    componentLength += (this.chromaWidth * 2) + (this.reconstructCompleteRow ? this.bufferWidth : 0);
                }

                // ICC interleaving and final pixel packing occur sequentially, so the same scratch
                // storage holds both. No additional buffer or allocation is needed per row.
                int packedRowCount = this.profileConverter is not null ? 4 : this.UsesBytePacking && this.alpha is null ? 1 : 2;
                return componentLength + (this.width * packedRowCount);
            }
        }

        /// <summary>
        /// Converts one reconstructed row to packed pixels.
        /// </summary>
        /// <param name="y">The zero-based output row.</param>
        /// <param name="scratch">The reusable pooled row buffer.</param>
        /// <param name="destination">The exact output region receiving the converted pixels.</param>
        /// <param name="origin">The final location of the first pixel in this source row.</param>
        /// <param name="step">The destination increment for each source pixel.</param>
        public void Convert(int y, Span<float> scratch, Buffer2DRegion<TPixel> destination, Point origin, Size step)
        {
            int width = this.width;
            Span<float> red = scratch[..width];
            Span<float> green = scratch.Slice(width, width);
            Span<float> blue = scratch.Slice(width * 2, width);
            int sourceY = y + this.sourceY;
            ReadOnlySpan<TSample> luma = this.buffer.GetLumaRowSpan(sourceY).Slice(this.sourceX, width);
            HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(luma, red, this.colorConverter.LumaBias, this.colorConverter.LumaScale);

            int packedOffset = width * 3;
            if (!this.isMonochrome)
            {
                if (this.useBilinear)
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
                        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(
                            cb0.Slice(this.sourceX, width), green, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(
                            cr0.Slice(this.sourceX, width), blue, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                    }
                    else
                    {
                        Span<float> chroma0 = scratch.Slice(packedOffset, this.chromaWidth);
                        Span<float> chroma1 = scratch.Slice(packedOffset + this.chromaWidth, this.chromaWidth);
                        Span<float> reconstructed = this.reconstructCompleteRow
                            ? scratch.Slice(packedOffset + (this.chromaWidth * 2), this.bufferWidth)
                            : green;

                        bool isCenteredX = this.chromaPositionX == 1;

                        HeifSampleConversion.ReconstructChromaRowBilinear<TSample, TLoader>(
                            cb0,
                            cb1,
                            y1Weight,
                            this.subsamplingX,
                            isCenteredX,
                            reconstructed,
                            chroma0,
                            chroma1,
                            this.colorConverter.ChromaBias,
                            this.colorConverter.ChromaScale);

                        if (this.reconstructCompleteRow)
                        {
                            reconstructed.Slice(this.sourceX, width).CopyTo(green);
                        }

                        reconstructed = this.reconstructCompleteRow ? reconstructed : blue;
                        HeifSampleConversion.ReconstructChromaRowBilinear<TSample, TLoader>(
                            cr0,
                            cr1,
                            y1Weight,
                            this.subsamplingX,
                            isCenteredX,
                            reconstructed,
                            chroma0,
                            chroma1,
                            this.colorConverter.ChromaBias,
                            this.colorConverter.ChromaScale);

                        if (this.reconstructCompleteRow)
                        {
                            reconstructed.Slice(this.sourceX, width).CopyTo(blue);
                        }

                        packedOffset += (this.chromaWidth * 2) + (this.reconstructCompleteRow ? this.bufferWidth : 0);
                    }
                }
                else
                {
                    // Each chroma row serves two luma rows when vertically subsampled. Use absolute
                    // source coordinates so cropped regions retain the coded sample-replication phase.
                    int chromaY = sourceY >> this.subsamplingY;
                    ReadOnlySpan<TSample> cb = this.buffer.GetChromaBlueRowSpan(chromaY);
                    ReadOnlySpan<TSample> cr = this.buffer.GetChromaRedRowSpan(chromaY);
                    if (this.subsamplingX == 0)
                    {
                        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(
                            cb.Slice(this.sourceX, width), green, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(
                            cr.Slice(this.sourceX, width), blue, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                    }
                    else
                    {
                        HeifSampleConversion.ReconstructChromaRow<TSample, TLoader>(
                            cb, this.sourceX, green, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                        HeifSampleConversion.ReconstructChromaRow<TSample, TLoader>(
                            cr, this.sourceX, blue, this.colorConverter.ChromaBias, this.colorConverter.ChromaScale);
                    }
                }
            }

            this.colorConverter.ConvertToRgbInPlace(red, green, blue);
            ReadOnlySpan<float> alpha = this.alpha is null ? default : this.alpha.ReadRow(y);
            if (this.premultiplied)
            {
                HeifColorConverterBase.UnassociateRgb(red, green, blue, alpha);
            }

            Span<float> packedStorage = scratch[packedOffset..];
            if (this.profileConverter is not null)
            {
                this.colorConverter.ConvertRgbToSrgbInPlace(
                    red, green, blue, this.profileConverter, MemoryMarshal.Cast<float, Rgb>(packedStorage)[..width]);

                // Keep the converted RGB and auxiliary alpha in floating point until TPixel performs its
                // final conversion. An integer intermediate would discard precision for floating-point pixels.
                // ICC's interleaved RGB is no longer live, so its scratch storage now holds RGBA vectors.
                Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(packedStorage)[..width];
                int x = 0;
                if (Vector128.IsHardwareAccelerated)
                {
                    // Load four samples per plane and transpose them into four RGBA pixels. Opaque alpha
                    // supplies four ones; auxiliary alpha has already been normalized by its row source.
                    for (; x <= width - Vector128<float>.Count; x += Vector128<float>.Count)
                    {
                        Vector128<float> r = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(red), (nuint)x);
                        Vector128<float> g = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(green), (nuint)x);
                        Vector128<float> b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(blue), (nuint)x);
                        Vector128<float> a = this.alpha is null
                            ? Vector128.Create(1F)
                            : Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(alpha), (nuint)x);

                        HeifColorConverterBase.Transpose4(
                            r,
                            g,
                            b,
                            a,
                            out Vector128<float> p0,
                            out Vector128<float> p1,
                            out Vector128<float> p2,
                            out Vector128<float> p3);

                        vectors[x] = p0.AsVector4();
                        vectors[x + 1] = p1.AsVector4();
                        vectors[x + 2] = p2.AsVector4();
                        vectors[x + 3] = p3.AsVector4();
                    }
                }

                for (; x < width; x++)
                {
                    vectors[x] = new Vector4(red[x], green[x], blue[x], this.alpha is null ? 1F : alpha[x]);
                }

                if (step.Width == 1)
                {
                    PixelOperations<TPixel>.Instance.FromVector4Destructive(
                        this.configuration,
                        vectors,
                        destination.DangerousGetRowSpan(origin.Y).Slice(origin.X, width),
                        PixelConversionModifiers.Scale);
                }
                else
                {
                    // Reversed rows and rotated columns write directly into their final locations.
                    for (x = 0; x < width; x++)
                    {
                        destination.DangerousGetRowSpan(origin.Y)[origin.X] = TPixel.FromUnassociatedScaledVector4(vectors[x]);
                        origin += step;
                    }
                }

                return;
            }

            if (this.UsesBytePacking && this.alpha is null)
            {
                // The shared planar packing contract lets existing pixel-specific SIMD packers own the
                // final RGB-to-TPixel conversion rather than a HEIF-codec-specific per-pixel implementation.
                Span<byte> byteStorage = MemoryMarshal.AsBytes(packedStorage)[..(width * 3)];
                Span<byte> redBytes = byteStorage[..width];
                Span<byte> greenBytes = byteStorage.Slice(width, width);
                Span<byte> blueBytes = byteStorage.Slice(width * 2, width);
                SimdUtils.NormalizedFloatToByteSaturate(red, redBytes);
                SimdUtils.NormalizedFloatToByteSaturate(green, greenBytes);
                SimdUtils.NormalizedFloatToByteSaturate(blue, blueBytes);

                if (step.Width == 1)
                {
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(
                        redBytes, greenBytes, blueBytes, destination.DangerousGetRowSpan(origin.Y).Slice(origin.X, width));
                }
                else
                {
                    // A reversed row or column is not a contiguous span. Construct the final pixel at its
                    // destination instead of packing a temporary TPixel row and reading it back to scatter.
                    for (int x = 0; x < width; x++)
                    {
                        destination.DangerousGetRowSpan(origin.Y)[origin.X] = TPixel.FromRgb24(new Rgb24(redBytes[x], greenBytes[x], blueBytes[x]));
                        origin += step;
                    }
                }

                return;
            }

            Span<Rgba64> packed = MemoryMarshal.Cast<float, Rgba64>(packedStorage)[..width];
            HeifSampleConversion.PackRgba64(red, green, blue, packed);
            if (this.alpha is not null)
            {
                // RGB has been packed, so its float row is no longer live. Reuse that storage for
                // alpha narrowing instead of allocating a fourth component buffer or revisiting the image.
                Span<L16> packedAlpha = MemoryMarshal.Cast<float, L16>(red)[..width];
                HeifSampleConversion.PackL16(alpha, packedAlpha);
                for (int x = 0; x < width; x++)
                {
                    packed[x].A = packedAlpha[x].PackedValue;
                }
            }

            if (step.Width == 1)
            {
                PixelOperations<TPixel>.Instance.FromRgba64(
                    this.configuration, packed, destination.DangerousGetRowSpan(origin.Y).Slice(origin.X, width));
            }
            else
            {
                // Keep the same component narrowing as contiguous output, including alpha, while avoiding
                // a second packed pixel buffer for reversed rows and quarter-turn destination columns.
                for (int x = 0; x < width; x++)
                {
                    destination.DangerousGetRowSpan(origin.Y)[origin.X] = TPixel.FromRgba64(packed[x]);
                    origin += step;
                }
            }
        }
    }

    /// <summary>
    /// Converts packed destination rows to native component planes using pooled planar storage.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the native component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    private struct RgbToYuvRowConverter<TPixel, TBuffer, TSample, TStorer>
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
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
        /// The source region mapped to the complete destination planes.
        /// </summary>
        private readonly Rectangle sourceRectangle;

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
        /// Whether source pixels use eight-bit RGB plane unpacking.
        /// </summary>
        private readonly bool usesByteInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbToYuvRowConverter{TPixel, TBuffer, TSample, TStorer}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for pixel conversion.</param>
        /// <param name="buffer">The codec adapter exposing the destination component planes.</param>
        /// <param name="image">The source image frame.</param>
        /// <param name="sourceRectangle">The source region mapped to the complete destination planes.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="parameters">The resolved H.273 component ranges.</param>
        public RgbToYuvRowConverter(
            Configuration configuration,
            TBuffer buffer,
            ImageFrame<TPixel> image,
            Rectangle sourceRectangle,
            HeifColorConverterBase colorConverter,
            in HeifColorConversionParameters parameters)
        {
            this.configuration = configuration;
            this.buffer = buffer;
            this.image = image;
            this.sourceRectangle = sourceRectangle;
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
        /// Gets a value indicating whether source pixels use eight-bit RGB plane unpacking.
        /// </summary>
        public readonly bool UsesByteInput => this.usesByteInput;

        /// <summary>
        /// Gets the number of float elements required by the reusable component buffer.
        /// </summary>
        public readonly int ComponentBufferLength
            => GetRgbToYuvComponentBufferLength(this.sourceRectangle.Width, this.isMonochrome, this.subsamplingY);

        /// <summary>
        /// Converts every packed source row to the destination component planes.
        /// </summary>
        /// <param name="packed">The reusable high-bit-depth RGB staging row.</param>
        /// <param name="components">The reusable planar component buffer.</param>
        public void Convert(Span<Rgb48> packed, Span<float> components)
        {
            int width = this.sourceRectangle.Width;
            Span<float> luma0 = components[..width];
            Span<float> blue0 = components.Slice(width, width);
            Span<float> red0 = components.Slice(width * 2, width);
            if (this.subsamplingY == 0)
            {
                for (int y = 0; y < this.sourceRectangle.Height; y++)
                {
                    this.ConvertSourceRow(y, packed, luma0, blue0, red0);
                    HeifSampleConversion.WriteSamples<TSample, TStorer>(
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
            int chromaHeight = (this.sourceRectangle.Height + 1) >> 1;
            for (int destinationY = 0; destinationY < chromaHeight; destinationY++)
            {
                // A vertically subsampled chroma row is owned by one two-row luma cell. Processing that cell as a
                // unit removes per-row state and lets the selected chroma position choose or average the two rows.
                int sourceY = destinationY << 1;
                this.ConvertSourceRow(sourceY, packed, luma0, blue0, red0);
                HeifSampleConversion.WriteSamples<TSample, TStorer>(
                    luma0,
                    this.buffer.GetLumaRowSpan(sourceY),
                    this.colorConverter.LumaScale,
                    this.colorConverter.LumaBias,
                    this.lumaMaximum);

                bool hasSecondRow = sourceY + 1 < this.sourceRectangle.Height;
                if (hasSecondRow)
                {
                    this.ConvertSourceRow(sourceY + 1, packed, luma1, blue1, red1);
                    HeifSampleConversion.WriteSamples<TSample, TStorer>(
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
            ReadOnlySpan<TPixel> source = this.image.PixelBuffer
                .DangerousGetRowSpan(this.sourceRectangle.Y + y)
                .Slice(this.sourceRectangle.X, this.sourceRectangle.Width);

            if (this.UsesByteInput)
            {
                // The shared planar unpack contract reaches the existing pixel-specific SIMD implementation before
                // the closed H.273 operator transforms the component rows in place.
                PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(luma, chromaBlue, chromaRed, source);
                this.colorConverter.ConvertFromRgbInPlace(luma, chromaBlue, chromaRed, ByteMaximum);
                return;
            }

            PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, source, packed);
            HeifSampleConversion.DeinterleaveRgb48(packed, luma, chromaBlue, chromaRed);
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
                HeifSampleConversion.WriteSamples<TSample, TStorer>(
                    blue0,
                    blueDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                HeifSampleConversion.WriteSamples<TSample, TStorer>(
                    red0,
                    redDestination,
                    this.colorConverter.ChromaScale,
                    this.colorConverter.ChromaBias,
                    this.chromaMaximum);

                return;
            }

            bool isCenteredX = this.chromaPositionX == 1;
            HeifSampleConversion.WriteSubsampledSamples<TSample, TStorer>(
                blue0,
                blue1,
                blueDestination,
                isCenteredX,
                row1Weight,
                this.colorConverter.ChromaScale,
                this.colorConverter.ChromaBias,
                this.chromaMaximum);

            HeifSampleConversion.WriteSubsampledSamples<TSample, TStorer>(
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
