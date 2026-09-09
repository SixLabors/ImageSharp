// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Components.Alpha;

/// <summary>
/// Composes a reconstructed HEIF luma plane directly into the alpha channel of a packed destination frame.
/// </summary>
internal static class HeifPlanarAlphaCompositor
{
    /// <summary>
    /// Composes a native codec luma plane into a destination image region without materializing an intermediate image.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <typeparam name="TBuffer">The codec adapter exposing the reconstructed component planes.</typeparam>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    /// <param name="configuration">The configuration used for pooled allocation and pixel conversion.</param>
    /// <param name="buffer">The native reconstructed component planes.</param>
    /// <param name="destination">The packed destination frame receiving alpha values.</param>
    /// <param name="parameters">The resolved H.273 component-range parameters.</param>
    /// <param name="sourceRectangle">The visible luma rectangle within the reconstructed plane.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    public static void Compose<TPixel, TBuffer, TSample, TLoader>(
        Configuration configuration,
        TBuffer buffer,
        Buffer2DRegion<TPixel> destination,
        in HeifColorConversionParameters parameters,
        Rectangle sourceRectangle,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
        where TBuffer : struct, IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        int sourceWidth = sourceRectangle.Width;
        int sourceHeight = sourceRectangle.Height;
        int outputWidth = outputSize.Width;
        int outputHeight = outputSize.Height;
        int composedWidth = destinationRectangle.Width;
        int composedHeight = destinationRectangle.Height;
        Matrix3x2 matrix = transform.GetMatrix(destinationRectangle.Size);

        if (sourceWidth == outputWidth && sourceHeight == outputHeight)
        {
            using IMemoryOwner<float> componentOwner = configuration.MemoryAllocator.Allocate<float>(composedWidth);
            using IMemoryOwner<L16> alphaOwner = configuration.MemoryAllocator.Allocate<L16>(composedWidth);
            using IMemoryOwner<Rgba64> colorOwner = configuration.MemoryAllocator.Allocate<Rgba64>(composedWidth);
            Span<float> alpha = componentOwner.GetSpan()[..composedWidth];
            Span<L16> packedAlpha = alphaOwner.GetSpan()[..composedWidth];
            Span<Rgba64> packedColor = colorOwner.GetSpan()[..composedWidth];

            // The overwhelmingly common path reads the codec plane once and immediately packs the corresponding
            // destination row. No resize maps or full-plane staging are required.
            for (int y = 0; y < composedHeight; y++)
            {
                ReadOnlySpan<TSample> source = buffer.GetLumaRowSpan(sourceRectangle.Y + destinationRectangle.Y + y)
                    .Slice(sourceRectangle.X + destinationRectangle.X, composedWidth);

                NormalizeAlphaRow<TSample, TLoader>(source, alpha, in parameters);
                ApplyAlphaRow(configuration, destination, y, alpha, packedAlpha, packedColor, premultiplied, matrix);
            }

            return;
        }

        using HeifPlanarAlphaResizeWorker<TBuffer, TSample, TLoader> worker = new(
            configuration,
            buffer,
            in parameters,
            sourceRectangle,
            destinationRectangle,
            outputSize);

        using IMemoryOwner<L16> resizedAlphaOwner = configuration.MemoryAllocator.Allocate<L16>(composedWidth);
        using IMemoryOwner<Rgba64> resizedColorOwner = configuration.MemoryAllocator.Allocate<Rgba64>(composedWidth);
        Span<L16> resizedPackedAlpha = resizedAlphaOwner.GetSpan()[..composedWidth];
        Span<Rgba64> resizedPackedColor = resizedColorOwner.GetSpan()[..composedWidth];

        for (int y = 0; y < composedHeight; y++)
        {
            ApplyAlphaRow(
                configuration, destination, y, worker.ReadRow(y), resizedPackedAlpha, resizedPackedColor, premultiplied, matrix);
        }
    }

    /// <summary>
    /// Widens and normalizes one native luma row to unbounded alpha values before packing or resampling.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TLoader">The SIMD widening operations for the sample type.</typeparam>
    /// <param name="source">The native luma samples.</param>
    /// <param name="destination">The normalized alpha samples.</param>
    /// <param name="parameters">The resolved H.273 component-range parameters.</param>
    public static void NormalizeAlphaRow<TSample, TLoader>(
        ReadOnlySpan<TSample> source,
        Span<float> destination,
        in HeifColorConversionParameters parameters)
        where TSample : unmanaged
        where TLoader : struct, IHeifSampleConverter<TSample>
    {
        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(source, destination, parameters.LumaBias, parameters.LumaScale);

        // Normalization divides by the encoded range during widening. Multiplication by a rounded
        // reciprocal can move alpha across a final half-unit boundary. Clamp before resampling.
        TensorPrimitives.Clamp(destination, 0F, 1F, destination);
    }

    /// <summary>
    /// Packs and composes one normalized alpha row into the destination frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="destination">The packed destination frame receiving alpha values.</param>
    /// <param name="destinationY">The source row mapped into the destination region.</param>
    /// <param name="alpha">The normalized alpha samples.</param>
    /// <param name="packedAlpha">The reusable 16-bit alpha packing row.</param>
    /// <param name="packedColor">The reusable high-bit-depth destination color row.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="matrix">The matrix resolved once for the destination region.</param>
    public static void ApplyAlphaRow<TPixel>(
        Configuration configuration,
        Buffer2DRegion<TPixel> destination,
        int destinationY,
        ReadOnlySpan<float> alpha,
        Span<L16> packedAlpha,
        Span<Rgba64> packedColor,
        bool premultiplied,
        Matrix3x2 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = alpha.Length;
        Point rowStart = HeifPixelTransform.Transform(0, destinationY, matrix);
        Size rowStep = new((int)matrix.M11, (int)matrix.M12);
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;

        HeifSampleConversion.PackL16(alpha, packedAlpha);
        if (matrix.IsIdentity)
        {
            pixelOperations.ToRgba64(configuration, destination.DangerousGetRowSpan(destinationY), packedColor);
        }
        else
        {
            // Gather only this alpha row's color pixels from their final coordinates. No second image is needed.
            Point point = rowStart;
            for (int x = 0; x < width; x++)
            {
                packedColor[x] = Rgba64.FromScaledVector4(destination.DangerousGetRowSpan(point.Y)[point.X].ToScaledVector4());
                point += rowStep;
            }
        }

        if (premultiplied)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba64 pixel = packedColor[x];
                pixel.A = packedAlpha[x].PackedValue;

                // Transparent associated samples have no recoverable color. Nonzero samples use the pixel type's
                // established conversion so unassociation retains ImageSharp's clamping and rounding behavior.
                packedColor[x] = pixel.A == 0
                    ? new Rgba64(0, 0, 0, 0)
                    : Rgba64.FromAssociatedScaledVector4(pixel.ToScaledVector4());
            }
        }
        else
        {
            for (int x = 0; x < width; x++)
            {
                packedColor[x].A = packedAlpha[x].PackedValue;
            }
        }

        if (matrix.IsIdentity)
        {
            pixelOperations.FromRgba64(configuration, packedColor, destination.DangerousGetRowSpan(destinationY));
        }
        else
        {
            Point point = rowStart;
            for (int x = 0; x < width; x++)
            {
                destination.DangerousGetRowSpan(point.Y)[point.X] = TPixel.FromRgba64(packedColor[x]);
                point += rowStep;
            }
        }
    }
}
