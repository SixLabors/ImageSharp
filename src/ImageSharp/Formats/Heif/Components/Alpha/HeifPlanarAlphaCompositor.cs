// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

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
    public static void Compose<TPixel, TBuffer, TSample, TLoader>(
        Configuration configuration,
        TBuffer buffer,
        ImageFrame<TPixel> destination,
        in HeifColorConversionParameters parameters,
        Rectangle sourceRectangle,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied)
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
                ReadOnlySpan<TSample> source = buffer.GetLumaRowSpan(sourceRectangle.Y + y).Slice(sourceRectangle.X, composedWidth);
                NormalizeAlphaRow<TSample, TLoader>(source, alpha, in parameters);
                ApplyAlphaRow(configuration, destination, destinationRectangle.X, destinationRectangle.Y + y, alpha, packedAlpha, packedColor, premultiplied);
            }

            return;
        }

        // Alpha scaling must match KnownResamplers.Box. That public instance is exposed as IResampler, while
        // ResizeKernelMap requires the concrete struct so Radius and GetValue remain statically dispatched.
        // BoxResampler is stateless, making its default value behaviorally identical to the known instance.
        BoxResampler boxResampler = default;
        using ResizeKernelMap horizontalKernels = ResizeKernelMap.Calculate(in boxResampler, outputWidth, sourceWidth, configuration.MemoryAllocator);
        using ResizeKernelMap verticalKernels = ResizeKernelMap.Calculate(in boxResampler, outputHeight, sourceHeight, configuration.MemoryAllocator);
        using HeifPlanarAlphaResizeWorker<TPixel, TBuffer, TSample, TLoader> worker = new(
            configuration,
            buffer,
            destination,
            in parameters,
            sourceRectangle,
            destinationRectangle,
            horizontalKernels,
            verticalKernels,
            premultiplied);

        worker.Compose();
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
        HeifSampleConversion.ConvertSamplesToFloat<TSample, TLoader>(source, destination);

        // Alpha auxiliaries use the luma code-value range but no color matrix. TensorPrimitives keeps this bulk
        // normalization SIMD-first on every supported architecture and clamps before resampling, matching the
        // established conversion to a bounded L16 plane.
        TensorPrimitives.Subtract(destination, parameters.LumaBias, destination);
        TensorPrimitives.Multiply(destination, 1F / parameters.LumaScale, destination);
        TensorPrimitives.Clamp(destination, 0F, 1F, destination);
    }

    /// <summary>
    /// Packs and composes one normalized alpha row into the destination frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">The configuration used for pixel conversion.</param>
    /// <param name="destination">The packed destination frame receiving alpha values.</param>
    /// <param name="destinationX">The horizontal start of the destination region.</param>
    /// <param name="destinationY">The destination row receiving alpha values.</param>
    /// <param name="alpha">The normalized alpha samples.</param>
    /// <param name="packedAlpha">The reusable 16-bit alpha packing row.</param>
    /// <param name="packedColor">The reusable high-bit-depth destination color row.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    public static void ApplyAlphaRow<TPixel>(
        Configuration configuration,
        ImageFrame<TPixel> destination,
        int destinationX,
        int destinationY,
        ReadOnlySpan<float> alpha,
        Span<L16> packedAlpha,
        Span<Rgba64> packedColor,
        bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = alpha.Length;
        Span<TPixel> destinationRow = destination.PixelBuffer.DangerousGetRowSpan(destinationY).Slice(destinationX, width);
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;

        HeifSampleConversion.PackL16(alpha, packedAlpha);
        pixelOperations.ToRgba64(configuration, destinationRow, packedColor);
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

        pixelOperations.FromRgba64(configuration, packedColor, destinationRow);
    }
}
