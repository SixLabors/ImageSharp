// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;

using SixLabors.ImageSharp.Memory;

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
        /// Bulk version of <see cref="IPixel.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
        /// The method is DESTRUCTIVE altering the contents of <paramref name="sourceVectors"/>.
        /// </summary>
        /// <remarks>
        /// The destructive behavior is a design choice for performance reasons.
        /// In a typical use case the contents of <paramref name="sourceVectors"/> are abandoned after the conversion.
        /// </remarks>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destPixels">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
        internal virtual void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<TPixel> destPixels,
            PixelConversionModifiers modifiers)
        {
            Guard.NotNull(configuration, nameof(configuration));

            Utils.Vector4Converters.Default.FromVector4(sourceVectors, destPixels, modifiers);
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
        /// The method is DESTRUCTIVE altering the contents of <paramref name="sourceVectors"/>.
        /// </summary>
        /// <remarks>
        /// The destructive behavior is a design choice for performance reasons.
        /// In a typical use case the contents of <paramref name="sourceVectors"/> are abandoned after the conversion.
        /// </remarks>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destPixels">The <see cref="Span{T}"/> to the destination colors.</param>
        internal void FromVector4Destructive(Configuration configuration, Span<Vector4> sourceVectors, Span<TPixel> destPixels) =>
            this.FromVector4Destructive(configuration, sourceVectors, destPixels, PixelConversionModifiers.None);

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
        internal virtual void ToVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destVectors,
            PixelConversionModifiers modifiers)
        {
            Guard.NotNull(configuration, nameof(configuration));

            Utils.Vector4Converters.Default.ToVector4(sourcePixels, destVectors, modifiers);
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        internal virtual void ToVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destVectors) =>
            this.ToVector4(configuration, sourcePixels, destVectors, PixelConversionModifiers.None);

        internal virtual void From<TSourcePixel>(
            Configuration configuration,
            ReadOnlySpan<TSourcePixel> sourcePixels,
            Span<TPixel> destinationPixels)
            where TSourcePixel : struct, IPixel<TSourcePixel>
        {
            const int SliceLength = 1024;
            int numberOfSlices = sourcePixels.Length / SliceLength;
            using (IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(SliceLength))
            {
                Span<Vector4> vectorSpan = tempVectors.GetSpan();
                for (int i = 0; i < numberOfSlices; i++)
                {
                    int start = i * SliceLength;
                    ReadOnlySpan<TSourcePixel> s = sourcePixels.Slice(start, SliceLength);
                    Span<TPixel> d = destinationPixels.Slice(start, SliceLength);
                    PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan);
                    this.FromVector4Destructive(configuration, vectorSpan, d);
                }

                int endOfCompleteSlices = numberOfSlices * SliceLength;
                int remainder = sourcePixels.Length - endOfCompleteSlices;
                if (remainder > 0)
                {
                    ReadOnlySpan<TSourcePixel> s = sourcePixels.Slice(endOfCompleteSlices);
                    Span<TPixel> d = destinationPixels.Slice(endOfCompleteSlices);
                    vectorSpan = vectorSpan.Slice(0, remainder);
                    PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan);
                    this.FromVector4Destructive(configuration, vectorSpan, d);
                }
            }
        }

        /// <summary>
        /// Converts 'sourcePixels.Length' pixels from 'sourcePixels' into 'destinationPixels'.
        /// </summary>
        /// <typeparam name="TDestinationPixel">The destination pixel type.</typeparam>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source pixels.</param>
        /// <param name="destinationPixels">The <see cref="Span{T}"/> to the destination pixels.</param>
        internal virtual void To<TDestinationPixel>(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<TDestinationPixel> destinationPixels)
            where TDestinationPixel : struct, IPixel<TDestinationPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

            PixelOperations<TDestinationPixel>.Instance.From(configuration, sourcePixels, destinationPixels);
        }
    }
}
