using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    using System.Diagnostics;

    /// <summary>
    /// Represents a Jpeg block with <see cref="short"/> coefficiens.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal unsafe struct Block8x8
    {
        /// <summary>
        /// A number of scalar coefficients in a <see cref="Block8x8F"/>
        /// </summary>
        public const int Size = 64;

        private fixed short data[Size];

        public Block8x8(Span<short> coefficients)
        {
            ref byte selfRef = ref Unsafe.As<Block8x8, byte>(ref this);
            ref byte sourceRef = ref coefficients.NonPortableCast<short, byte>().DangerousGetPinnableReference();
            Unsafe.CopyBlock(ref selfRef, ref sourceRef, Size * sizeof(short));
        }

        /// <summary>
        /// Pointer-based "Indexer" (getter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <returns>The scaleVec value at the specified index</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe short GetScalarAt(Block8x8* blockPtr, int idx)
        {
            GuardBlockIndex(idx);

            short* fp = (short*)blockPtr;
            return fp[idx];
        }

        /// <summary>
        /// Pointer-based "Indexer" (setter part)
        /// </summary>
        /// <param name="blockPtr">Block pointer</param>
        /// <param name="idx">Index</param>
        /// <param name="value">Value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetScalarAt(Block8x8* blockPtr, int idx, short value)
        {
            GuardBlockIndex(idx);

            short* fp = (short*)blockPtr;
            fp[idx] = value;
        }


        public short this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                GuardBlockIndex(idx);
                ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                GuardBlockIndex(idx);
                ref short selfRef = ref Unsafe.As<Block8x8, short>(ref this);
                Unsafe.Add(ref selfRef, idx) = value;
            }
        }

        [Conditional("DEBUG")]
        private static void GuardBlockIndex(int idx)
        {
            DebugGuard.MustBeLessThan(idx, Size, nameof(idx));
            DebugGuard.MustBeGreaterThanOrEqualTo(idx, 0, nameof(idx));
        }

        public Block8x8F AsFloatBlock()
        {
            // TODO: Optimize this
            var result = default(Block8x8F);
            for (int i = 0; i < Size; i++)
            {
                result[i] = this[i];
            }

            return result;
        }
    }
}