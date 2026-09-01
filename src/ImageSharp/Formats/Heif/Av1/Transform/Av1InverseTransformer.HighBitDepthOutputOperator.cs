// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines high-bit-depth inverse-transform reconstruction arithmetic.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// Reconstructs high-bit-depth samples from predicted values and inverse-transform residuals.
    /// </summary>
    internal readonly struct HighBitDepthOutputOperator : IAv1InverseTransformOutputOperator<short>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Add(short prediction, int residual, int bitDepth)
            => (short)Math.Clamp(prediction + residual, 0, (1 << bitDepth) - 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref short prediction, ref short destination, Vector128<int> residual, int bitDepth)
        {
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref prediction));
            Vector128<int> predicted = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16());
            Vector128<int> reconstructed =
                Vector128.Clamp(predicted + residual, Vector128<int>.Zero, Vector128.Create((1 << bitDepth) - 1));

            Vector128<short> narrowed = Vector128.Narrow(reconstructed, Vector128<int>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref destination), narrowed.AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref short prediction, ref short destination, Vector256<int> residual, int bitDepth)
        {
            Vector256<int> predicted = Vector256_.Widen(Vector128.LoadUnsafe(ref prediction));
            Vector256<int> reconstructed =
                Vector256.Clamp(predicted + residual, Vector256<int>.Zero, Vector256.Create((1 << bitDepth) - 1));

            Vector128<short> narrowed = Vector128.Narrow(reconstructed.GetLower(), reconstructed.GetUpper());
            narrowed.StoreUnsafe(ref destination);
        }
    }
}
