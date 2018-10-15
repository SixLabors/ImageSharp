using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
// TODO: Add a build target for .NET 4.7.2
                true;
#else
                false;
#endif

            // ReSharper disable once MemberHidesStaticFromOuterClass
            internal static void BulkConvertByteToNormalizedFloat(ReadOnlySpan<byte> source, Span<float> dest)
            {
                Guard.IsTrue(
                    source.Length % Vector<byte>.Count == 0,
                    nameof(source),
                    "dest.Length should be divisable by Vector<byte>.Count!");

                int n = source.Length / Vector<byte>.Count;

                ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference(source));
                ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dest));

                var scale = new Vector<float>(1f / 255f);

                for (int i = 0; i < n; i++)
                {
                    Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                    Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                    Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                    Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                    Vector<float> f0 = Vector.ConvertToSingle(w0) * scale;
                    Vector<float> f1 = Vector.ConvertToSingle(w1) * scale;
                    Vector<float> f2 = Vector.ConvertToSingle(w2) * scale;
                    Vector<float> f3 = Vector.ConvertToSingle(w3) * scale;

                    ref Vector<float> d = ref Unsafe.Add(ref destBase, i * 4);
                    d = f0;
                    Unsafe.Add(ref d, 1) = f1;
                    Unsafe.Add(ref d, 2) = f2;
                    Unsafe.Add(ref d, 3) = f3;
                }
            }
        }
    }
}
