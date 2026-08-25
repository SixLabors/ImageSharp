// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Color;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using static SixLabors.ImageSharp.Formats.Heif.Color.HeifColorConverterBase;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <content>
/// Provides the pooled row converters used by AV1 color conversion.
/// </content>
internal static partial class Av1YuvConverter
{
    /// <summary>
    /// Converts one reconstructed AV1 row using frame-scoped pooled component, chroma, and packed-pixel storage.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    private readonly struct YuvToRgbRowConverter<TPixel, TSample, TLoader>
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleLoader<TSample>
    {
        /// <summary>
        /// The configuration used by bulk pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The reconstructed AV1 frame containing the source planes.
        /// </summary>
        private readonly Av1FrameBuffer<byte> frameBuffer;

        /// <summary>
        /// The destination image frame.
        /// </summary>
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// The full-resolution luma plane.
        /// </summary>
        private readonly Buffer2DRegion<byte> yPlane;

        /// <summary>
        /// The blue-difference plane when the frame contains chroma.
        /// </summary>
        private readonly Buffer2DRegion<byte> uPlane;

        /// <summary>
        /// The red-difference plane when the frame contains chroma.
        /// </summary>
        private readonly Buffer2DRegion<byte> vPlane;

        /// <summary>
        /// The frame-scoped color-model converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The signaled chroma sample position used for reconstruction.
        /// </summary>
        private readonly ObuChromoSamplePosition chromaSamplePosition;

        /// <summary>
        /// Whether the source contains only a luma plane.
        /// </summary>
        private readonly bool isMonochrome;

        /// <summary>
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int subY;

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbRowConverter{TPixel, TSample, TLoader}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for packed-pixel conversion.</param>
        /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
        /// <param name="image">The destination image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        public YuvToRgbRowConverter(
            Configuration configuration,
            Av1FrameBuffer<byte> frameBuffer,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter)
        {
            this.configuration = configuration;
            this.frameBuffer = frameBuffer;
            this.image = image;
            this.colorConverter = colorConverter;
            this.isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
            this.subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
            this.subY = frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
            this.chromaSamplePosition = frameBuffer.ColorConfig.ChromaSamplePosition;
            this.yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
            this.uPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, this.subX, this.subY);
            this.vPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, this.subX, this.subY);
        }

        /// <summary>
        /// Gets the number of float elements required by the reusable conversion buffer.
        /// </summary>
        public int BufferLength
        {
            get
            {
                // Three float component rows are converted in place. Color input adds two reusable chroma scratch
                // rows, and the final one or two float-sized slots per pixel back RGB byte planes or an Rgba64 row.
                int rowCount = this.isMonochrome ? 3 : 5;
                int packedRowCount = typeof(TSample) == typeof(byte) ? 1 : 2;
                return this.image.Width * (rowCount + packedRowCount);
            }
        }

        /// <summary>
        /// Converts one reconstructed AV1 row to packed pixels.
        /// </summary>
        /// <param name="y">The row index.</param>
        /// <param name="span">The reusable conversion buffer.</param>
        /// <param name="proxy">The padded destination used when an eight-bit image row cannot expose sufficient padding.</param>
        public void Convert(int y, Span<float> span, Span<TPixel> proxy)
        {
            int width = this.image.Width;
            Span<float> red = span[..width];
            Span<float> green = span.Slice(width, width);
            Span<float> blue = span.Slice(width * 2, width);

            ReadOnlySpan<TSample> ySource;
            if (typeof(TSample) == typeof(byte))
            {
                ySource = MemoryMarshal.Cast<byte, TSample>(this.yPlane.DangerousGetRowSpan(y));
            }
            else
            {
                ySource = MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0));
            }

            ConvertSamplesToFloat<TSample, TLoader>(ySource, red);

            int packedOffset = width * 3;
            if (!this.isMonochrome)
            {
                GetChromaCoordinates(
                    y,
                    this.subY,
                    this.subY != 0 && this.chromaSamplePosition != ObuChromoSamplePosition.Colocated,
                    this.uPlane.Height - 1,
                    out int y0,
                    out int y1,
                    out int y1Weight);

                ReadOnlySpan<TSample> uRow0;
                ReadOnlySpan<TSample> uRow1;
                ReadOnlySpan<TSample> vRow0;
                ReadOnlySpan<TSample> vRow1;
                if (typeof(TSample) == typeof(byte))
                {
                    uRow0 = MemoryMarshal.Cast<byte, TSample>(this.uPlane.DangerousGetRowSpan(y0));
                    uRow1 = MemoryMarshal.Cast<byte, TSample>(this.uPlane.DangerousGetRowSpan(y1));
                    vRow0 = MemoryMarshal.Cast<byte, TSample>(this.vPlane.DangerousGetRowSpan(y0));
                    vRow1 = MemoryMarshal.Cast<byte, TSample>(this.vPlane.DangerousGetRowSpan(y1));
                }
                else
                {
                    uRow0 = MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y0, this.subX, this.subY));
                    uRow1 = MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, y1, this.subX, this.subY));
                    vRow0 = MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y0, this.subX, this.subY));
                    vRow1 = MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, y1, this.subX, this.subY));
                }

                Span<float> scratch0 = span.Slice(width * 3, width);
                Span<float> scratch1 = span.Slice(width * 4, width);
                bool isCenteredX = this.subX != 0 && (this.subY == 0 || this.chromaSamplePosition == ObuChromoSamplePosition.Unknown);
                ReconstructChromaRow<TSample, TLoader>(uRow0, uRow1, y1Weight, this.subX, isCenteredX, green, scratch0, scratch1);
                ReconstructChromaRow<TSample, TLoader>(vRow0, vRow1, y1Weight, this.subX, isCenteredX, blue, scratch0, scratch1);
                packedOffset = width * 5;
            }

            this.colorConverter.ConvertToRgbInPlace(red, green, blue);
            Span<float> packedStorage = span[packedOffset..];
            Span<TPixel> destination = this.image.PixelBuffer.DangerousGetRowSpan(y);
            if (typeof(TSample) == typeof(byte))
            {
                // Match JPEG's byte-plane packing contract so optimized pixel types use the existing RGB packer.
                // The final float row provides enough byte storage for three tightly packed component planes.
                Span<byte> byteStorage = MemoryMarshal.AsBytes(packedStorage)[..(width * 3)];
                Span<byte> redBytes = byteStorage[..width];
                Span<byte> greenBytes = byteStorage.Slice(width, width);
                Span<byte> blueBytes = byteStorage.Slice(width * 2, width);
                SimdUtils.NormalizedFloatToByteSaturate(red, redBytes);
                SimdUtils.NormalizedFloatToByteSaturate(green, greenBytes);
                SimdUtils.NormalizedFloatToByteSaturate(blue, blueBytes);

                if (this.image.PixelBuffer.DangerousTryGetPaddedRowSpan(y, 3, out Span<TPixel> paddedDestination))
                {
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(redBytes, greenBytes, blueBytes, paddedDestination);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.PackFromRgbPlanes(redBytes, greenBytes, blueBytes, proxy);
                    proxy[..width].CopyTo(destination);
                }
            }
            else
            {
                Span<Rgba64> packed = MemoryMarshal.Cast<float, Rgba64>(packedStorage)[..width];
                PackRgba64(red, green, blue, packed);
                PixelOperations<TPixel>.Instance.FromRgba64(this.configuration, packed, destination);
            }
        }
    }

    /// <summary>
    /// Converts one source row, or one vertically subsampled row pair, using reusable pooled component storage.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TSample">The encoded sample type.</typeparam>
    /// <typeparam name="TStorer">The SIMD narrowing and storage operations for the sample type.</typeparam>
    private readonly struct RgbToYuvRowConverter<TPixel, TSample, TStorer>
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleStorer<TSample>
    {
        /// <summary>
        /// The configuration used by bulk pixel conversion.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The destination AV1 frame.
        /// </summary>
        private readonly Av1FrameBuffer<byte> frameBuffer;

        /// <summary>
        /// The source image frame.
        /// </summary>
        private readonly ImageFrame<TPixel> image;

        /// <summary>
        /// The full-resolution luma plane.
        /// </summary>
        private readonly Buffer2DRegion<byte> yPlane;

        /// <summary>
        /// The blue-difference plane when the frame contains chroma.
        /// </summary>
        private readonly Buffer2DRegion<byte> uPlane;

        /// <summary>
        /// The red-difference plane when the frame contains chroma.
        /// </summary>
        private readonly Buffer2DRegion<byte> vPlane;

        /// <summary>
        /// The frame-scoped color-model converter.
        /// </summary>
        private readonly HeifColorConverterBase colorConverter;

        /// <summary>
        /// The largest value represented by the encoded AV1 bit depth.
        /// </summary>
        private readonly float sampleMaximum;

        /// <summary>
        /// Whether the destination contains only a luma plane.
        /// </summary>
        private readonly bool isMonochrome;

        /// <summary>
        /// The horizontal chroma subsampling shift.
        /// </summary>
        private readonly int subX;

        /// <summary>
        /// The vertical chroma subsampling shift.
        /// </summary>
        private readonly int rowShift;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbToYuvRowConverter{TPixel, TSample, TStorer}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for packed-pixel conversion.</param>
        /// <param name="frameBuffer">The destination AV1 frame.</param>
        /// <param name="image">The source image frame.</param>
        /// <param name="colorConverter">The selected H.273 color converter.</param>
        /// <param name="sampleMaximum">The largest encoded sample value.</param>
        public RgbToYuvRowConverter(
            Configuration configuration,
            Av1FrameBuffer<byte> frameBuffer,
            ImageFrame<TPixel> image,
            HeifColorConverterBase colorConverter,
            float sampleMaximum)
        {
            this.configuration = configuration;
            this.frameBuffer = frameBuffer;
            this.image = image;
            this.colorConverter = colorConverter;
            this.sampleMaximum = sampleMaximum;
            this.isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
            this.subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
            this.rowShift = !this.isMonochrome && frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
            this.yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
            this.uPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, this.subX, this.rowShift);
            this.vPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, this.subX, this.rowShift);
        }

        /// <summary>
        /// Gets the number of float elements required by the reusable conversion buffer.
        /// </summary>
        public int ComponentBufferLength
        {
            get
            {
                // Three planar rows hold the converted YUV values, and 4:2:0 keeps a second set until its
                // chroma has been averaged with the first.
                int componentRowCount = this.rowShift == 0 ? 3 : 6;
                return this.image.Width * componentRowCount;
            }
        }

        /// <summary>
        /// Converts one source row, or one vertically subsampled row pair, to AV1 planes.
        /// </summary>
        /// <param name="y">The conversion iteration index.</param>
        /// <param name="packed">The reusable high-bit-depth RGB staging row.</param>
        /// <param name="components">The reusable planar component buffer.</param>
        public void Convert(int y, Span<Rgb48> packed, Span<float> components)
        {
            int width = this.image.Width;
            int sourceY = y << this.rowShift;
            Span<float> yRow0 = components[..width];
            Span<float> cbRow0 = components.Slice(width, width);
            Span<float> crRow0 = components.Slice(width * 2, width);
            this.ConvertSourceRow(sourceY, packed, yRow0, cbRow0, crRow0);
            WriteSamples<TSample, TStorer>(
                yRow0,
                this.GetPlaneRow(Av1Plane.Y, sourceY),
                this.colorConverter.LumaScale,
                this.colorConverter.LumaBias,
                this.sampleMaximum);

            bool hasSecondSourceRow = this.rowShift != 0 && sourceY + 1 < this.image.Height;
            Span<float> yRow1 = Span<float>.Empty;
            Span<float> cbRow1 = Span<float>.Empty;
            Span<float> crRow1 = Span<float>.Empty;
            if (hasSecondSourceRow)
            {
                yRow1 = components.Slice(width * 3, width);
                cbRow1 = components.Slice(width * 4, width);
                crRow1 = components.Slice(width * 5, width);
                this.ConvertSourceRow(sourceY + 1, packed, yRow1, cbRow1, crRow1);
                WriteSamples<TSample, TStorer>(
                    yRow1,
                    this.GetPlaneRow(Av1Plane.Y, sourceY + 1),
                    this.colorConverter.LumaScale,
                    this.colorConverter.LumaBias,
                    this.sampleMaximum);
            }

            if (this.isMonochrome)
            {
                return;
            }

            Span<TSample> uDestination = this.GetPlaneRow(Av1Plane.U, y);
            Span<TSample> vDestination = this.GetPlaneRow(Av1Plane.V, y);
            float chromaScale = this.colorConverter.ChromaScale;
            float chromaBias = this.colorConverter.ChromaBias;
            if (this.subX == 0)
            {
                WriteSamples<TSample, TStorer>(cbRow0, uDestination, chromaScale, chromaBias, this.sampleMaximum);
                WriteSamples<TSample, TStorer>(crRow0, vDestination, chromaScale, chromaBias, this.sampleMaximum);
            }
            else
            {
                float secondRowWeight = hasSecondSourceRow ? 0.5F : 0F;
                WriteSubsampledSamples<TSample, TStorer>(
                    cbRow0,
                    cbRow1,
                    uDestination,
                    true,
                    secondRowWeight,
                    chromaScale,
                    chromaBias,
                    this.sampleMaximum);

                WriteSubsampledSamples<TSample, TStorer>(
                    crRow0,
                    crRow1,
                    vDestination,
                    true,
                    secondRowWeight,
                    chromaScale,
                    chromaBias,
                    this.sampleMaximum);
            }
        }

        /// <summary>
        /// Converts one source row into normalized planar AV1 components.
        /// </summary>
        /// <param name="y">The source row index.</param>
        /// <param name="packed">The high-bit-depth RGB staging row.</param>
        /// <param name="luma">The destination luma values.</param>
        /// <param name="chromaBlue">The destination blue-difference values.</param>
        /// <param name="chromaRed">The destination red-difference values.</param>
        private void ConvertSourceRow(int y, Span<Rgb48> packed, Span<float> luma, Span<float> chromaBlue, Span<float> chromaRed)
        {
            ReadOnlySpan<TPixel> source = this.image.PixelBuffer.DangerousGetRowSpan(y);
            if (typeof(TSample) == typeof(byte))
            {
                // This is the same planar input contract used by JPEG encoding. Pixel types with optimized
                // unpackers reach their existing SIMD path before the AV1 operator consumes the planes.
                PixelOperations<TPixel>.Instance.UnpackIntoRgbPlanes(luma, chromaBlue, chromaRed, source);
                this.colorConverter.ConvertFromRgbInPlace(luma, chromaBlue, chromaRed, ByteMaximum);
            }
            else
            {
                // JPEG's planar unpack contract is eight-bit. AV1 10/12-bit encoding stages Rgb48 instead
                // so high-precision source pixels are not truncated before the color transform.
                PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, source, packed);
                DeinterleaveRgb48(packed, luma, chromaBlue, chromaRed);
                this.colorConverter.ConvertFromRgbInPlace(luma, chromaBlue, chromaRed, UShortMaximum);
            }
        }

        /// <summary>
        /// Gets a writable row from an eight-bit or high-bit-depth AV1 plane.
        /// </summary>
        /// <param name="plane">The requested plane.</param>
        /// <param name="y">The row index in the requested plane.</param>
        /// <returns>The writable sample row.</returns>
        private Span<TSample> GetPlaneRow(Av1Plane plane, int y)
        {
            if (typeof(TSample) == typeof(byte))
            {
                Buffer2DRegion<byte> region = plane switch
                {
                    Av1Plane.Y => this.yPlane,
                    Av1Plane.U => this.uPlane,
                    _ => this.vPlane,
                };

                return MemoryMarshal.Cast<byte, TSample>(region.DangerousGetRowSpan(y));
            }

            int subX = plane == Av1Plane.Y ? 0 : this.subX;
            int subY = plane == Av1Plane.Y ? 0 : this.rowShift;
            return MemoryMarshal.Cast<ushort, TSample>(this.frameBuffer.GetHighBitDepthRowSpan(plane, y, subX, subY));
        }
    }
}
