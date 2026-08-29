// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Writes filtered samples to 16-bit plane storage.
    /// </summary>
    private readonly struct UInt16OutputOperator : IOutputOperator<ushort>
    {
        /// <inheritdoc/>
        public static void StoreVector(ref ushort destination, int offset, Vector128<short> value, int count)
        {
            ref ushort output = ref Unsafe.Add(ref destination, offset);
            if (count == 8)
            {
                value.AsUInt16().StoreUnsafe(ref output);
            }
            else
            {
                ref byte outputBytes = ref Unsafe.As<ushort, byte>(ref output);
                Unsafe.WriteUnaligned(ref outputBytes, value.AsUInt64().GetLower().ToScalar());
            }
        }

        /// <inheritdoc/>
        public static void StoreScalar(ref ushort destination, int offset, int value) => Unsafe.Add(ref destination, offset) = (ushort)value;
    }
}
