// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Writes filtered samples to eight-bit plane storage.
    /// </summary>
    private readonly struct ByteOutputOperator : IOutputOperator<byte>
    {
        /// <inheritdoc/>
        public static void StoreVector(ref byte destination, int offset, Vector128<short> value, int count)
        {
            Vector64<byte> packed = Vector128.Narrow(value.AsUInt16(), Vector128<ushort>.Zero).GetLower();
            ref byte output = ref Unsafe.Add(ref destination, offset);
            if (count == 8)
            {
                packed.StoreUnsafe(ref output);
            }
            else
            {
                Unsafe.WriteUnaligned(ref output, packed.AsUInt32().ToScalar());
            }
        }

        /// <inheritdoc/>
        public static void StoreScalar(ref byte destination, int offset, int value) => Unsafe.Add(ref destination, offset) = (byte)value;
    }
}
