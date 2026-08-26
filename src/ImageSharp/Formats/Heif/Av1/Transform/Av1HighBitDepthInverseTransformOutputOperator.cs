// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Reconstructs high-bit-depth AV1 samples from predicted values and inverse-transform residuals.
/// </summary>
internal readonly struct Av1HighBitDepthInverseTransformOutputOperator : IAv1InverseTransformOutputOperator<short>
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
        Vector128<int> reconstructed = Vector128.Clamp(predicted + residual, Vector128<int>.Zero, Vector128.Create((1 << bitDepth) - 1));
        Vector128<short> narrowed = Vector128.Narrow(reconstructed, Vector128<int>.Zero);
        Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref destination), narrowed.AsUInt64().ToScalar());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref short prediction, ref short destination, Vector256<int> residual, int bitDepth)
    {
        Vector256<int> predicted = Vector256_.Widen(Vector128.LoadUnsafe(ref prediction));
        Vector256<int> reconstructed = Vector256.Clamp(predicted + residual, Vector256<int>.Zero, Vector256.Create((1 << bitDepth) - 1));
        Vector128<short> narrowed = Vector128.Narrow(reconstructed.GetLower(), reconstructed.GetUpper());
        narrowed.StoreUnsafe(ref destination);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref short prediction, ref short destination, Vector512<int> residual, int bitDepth)
    {
        // AV1 high-bit-depth samples are nonnegative Int16 values. Widening before the residual add preserves signed
        // arithmetic, and the bit-depth clamp makes the final narrowing exact for both 10-bit and 12-bit output.
        Vector256<short> packed = Vector256.LoadUnsafe(ref prediction);
        (Vector256<int> predictedLower, Vector256<int> predictedUpper) = Vector256.Widen(packed);
        Vector512<int> predicted = Vector512.Create(predictedLower, predictedUpper);
        Vector512<int> reconstructed = Vector512.Clamp(predicted + residual, Vector512<int>.Zero, Vector512.Create((1 << bitDepth) - 1));
        Vector256<short> narrowed = Vector256.Narrow(reconstructed.GetLower(), reconstructed.GetUpper());
        narrowed.StoreUnsafe(ref destination);
    }
}
