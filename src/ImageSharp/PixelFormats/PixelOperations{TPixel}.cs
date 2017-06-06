// <copyright file="PixelOperations{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public partial class PixelOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the global <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>
        /// </summary>
        public static PixelOperations<TPixel> Instance { get; } = default(TPixel).CreatePixelOperations();

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromVector4(Vector4)"/>
        /// </summary>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromVector4(Span<Vector4> sourceVectors, Span<TPixel> destColors, int count)
        {
            GuardSpans(sourceVectors, nameof(sourceVectors), destColors, nameof(destColors), count);

            ref Vector4 sourceRef = ref sourceVectors.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromVector4(sp);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToVector4(Span<TPixel> sourceColors, Span<Vector4> destVectors, int count)
        {
            GuardSpans(sourceColors, nameof(sourceColors), destVectors, nameof(destVectors), count);

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            ref Vector4 destRef = ref destVectors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToVector4();
            }
        }

        /// <summary>
        /// Verifies that the given 'source' and 'dest' spans are at least of 'minLength' size.
        /// Throwing an <see cref="ArgumentException"/> if the condition is not met.
        /// </summary>
        /// <typeparam name="TSource">The source element type</typeparam>
        /// <typeparam name="TDest">The destination element type</typeparam>
        /// <param name="source">The source span</param>
        /// <param name="sourceParamName">The source parameter name</param>
        /// <param name="dest">The destination span</param>
        /// <param name="destParamName">The destination parameter name</param>
        /// <param name="minLength">The minimum length</param>
        protected internal static void GuardSpans<TSource, TDest>(
            Span<TSource> source,
            string sourceParamName,
            Span<TDest> dest,
            string destParamName,
            int minLength)
        {
            Guard.MustBeSizedAtLeast(source, minLength, sourceParamName);
            Guard.MustBeSizedAtLeast(dest, minLength, destParamName);
        }
    }
}