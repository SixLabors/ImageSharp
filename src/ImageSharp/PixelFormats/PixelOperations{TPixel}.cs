// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides bulk conversion operations for pixel buffers of type <typeparamref name="TPixel"/>.
/// </summary>
/// <remarks>
/// The default conversion behavior is suitable for pixel formats that store unassociated alpha.
/// Pixel formats that store associated alpha should derive their operations from
/// <see cref="AssociatedAlphaPixelOperations{TPixel}"/>.
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public partial class PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the shared <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <exception cref="TypeInitializationException">The pixel type's factory could not create the shared instance.</exception>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static PixelOperations<TPixel> Instance => InstanceHolder.Value;
#pragma warning restore CA1000 // Do not declare static members on generic types

    /// <summary>
    /// Converts pixels to unassociated vectors in the pixel format's native numeric range.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    protected virtual void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => Utils.Vector4Converters.Default.ToVector4(source, destination, PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Converts pixels to associated vectors in the pixel format's native numeric range.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    protected virtual void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => Utils.Vector4Converters.Default.ToVector4(source, destination, PixelConversionModifiers.Premultiply);

    /// <summary>
    /// Converts pixels to the unassociated scaled vector representation used by color operations.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    protected virtual void ToUnassociatedScaledVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination)
        => Utils.Vector4Converters.Default.ToVector4(source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Converts pixels to associated scaled vectors.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    protected virtual void ToAssociatedScaledVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destination)
        => Utils.Vector4Converters.Default.ToVector4(source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

    /// <summary>
    /// Converts unassociated vectors in the pixel format's native numeric range to pixels.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors. Implementations may modify this buffer during conversion.</param>
    /// <param name="destination">The destination pixels.</param>
    protected virtual void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => Utils.Vector4Converters.Default.FromVector4(source, destination, PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Converts associated vectors in the pixel format's native numeric range to pixels.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors. Implementations may modify this buffer during conversion.</param>
    /// <param name="destination">The destination pixels.</param>
    protected virtual void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => Utils.Vector4Converters.Default.FromVector4(source, destination, PixelConversionModifiers.Premultiply);

    /// <summary>
    /// Converts unassociated scaled vectors to pixels.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors. Implementations may modify this buffer during conversion.</param>
    /// <param name="destination">The destination pixels.</param>
    protected virtual void FromUnassociatedScaledVector4Destructive(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination)
        => Utils.Vector4Converters.Default.FromVector4(source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Converts associated scaled vectors to pixels.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors. Implementations may modify this buffer during conversion.</param>
    /// <param name="destination">The destination pixels.</param>
    protected virtual void FromAssociatedScaledVector4Destructive(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination)
        => Utils.Vector4Converters.Default.FromVector4(source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);

    /// <summary>
    /// Gets the pixel type info for the given <typeparamref name="TPixel"/>.
    /// </summary>
    /// <returns>The <see cref="PixelTypeInfo"/>.</returns>
    public PixelTypeInfo GetPixelTypeInfo() => TPixel.GetPixelTypeInfo();

    /// <summary>
    /// Converts vectors to pixels using the numeric range, alpha representation, and companding specified by <paramref name="modifiers"/>.
    /// The contents of <paramref name="sourceVectors"/> are not preserved.
    /// </summary>
    /// <remarks>
    /// Callers must not rely on the contents of <paramref name="sourceVectors"/> after this method returns.
    /// </remarks>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sourceVectors">The source vectors. Their contents may be modified during conversion.</param>
    /// <param name="destination">The destination pixels.</param>
    /// <param name="modifiers">The representation of the source vectors and the transformations applied before storing the pixels.</param>
    public virtual void FromVector4Destructive(
        Configuration configuration,
        Span<Vector4> sourceVectors,
        Span<TPixel> destination,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(sourceVectors, destination, nameof(destination));

        bool associated = modifiers.IsDefined(PixelConversionModifiers.Premultiply);
        bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

        if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
        {
            // Transfer functions operate on straight color components, so restore straight RGB before compressing associated input.
            if (associated)
            {
                Numerics.UnPremultiply(sourceVectors);
            }

            SRgbCompanding.Compress(sourceVectors);

            if (scaled)
            {
                this.FromUnassociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
            else
            {
                this.FromUnassociatedVector4Destructive(configuration, sourceVectors, destination);
            }

            return;
        }

        if (scaled)
        {
            if (associated)
            {
                this.FromAssociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
            else
            {
                this.FromUnassociatedScaledVector4Destructive(configuration, sourceVectors, destination);
            }
        }
        else if (associated)
        {
            this.FromAssociatedVector4Destructive(configuration, sourceVectors, destination);
        }
        else
        {
            this.FromUnassociatedVector4Destructive(configuration, sourceVectors, destination);
        }
    }

    /// <summary>
    /// Bulk version of <see cref="IPixel{TPixel}.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
    /// The contents of <paramref name="sourceVectors"/> are not preserved.
    /// </summary>
    /// <remarks>
    /// Callers must not rely on the contents of <paramref name="sourceVectors"/> after this method returns.
    /// </remarks>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="sourceVectors">The source vectors. Their contents may be modified during conversion.</param>
    /// <param name="destination">The <see cref="Span{T}"/> to the destination colors.</param>
    public void FromVector4Destructive(
        Configuration configuration,
        Span<Vector4> sourceVectors,
        Span<TPixel> destination)
        => this.FromVector4Destructive(configuration, sourceVectors, destination, PixelConversionModifiers.None);

    /// <summary>
    /// Converts pixels to vectors using the numeric range, alpha representation, and companding specified by <paramref name="modifiers"/>.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destinationVectors">The destination vectors.</param>
    /// <param name="modifiers">The requested vector representation and the transformations applied after reading the pixels.</param>
    public virtual void ToVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destinationVectors,
        PixelConversionModifiers modifiers)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destinationVectors, nameof(destinationVectors));

        bool associated = modifiers.IsDefined(PixelConversionModifiers.Premultiply);
        bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

        if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
        {
            // Expand straight RGB first because a transfer function applied to associated components would make color depend on alpha.
            if (scaled)
            {
                this.ToUnassociatedScaledVector4(configuration, source, destinationVectors);
            }
            else
            {
                this.ToUnassociatedVector4(configuration, source, destinationVectors);
            }

            Span<Vector4> converted = destinationVectors[..source.Length];
            SRgbCompanding.Expand(converted);

            if (associated)
            {
                Numerics.Premultiply(converted);
            }

            return;
        }

        if (scaled)
        {
            if (associated)
            {
                this.ToAssociatedScaledVector4(configuration, source, destinationVectors);
            }
            else
            {
                this.ToUnassociatedScaledVector4(configuration, source, destinationVectors);
            }
        }
        else if (associated)
        {
            this.ToAssociatedVector4(configuration, source, destinationVectors);
        }
        else
        {
            this.ToUnassociatedVector4(configuration, source, destinationVectors);
        }
    }

    /// <summary>
    /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
    /// </summary>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
    /// <param name="source">The <see cref="Span{T}"/> to the source colors.</param>
    /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
    public void ToVector4(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<Vector4> destinationVectors)
        => this.ToVector4(configuration, source, destinationVectors, PixelConversionModifiers.None);

    /// <summary>
    /// Bulk operation that converts <paramref name="source"/> pixels from <typeparamref name="TSourcePixel"/> format to
    /// <typeparamref name="TPixel"/> destination pixels.
    /// </summary>
    /// <typeparam name="TSourcePixel">The source pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TSourcePixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TPixel}"/> to the destination pixels.</param>
    public virtual void From<TSourcePixel>(
        Configuration configuration,
        ReadOnlySpan<TSourcePixel> source,
        Span<TPixel> destination)
        where TSourcePixel : unmanaged, IPixel<TSourcePixel>
    {
        const int sliceLength = 1024;
        int numberOfSlices = source.Length / sliceLength;

        using IMemoryOwner<Vector4> tempVectors = configuration.MemoryAllocator.Allocate<Vector4>(sliceLength);
        Span<Vector4> vectorSpan = tempVectors.GetSpan();

        for (int i = 0; i < numberOfSlices; i++)
        {
            int start = i * sliceLength;
            ReadOnlySpan<TSourcePixel> s = source.Slice(start, sliceLength);
            Span<TPixel> d = destination.Slice(start, sliceLength);
            PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            this.FromVector4Destructive(configuration, vectorSpan, d, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        }

        int endOfCompleteSlices = numberOfSlices * sliceLength;
        int remainder = source.Length - endOfCompleteSlices;
        if (remainder > 0)
        {
            ReadOnlySpan<TSourcePixel> s = source[endOfCompleteSlices..];
            Span<TPixel> d = destination[endOfCompleteSlices..];
            vectorSpan = vectorSpan[..remainder];
            PixelOperations<TSourcePixel>.Instance.ToVector4(configuration, s, vectorSpan, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            this.FromVector4Destructive(configuration, vectorSpan, d, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        }
    }

    /// <summary>
    /// Bulk operation that copies the <paramref name="source"/> to <paramref name="destination"/> in
    /// <typeparamref name="TDestinationPixel"/> format.
    /// </summary>
    /// <typeparam name="TDestinationPixel">The destination pixel type.</typeparam>
    /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations.</param>
    /// <param name="source">The <see cref="ReadOnlySpan{TPixel}"/> to the source pixels.</param>
    /// <param name="destination">The <see cref="Span{TDestinationPixel}"/> to the destination pixels.</param>
    public virtual void To<TDestinationPixel>(
        Configuration configuration,
        ReadOnlySpan<TPixel> source,
        Span<TDestinationPixel> destination)
        where TDestinationPixel : unmanaged, IPixel<TDestinationPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (typeof(TPixel) == typeof(TDestinationPixel))
        {
            // An identical destination stores the same representation, including associated alpha, so copying preserves every packed component without a lossy representation round trip.
            MemoryMarshal.Cast<TPixel, TDestinationPixel>(source).CopyTo(destination);
            return;
        }

        PixelOperations<TDestinationPixel>.Instance.From(configuration, source, destination);
    }

    /// <summary>
    /// Bulk operation that packs 3 separate RGB channels to <paramref name="destination"/>.
    /// The destination must have a padding of 3.
    /// </summary>
    /// <param name="redChannel">A <see cref="ReadOnlySpan{T}"/> to the red values.</param>
    /// <param name="greenChannel">A <see cref="ReadOnlySpan{T}"/> to the green values.</param>
    /// <param name="blueChannel">A <see cref="ReadOnlySpan{T}"/> to the blue values.</param>
    /// <param name="destination">A <see cref="Span{T}"/> to the destination pixels.</param>
    internal virtual void PackFromRgbPlanes(
        ReadOnlySpan<byte> redChannel,
        ReadOnlySpan<byte> greenChannel,
        ReadOnlySpan<byte> blueChannel,
        Span<TPixel> destination)
    {
        int count = redChannel.Length;
        GuardPackFromRgbPlanes(greenChannel, blueChannel, destination, count);

        Rgb24 rgb24 = default;
        ref byte r = ref MemoryMarshal.GetReference(redChannel);
        ref byte g = ref MemoryMarshal.GetReference(greenChannel);
        ref byte b = ref MemoryMarshal.GetReference(blueChannel);
        ref TPixel d = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            rgb24.R = Unsafe.Add(ref r, i);
            rgb24.G = Unsafe.Add(ref g, i);
            rgb24.B = Unsafe.Add(ref b, i);
            Unsafe.Add(ref d, i) = TPixel.FromRgb24(rgb24);
        }
    }

    /// <summary>
    /// Bulk operation that unpacks pixels from <paramref name="source"/>
    /// into 3 separate RGB channels.
    /// </summary>
    /// <param name="redChannel">A <see cref="ReadOnlySpan{T}"/> to the red values.</param>
    /// <param name="greenChannel">A <see cref="ReadOnlySpan{T}"/> to the green values.</param>
    /// <param name="blueChannel">A <see cref="ReadOnlySpan{T}"/> to the blue values.</param>
    /// <param name="source">A <see cref="Span{T}"/> to the destination pixels.</param>
    internal virtual void UnpackIntoRgbPlanes(
        Span<float> redChannel,
        Span<float> greenChannel,
        Span<float> blueChannel,
        ReadOnlySpan<TPixel> source)
    {
        GuardUnpackIntoRgbPlanes(redChannel, greenChannel, blueChannel, source);

        // TODO: This can be much faster.
        // Convert to Rgba32 first using pixel operations then use the R, G, B properties.
        int count = source.Length;

        ref float r = ref MemoryMarshal.GetReference(redChannel);
        ref float g = ref MemoryMarshal.GetReference(greenChannel);
        ref float b = ref MemoryMarshal.GetReference(blueChannel);
        ref TPixel src = ref MemoryMarshal.GetReference(source);
        for (nuint i = 0; i < (uint)count; i++)
        {
            Rgba32 rgba32 = Unsafe.Add(ref src, i).ToRgba32();
            Unsafe.Add(ref r, i) = rgba32.R;
            Unsafe.Add(ref g, i) = rgba32.G;
            Unsafe.Add(ref b, i) = rgba32.B;
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void GuardUnpackIntoRgbPlanes(Span<float> redChannel, Span<float> greenChannel, Span<float> blueChannel, ReadOnlySpan<TPixel> source)
    {
        Guard.IsTrue(greenChannel.Length == redChannel.Length, nameof(greenChannel), "Channels must be of same size!");
        Guard.IsTrue(blueChannel.Length == redChannel.Length, nameof(blueChannel), "Channels must be of same size!");
        Guard.IsTrue(source.Length <= redChannel.Length, nameof(source), "'source' span should not be bigger than the destination channels!");
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    internal static void GuardPackFromRgbPlanes(ReadOnlySpan<byte> greenChannel, ReadOnlySpan<byte> blueChannel, Span<TPixel> destination, int count)
    {
        Guard.IsTrue(greenChannel.Length == count, nameof(greenChannel), "Channels must be of same size!");
        Guard.IsTrue(blueChannel.Length == count, nameof(blueChannel), "Channels must be of same size!");
        Guard.IsTrue(destination.Length > count + 2, nameof(destination), "'destination' must contain a padding of 3 elements!");
    }

    /// <summary>
    /// Creates and retains the bulk operations instance when <see cref="Instance"/> is first accessed.
    /// </summary>
    /// <remarks>
    /// Each closed <see cref="PixelOperations{TPixel}"/> type has its own holder and instance.
    /// The holder is separate so calls to the outer type's other static helpers do not create the shared instance.
    /// The explicit static constructor prevents early field initialization. The runtime synchronizes that constructor,
    /// so concurrent callers share one instance without additional locks or a factory delegate.
    /// The constructor calls the existing pixel factory directly. Mono WASM AOT can compile this ordinary static abstract call.
    /// Creating a delegate directly from that interface method instead encounters the compiler defect described in
    /// <see href="https://github.com/dotnet/runtime/pull/131880">dotnet/runtime#131880</see> on affected runtimes.
    /// No list of pixel types or compile-only conversion calls is required by this initialization path.
    /// If the factory throws, the runtime leaves this holder uninitialized and reports a <see cref="TypeInitializationException"/>.
    /// It does not retry the factory on later accesses. The original exception is available through the exception's inner exception.
    /// </remarks>
    private static class InstanceHolder
    {
        /// <summary>
        /// The bulk operations instance returned by this pixel type's factory.
        /// </summary>
        public static readonly PixelOperations<TPixel> Value;

        /// <summary>
        /// Initializes static members of the <see cref="InstanceHolder"/> class.
        /// </summary>
        static InstanceHolder()
        {
            // Keep this as a direct call. A delegate from the static abstract method uses Mono's faulty delegate-construction path.
            Value = TPixel.CreatePixelOperations();
        }
    }
}
