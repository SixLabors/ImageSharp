// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines eight-bit inverse-transform reconstruction arithmetic.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// Reconstructs eight-bit samples from predicted values and inverse-transform residuals.
    /// </summary>
    internal readonly struct ByteOutputOperator : IAv1InverseTransformOutputOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Add(byte prediction, int residual, int bitDepth)
        {
            _ = bitDepth;
            return (byte)Math.Clamp(prediction + residual, byte.MinValue, byte.MaxValue);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref byte prediction, ref byte destination, Vector128<int> residual, int bitDepth)
        {
            _ = bitDepth;

            // Read and write exactly four bytes. The unused upper lanes only participate in narrowing and never reach
            // memory, which keeps reconstruction valid at a tightly packed row boundary.
            uint packed = Unsafe.ReadUnaligned<uint>(ref prediction);
            Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            Vector128<int> predicted32 = Vector128.WidenLower(predicted16).AsInt32();
            Vector128<int> reconstructed = Vector128.Clamp(predicted32 + residual, Vector128<int>.Zero, Vector128.Create((int)byte.MaxValue));
            Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref destination, reconstructed8.AsUInt32().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref byte prediction, ref byte destination, Vector256<int> residual, int bitDepth)
        {
            _ = bitDepth;

            // Eight byte predictions widen through UInt16 into the eight Int32 residual lanes. The final 64-bit store
            // covers only those reconstructed samples and does not require destination padding.
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref prediction);
            Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            Vector256<int> predicted32 = Vector256.Create(Vector128.WidenLower(predicted16), Vector128.WidenUpper(predicted16)).AsInt32();
            Vector256<int> reconstructed = Vector256.Clamp(predicted32 + residual, Vector256<int>.Zero, Vector256.Create((int)byte.MaxValue));
            Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32());
            Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref destination, reconstructed8.AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref byte prediction, ref byte destination, Vector512<int> residual, int bitDepth)
        {
            _ = bitDepth;

            // Sixteen packed bytes widen to sixteen Int32 lanes without a lane permutation. Narrow each half in
            // order after clipping, then store exactly the original sixteen samples rather than a padded vector.
            Vector128<byte> packed = Vector128.LoadUnsafe(ref prediction);
            Vector256<ushort> predicted16 = Vector256.Create(Vector128.WidenLower(packed), Vector128.WidenUpper(packed));
            Vector512<int> predicted32 = Vector512.Create(Vector256.WidenLower(predicted16), Vector256.WidenUpper(predicted16)).AsInt32();
            Vector512<int> reconstructed = Vector512.Clamp(predicted32 + residual, Vector512<int>.Zero, Vector512.Create((int)byte.MaxValue));
            Vector256<ushort> reconstructed16 = Vector256.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32());
            Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16.GetLower(), reconstructed16.GetUpper());
            reconstructed8.StoreUnsafe(ref destination);
        }
    }
}
