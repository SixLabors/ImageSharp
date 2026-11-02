// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Restores three AC coefficient planes, including quantization bias, matrix scaling and Y correlation.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int coefficient storage.</typeparam>
    /// <param name="quantizedX">The packed X residual coefficients.</param>
    /// <param name="quantizedY">The packed Y coefficients.</param>
    /// <param name="quantizedB">The packed B residual coefficients.</param>
    /// <param name="matrices">Three contiguous decoder-matrix planes in X, Y, B order.</param>
    /// <param name="destination">Three contiguous float coefficient planes, with no pixel packing or bounds applied.</param>
    /// <param name="scale">The inverse global scale divided by the block quantizer, including each component's matrix multiplier.</param>
    /// <param name="correlation">The complete Y-to-X and Y-to-B ratios.</param>
    /// <param name="bias">The X, Y and B unit-coefficient biases followed by the nonunit correction numerator.</param>
    public static void DequantizeAcBlock<TCoefficient>(
        ReadOnlySpan<TCoefficient> quantizedX,
        ReadOnlySpan<TCoefficient> quantizedY,
        ReadOnlySpan<TCoefficient> quantizedB,
        ReadOnlySpan<float> matrices,
        Span<float> destination,
        Vector3 scale,
        Vector2 correlation,
        Vector4 bias)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        int length = quantizedX.Length;
        ref TCoefficient sourceX = ref MemoryMarshal.GetReference(quantizedX);
        ref TCoefficient sourceY = ref MemoryMarshal.GetReference(quantizedY);
        ref TCoefficient sourceB = ref MemoryMarshal.GetReference(quantizedB);
        ref float matrixX = ref MemoryMarshal.GetReference(matrices);
        ref float matrixY = ref Unsafe.Add(ref matrixX, length);
        ref float matrixB = ref Unsafe.Add(ref matrixY, length);
        ref float outputX = ref MemoryMarshal.GetReference(destination);
        ref float outputY = ref Unsafe.Add(ref outputX, length);
        ref float outputB = ref Unsafe.Add(ref outputY, length);
        AcDequantizationOperator operation = new(scale, correlation, bias);
        int i = 0;

        // Each lane owns one coefficient position across X, Y and B. Load signed storage at its actual width,
        // widen once, then retain float arithmetic through bias, scaling and correlation. Descending widths
        // consume only the remaining complete lanes; the scalar tail uses the same arithmetic order.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                operation.Invoke(
                    LoadCoefficients512(ref sourceX, (nuint)i),
                    LoadCoefficients512(ref sourceY, (nuint)i),
                    LoadCoefficients512(ref sourceB, (nuint)i),
                    Vector512.LoadUnsafe(ref matrixX, (nuint)i),
                    Vector512.LoadUnsafe(ref matrixY, (nuint)i),
                    Vector512.LoadUnsafe(ref matrixB, (nuint)i),
                    out Vector512<float> x,
                    out Vector512<float> y,
                    out Vector512<float> b);

                x.StoreUnsafe(ref outputX, (nuint)i);
                y.StoreUnsafe(ref outputY, (nuint)i);
                b.StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                operation.Invoke(
                    LoadCoefficients256(ref sourceX, (nuint)i),
                    LoadCoefficients256(ref sourceY, (nuint)i),
                    LoadCoefficients256(ref sourceB, (nuint)i),
                    Vector256.LoadUnsafe(ref matrixX, (nuint)i),
                    Vector256.LoadUnsafe(ref matrixY, (nuint)i),
                    Vector256.LoadUnsafe(ref matrixB, (nuint)i),
                    out Vector256<float> x,
                    out Vector256<float> y,
                    out Vector256<float> b);

                x.StoreUnsafe(ref outputX, (nuint)i);
                y.StoreUnsafe(ref outputY, (nuint)i);
                b.StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                operation.Invoke(
                    LoadCoefficients128(ref sourceX, (nuint)i),
                    LoadCoefficients128(ref sourceY, (nuint)i),
                    LoadCoefficients128(ref sourceB, (nuint)i),
                    Vector128.LoadUnsafe(ref matrixX, (nuint)i),
                    Vector128.LoadUnsafe(ref matrixY, (nuint)i),
                    Vector128.LoadUnsafe(ref matrixB, (nuint)i),
                    out Vector128<float> x,
                    out Vector128<float> y,
                    out Vector128<float> b);

                x.StoreUnsafe(ref outputX, (nuint)i);
                y.StoreUnsafe(ref outputY, (nuint)i);
                b.StoreUnsafe(ref outputB, (nuint)i);
            }
        }

        for (; i < length; i++)
        {
            operation.Invoke(
                int.CreateTruncating(Unsafe.Add(ref sourceX, i)),
                int.CreateTruncating(Unsafe.Add(ref sourceY, i)),
                int.CreateTruncating(Unsafe.Add(ref sourceB, i)),
                Unsafe.Add(ref matrixX, i),
                Unsafe.Add(ref matrixY, i),
                Unsafe.Add(ref matrixB, i),
                out Unsafe.Add(ref outputX, i),
                out Unsafe.Add(ref outputY, i),
                out Unsafe.Add(ref outputB, i));
        }
    }

    /// <summary>
    /// Loads 4 signed coefficients and widens short storage without reading beyond the requested lane count.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int storage.</typeparam>
    /// <param name="source">The first coefficient in the component plane.</param>
    /// <param name="offset">The coefficient offset, not a byte offset.</param>
    /// <returns>The coefficient lanes in their original order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LoadCoefficients128<TCoefficient>(ref TCoefficient source, nuint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The frame selects one of these two storage types before group traversal. The JIT removes this
        // type decision in each closed generic method. Load only half a register for the short representation.
        if (typeof(TCoefficient) == typeof(short))
        {
            ref short first = ref Unsafe.As<TCoefficient, short>(ref source);
            return Vector128.WidenLower(Vector64.LoadUnsafe(ref first, offset).ToVector128());
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset);
    }

    /// <summary>
    /// Loads 8 signed coefficients and widens short storage without reading beyond the requested lane count.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int storage.</typeparam>
    /// <param name="source">The first coefficient in the component plane.</param>
    /// <param name="offset">The coefficient offset, not a byte offset.</param>
    /// <returns>The coefficient lanes in their original order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> LoadCoefficients256<TCoefficient>(ref TCoefficient source, nuint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The frame selects one of these two storage types before group traversal. The JIT removes this
        // type decision in each closed generic method. Load only half a register for the short representation.
        if (typeof(TCoefficient) == typeof(short))
        {
            ref short first = ref Unsafe.As<TCoefficient, short>(ref source);
            return Vector256_.Widen(Vector128.LoadUnsafe(ref first, offset));
        }

        return Vector256.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset);
    }

    /// <summary>
    /// Loads 16 signed coefficients and widens short storage without reading beyond the requested lane count.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int storage.</typeparam>
    /// <param name="source">The first coefficient in the component plane.</param>
    /// <param name="offset">The coefficient offset, not a byte offset.</param>
    /// <returns>The coefficient lanes in their original order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> LoadCoefficients512<TCoefficient>(ref TCoefficient source, nuint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The frame selects one of these two storage types before group traversal. The JIT removes this
        // type decision in each closed generic method. Load only half a register for the short representation.
        if (typeof(TCoefficient) == typeof(short))
        {
            ref short first = ref Unsafe.As<TCoefficient, short>(ref source);
            return Vector512.WidenLower(Vector256.LoadUnsafe(ref first, offset).ToVector512());
        }

        return Vector512.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset);
    }

    /// <summary>
    /// Applies the format's quantization bias and restores X/B residuals from the dequantized Y plane.
    /// </summary>
    private readonly struct AcDequantizationOperator
    {
        private readonly Vector3 scale;
        private readonly Vector2 correlation;
        private readonly Vector4 bias;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcDequantizationOperator"/> struct.
        /// </summary>
        /// <param name="scale">The prepared X, Y and B matrix scales for this block.</param>
        /// <param name="correlation">The complete Y-to-X and Y-to-B ratios.</param>
        /// <param name="bias">The three unit biases and the nonunit numerator.</param>
        public AcDequantizationOperator(Vector3 scale, Vector2 correlation, Vector4 bias)
        {
            this.scale = scale;
            this.correlation = correlation;
            this.bias = bias;
        }

        /// <summary>
        /// Restores one coefficient triple without changing its frequency position.
        /// </summary>
        /// <param name="qx">The signed X coefficient.</param>
        /// <param name="qy">The signed Y coefficient.</param>
        /// <param name="qb">The signed B coefficient.</param>
        /// <param name="mx">The X decoder-matrix value.</param>
        /// <param name="my">The Y decoder-matrix value.</param>
        /// <param name="mb">The B decoder-matrix value.</param>
        /// <param name="x">The restored X coefficient.</param>
        /// <param name="y">The restored Y coefficient.</param>
        /// <param name="b">The restored B coefficient.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int qx, int qy, int qb, float mx, float my, float mb, out float x, out float y, out float b)
        {
            y = this.AdjustBias(qy, this.bias.Y) * (my * this.scale.Y);
            x = float.MultiplyAddEstimate(this.correlation.X, y, this.AdjustBias(qx, this.bias.X) * (mx * this.scale.X));
            b = float.MultiplyAddEstimate(this.correlation.Y, y, this.AdjustBias(qb, this.bias.Z) * (mb * this.scale.Z));
        }

        /// <summary>
        /// Restores 4 independent coefficient triples in unchanged lane order.
        /// </summary>
        /// <param name="qx">The signed X lanes.</param>
        /// <param name="qy">The signed Y lanes.</param>
        /// <param name="qb">The signed B lanes.</param>
        /// <param name="mx">The X matrix lanes.</param>
        /// <param name="my">The Y matrix lanes.</param>
        /// <param name="mb">The B matrix lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<int> qx,
            Vector128<int> qy,
            Vector128<int> qb,
            Vector128<float> mx,
            Vector128<float> my,
            Vector128<float> mb,
            out Vector128<float> x,
            out Vector128<float> y,
            out Vector128<float> b)
        {
            y = this.AdjustBias(Vector128.ConvertToSingle(qy), this.bias.Y) * (my * Vector128.Create(this.scale.Y));
            x = Vector128.MultiplyAddEstimate(
                Vector128.Create(this.correlation.X), y, this.AdjustBias(Vector128.ConvertToSingle(qx), this.bias.X) * (mx * Vector128.Create(this.scale.X)));

            b = Vector128.MultiplyAddEstimate(
                Vector128.Create(this.correlation.Y), y, this.AdjustBias(Vector128.ConvertToSingle(qb), this.bias.Z) * (mb * Vector128.Create(this.scale.Z)));
        }

        /// <summary>
        /// Restores 8 independent coefficient triples in unchanged lane order.
        /// </summary>
        /// <param name="qx">The signed X lanes.</param>
        /// <param name="qy">The signed Y lanes.</param>
        /// <param name="qb">The signed B lanes.</param>
        /// <param name="mx">The X matrix lanes.</param>
        /// <param name="my">The Y matrix lanes.</param>
        /// <param name="mb">The B matrix lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<int> qx,
            Vector256<int> qy,
            Vector256<int> qb,
            Vector256<float> mx,
            Vector256<float> my,
            Vector256<float> mb,
            out Vector256<float> x,
            out Vector256<float> y,
            out Vector256<float> b)
        {
            y = this.AdjustBias(Vector256.ConvertToSingle(qy), this.bias.Y) * (my * Vector256.Create(this.scale.Y));
            x = Vector256.MultiplyAddEstimate(
                Vector256.Create(this.correlation.X), y, this.AdjustBias(Vector256.ConvertToSingle(qx), this.bias.X) * (mx * Vector256.Create(this.scale.X)));

            b = Vector256.MultiplyAddEstimate(
                Vector256.Create(this.correlation.Y), y, this.AdjustBias(Vector256.ConvertToSingle(qb), this.bias.Z) * (mb * Vector256.Create(this.scale.Z)));
        }

        /// <summary>
        /// Restores 16 independent coefficient triples in unchanged lane order.
        /// </summary>
        /// <param name="qx">The signed X lanes.</param>
        /// <param name="qy">The signed Y lanes.</param>
        /// <param name="qb">The signed B lanes.</param>
        /// <param name="mx">The X matrix lanes.</param>
        /// <param name="my">The Y matrix lanes.</param>
        /// <param name="mb">The B matrix lanes.</param>
        /// <param name="x">The restored X lanes.</param>
        /// <param name="y">The restored Y lanes.</param>
        /// <param name="b">The restored B lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<int> qx,
            Vector512<int> qy,
            Vector512<int> qb,
            Vector512<float> mx,
            Vector512<float> my,
            Vector512<float> mb,
            out Vector512<float> x,
            out Vector512<float> y,
            out Vector512<float> b)
        {
            y = this.AdjustBias(Vector512.ConvertToSingle(qy), this.bias.Y) * (my * Vector512.Create(this.scale.Y));
            x = Vector512.MultiplyAddEstimate(
                Vector512.Create(this.correlation.X), y, this.AdjustBias(Vector512.ConvertToSingle(qx), this.bias.X) * (mx * Vector512.Create(this.scale.X)));

            b = Vector512.MultiplyAddEstimate(
                Vector512.Create(this.correlation.Y), y, this.AdjustBias(Vector512.ConvertToSingle(qb), this.bias.Z) * (mb * Vector512.Create(this.scale.Z)));
        }

        /// <summary>
        /// Preserves zero, scales unit magnitudes, and corrects larger signed coefficients by numerator divided by coefficient.
        /// </summary>
        /// <param name="value">The signed coefficient converted to float.</param>
        /// <param name="unitBias">The component's unit-magnitude bias.</param>
        /// <returns>The bias-adjusted coefficient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float AdjustBias(float value, float unitBias)
            => MathF.Abs(value) < 2F ? value * unitBias : value - (this.bias.W / value);

        /// <inheritdoc cref="AdjustBias(float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector128<float> AdjustBias(Vector128<float> value, float unitBias)
        {
            // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
            // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
            Vector128<float> small = value * Vector128.Create(unitBias);
            Vector128<float> large = value - (Vector128.Create(this.bias.W) / value);
            return Vector128.ConditionalSelect(Vector128.LessThan(Vector128.Abs(value), Vector128.Create(2F)), small, large);
        }

        /// <inheritdoc cref="AdjustBias(float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector256<float> AdjustBias(Vector256<float> value, float unitBias)
        {
            // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
            // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
            Vector256<float> small = value * Vector256.Create(unitBias);
            Vector256<float> large = value - (Vector256.Create(this.bias.W) / value);
            return Vector256.ConditionalSelect(Vector256.LessThan(Vector256.Abs(value), Vector256.Create(2F)), small, large);
        }

        /// <inheritdoc cref="AdjustBias(float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector512<float> AdjustBias(Vector512<float> value, float unitBias)
        {
            // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
            // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
            Vector512<float> small = value * Vector512.Create(unitBias);
            Vector512<float> large = value - (Vector512.Create(this.bias.W) / value);
            return Vector512.ConditionalSelect(Vector512.LessThan(Vector512.Abs(value), Vector512.Create(2F)), small, large);
        }
    }
}
