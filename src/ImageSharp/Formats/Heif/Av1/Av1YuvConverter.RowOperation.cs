// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <content>
/// Provides the pooled parallel row operation used by AV1 YUV-to-RGB decoding.
/// </content>
internal static partial class Av1YuvConverter
{
    /// <summary>
    /// Converts one reconstructed AV1 row using worker-owned pooled component, chroma, and packed-pixel storage.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TSample">The reconstructed sample type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    private readonly struct YuvToRgbRowOperation<TPixel, TSample, TLoader> : IRowOperation<float>
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TLoader : struct, ISampleLoader<TSample>
    {
        private readonly Configuration configuration;
        private readonly Av1FrameBuffer<byte> frameBuffer;
        private readonly ImageFrame<TPixel> image;
        private readonly Buffer2DRegion<byte> yPlane;
        private readonly Buffer2DRegion<byte> uPlane;
        private readonly Buffer2DRegion<byte> vPlane;
        private readonly YuvToRgbParameters parameters;
        private readonly ConversionMode mode;
        private readonly ObuChromoSamplePosition chromaSamplePosition;
        private readonly bool isMonochrome;
        private readonly int subX;
        private readonly int subY;

        /// <summary>
        /// Initializes a new instance of the <see cref="YuvToRgbRowOperation{TPixel, TSample, TLoader}"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration used for packed-pixel conversion.</param>
        /// <param name="frameBuffer">The reconstructed AV1 frame.</param>
        /// <param name="image">The destination image frame.</param>
        /// <param name="mode">The resolved H.273 conversion mode.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public YuvToRgbRowOperation(
            Configuration configuration,
            Av1FrameBuffer<byte> frameBuffer,
            ImageFrame<TPixel> image,
            ConversionMode mode,
            in YuvToRgbParameters parameters)
        {
            this.configuration = configuration;
            this.frameBuffer = frameBuffer;
            this.image = image;
            this.mode = mode;
            this.parameters = parameters;
            this.isMonochrome = frameBuffer.ColorFormat == Av1ColorFormat.Yuv400;
            this.subX = frameBuffer.ColorConfig.SubSamplingX ? 1 : 0;
            this.subY = frameBuffer.ColorConfig.SubSamplingY ? 1 : 0;
            this.chromaSamplePosition = frameBuffer.ColorConfig.ChromaSamplePosition;
            this.yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
            this.uPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.U, this.subX, this.subY);
            this.vPlane = this.isMonochrome ? default : frameBuffer.DeriveBlockPointer(Av1Plane.V, this.subX, this.subY);
        }

        /// <inheritdoc/>
        public int GetRequiredBufferLength(Rectangle bounds)
        {
            // Three float component rows are converted in place. Color input adds two reusable chroma scratch
            // rows, and the final one or two float-sized slots per pixel back an Rgba32 or Rgba64 staging row.
            int rowCount = this.isMonochrome ? 3 : 5;
            int packedRowCount = typeof(TSample) == typeof(byte) ? 1 : 2;
            return bounds.Width * (rowCount + packedRowCount);
        }

        /// <inheritdoc/>
        public void Invoke(int y, Span<float> span)
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

            ConvertYuvToRgbRow(red, green, blue, this.isMonochrome, this.mode, in this.parameters);
            Span<float> packedStorage = span[packedOffset..];
            Span<TPixel> destination = this.image.PixelBuffer.DangerousGetRowSpan(y);
            if (typeof(TSample) == typeof(byte))
            {
                Span<Rgba32> packed = MemoryMarshal.Cast<float, Rgba32>(packedStorage)[..width];
                PackRgba32(red, green, blue, packed);
                PixelOperations<TPixel>.Instance.FromRgba32(this.configuration, packed, destination);
            }
            else
            {
                Span<Rgba64> packed = MemoryMarshal.Cast<float, Rgba64>(packedStorage)[..width];
                PackRgba64(red, green, blue, packed);
                PixelOperations<TPixel>.Instance.FromRgba64(this.configuration, packed, destination);
            }
        }
    }
}
