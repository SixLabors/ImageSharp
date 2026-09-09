// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Owns one independently adaptive AV1 motion-vector entropy context.
/// </summary>
/// <remarks>
/// Normal inter-prediction vectors and intra-block-copy displacement vectors use identical initial distributions, but
/// each syntax domain owns a separate instance so observations from one domain cannot adapt the other.
/// </remarks>
internal sealed class Av1MotionVectorContext
{
    /// <summary>
    /// The number of magnitude classes defined by AV1.
    /// </summary>
    private const int MagnitudeClassCount = 11;

    /// <summary>
    /// The number of integer magnitude bits coded directly for class zero.
    /// </summary>
    private const int ClassZeroBitCount = 1;

    /// <summary>
    /// The number of integer magnitude offsets represented by class zero.
    /// </summary>
    private const int ClassZeroSize = 1 << ClassZeroBitCount;

    /// <summary>
    /// Defines one non-mutating cost or mutating write operation over shared motion-vector syntax.
    /// </summary>
    public interface IMotionVectorSymbolOperation
    {
        /// <summary>
        /// Processes one entropy-coded symbol.
        /// </summary>
        /// <param name="writer">The tile range encoder.</param>
        /// <param name="symbol">The zero-based symbol.</param>
        /// <param name="distribution">The live symbol distribution.</param>
        /// <returns>The symbol cost in 1/512-bit units, or zero when writing.</returns>
        public static abstract int ProcessSymbol(Av1SymbolWriter writer, int symbol, Av1Distribution distribution);
    }

    /// <summary>
    /// Gets the distribution selecting which vector components are nonzero.
    /// </summary>
    public Av1Distribution Joint { get; } = new(4096, 11264, 19328);

    /// <summary>
    /// Gets the adaptive distributions for the vertical vector component.
    /// </summary>
    public Component Vertical { get; } = new();

    /// <summary>
    /// Gets the adaptive distributions for the horizontal vector component.
    /// </summary>
    public Component Horizontal { get; } = new();

    /// <summary>
    /// Replaces every motion-vector distribution with state copied from another context.
    /// </summary>
    /// <param name="source">The motion-vector context state to copy.</param>
    public void CopyFrom(Av1MotionVectorContext source)
    {
        this.Joint.CopyFrom(source.Joint);
        this.Vertical.CopyFrom(source.Vertical);
        this.Horizontal.CopyFrom(source.Horizontal);
    }

    /// <summary>
    /// Resets every observation count used to adapt the motion-vector distributions.
    /// </summary>
    public void ResetUpdateCounts()
    {
        this.Joint.ResetUpdateCount();
        this.Vertical.ResetUpdateCounts();
        this.Horizontal.ResetUpdateCounts();
    }

    /// <summary>
    /// Reads a motion-vector delta relative to a spatially derived reference.
    /// </summary>
    /// <param name="reader">The tile range decoder.</param>
    /// <param name="reference">The reference motion vector.</param>
    /// <param name="precision">The fractional precision allowed by the current frame.</param>
    /// <returns>The decoded motion vector in one-eighth-sample units.</returns>
    public Av1MotionVector Read(ref Av1SymbolReader reader, Av1MotionVector reference, Av1MotionVectorPrecision precision)
    {
        int jointType = reader.ReadSymbol(this.Joint);

        // Joint values 1 and 3 carry a horizontal delta; values 2 and 3 carry a vertical delta. Reading only the
        // signaled components preserves the normative entropy-symbol order and leaves zero components unadapted.
        int row = jointType >= 2 ? this.Vertical.Read(ref reader, precision) : 0;
        int column = (jointType & 1) != 0 ? this.Horizontal.Read(ref reader, precision) : 0;

        return reference + new Av1MotionVector(row, column);
    }

    /// <summary>
    /// Writes a motion vector relative to a spatially derived reference.
    /// </summary>
    /// <param name="writer">The tile range encoder.</param>
    /// <param name="value">The displacement vector to encode.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    /// <param name="precision">The fractional precision selected by the frame header.</param>
    public void Write(Av1SymbolWriter writer, Av1MotionVector value, Av1MotionVector reference, Av1MotionVectorPrecision precision)
        => this.Write<Av1SymbolEncoder.SymbolWriteOperation>(writer, value, reference, precision);

