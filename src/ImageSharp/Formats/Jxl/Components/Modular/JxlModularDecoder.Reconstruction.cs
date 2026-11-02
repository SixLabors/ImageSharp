// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

internal sealed partial class JxlModularDecoder
{
    /// <summary>
    /// Restores native XYB float planes from completed modular Y, X and B-minus-Y integer channels.
    /// </summary>
    /// <param name="frame">The coded rectangle and final reduced-frame capacity, including color upsampling.</param>
    /// <param name="dcQuants">The exact preceding DC factors in X, Y, B order.</param>
    /// <param name="cancellationToken">The token checked before each coded row.</param>
    /// <returns>
    /// Three caller-owned planes in X, Y, B order. Only the top-left coded rectangle is initialized.
    /// The caller must apply any frame upsampling before retaining the complete planes as low-frequency reconstruction state.
    /// </returns>
    /// <remarks>
    /// The caller must complete the modular inverse transforms first. This operation keeps the original integer channels unchanged.
    /// It does not apply color conversion, pixel bounds or alpha rules. Failure releases every float plane acquired by this operation.
    /// Conversion order follows libjxl revision 91837342b24034078780b687f57f670a7def8153, lib/jxl/dec_modular.cc:
    /// ModularImageToDecodedRect selects SingleFromSingleAccurate for X/Y at metadata precision 23 or higher.
    /// MultiplySum restores B with signed integer addition before its float conversion at every precision.
    /// </remarks>
    public Buffer2D<float>[] ReconstructXyb(JxlFrameHeader frame, Vector3 dcQuants, CancellationToken cancellationToken)
    {
        Size codingSize = frame.CodingSize;
        Buffer2DRegion<int> sourceY = this.GetChannel(0);
        Buffer2DRegion<int> sourceX = this.GetChannel(1);
        Buffer2DRegion<int> sourceB = this.GetChannel(2);
        Span<int> sum = this.workspace.PrepareReconstructionRow(codingSize.Width);
        Buffer2D<float>? x = null;
        Buffer2D<float>? y = null;
        Buffer2D<float>? b = null;
        try
        {
            // Keep acquired owners local until all coded rows are complete. The final array is created only on success,
            // so a failure between plane allocations or during conversion cannot expose or leak an unfinished result.
            x = this.allocator.Allocate2D<float>(frame.OutputSize);
            y = this.allocator.Allocate2D<float>(frame.OutputSize);
            b = this.allocator.Allocate2D<float>(frame.OutputSize);

            // Native SingleFromSingleAccurate selects original metadata precision, even for XYB integer channels.
            // Keep that decision outside row traversal. All stored planes remain float; widened products round only at their final store.
            if (this.bitDepth >= 23)
            {
                ReconstructAccurateXybPlane(sourceX, x, dcQuants.X, cancellationToken);
                ReconstructAccurateXybPlane(sourceY, y, dcQuants.Y, cancellationToken);
            }
            else
            {
                ReconstructXybPlane(sourceX, x, dcQuants.X, cancellationToken);
                ReconstructXybPlane(sourceY, y, dcQuants.Y, cancellationToken);
            }

            for (int row = 0; row < codingSize.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<int> rawY = sourceY.DangerousGetRowSpan(row);
                ReadOnlySpan<int> rawB = sourceB.DangerousGetRowSpan(row);
                Span<float> outputB = b.DangerousGetRowSpan(row)[..codingSize.Width];

                // Modular XYB stores Y, X and B-Y. Native MultiplySum adds signed integer lanes before conversion,
                // including their 32-bit wrap. Converting the two operands first would lose canceling low bits above 2^24.
                // Apply B's own factor after that sum: it need not equal the factor already applied to Y.
                TensorPrimitives.Add(rawB, rawY, sum);
                TensorPrimitives.ConvertChecked<int, float>(sum, outputB);
                TensorPrimitives.Multiply(outputB, dcQuants.Z, outputB);
            }

            return [x, y, b];
        }
        catch
        {
            b?.Dispose();
            y?.Dispose();
            x?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Converts one original integer component with the native below-23-bit float multiplication rule.
    /// </summary>
    /// <param name="source">The completed original integer plane.</param>
    /// <param name="destination">The final-capacity float plane.</param>
    /// <param name="factor">The component's DC factor.</param>
    /// <param name="cancellationToken">The token checked before each row.</param>
    private static void ReconstructXybPlane(Buffer2DRegion<int> source, Buffer2D<float> destination, float factor, CancellationToken cancellationToken)
    {
        for (int row = 0; row < source.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<float> output = destination.DangerousGetRowSpan(row)[..source.Width];
            TensorPrimitives.ConvertChecked<int, float>(source.DangerousGetRowSpan(row), output);
            TensorPrimitives.Multiply(output, factor, output);
        }
    }

    /// <summary>
    /// Converts one original component with the native metadata-precision rule for 23 or more bits.
    /// </summary>
    /// <param name="source">The completed original integer plane.</param>
    /// <param name="destination">The final-capacity float plane.</param>
    /// <param name="factor">The component's DC factor, widened exactly before multiplication.</param>
    /// <param name="cancellationToken">The token checked before each row.</param>
    private static void ReconstructAccurateXybPlane(Buffer2DRegion<int> source, Buffer2D<float> destination, float factor, CancellationToken cancellationToken)
    {
        // Native dec_modular.cc SingleFromSingleAccurate multiplies an exact integer by a widened DC factor,
        // then rounds only the product to float. Casting the integer to float first can cross a final rounding midpoint.
        AccurateScaleOperator operation = new(factor);
        for (int row = 0; row < source.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScaleAccurateXybRow(source.DangerousGetRowSpan(row), destination.DangerousGetRowSpan(row)[..source.Width], in operation);
        }
    }

    /// <summary>
    /// Scales signed integers directly into float output with one final rounding and no intermediate row.
    /// </summary>
    /// <param name="source">The original integer component row.</param>
    /// <param name="destination">The matching float row.</param>
    /// <param name="operation">The component's prepared exact multiplier.</param>
    private static void ScaleAccurateXybRow(ReadOnlySpan<int> source, Span<float> destination, in AccurateScaleOperator operation)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Each integer register produces the same number of float lanes. The operator widens its two halves,
        // multiplies without losing integer bits and narrows them in their original order. Loads need no alignment.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = source.Length.Vector512Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector512<int>.Count)
            {
                operation.Invoke(Vector512.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector256<int>.Count)
            {
                operation.Invoke(Vector256.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<int>();
            for (nuint block = 0; block < count; block++, i += Vector128<int>.Count)
            {
                operation.Invoke(Vector128.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        // The remainder uses the same exact integer product and one final float rounding.
        for (; i < source.Length; i++)
        {
            destination[i] = operation.Invoke(source[i]);
        }
    }

    /// <summary>
    /// Applies the native high-depth X/Y multiplier before the single final float rounding.
    /// </summary>
    private readonly struct AccurateScaleOperator
    {
        private readonly double factor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccurateScaleOperator"/> struct with an exactly widened DC factor.
        /// </summary>
        /// <param name="factor">The parsed finite float multiplier.</param>
        public AccurateScaleOperator(float factor) => this.factor = factor;

        /// <summary>
        /// Multiplies an exact signed integer and rounds the product to float.
        /// </summary>
        /// <param name="value">The original integer.</param>
        /// <returns>The scaled float sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value) => (float)(value * this.factor);

        /// <summary>
        /// Scales four original integers with one final float rounding in each lane.
        /// </summary>
        /// <param name="value">The four original integer lanes.</param>
        /// <returns>The scaled float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value)
        {
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(value);
            Vector128<double> multiplier = Vector128.Create(this.factor);
            return Vector128.Narrow(Vector128.ConvertToDouble(lower) * multiplier, Vector128.ConvertToDouble(upper) * multiplier);
        }

        /// <summary>
        /// Scales eight original integers with one final float rounding in each lane.
        /// </summary>
        /// <param name="value">The eight original integer lanes.</param>
        /// <returns>The scaled float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value)
        {
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(value);
            Vector256<double> multiplier = Vector256.Create(this.factor);
            return Vector256.Narrow(Vector256.ConvertToDouble(lower) * multiplier, Vector256.ConvertToDouble(upper) * multiplier);
        }

        /// <summary>
        /// Scales sixteen original integers with one final float rounding in each lane.
        /// </summary>
        /// <param name="value">The sixteen original integer lanes.</param>
        /// <returns>The scaled float lanes in the same order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value)
        {
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(value);
            Vector512<double> multiplier = Vector512.Create(this.factor);
            return Vector512.Narrow(Vector512.ConvertToDouble(lower) * multiplier, Vector512.ConvertToDouble(upper) * multiplier);
        }
    }
}
