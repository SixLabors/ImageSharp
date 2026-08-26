// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Reconstructs AV1 samples from predicted values and inverse-transform residuals.
/// </summary>
/// <typeparam name="TSample">The decoded sample storage type.</typeparam>
internal readonly struct Av1InverseTransformOutputOperator<TSample> : IAv1InverseTransformOutputOperator<TSample>
    where TSample : unmanaged
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSample Add(TSample prediction, int residual, int bitDepth)
    {
        // TSample is fixed by the byte and short decoder entry points. The JIT removes this type test from each
        // closed transform so storage selection does not introduce a branch in the reconstruction loop.
        if (typeof(TSample) == typeof(byte))
        {
            byte value = (byte)Math.Clamp(Unsafe.As<TSample, byte>(ref prediction) + residual, byte.MinValue, byte.MaxValue);
            return Unsafe.As<byte, TSample>(ref value);
        }

        short result = (short)Math.Clamp(Unsafe.As<TSample, short>(ref prediction) + residual, 0, (1 << bitDepth) - 1);
        return Unsafe.As<short, TSample>(ref result);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref TSample prediction, ref TSample destination, Vector128<int> residual, int bitDepth)
    {
        if (typeof(TSample) == typeof(byte))
        {
            ref byte source = ref Unsafe.As<TSample, byte>(ref prediction);
            uint packed = Unsafe.ReadUnaligned<uint>(ref source);
            Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            Vector128<int> predicted32 = Vector128.WidenLower(predicted16).AsInt32();
            Vector128<int> reconstructed = Vector128.Clamp(predicted32 + residual, Vector128<int>.Zero, Vector128.Create((int)byte.MaxValue));
            Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.As<TSample, byte>(ref destination), reconstructed8.AsUInt32().ToScalar());
            return;
        }

        ref short highBitDepthSource = ref Unsafe.As<TSample, short>(ref prediction);
        ulong highBitDepthPacked = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref highBitDepthSource));
        Vector128<int> highBitDepthPredicted = Vector128.WidenLower(Vector128.CreateScalarUnsafe(highBitDepthPacked).AsInt16());
        Vector128<int> highBitDepthReconstructed =
            Vector128.Clamp(highBitDepthPredicted + residual, Vector128<int>.Zero, Vector128.Create((1 << bitDepth) - 1));

        Vector128<short> narrowed = Vector128.Narrow(highBitDepthReconstructed, Vector128<int>.Zero);
        Unsafe.WriteUnaligned(ref Unsafe.As<TSample, byte>(ref destination), narrowed.AsUInt64().ToScalar());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref TSample prediction, ref TSample destination, Vector256<int> residual, int bitDepth)
    {
        if (typeof(TSample) == typeof(byte))
        {
            ref byte source = ref Unsafe.As<TSample, byte>(ref prediction);
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref source);
            Vector128<ushort> predicted16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            Vector256<int> predicted32 = Vector256.Create(Vector128.WidenLower(predicted16), Vector128.WidenUpper(predicted16)).AsInt32();
            Vector256<int> reconstructed = Vector256.Clamp(predicted32 + residual, Vector256<int>.Zero, Vector256.Create((int)byte.MaxValue));
            Vector128<ushort> reconstructed16 = Vector128.Narrow(reconstructed.GetLower().AsUInt32(), reconstructed.GetUpper().AsUInt32());
            Vector128<byte> reconstructed8 = Vector128.Narrow(reconstructed16, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.As<TSample, byte>(ref destination), reconstructed8.AsUInt64().ToScalar());
            return;
        }

        ref short highBitDepthSource = ref Unsafe.As<TSample, short>(ref prediction);
        Vector256<int> highBitDepthPredicted = Vector256_.Widen(Vector128.LoadUnsafe(ref highBitDepthSource));
        Vector256<int> highBitDepthReconstructed =
            Vector256.Clamp(highBitDepthPredicted + residual, Vector256<int>.Zero, Vector256.Create((1 << bitDepth) - 1));

        Vector128<short> narrowed = Vector128.Narrow(highBitDepthReconstructed.GetLower(), highBitDepthReconstructed.GetUpper());
        narrowed.StoreUnsafe(ref Unsafe.As<TSample, short>(ref destination));
    }
}
