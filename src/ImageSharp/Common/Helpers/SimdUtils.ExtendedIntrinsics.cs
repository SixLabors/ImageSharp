// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Implementation methods based on newer <see cref="Vector{T}"/> API-s (Vector.Widen, Vector.Narrow, Vector.ConvertTo*).
    /// Only accelerated only on RyuJIT having dotnet/coreclr#10662 merged (.NET Core 2.1+ .NET 4.7.2+)
    /// See:
    /// https://github.com/dotnet/coreclr/pull/10662
    /// API Proposal:
    /// https://github.com/dotnet/corefx/issues/15957
    /// </summary>
    public static class ExtendedIntrinsics
    {
        public static bool IsAvailable { get; } = Vector.IsHardwareAccelerated;

        /// <summary>
        /// Widen and convert a vector of <see cref="short"/> values into 2 vectors of <see cref="float"/>-s.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ConvertToSingle(
            Vector<short> source,
            out Vector<float> dest1,
            out Vector<float> dest2)
        {
            Vector.Widen(source, out Vector<int> i1, out Vector<int> i2);
            dest1 = Vector.ConvertToSingle(i1);
            dest2 = Vector.ConvertToSingle(i2);
        }

        /// <summary>
        /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ByteToNormalizedFloatReduce(
            ref ReadOnlySpan<byte> source,
            ref Span<float> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

            if (!IsAvailable)
            {
                return;
            }

            int remainder = Numerics.ModuloP2(source.Length, Vector<byte>.Count);
            int adjustedCount = source.Length - remainder;

            if (adjustedCount > 0)
            {
                ByteToNormalizedFloat(source[..adjustedCount], dest[..adjustedCount]);

                source = source[adjustedCount..];
                dest = dest[adjustedCount..];
            }
        }

        /// <summary>
        /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void NormalizedFloatToByteSaturateReduce(
            ref ReadOnlySpan<float> source,
            ref Span<byte> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

            if (!IsAvailable)
            {
                return;
            }

            int remainder = Numerics.ModuloP2(source.Length, Vector<byte>.Count);
            int adjustedCount = source.Length - remainder;

            if (adjustedCount > 0)
            {
                NormalizedFloatToByteSaturate(source[..adjustedCount], dest[..adjustedCount]);

                source = source[adjustedCount..];
                dest = dest[adjustedCount..];
            }
        }

        /// <summary>
        /// Implementation <see cref="SimdUtils.ByteToNormalizedFloat"/>, which is faster on new RyuJIT runtime.
        /// </summary>
        internal static void ByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
        {
            VerifySpanInput(source, dest, Vector<byte>.Count);

            nuint n = dest.VectorCount<byte>();

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(source));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dest));

            for (nuint i = 0; i < n; i++)
            {
                Vector<byte> b = Extensions.UnsafeAdd(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                Vector<float> f0 = ConvertToSingle(w0);
                Vector<float> f1 = ConvertToSingle(w1);
                Vector<float> f2 = ConvertToSingle(w2);
                Vector<float> f3 = ConvertToSingle(w3);

                ref Vector<float> d = ref Extensions.UnsafeAdd(ref destBase, i * 4);
                d = f0;
                Extensions.UnsafeAdd(ref d, 1) = f1;
                Extensions.UnsafeAdd(ref d, 2) = f2;
                Extensions.UnsafeAdd(ref d, 3) = f3;
            }
        }

        /// <summary>
        /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
        /// </summary>
        internal static void NormalizedFloatToByteSaturate(
            ReadOnlySpan<float> source,
            Span<byte> dest)
        {
            VerifySpanInput(source, dest, Vector<byte>.Count);

            nuint n = dest.VectorCount<byte>();

            ref Vector<float> sourceBase =
                ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector<byte> destBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(dest));

            for (nuint i = 0; i < n; i++)
            {
                ref Vector<float> s = ref Extensions.UnsafeAdd(ref sourceBase, i * 4);

                Vector<float> f0 = s;
                Vector<float> f1 = Extensions.UnsafeAdd(ref s, 1);
                Vector<float> f2 = Extensions.UnsafeAdd(ref s, 2);
                Vector<float> f3 = Extensions.UnsafeAdd(ref s, 3);

                Vector<uint> w0 = ConvertToUInt32(f0);
                Vector<uint> w1 = ConvertToUInt32(f1);
                Vector<uint> w2 = ConvertToUInt32(f2);
                Vector<uint> w3 = ConvertToUInt32(f3);

                var u0 = Vector.Narrow(w0, w1);
                var u1 = Vector.Narrow(w2, w3);

                Extensions.UnsafeAdd(ref destBase, i) = Vector.Narrow(u0, u1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<uint> ConvertToUInt32(Vector<float> vf)
        {
            var maxBytes = new Vector<float>(255f);
            vf *= maxBytes;
            vf += new Vector<float>(0.5f);
            vf = Vector.Min(Vector.Max(vf, Vector<float>.Zero), maxBytes);
            var vi = Vector.ConvertToInt32(vf);
            return Vector.AsVectorUInt32(vi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<float> ConvertToSingle(Vector<uint> u)
        {
            var vi = Vector.AsVectorInt32(u);
            var v = Vector.ConvertToSingle(vi);
            v *= new Vector<float>(1f / 255f);
            return v;
        }
    }
}
