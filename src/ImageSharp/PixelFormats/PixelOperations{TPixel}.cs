// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
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
        /// <param name="destinationColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromVector4(ReadOnlySpan<Vector4> sourceVectors, Span<TPixel> destinationColors, int count)
        {
            GuardSpans(sourceVectors, nameof(sourceVectors), destinationColors, nameof(destinationColors), count);

            ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
            ref TPixel destRef = ref MemoryMarshal.GetReference(destinationColors);

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
        /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToVector4(ReadOnlySpan<TPixel> sourceColors, Span<Vector4> destinationVectors, int count)
        {
            GuardSpans(sourceColors, nameof(sourceColors), destinationVectors, nameof(destinationVectors), count);

            ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourceColors);
            ref Vector4 destRef = ref MemoryMarshal.GetReference(destinationVectors);

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToVector4();
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromScaledVector4(Vector4)"/>
        /// </summary>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destinationColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromScaledVector4(ReadOnlySpan<Vector4> sourceVectors, Span<TPixel> destinationColors, int count)
        {
            GuardSpans(sourceVectors, nameof(sourceVectors), destinationColors, nameof(destinationColors), count);

            ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
            ref TPixel destRef = ref MemoryMarshal.GetReference(destinationColors);

            for (int i = 0; i < count; i++)
            {
                ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromScaledVector4(sp);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToScaledVector4()"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToScaledVector4(ReadOnlySpan<TPixel> sourceColors, Span<Vector4> destinationVectors, int count)
        {
            GuardSpans(sourceColors, nameof(sourceColors), destinationVectors, nameof(destinationVectors), count);

            ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourceColors);
            ref Vector4 destRef = ref MemoryMarshal.GetReference(destinationVectors);

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToScaledVector4();
            }
        }

        /// <summary>
        /// Verifies that the given 'source' and 'destination' spans are at least of 'minLength' size.
        /// Throwing an <see cref="ArgumentException"/> if the condition is not met.
        /// </summary>
        /// <typeparam name="TSource">The source element type</typeparam>
        /// <typeparam name="TDest">The destination element type</typeparam>
        /// <param name="source">The source span</param>
        /// <param name="sourceParamName">The source parameter name</param>
        /// <param name="destination">The destination span</param>
        /// <param name="destinationParamName">The destination parameter name</param>
        /// <param name="minLength">The minimum length</param>
        protected internal static void GuardSpans<TSource, TDest>(
            ReadOnlySpan<TSource> source,
            string sourceParamName,
            Span<TDest> destination,
            string destinationParamName,
            int minLength)
        {
            Guard.MustBeSizedAtLeast(source, minLength, sourceParamName);
            Guard.MustBeSizedAtLeast(destination, minLength, destinationParamName);
        }
    }
}