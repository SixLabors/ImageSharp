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
    /// Fallback implementation based on <see cref="Vector4"/> (128bit).
    /// For <see cref="Vector4"/>, efficient software fallback implementations are present,
    /// and we hope that even mono's JIT is able to emit SIMD instructions for that type :P
    /// </summary>
    public static class FallbackIntrinsics128
    {
        /// <summary>
        /// <see cref="ByteToNormalizedFloat"/> as many elements as possible, slicing them down (keeping the remainder).
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal static void ByteToNormalizedFloatReduce(
            ref ReadOnlySpan<byte> source,
            ref Span<float> dest)
        {
            DebugGuard.IsTrue(source.Length == dest.Length, nameof(source), "Input spans must be of same length!");

            int remainder = Numerics.Modulo4(source.Length);
            int adjustedCount = source.Length - remainder;

            if (adjustedCount > 0)
            {
                ByteToNormalizedFloat(source[..adjustedCount], dest[..adjustedCount]);

                source = source[adjustedCount..];
                dest = dest[adjustedCount..];
            }
        }

        /// <summary>
        /// Implementation of <see cref="SimdUtils.ByteToNormalizedFloat"/> using <see cref="Vector4"/>.
        /// </summary>
        [MethodImpl(InliningOptions.ColdPath)]
        internal static void ByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
        {
            DebugVerifySpanInput(source, dest, 4);

            uint count = (uint)dest.Length / 4;
            if (count == 0)
            {
                return;
            }

            ref ByteVector4 sBase = ref Unsafe.As<byte, ByteVector4>(ref MemoryMarshal.GetReference(source));
            ref Vector4 dBase = ref Unsafe.As<float, Vector4>(ref MemoryMarshal.GetReference(dest));

            const float scale = 1f / 255f;
            Vector4 d = default;

            for (nuint i = 0; i < count; i++)
            {
                ref ByteVector4 s = ref Unsafe.Add(ref sBase, i);
                d.X = s.X;
                d.Y = s.Y;
                d.Z = s.Z;
                d.W = s.W;
                d *= scale;
                Unsafe.Add(ref dBase, i) = d;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByteVector4
        {
            public byte X;
            public byte Y;
            public byte Z;
            public byte W;
        }
    }
}
