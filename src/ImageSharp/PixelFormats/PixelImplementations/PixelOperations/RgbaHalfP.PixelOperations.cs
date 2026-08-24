// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct RgbaHalfP
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="RgbaHalfP"/>.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<RgbaHalfP>
    {
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            UnpackUnassociated(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Unpack(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
            => this.ToAssociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PackUnassociated(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PackAssociated(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
            => this.FromUnassociatedVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
            => this.FromAssociatedVector4Destructive(configuration, source, destination);

        /// <summary>
        /// Expands binary16 components without changing their unit-range representation.
        /// </summary>
        /// <param name="source">The packed source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void Unpack(ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            ref ushort sourceBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(source));
            ref float destinationBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector512<float> lower, Vector512<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector512.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                    Vector512.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector512<float>.Count));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector256<float> lower, Vector256<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector256.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                    Vector256.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector256<float>.Count));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector128<float> lower, Vector128<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector128.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                    Vector128.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector128<float>.Count));
                }

                if (i < componentCount)
                {
                    // Four binary16 components form one pixel, so the only possible remainder is one complete pixel.
                    ulong remainder = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref sourceBase, (uint)i)));
                    Vector128<ushort> packed = Vector128.CreateScalarUnsafe(remainder).AsUInt16();
                    Vector128.StoreUnsafe(HalfTypeHelper.Unpack(packed).Lower, ref destinationBase, (nuint)i);
                }

                return;
            }

            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref sourceBase);
            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Unsafe.Add(ref vectorBase, (uint)pixelIndex) = Unsafe.Add(ref pixelBase, (uint)pixelIndex).ToVector4();
            }
        }

        /// <summary>
        /// Expands unassociated binary16 components and associates RGB in the same pass.
        /// </summary>
        /// <param name="source">The packed source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void UnpackAssociated(ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            ref ushort sourceBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(source));
            ref float destinationBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector512<float> lower, Vector512<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector512.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                    Vector512.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector512<float>.Count));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector256<float> lower, Vector256<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector256.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                    Vector256.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector256<float>.Count));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector128<float> lower, Vector128<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector128.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                    Vector128.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector128<float>.Count));
                }

                if (i < componentCount)
                {
                    // Four binary16 components form one pixel, so the only possible remainder is one complete pixel.
                    ulong remainder = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref sourceBase, (uint)i)));
                    Vector128<ushort> packed = Vector128.CreateScalarUnsafe(remainder).AsUInt16();
                    Vector128.StoreUnsafe(Associate(HalfTypeHelper.Unpack(packed).Lower), ref destinationBase, (nuint)i);
                }

                return;
            }

            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref sourceBase);
            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Vector4 vector = Unsafe.Add(ref pixelBase, (uint)pixelIndex).ToVector4();
                Numerics.Premultiply(ref vector);
                Unsafe.Add(ref vectorBase, (uint)pixelIndex) = vector;
            }
        }

        /// <summary>
        /// Expands associated binary16 components and unassociates RGB in the same pass.
        /// </summary>
        /// <param name="source">The packed source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void UnpackUnassociated(ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            ref ushort sourceBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(source));
            ref float destinationBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector512<float> lower, Vector512<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector512.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                    Vector512.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector512<float>.Count));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector256<float> lower, Vector256<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector256.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                    Vector256.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector256<float>.Count));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector128<float> lower, Vector128<float> upper) = HalfTypeHelper.Unpack(packed);
                    Vector128.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                    Vector128.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector128<float>.Count));
                }

                if (i < componentCount)
                {
                    // Four binary16 components form one pixel, so the only possible remainder is one complete pixel.
                    ulong remainder = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref sourceBase, (uint)i)));
                    Vector128<ushort> packed = Vector128.CreateScalarUnsafe(remainder).AsUInt16();
                    Vector128.StoreUnsafe(Unassociate(HalfTypeHelper.Unpack(packed).Lower), ref destinationBase, (nuint)i);
                }

                return;
            }

            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref sourceBase);
            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Unsafe.Add(ref vectorBase, (uint)pixelIndex) = Unsafe.Add(ref pixelBase, (uint)pixelIndex).ToUnassociatedVector4();
            }
        }

        /// <summary>
        /// Packs unassociated unit-range vectors directly into binary16 storage.
        /// </summary>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void Pack(Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            ref float sourceBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<float> lower = ClampUnit(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector512<float> upper = ClampUnit(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count)));
                    Vector512.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<float> lower = ClampUnit(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector256<float> upper = ClampUnit(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count)));
                    Vector256.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<float> lower = ClampUnit(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<float> upper = ClampUnit(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count)));
                    Vector128.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }

                if (i < componentCount)
                {
                    // Duplicate the final vector to use the two-input narrowing primitive, then store only one complete pixel.
                    Vector128<float> vector = ClampUnit(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<ushort> packed = HalfTypeHelper.Pack(vector, vector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, (uint)i)), packed.AsUInt64().GetElement(0));
                }

                return;
            }

            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref sourceBase);
            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Vector4 vector = Numerics.Clamp(Unsafe.Add(ref vectorBase, (uint)pixelIndex), Vector4.Zero, Vector4.One);
                Unsafe.Add(ref pixelBase, (uint)pixelIndex) = new RgbaHalfP(vector.X, vector.Y, vector.Z, vector.W);
            }
        }

        /// <summary>
        /// Packs vectors directly into IEEE 754 binary16 storage without applying unit-range color constraints.
        /// </summary>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void PackUnclamped(Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            ref float sourceBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<float> lower = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                    Vector512<float> upper = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count));
                    Vector512.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<float> lower = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                    Vector256<float> upper = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count));
                    Vector256.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<float> lower = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    Vector128<float> upper = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count));
                    Vector128.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }

                if (i < componentCount)
                {
                    // Duplicate the final vector to use the two-input narrowing primitive, then store only one complete pixel.
                    Vector128<float> vector = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    Vector128<ushort> packed = HalfTypeHelper.Pack(vector, vector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, (uint)i)), packed.AsUInt64().GetElement(0));
                }

                return;
            }

            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref sourceBase);
            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Vector4 vector = Unsafe.Add(ref vectorBase, (uint)pixelIndex);
                Unsafe.Add(ref pixelBase, (uint)pixelIndex) = new RgbaHalfP(vector.X, vector.Y, vector.Z, vector.W);
            }
        }

        /// <summary>
        /// Unassociates vectors and packs unassociated unit-range binary16 storage in the same pass.
        /// </summary>
        /// <param name="source">The associated source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void PackFromAssociated(Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            ref float sourceBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<float> lower = ClampUnit(Unassociate(Vector512.LoadUnsafe(ref sourceBase, (nuint)i)));
                    Vector512<float> upper = ClampUnit(Unassociate(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count))));
                    Vector512.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<float> lower = ClampUnit(Unassociate(Vector256.LoadUnsafe(ref sourceBase, (nuint)i)));
                    Vector256<float> upper = ClampUnit(Unassociate(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count))));
                    Vector256.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<float> lower = ClampUnit(Unassociate(Vector128.LoadUnsafe(ref sourceBase, (nuint)i)));
                    Vector128<float> upper = ClampUnit(Unassociate(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count))));
                    Vector128.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }

                if (i < componentCount)
                {
                    // Duplicate the final vector to use the two-input narrowing primitive, then store only one complete pixel.
                    Vector128<float> vector = ClampUnit(Unassociate(Vector128.LoadUnsafe(ref sourceBase, (nuint)i)));
                    Vector128<ushort> packed = HalfTypeHelper.Pack(vector, vector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, (uint)i)), packed.AsUInt64().GetElement(0));
                }

                return;
            }

            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref sourceBase);
            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Vector4 vector = Unsafe.Add(ref vectorBase, (uint)pixelIndex);
                Numerics.UnPremultiply(ref vector);
                vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
                Unsafe.Add(ref pixelBase, (uint)pixelIndex) = new RgbaHalfP(vector.X, vector.Y, vector.Z, vector.W);
            }
        }

        /// <summary>
        /// Associates unassociated vectors with their stored binary16 alpha and packs them in one pass.
        /// </summary>
        /// <param name="source">The unassociated source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void PackUnassociated(Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            ref float sourceBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            // Alpha is rounded to the exact binary16 value that will be stored before RGB is associated with it.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<float> lower = AssociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector512<float> upper = AssociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count)));
                    Vector512.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<float> lower = AssociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector256<float> upper = AssociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count)));
                    Vector256.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<float> lower = AssociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<float> upper = AssociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count)));
                    Vector128.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }

                if (i < componentCount)
                {
                    // Duplicate the final vector to use the two-input narrowing primitive, then store only one complete pixel.
                    Vector128<float> vector = AssociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<ushort> packed = HalfTypeHelper.Pack(vector, vector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, (uint)i)), packed.AsUInt64().GetElement(0));
                }

                return;
            }

            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref sourceBase);
            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Unsafe.Add(ref pixelBase, (uint)pixelIndex) = RgbaHalfP.FromUnassociatedVector4(Unsafe.Add(ref vectorBase, (uint)pixelIndex));
            }
        }

        /// <summary>
        /// Reassociates vectors with their stored binary16 alpha and packs them in one pass.
        /// </summary>
        /// <param name="source">The associated source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void PackAssociated(Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            ref float sourceBase = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
            ref ushort destinationBase = ref Unsafe.As<RgbaHalfP, ushort>(ref MemoryMarshal.GetReference(destination));
            int componentCount = source.Length * Vector128<float>.Count;
            int i = 0;

            // Scaling RGB by storedAlpha / inputAlpha preserves straight color when binary16 rounds the alpha channel.
            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector512<ushort>.Count; i += Vector512<ushort>.Count)
                {
                    Vector512<float> lower = ReassociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector512<float> upper = ReassociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count)));
                    Vector512.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector256<ushort>.Count; i += Vector256<ushort>.Count)
                {
                    Vector256<float> lower = ReassociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector256<float> upper = ReassociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count)));
                    Vector256.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= componentCount - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
                {
                    Vector128<float> lower = ReassociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<float> upper = ReassociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count)));
                    Vector128.StoreUnsafe(HalfTypeHelper.Pack(lower, upper), ref destinationBase, (nuint)i);
                }

                if (i < componentCount)
                {
                    // Duplicate the final vector to use the two-input narrowing primitive, then store only one complete pixel.
                    Vector128<float> vector = ReassociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                    Vector128<ushort> packed = HalfTypeHelper.Pack(vector, vector);
                    Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref destinationBase, (uint)i)), packed.AsUInt64().GetElement(0));
                }

                return;
            }

            ref Vector4 vectorBase = ref Unsafe.As<float, Vector4>(ref sourceBase);
            ref RgbaHalfP pixelBase = ref Unsafe.As<ushort, RgbaHalfP>(ref destinationBase);

            for (int pixelIndex = 0; pixelIndex < source.Length; pixelIndex++)
            {
                Unsafe.Add(ref pixelBase, (uint)pixelIndex) = RgbaHalfP.FromAssociatedVector4(Unsafe.Add(ref vectorBase, (uint)pixelIndex));
            }
        }

        /// <summary>
        /// Associates RGB with alpha while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Associate(Vector128<float> source)
        {
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> result = source * alpha;
            return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), alpha, result);
        }

        /// <summary>
        /// Associates RGB with alpha while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Associate(Vector256<float> source)
        {
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> result = source * alpha;
            return Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), alpha, result);
        }

        /// <summary>
        /// Associates RGB with alpha while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Associate(Vector512<float> source)
        {
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> result = source * alpha;
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, alpha, result);
        }

        /// <summary>
        /// Unassociates RGB while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The unassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Unassociate(Vector128<float> source)
        {
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            return Numerics.UnPremultiply(source, alpha);
        }

        /// <summary>
        /// Unassociates RGB while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The unassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Unassociate(Vector256<float> source)
        {
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            return Numerics.UnPremultiply(source, alpha);
        }

        /// <summary>
        /// Unassociates RGB while preserving each alpha lane.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The unassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Unassociate(Vector512<float> source)
        {
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            return Numerics.UnPremultiply(source, alpha);
        }

        /// <summary>
        /// Clamps vectors to the unit range represented by the pixel format.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ClampUnit(Vector128<float> source)
        {
            Vector128<float> clamped = Vector128.Min(Vector128.Max(source, Vector128<float>.Zero), Vector128<float>.One);

            // Ordered comparison is false for NaN, restoring the source lane to match the scalar clamp contract.
            return Vector128.ConditionalSelect(Vector128.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Clamps vectors to the unit range represented by the pixel format.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ClampUnit(Vector256<float> source)
        {
            Vector256<float> clamped = Vector256.Min(Vector256.Max(source, Vector256<float>.Zero), Vector256<float>.One);

            // Ordered comparison is false for NaN, restoring the source lane to match the scalar clamp contract.
            return Vector256.ConditionalSelect(Vector256.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Clamps vectors to the unit range represented by the pixel format.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ClampUnit(Vector512<float> source)
        {
            Vector512<float> clamped = Vector512.Min(Vector512.Max(source, Vector512<float>.Zero), Vector512<float>.One);

            // Ordered comparison is false for NaN, restoring the source lane to match the scalar clamp contract.
            return Vector512.ConditionalSelect(Vector512.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Associates unassociated vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> AssociateForStorage(Vector128<float> source)
        {
            source = ClampUnit(source);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> storedAlpha = HalfTypeHelper.RoundToHalf(alpha);
            Vector128<float> result = source * storedAlpha;
            return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, result);
        }

        /// <summary>
        /// Associates unassociated vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> AssociateForStorage(Vector256<float> source)
        {
            source = ClampUnit(source);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> storedAlpha = HalfTypeHelper.RoundToHalf(alpha);
            Vector256<float> result = source * storedAlpha;
            return Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), storedAlpha, result);
        }

        /// <summary>
        /// Associates unassociated vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> AssociateForStorage(Vector512<float> source)
        {
            source = ClampUnit(source);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> storedAlpha = HalfTypeHelper.RoundToHalf(alpha);
            Vector512<float> result = source * storedAlpha;
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
        }

        /// <summary>
        /// Reassociates vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ReassociateForStorage(Vector128<float> source)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> clampedAlpha = ClampUnit(alpha);
            Vector128<float> storedAlpha = HalfTypeHelper.RoundToHalf(clampedAlpha);
            Vector128<float> result = source * (storedAlpha / alpha);
            result = Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, result);
            result = Vector128.Min(Vector128.Max(result, zero), storedAlpha);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ReassociateForStorage(Vector256<float> source)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> clampedAlpha = ClampUnit(alpha);
            Vector256<float> storedAlpha = HalfTypeHelper.RoundToHalf(clampedAlpha);
            Vector256<float> result = source * (storedAlpha / alpha);
            result = Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), storedAlpha, result);
            result = Vector256.Min(Vector256.Max(result, zero), storedAlpha);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates vectors with the alpha value binary16 storage can reproduce.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The associated vectors ready for packing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ReassociateForStorage(Vector512<float> source)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> clampedAlpha = ClampUnit(alpha);
            Vector512<float> storedAlpha = HalfTypeHelper.RoundToHalf(clampedAlpha);
            Vector512<float> result = source * (storedAlpha / alpha);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector512.Min(Vector512.Max(result, zero), storedAlpha);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(alpha, zero), zero, result);
        }
    }
}
