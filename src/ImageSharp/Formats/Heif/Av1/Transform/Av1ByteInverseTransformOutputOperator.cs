// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Reconstructs eight-bit AV1 samples from predicted values and inverse-transform residuals.
/// </summary>
internal readonly struct Av1ByteInverseTransformOutputOperator : IAv1InverseTransformOutputOperator<byte>
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
        uint packed = Unsafe.ReadUnaligned<uint>(ref prediction);
        Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        Vector128<int> predicted32 = Vector128.WidenLower(predicted16).AsInt32();
        Vector128<int> reconstructed = Vector128.Clamp(predicted32 + residual, Vector128<int>.Zero, Vector128.Create((int)byte.MaxValue));
        Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.AsUInt32(), Vector128<uint>.Zero);
        Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
        Unsafe.WriteUnaligned(ref destination, reconstructed8.AsUInt32().ToScalar());
        _ = bitDepth;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref byte prediction, ref byte destination, Vector256<int> residual, int bitDepth)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref prediction);
        Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        Vector256<int> predicted32 = Vector256.Create(Vector128.WidenLower(predicted16), Vector128.WidenUpper(predicted16)).AsInt32();
        Vector256<int> reconstructed = Vector256.Clamp(predicted32 + residual, Vector256<int>.Zero, Vector256.Create((int)byte.MaxValue));
        Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32());
        Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
        Unsafe.WriteUnaligned(ref destination, reconstructed8.AsUInt64().ToScalar());
        _ = bitDepth;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref byte prediction, ref byte destination, Vector512<int> residual, int bitDepth)
    {
        // Residuals occupy signed 32-bit lanes, so widen the sixteen packed predictions before adding. The clamp
        // then guarantees that both narrowing steps preserve the reconstructed byte values exactly.
        Vector128<byte> packed = Vector128.LoadUnsafe(ref prediction);
        (Vector128<ushort> predicted16Lower, Vector128<ushort> predicted16Upper) = Vector128.Widen(packed);
        Vector256<int> predicted32Lower = Vector256.Create(Vector128.WidenLower(predicted16Lower), Vector128.WidenUpper(predicted16Lower)).AsInt32();
        Vector256<int> predicted32Upper = Vector256.Create(Vector128.WidenLower(predicted16Upper), Vector128.WidenUpper(predicted16Upper)).AsInt32();
        Vector512<int> predicted32 = Vector512.Create(predicted32Lower, predicted32Upper);
        Vector512<int> reconstructed = Vector512.Clamp(predicted32 + residual, Vector512<int>.Zero, Vector512.Create((int)byte.MaxValue));
        Vector256<ushort> reconstructed16 = Vector256.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32());
        Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16.GetLower(), reconstructed16.GetUpper());
        reconstructed8.StoreUnsafe(ref destination);
        _ = bitDepth;
    }
}
