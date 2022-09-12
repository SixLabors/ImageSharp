// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Abstract base class for calling pixel composition functions
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel</typeparam>
    public abstract class PixelBlender<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Blend 2 pixels together.
        /// </summary>
        /// <param name="background">The background color.</param>
        /// <param name="source">The source color.</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        /// <returns>The final pixel value after composition.</returns>
        public abstract TPixel Blend(TPixel background, TPixel source, float amount);

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
        /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        public void Blend<TPixelSrc>(
            Configuration configuration,
            Span<TPixel> destination,
            ReadOnlySpan<TPixel> background,
            ReadOnlySpan<TPixelSrc> source,
            float amount)
            where TPixelSrc : unmanaged, IPixel<TPixelSrc>
        {
            int maxLength = destination.Length;
            Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
            Span<Vector4> destinationVectors = buffer.Slice(0, maxLength);
            Span<Vector4> backgroundVectors = buffer.Slice(maxLength, maxLength);
            Span<Vector4> sourceVectors = buffer.Slice(maxLength * 2, maxLength);

            PixelOperations<TPixel>.Instance.ToVector4(configuration, background[..maxLength], backgroundVectors, PixelConversionModifiers.Scale);
            PixelOperations<TPixelSrc>.Instance.ToVector4(configuration, source[..maxLength], sourceVectors, PixelConversionModifiers.Scale);

            this.BlendFunction(destinationVectors, backgroundVectors, sourceVectors, amount);

            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, destinationVectors[..maxLength], destination, PixelConversionModifiers.Scale);
        }

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        public void Blend(
            Configuration configuration,
            Span<TPixel> destination,
            ReadOnlySpan<TPixel> background,
            ReadOnlySpan<TPixel> source,
            ReadOnlySpan<float> amount)
            => this.Blend<TPixel>(configuration, destination, background, source, amount);

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
        /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        public void Blend<TPixelSrc>(
            Configuration configuration,
            Span<TPixel> destination,
            ReadOnlySpan<TPixel> background,
            ReadOnlySpan<TPixelSrc> source,
            ReadOnlySpan<float> amount)
            where TPixelSrc : unmanaged, IPixel<TPixelSrc>
        {
            int maxLength = destination.Length;
            Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));

            using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
            Span<Vector4> destinationVectors = buffer.Slice(0, maxLength);
            Span<Vector4> backgroundVectors = buffer.Slice(maxLength, maxLength);
            Span<Vector4> sourceVectors = buffer.Slice(maxLength * 2, maxLength);

            PixelOperations<TPixel>.Instance.ToVector4(configuration, background[..maxLength], backgroundVectors, PixelConversionModifiers.Scale);
            PixelOperations<TPixelSrc>.Instance.ToVector4(configuration, source[..maxLength], sourceVectors, PixelConversionModifiers.Scale);

            this.BlendFunction(destinationVectors, backgroundVectors, sourceVectors, amount);

            PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, destinationVectors[..maxLength], destination, PixelConversionModifiers.Scale);
        }

        /// <summary>
        /// Blend 2 rows together.
        /// </summary>
        /// <param name="destination">destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        protected abstract void BlendFunction(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            float amount);

        /// <summary>
        /// Blend 2 rows together.
        /// </summary>
        /// <param name="destination">destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
        /// </param>
        protected abstract void BlendFunction(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            ReadOnlySpan<float> amount);
    }
}
