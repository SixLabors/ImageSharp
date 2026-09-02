// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Performs operation-scoped AV1 still-image frame encoding.
/// </summary>
internal static class Av1FrameEncoder
{
    /// <summary>
    /// Converts packed pixels directly into an eight-bit bordered AV1 source frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="source">The operation-owned AV1 source planes.</param>
    /// <param name="colorConfig">The color configuration written to the AV1 sequence header.</param>
    public static void PrepareSource<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<byte> source,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
        => PrepareSource<TPixel, byte, HeifByteSampleConverter>(configuration, image, source, colorConfig);

    /// <summary>
    /// Converts packed pixels directly into a high-bit-depth bordered AV1 source frame.
    /// </summary>
    /// <typeparam name="TPixel">The packed source pixel type.</typeparam>
    /// <param name="configuration">The configuration used for row-buffer allocation and pixel conversion.</param>
    /// <param name="image">The packed source frame.</param>
    /// <param name="source">The operation-owned AV1 source planes.</param>
    /// <param name="colorConfig">The color configuration written to the AV1 sequence header.</param>
    public static void PrepareSource<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<ushort> source,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
        => PrepareSource<TPixel, ushort, HeifUShortSampleConverter>(configuration, image, source, colorConfig);

    /// <summary>
    /// Converts packed pixels into native component planes and initializes every coded and physical edge sample.
    /// </summary>
    private static void PrepareSource<TPixel, TSample, TStorer>(
        Configuration configuration,
        ImageFrame<TPixel> image,
        Av1EncoderFrame<TSample> source,
        ObuColorConfig colorConfig)
        where TPixel : unmanaged, IPixel<TPixel>
        where TSample : unmanaged
        where TStorer : struct, IHeifSampleConverter<TSample>
    {
        HeifColorConversionParameters parameters = Av1YuvConverter.GetConversionParameters(
            colorConfig,
            out HeifColorConversionMode mode);

        // Conversion writes into the final bordered analysis planes. The later coding stages therefore consume the
        // native source directly without a second full-frame copy from an intermediate component buffer.
        HeifPlanarColorConverter.ConvertFromRgb<
            TPixel,
            Av1EncoderFrame<TSample>.PlanarView,
            TSample,
            TStorer>(
            configuration,
            image,
            source.View,
            in parameters,
            mode);

        source.ExtendBorders();
    }
}
