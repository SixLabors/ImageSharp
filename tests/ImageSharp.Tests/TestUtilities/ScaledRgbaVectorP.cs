// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// A test pixel format that stores associated scaled components as 32-bit floating-point values.
/// </summary>
/// <remarks>
/// Its native vector range differs from its scaled range so processor tests also detect missing
/// <see cref="PixelConversionModifiers.Scale"/> conversion boundaries without introducing packed-pixel quantization.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct ScaledRgbaVectorP : IPixel<ScaledRgbaVectorP>
{
    private Vector4 scaled;

    public ScaledRgbaVectorP(Vector4 scaled) => this.scaled = Numerics.Clamp(scaled, Vector4.Zero, Vector4.One);

    public readonly Rgba32 ToRgba32()
    {
        Vector4 unassociated = this.scaled;
        Numerics.UnPremultiply(ref unassociated);
        return Rgba32.FromScaledVector4(unassociated);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.scaled;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => (this.scaled * 2F) - Vector4.One;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4()
    {
        Vector4 vector = this.scaled;
        Numerics.UnPremultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4()
    {
        Vector4 vector = this.ToUnassociatedScaledVector4();

        // The native vector is an affine view of the scaled data, so association changes occur before mapping the result.
        return (vector * 2F) - Vector4.One;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<ScaledRgbaVectorP>(
            PixelComponentInfo.Create<ScaledRgbaVectorP>(4, 32, 32, 32, 32),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Associated);

    public static PixelOperations<ScaledRgbaVectorP> CreatePixelOperations() => new ScaledPixelOperations();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromScaledVector4(Vector4 source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromVector4(Vector4 source) => new((source + Vector4.One) * .5F);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromAssociatedScaledVector4(Vector4 source)
    {
        // Clamp logical color before restoring association so out-of-range alpha cannot permanently darken it.
        Numerics.UnPremultiply(ref source);
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
        Numerics.Premultiply(ref source);
        return new(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromUnassociatedVector4(Vector4 source)
    {
        // The native vector is affine, so map it to scaled space before changing alpha representation.
        source = (source + Vector4.One) * .5F;
        return FromUnassociatedScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromAssociatedVector4(Vector4 source)
    {
        // The native vector is affine, so map it to scaled space before changing alpha representation.
        source = (source + Vector4.One) * .5F;
        return FromAssociatedScaledVector4(source);
    }

    public static ScaledRgbaVectorP FromAbgr32(Abgr32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromArgb32(Argb32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromBgra5551(Bgra5551 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromBgr24(Bgr24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromBgra32(Bgra32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromL8(L8 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromL16(L16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromLa16(La16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromLa32(La32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromRgb24(Rgb24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromRgba32(Rgba32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromRgb48(Rgb48 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public static ScaledRgbaVectorP FromRgba64(Rgba64 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    public override readonly bool Equals(object obj) => obj is ScaledRgbaVectorP other && this.Equals(other);

    public readonly bool Equals(ScaledRgbaVectorP other) => this.scaled.Equals(other.scaled);

    public override readonly int GetHashCode() => this.scaled.GetHashCode();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScaledRgbaVectorP FromUnassociatedScaledVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
        Numerics.Premultiply(ref source);
        return new ScaledRgbaVectorP(source);
    }

    /// <summary>
    /// Provides representation-aware bulk operations for the floating-point test pixel.
    /// </summary>
    private sealed class ScaledPixelOperations : AssociatedAlphaPixelOperations<ScaledRgbaVectorP>
    {
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<ScaledRgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Span<Vector4> vectors = destination[..source.Length];

            // The test pixel contains one Vector4 field, so the cast copies its associated storage without changing representation.
            MemoryMarshal.Cast<ScaledRgbaVectorP, Vector4>(source).CopyTo(vectors);
            Numerics.UnPremultiply(vectors);

            // Association is defined in scaled space, so map the unassociated result to the test format's native [-1, 1] range afterwards.
            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i] = (vectors[i] * 2F) - Vector4.One;
            }
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<ScaledRgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Span<Vector4> vectors = destination[..source.Length];
            MemoryMarshal.Cast<ScaledRgbaVectorP, Vector4>(source).CopyTo(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i] = (vectors[i] * 2F) - Vector4.One;
            }
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<ScaledRgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Span<Vector4> vectors = destination[..source.Length];
            MemoryMarshal.Cast<ScaledRgbaVectorP, Vector4>(source).CopyTo(vectors);
            Numerics.UnPremultiply(vectors);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<ScaledRgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            MemoryMarshal.Cast<ScaledRgbaVectorP, Vector4>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<ScaledRgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            MapNativeToScaled(source);
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(source), 0F, 1F);
            Numerics.Premultiply(source);
            MemoryMarshal.Cast<Vector4, ScaledRgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<ScaledRgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            MapNativeToScaled(source);
            Numerics.UnPremultiply(source);
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(source), 0F, 1F);
            Numerics.Premultiply(source);
            MemoryMarshal.Cast<Vector4, ScaledRgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<ScaledRgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // This test format stores floating-point alpha without quantization, so clamping and premultiplication can use the shared SIMD span operations directly.
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(source), 0F, 1F);
            Numerics.Premultiply(source);

            // Source now matches the pixel's sole associated Vector4 field and can be copied without a scalar conversion loop.
            MemoryMarshal.Cast<Vector4, ScaledRgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<ScaledRgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Recovering the logical color before clamping preserves it when alpha lies outside the scaled range; premultiplication then restores the associated representation.
            Numerics.UnPremultiply(source);
            Numerics.Clamp(MemoryMarshal.Cast<Vector4, float>(source), 0F, 1F);
            Numerics.Premultiply(source);
            MemoryMarshal.Cast<Vector4, ScaledRgbaVectorP>(source).CopyTo(destination);
        }

        /// <summary>
        /// Maps vectors from the test format's native numeric range to its scaled range.
        /// </summary>
        /// <param name="vectors">The vectors to convert in place.</param>
        private static void MapNativeToScaled(Span<Vector4> vectors)
        {
            // Native values use [-1, 1], so the affine inverse maps them to [0, 1] before changing alpha representation.
            for (int i = 0; i < vectors.Length; i++)
            {
                vectors[i] = (vectors[i] + Vector4.One) * .5F;
            }
        }
    }
}