    /// <inheritdoc cref="Write(Av1SymbolWriter, Av1MotionVector, Av1MotionVector, Av1MotionVectorPrecision)"/>
    /// <typeparam name="TOperation">The operation applied to each motion-vector symbol.</typeparam>
    public void Write<TOperation>(
        Av1SymbolWriter writer,
        Av1MotionVector value,
        Av1MotionVector reference,
        Av1MotionVectorPrecision precision)
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
        => _ = this.Process<MotionVectorWriteOperation<TOperation>>(writer, value, reference, precision);

    /// <summary>
    /// Measures a motion-vector delta against the live distributions without changing them.
    /// </summary>
    /// <param name="writer">The tile range encoder associated with the live context.</param>
    /// <param name="value">The motion vector to measure.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    /// <param name="precision">The fractional precision selected by the frame header.</param>
    /// <returns>The syntax cost in 1/512-bit units.</returns>
    public int GetCost(Av1SymbolWriter writer, Av1MotionVector value, Av1MotionVector reference, Av1MotionVectorPrecision precision)
        => this.Process<MotionVectorCostOperation>(writer, value, reference, precision);

    /// <summary>
    /// Processes one complete motion-vector delta through a closed symbol operation.
    /// </summary>
    private int Process<TOperation>(Av1SymbolWriter writer, Av1MotionVector value, Av1MotionVector reference, Av1MotionVectorPrecision precision)
        where TOperation : struct, IMotionVectorSymbolOperation
    {
        int row = value.Row - reference.Row;
        int column = value.Column - reference.Column;

        // Bit zero signals a horizontal delta and bit one signals a vertical delta, producing the four normative
        // zero/horizontal/vertical/both joint symbols without a lookup.
        int jointType = (row != 0 ? 2 : 0) | (column != 0 ? 1 : 0);

        int rate = TOperation.ProcessSymbol(writer, jointType, this.Joint);
        if (row != 0)
        {
            rate += this.Vertical.Process<TOperation>(writer, row, precision);
        }

        if (column != 0)
        {
            rate += this.Horizontal.Process<TOperation>(writer, column, precision);
        }

        return rate;
    }

    /// <summary>
    /// Emits motion-vector syntax and reports no estimated rate.
    /// </summary>
    private readonly struct MotionVectorWriteOperation<TOperation> : IMotionVectorSymbolOperation
        where TOperation : struct, Av1SymbolEncoder.ISymbolOperation
    {
        /// <inheritdoc/>
        public static int ProcessSymbol(Av1SymbolWriter writer, int symbol, Av1Distribution distribution)
            => TOperation.ProcessSymbol(ref writer, symbol, distribution);
    }

    /// <summary>
    /// Measures motion-vector syntax against the live distributions without changing them.
    /// </summary>
    private readonly struct MotionVectorCostOperation : IMotionVectorSymbolOperation
    {
        /// <inheritdoc/>
        public static int ProcessSymbol(Av1SymbolWriter writer, int symbol, Av1Distribution distribution)
            => Av1ProbabilityCost.GetSymbolCost(distribution, symbol);
    }

    /// <summary>
    /// Owns the adaptive magnitude distributions for one motion-vector component.
    /// </summary>
    public sealed class Component
    {
        /// <summary>
        /// Gets the distribution selecting the magnitude class of a nonzero component.
        /// </summary>
        public Av1Distribution MagnitudeClass { get; } = new(28672, 30976, 31858, 32320, 32551, 32656, 32740, 32757, 32762, 32767);

        /// <summary>
        /// Gets the fractional distributions selected by the two class-zero integer offsets.
        /// </summary>
        public Av1Distribution[] ClassZeroFractional { get; } =
        [
            new(16384, 24576, 26624),
            new(12288, 21248, 24128)
        ];

        /// <summary>
        /// Gets the fractional distribution used by nonzero magnitude classes.
        /// </summary>
        public Av1Distribution Fractional { get; } = new(8192, 17408, 21248);

        /// <summary>
        /// Gets the distribution selecting the sign of a nonzero component.
        /// </summary>
        public Av1Distribution Sign { get; } = new(16384);

        /// <summary>
        /// Gets the eighth-sample distribution used by class-zero magnitudes.
        /// </summary>
        public Av1Distribution ClassZeroHighPrecision { get; } = new(20480);

