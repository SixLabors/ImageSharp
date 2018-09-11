// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Abstract base class for calling pixel composition functions
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel</typeparam>
    internal abstract class PixelBlender<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Blend 2 pixels together.
        /// </summary>
        /// <param name="background">The background color.</param>
        /// <param name="source">The source color.</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        /// <returns>The final pixel value after composition</returns>
        public abstract TPixel Blend(TPixel background, TPixel source, float amount);

        /// <summary>
        /// Blend 2 rows together.
        /// </summary>
        /// <param name="destination">destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        protected abstract void BlendFunction(Span<Vector4> destination, ReadOnlySpan<Vector4> background, ReadOnlySpan<Vector4> source, float amount);

        /// <summary>
        /// Blend 2 rows together.
        /// </summary>
        /// <param name="destination">destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        protected abstract void BlendFunction(Span<Vector4> destination, ReadOnlySpan<Vector4> background, ReadOnlySpan<Vector4> source, ReadOnlySpan<float> amount);

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <param name="memoryManager">memory manager to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        public void Blend(MemoryAllocator memoryManager, Span<TPixel> destination, ReadOnlySpan<TPixel> background, ReadOnlySpan<TPixel> source, ReadOnlySpan<float> amount)
        {
            this.Blend<TPixel>(memoryManager, destination, background, source, amount);
        }

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
        /// <param name="memoryManager">memory manager to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A span with values between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        public void Blend<TPixelSrc>(MemoryAllocator memoryManager, Span<TPixel> destination, ReadOnlySpan<TPixel> background, ReadOnlySpan<TPixelSrc> source, ReadOnlySpan<float> amount)
            where TPixelSrc : struct, IPixel<TPixelSrc>
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, destination.Length, nameof(amount.Length));

            using (IMemoryOwner<Vector4> buffer = memoryManager.Allocate<Vector4>(destination.Length * 3))
            {
                Span<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                Span<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                Span<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToScaledVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixelSrc>.Instance.ToScaledVector4(source, sourceSpan, destination.Length);

                this.BlendFunction(destinationSpan, backgroundSpan, sourceSpan, amount);

                PixelOperations<TPixel>.Instance.PackFromScaledVector4(destinationSpan, destination, destination.Length);
            }
        }

        /// <summary>
        /// Blends 2 rows together
        /// </summary>
        /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
        /// <param name="memoryManager">memory manager to use internally</param>
        /// <param name="destination">the destination span</param>
        /// <param name="background">the background span</param>
        /// <param name="source">the source span</param>
        /// <param name="amount">
        /// A value between 0 and 1 indicating the weight of the second source vector.
        /// At amount = 0, "from" is returned, at amount = 1, "to" is returned.
        /// </param>
        public void Blend<TPixelSrc>(MemoryAllocator memoryManager, Span<TPixel> destination, ReadOnlySpan<TPixel> background, ReadOnlySpan<TPixelSrc> source, float amount)
            where TPixelSrc : struct, IPixel<TPixelSrc>
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

            using (IMemoryOwner<Vector4> buffer = memoryManager.Allocate<Vector4>(destination.Length * 3))
            {
                Span<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                Span<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                Span<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToScaledVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixelSrc>.Instance.ToScaledVector4(source, sourceSpan, destination.Length);

                this.BlendFunction(destinationSpan, backgroundSpan, sourceSpan, amount);

                PixelOperations<TPixel>.Instance.PackFromScaledVector4(destinationSpan, destination, destination.Length);
            }
        }
    }
}
