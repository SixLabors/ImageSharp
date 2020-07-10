// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp
{
    internal static partial class SimdUtils
    {
        public static class Avx2Intrinsics
        {
            private static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };

            /// <summary>
            /// <see cref="NormalizedFloatToByteSaturate"/> as many elements as possible, slicing them down (keeping the remainder).
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void NormalizedFloatToByteSaturateReduce(
                ref ReadOnlySpan<float> source,
                ref Span<byte> dest)
            {
                DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

                if (Avx2.IsSupported)
                {
                    int remainder = ImageMaths.ModuloP2(source.Length, Vector<byte>.Count);
                    int adjustedCount = source.Length - remainder;

                    if (adjustedCount > 0)
                    {
                        NormalizedFloatToByteSaturate(
                            source.Slice(0, adjustedCount),
                            dest.Slice(0, adjustedCount));

                        source = source.Slice(adjustedCount);
                        dest = dest.Slice(adjustedCount);
                    }
                }
            }

            /// <summary>
            /// Implementation of <see cref="SimdUtils.NormalizedFloatToByteSaturate"/>, which is faster on new .NET runtime.
            /// </summary>
            /// <remarks>
            /// Implementation is based on MagicScaler code:
            /// https://github.com/saucecontrol/PhotoSauce/blob/a9bd6e5162d2160419f0cf743fd4f536c079170b/src/MagicScaler/Magic/Processors/ConvertersFloat.cs#L453-L477
            /// </remarks>
            internal static void NormalizedFloatToByteSaturate(
                ReadOnlySpan<float> source,
                Span<byte> dest)
            {
                VerifySpanInput(source, dest, Vector256<byte>.Count);

                int n = dest.Length / Vector256<byte>.Count;

                ref Vector256<float> sourceBase =
                    ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector256<byte> destBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

                var maxBytes = Vector256.Create(255f);
                ref byte maskBase = ref MemoryMarshal.GetReference(PermuteMaskDeinterleave8x32);
                Vector256<int> mask = Unsafe.As<byte, Vector256<int>>(ref maskBase);

                for (int i = 0; i < n; i++)
                {
                    ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                    Vector256<float> f0 = s;
                    Vector256<float> f1 = Unsafe.Add(ref s, 1);
                    Vector256<float> f2 = Unsafe.Add(ref s, 2);
                    Vector256<float> f3 = Unsafe.Add(ref s, 3);

                    Vector256<int> w0 = ConvertToInt32(f0, maxBytes);
                    Vector256<int> w1 = ConvertToInt32(f1, maxBytes);
                    Vector256<int> w2 = ConvertToInt32(f2, maxBytes);
                    Vector256<int> w3 = ConvertToInt32(f3, maxBytes);

                    Vector256<short> u0 = Avx2.PackSignedSaturate(w0, w1);
                    Vector256<short> u1 = Avx2.PackSignedSaturate(w2, w3);
                    Vector256<byte> b = Avx2.PackUnsignedSaturate(u0, u1);
                    b = Avx2.PermuteVar8x32(b.AsInt32(), mask).AsByte();

                    Unsafe.Add(ref destBase, i) = b;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Vector256<int> ConvertToInt32(Vector256<float> vf, Vector256<float> scale)
            {
                vf = Avx.Multiply(vf, scale);
                return Avx.ConvertToVector256Int32(vf);
            }
        }
    }
}
#endif