        /// <summary>
        /// Gets the eighth-sample distribution used by nonzero magnitude classes.
        /// </summary>
        public Av1Distribution HighPrecision { get; } = new(16384);

        /// <summary>
        /// Gets the distribution selecting either of the two class-zero integer magnitude offsets.
        /// </summary>
        public Av1Distribution ClassZero { get; } = new(27648);

        /// <summary>
        /// Gets the binary distributions that reconstruct larger integer magnitude offsets from least to most significant bit.
        /// </summary>
        public Av1Distribution[] OffsetBits { get; } =
        [
            new(17408), new(17920), new(18944), new(20480), new(22528),
            new(24576), new(28672), new(29952), new(29952), new(30720)
        ];

        /// <summary>
        /// Replaces every component distribution with state copied from another component.
        /// </summary>
        /// <param name="source">The component state to copy.</param>
        public void CopyFrom(Component source)
        {
            this.MagnitudeClass.CopyFrom(source.MagnitudeClass);

            for (int offset = 0; offset < this.ClassZeroFractional.Length; offset++)
            {
                this.ClassZeroFractional[offset].CopyFrom(source.ClassZeroFractional[offset]);
            }

            this.Fractional.CopyFrom(source.Fractional);
            this.Sign.CopyFrom(source.Sign);
            this.ClassZeroHighPrecision.CopyFrom(source.ClassZeroHighPrecision);
            this.HighPrecision.CopyFrom(source.HighPrecision);
            this.ClassZero.CopyFrom(source.ClassZero);

            for (int bit = 0; bit < this.OffsetBits.Length; bit++)
            {
                this.OffsetBits[bit].CopyFrom(source.OffsetBits[bit]);
            }
        }

        /// <summary>
        /// Resets every observation count used to adapt one component's distributions.
        /// </summary>
        public void ResetUpdateCounts()
        {
            this.MagnitudeClass.ResetUpdateCount();

            for (int offset = 0; offset < this.ClassZeroFractional.Length; offset++)
            {
                this.ClassZeroFractional[offset].ResetUpdateCount();
            }

            this.Fractional.ResetUpdateCount();
            this.Sign.ResetUpdateCount();
            this.ClassZeroHighPrecision.ResetUpdateCount();
            this.HighPrecision.ResetUpdateCount();
            this.ClassZero.ResetUpdateCount();

            for (int bit = 0; bit < this.OffsetBits.Length; bit++)
            {
                this.OffsetBits[bit].ResetUpdateCount();
            }
        }

        /// <summary>
        /// Reads one signed motion-vector component at the requested precision.
        /// </summary>
        /// <param name="reader">The tile range decoder.</param>
        /// <param name="precision">The fractional precision allowed by the current frame.</param>
        /// <returns>The signed component in one-eighth-sample units.</returns>
        public int Read(ref Av1SymbolReader reader, Av1MotionVectorPrecision precision)
        {
            bool isNegative = reader.ReadSymbol(this.Sign) != 0;
            int magnitudeClass = reader.ReadSymbol(this.MagnitudeClass);
            bool isClassZero = magnitudeClass == 0;
            int integerOffset;
            int magnitudeBase;

            if (isClassZero)
            {
                integerOffset = reader.ReadSymbol(this.ClassZero);
                magnitudeBase = 0;
            }
            else
            {
                int bitCount = magnitudeClass + ClassZeroBitCount - 1;
                integerOffset = 0;

                for (int bit = 0; bit < bitCount; bit++)
                {
                    // AV1 transmits the integer offset least-significant bit first, with an independently adapting
                    // distribution for every bit position.
                    integerOffset |= reader.ReadSymbol(this.OffsetBits[bit]) << bit;
                }

                // Class one uses a base of two whole samples, or sixteen eighth-sample units, and every later class doubles
                // that base. CLASS0_SIZE shifted by class + 2 expresses the same scale directly in eighth-sample units.
                magnitudeBase = ClassZeroSize << (magnitudeClass + 2);
            }

            int fractional;
            int highPrecision;

            if (precision != Av1MotionVectorPrecision.Integer)
            {
                // Class-zero magnitudes select one of two fractional CDFs using the already decoded integer offset;
                // larger classes share one fractional CDF because their expanded integer range supplies the context.
                Av1Distribution fractionalDistribution = isClassZero ? this.ClassZeroFractional[integerOffset] : this.Fractional;
                fractional = reader.ReadSymbol(fractionalDistribution);

                // Quarter-sample motion omits the eighth-sample symbol. The normative implicit one, combined with the
                // final increment below, constrains the result to even one-eighth-sample units.
                highPrecision = precision == Av1MotionVectorPrecision.EighthSample
                    ? reader.ReadSymbol(isClassZero ? this.ClassZeroHighPrecision : this.HighPrecision)
                    : 1;
            }
            else
            {
                // Integer motion omits both fractional symbols. The implicit maximum values make the low three bits
                // all one before the final increment, constraining the result to whole-sample multiples of eight.
                fractional = 3;
                highPrecision = 1;
            }

            // The entropy syntax represents magnitude minus one. Integer offset occupies bits three and above,
            // fractional occupies bits one and two, and high precision occupies bit zero, all in one-eighth-sample units.
            int magnitude = magnitudeBase + ((integerOffset << 3) | (fractional << 1) | highPrecision) + 1;

            return isNegative ? -magnitude : magnitude;
        }

