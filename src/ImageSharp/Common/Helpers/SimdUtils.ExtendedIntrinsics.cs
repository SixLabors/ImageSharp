using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        /// <summary>
        /// Methods accelerated only in RyuJIT having dotnet/coreclr#10662 merged (.NET Core 2.1+ .NET 4.7.2+)
        /// PR:
        /// https://github.com/dotnet/coreclr/pull/10662
        /// API Proposal:
        /// https://github.com/dotnet/corefx/issues/15957
        /// </summary>
        public static class ExtendedIntrinsics
        {
            public static bool IsAvailable { get; } =
#if NETCOREAPP2_1
                // TODO: Also available in .NET 4.7.2, we need to add a build target!
                Vector.IsHardwareAccelerated;
#else
                false;
#endif

            /// <summary>
            /// <see cref="BulkConvertByteToNormalizedFloat"/> as much elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [Conditional("NETCOREAPP2_1")]
            internal static void BulkConvertByteToNormalizedFloatReduce(
                ref ReadOnlySpan<byte> source,
                ref Span<float> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

                if (IsAvailable)
                {
                    int remainder = source.Length % Vector<byte>.Count;
                    int alignedCount = source.Length - remainder;

                    if (alignedCount > 0)
                    {
                        BulkConvertByteToNormalizedFloat(source.Slice(0, alignedCount), dest.Slice(0, alignedCount));

                        source = source.Slice(alignedCount);
                        dest = dest.Slice(alignedCount);
                    }
                }
            }

            /// <summary>
            /// A variant of <see cref="BasicIntrinsics256.BulkConvertByteToNormalizedFloat"/>, which is faster on new RyuJIT runtime.
            /// </summary>
            // ReSharper disable once MemberHidesStaticFromOuterClass
            internal static void BulkConvertByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
            {
                DebugGuard.IsTrue(
                    dest.Length % Vector<byte>.Count == 0,
                    nameof(source),
                    "dest.Length should be divisible by Vector<byte>.Count!");

                int n = dest.Length / Vector<byte>.Count;

                ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dest));

                var scale = new Vector<float>(1f / 255f);

                for (int i = 0; i < n; i++)
                {
                    Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                    Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                    Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                    Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                    Vector<float> f0 = ConvertToSingle(w0, scale);
                    Vector<float> f1 = ConvertToSingle(w1, scale);
                    Vector<float> f2 = ConvertToSingle(w2, scale);
                    Vector<float> f3 = ConvertToSingle(w3, scale);

                    ref Vector<float> d = ref Unsafe.Add(ref destBase, i * 4);
                    d = f0;
                    Unsafe.Add(ref d, 1) = f1;
                    Unsafe.Add(ref d, 2) = f2;
                    Unsafe.Add(ref d, 3) = f3;
                }
            }

            /// <summary>
            /// <see cref="BulkConvertNormalizedFloatToByteClampOverflows"/> as much elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [Conditional("NETCOREAPP2_1")]
            internal static void BulkConvertNormalizedFloatToByteClampOverflowsReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same size!");

                if (IsAvailable)
                {
                    int remainder = source.Length % Vector<byte>.Count;
                    int alignedCount = source.Length - remainder;

                    if (alignedCount > 0)
                    {
                        BulkConvertNormalizedFloatToByteClampOverflows(source.Slice(0, alignedCount), dest.Slice(0, alignedCount));

                        source = source.Slice(alignedCount);
                        dest = dest.Slice(alignedCount);
                    }
                }
            }

            /// <summary>
            /// A variant of <see cref="BasicIntrinsics256.BulkConvertNormalizedFloatToByteClampOverflows"/>, which is faster on new .NET runtime.
            /// </summary>
            /// <remarks>
            /// It does NOT worth yet to utilize this method (2018 Oct).
            /// See benchmark results for the "PackFromVector4_Rgba32" benchmark!
            /// TODO: Check again later!
            /// </remarks>
            internal static void BulkConvertNormalizedFloatToByteClampOverflows(
                ReadOnlySpan<float> source,
                Span<byte> dest)
            {
                DebugGuard.IsTrue(
                    dest.Length % Vector<byte>.Count == 0,
                    nameof(dest),
                    "dest.Length should be divisible by Vector<byte>.Count!");

                int n = dest.Length / Vector<byte>.Count;

                ref Vector<float> sourceBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector<byte> destBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(dest));

                for (int i = 0; i < n; i++)
                {
                    ref Vector<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                    Vector<float> f0 = s;
                    Vector<float> f1 = Unsafe.Add(ref s, 1);
                    Vector<float> f2 = Unsafe.Add(ref s, 2);
                    Vector<float> f3 = Unsafe.Add(ref s, 3);

                    Vector<uint> w0 = ConvertToUInt32(f0);
                    Vector<uint> w1 = ConvertToUInt32(f1);
                    Vector<uint> w2 = ConvertToUInt32(f2);
                    Vector<uint> w3 = ConvertToUInt32(f3);

                    Vector<ushort> u0 = Vector.Narrow(w0, w1);
                    Vector<ushort> u1 = Vector.Narrow(w2, w3);

                    Vector<byte> b = Vector.Narrow(u0, u1);

                    Unsafe.Add(ref destBase, i) = b;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector<uint> ConvertToUInt32(Vector<float> vf)
            {
                Vector<float> maxBytes = new Vector<float>(255f);
                vf *= maxBytes;
                vf += new Vector<float>(0.5f);
                vf = Vector.Min(Vector.Max(vf, Vector<float>.Zero), maxBytes);
                Vector<int> vi = Vector.ConvertToInt32(vf);
                return Vector.AsVectorUInt32(vi);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector<float> ConvertToSingle(Vector<uint> u, Vector<float> scale)
            {
                Vector<int> vi = Vector.AsVectorInt32(u);
                Vector<float> v = Vector.ConvertToSingle(vi);
                v *= scale;
                return v;
            }
        }
    }
}