// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public partial class PixelOperations<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private static readonly Lazy<PixelTypeInfo> LazyInfo = new Lazy<PixelTypeInfo>(() => PixelTypeInfo.Create<TPixel>(), true);

        /// <summary>
        /// Gets the global <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>
        /// </summary>
        public static PixelOperations<TPixel> Instance { get; } = default(TPixel).CreatePixelOperations();

        /// <summary>
        /// Gets the pixel type info for the given <typeparamref name="TPixel"/>.
        /// </summary>
        /// <returns>The <see cref="PixelTypeInfo"/>.</returns>
        public virtual PixelTypeInfo GetPixelTypeInfo() => LazyInfo.Value;

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
        /// <param name="destinationPixels">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
        public virtual void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<TPixel> destinationPixels,
            PixelConversionModifiers modifiers)
        {
            Guard.NotNull(configuration, nameof(configuration));

            Utils.Vector4Converters.Default.FromVector4(sourceVectors, destinationPixels, modifiers);
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
        /// <param name="destinationPixels">The <see cref="Span{T}"/> to the destination colors.</param>
        public void FromVector4Destructive(
            Configuration configuration,
            Span<Vector4> sourceVectors,
            Span<TPixel> destinationPixels)
            => this.FromVector4Destructive(configuration, sourceVectors, destinationPixels, PixelConversionModifiers.None);

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="modifiers">The <see cref="PixelConversionModifiers"/> to apply during the conversion</param>
        public virtual void ToVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destinationVectors,
            PixelConversionModifiers modifiers)
        {
            Guard.NotNull(configuration, nameof(configuration));

            Utils.Vector4Converters.Default.ToVector4(sourcePixels, destinationVectors, modifiers);
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        public void ToVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destinationVectors)
            => this.ToVector4(configuration, sourcePixels, destinationVectors, PixelConversionModifiers.None);

        /// <summary>
        /// Bulk operation that copies the <paramref name="sourcePixels"/> to <paramref name="destinationPixels"/> in
        /// <typeparamref name="TSourcePixel"/> format.
        /// </summary>
        /// <typeparam name="TSourcePixel">The destination pixel type.</typeparam>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="sourcePixels">The <see cref="ReadOnlySpan{TSourcePixel}"/> to the source pixels.</param>
        /// <param name="destinationPixels">The <see cref="Span{TPixel}"/> to the destination pixels.</param>
        public virtual void From<TSourcePixel>(
            Configuration configuration,
            ReadOnlySpan<TSourcePixel> sourcePixels,
            Span<TPixel> destinationPixels)
            where TSourcePixel : unmanaged, IPixel<TSourcePixel>
        {
            const int SliceLength = 1024;
            int numberOfSlices = sourcePixels.Length / SliceLength;

            using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(SliceLength);
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

        /// <summary>
        /// Bulk operation that copies the <paramref name="sourcePixels"/> to <paramref name="destinationPixels"/> in
        /// <typeparamref name="TDestinationPixel"/> format.
        /// </summary>
        /// <typeparam name="TDestinationPixel">The destination pixel type.</typeparam>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="sourcePixels">The <see cref="ReadOnlySpan{TPixel}"/> to the source pixels.</param>
        /// <param name="destinationPixels">The <see cref="Span{TDestinationPixel}"/> to the destination pixels.</param>
        public virtual void To<TDestinationPixel>(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<TDestinationPixel> destinationPixels)
            where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destinationPixels, nameof(destinationPixels));

            PixelOperations<TDestinationPixel>.Instance.From(configuration, sourcePixels, destinationPixels);
        }

        /// <summary>
        /// Bulk operation that packs 3 seperate RGB channels to <paramref name="destination"/>.
        /// The destination must have a padding of 3.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="redChannel">A <see cref="ReadOnlySpan{T}"/> to the red values.</param>
        /// <param name="greenChannel">A <see cref="ReadOnlySpan{T}"/> to the green values.</param>
        /// <param name="blueChannel">A <see cref="ReadOnlySpan{T}"/> to the blue values.</param>
        /// <param name="destination">A <see cref="Span{T}"/> to the destination pixels.</param>
        internal virtual void PackFromRgbPlanes(
            Configuration configuration,
            ReadOnlySpan<byte> redChannel,
            ReadOnlySpan<byte> greenChannel,
            ReadOnlySpan<byte> blueChannel,
            Span<TPixel> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));

            int count = redChannel.Length;
            Guard.IsTrue(greenChannel.Length == count, nameof(greenChannel), "Channels must be of same size!");
            Guard.IsTrue(blueChannel.Length == count, nameof(blueChannel), "Channels must be of same size!");
            Guard.IsTrue(destination.Length > count + 2, nameof(destination), "'destination' must contain a padding of 3 elements!");

            Guard.DestinationShouldNotBeTooShort(redChannel, destination, nameof(destination));

            Rgb24 rgb24 = default;
            ref byte r = ref MemoryMarshal.GetReference(redChannel);
            ref byte g = ref MemoryMarshal.GetReference(greenChannel);
            ref byte b = ref MemoryMarshal.GetReference(blueChannel);
            ref TPixel d = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                rgb24.R = Unsafe.Add(ref r, i);
                rgb24.G = Unsafe.Add(ref g, i);
                rgb24.B = Unsafe.Add(ref b, i);
                Unsafe.Add(ref d, i).FromRgb24(rgb24);
            }
        }
    }
}