        /// <summary>
        /// Writes one signed motion-vector component.
        /// </summary>
        /// <param name="writer">The tile range encoder.</param>
        /// <param name="value">The nonzero component in one-eighth-sample units.</param>
        /// <param name="precision">The fractional precision selected by the frame header.</param>
        public void Write(Av1SymbolWriter writer, int value, Av1MotionVectorPrecision precision)
            => _ = this.Process<MotionVectorWriteOperation<Av1SymbolEncoder.SymbolWriteOperation>>(writer, value, precision);

        /// <summary>
        /// Processes one nonzero signed component through the shared motion-vector symbol operation.
        /// </summary>
        public int Process<TOperation>(Av1SymbolWriter writer, int value, Av1MotionVectorPrecision precision)
            where TOperation : struct, IMotionVectorSymbolOperation
        {
            int magnitude = Math.Abs(value);
            int precisionMask = precision == Av1MotionVectorPrecision.Integer
                ? 7
                : precision == Av1MotionVectorPrecision.QuarterSample ? 1 : 0;

            DebugGuard.IsTrue(
                magnitude > 0 && (magnitude & precisionMask) == 0,
                "Motion-vector components must match the frame precision.");

            // The coded value is magnitude minus one. Its whole-sample portion selects the doubling class,
            // while the remainder carries integer offset, fractional phase, and the high-precision bit.
            int codedMagnitude = magnitude - 1;
            uint classValue = (uint)(codedMagnitude >> 3);
            int magnitudeClass = classValue == 0 ? 0 : Av1Math.MostSignificantBit(classValue);
            DebugGuard.MustBeLessThan(magnitudeClass, MagnitudeClassCount, nameof(magnitudeClass));
            int magnitudeBase = magnitudeClass == 0 ? 0 : ClassZeroSize << (magnitudeClass + 2);
            int offset = codedMagnitude - magnitudeBase;
            int integerOffset = offset >> 3;
            int fractional = (offset >> 1) & 3;
            int highPrecision = offset & 1;
            int rate = TOperation.ProcessSymbol(writer, value < 0 ? 1 : 0, this.Sign);
            rate += TOperation.ProcessSymbol(writer, magnitudeClass, this.MagnitudeClass);

            if (magnitudeClass == 0)
            {
                rate += TOperation.ProcessSymbol(writer, integerOffset, this.ClassZero);
            }
            else
            {
                for (int bit = 0; bit < magnitudeClass; bit++)
                {
                    // Integer offsets are transmitted least-significant bit first through independent models.
                    rate += TOperation.ProcessSymbol(writer, (integerOffset >> bit) & 1, this.OffsetBits[bit]);
                }
            }

            if (precision != Av1MotionVectorPrecision.Integer)
            {
                Av1Distribution fractionalDistribution = magnitudeClass == 0
                    ? this.ClassZeroFractional[integerOffset]
                    : this.Fractional;

                rate += TOperation.ProcessSymbol(writer, fractional, fractionalDistribution);
            }

            if (precision == Av1MotionVectorPrecision.EighthSample)
            {
                Av1Distribution highPrecisionDistribution = magnitudeClass == 0
                    ? this.ClassZeroHighPrecision
                    : this.HighPrecision;

                rate += TOperation.ProcessSymbol(writer, highPrecision, highPrecisionDistribution);
            }

            return rate;
        }
    }
}
