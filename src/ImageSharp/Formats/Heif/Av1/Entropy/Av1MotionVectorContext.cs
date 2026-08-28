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
    /// Writes an integer displacement vector relative to a spatially derived reference.
    /// </summary>
    /// <param name="writer">The tile range encoder.</param>
    /// <param name="value">The displacement vector to encode.</param>
    /// <param name="reference">The spatially derived reference vector.</param>
    public void Write(Av1SymbolWriter writer, Av1MotionVector value, Av1MotionVector reference)
    {
        int row = value.Row - reference.Row;
        int column = value.Column - reference.Column;

        // Bit zero signals a horizontal delta and bit one signals a vertical delta, producing the four normative
        // zero/horizontal/vertical/both joint symbols without a lookup.
        int jointType = (row != 0 ? 2 : 0) | (column != 0 ? 1 : 0);

        writer.WriteSymbol(jointType, this.Joint);
        if (row != 0)
        {
            this.Vertical.Write(writer, row);
        }

        if (column != 0)
        {
            this.Horizontal.Write(writer, column);
        }
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
        /// Writes one signed integer-precision component.
        /// </summary>
        /// <param name="writer">The tile range encoder.</param>
        /// <param name="value">The nonzero component in one-eighth-sample units.</param>
        public void Write(Av1SymbolWriter writer, int value)
        {
            int magnitude = Math.Abs(value);
            DebugGuard.IsTrue(magnitude > 0 && (magnitude & 7) == 0, "Displacement-vector components must use whole-sample precision.");

            // Class zero contains the two whole-sample magnitudes 8 and 16. Above it, the highest set bit of magnitude
            // minus one selects the doubling range; subtracting three converts the eighth-sample bit index to the class.
            int magnitudeClass = magnitude <= (ClassZeroSize << 3) ? 0 : Av1Math.MostSignificantBit((uint)(magnitude - 1)) - 3;
            DebugGuard.MustBeLessThan(magnitudeClass, MagnitudeClassCount, nameof(magnitudeClass));
            writer.WriteSymbol(value < 0, this.Sign);
            writer.WriteSymbol(magnitudeClass, this.MagnitudeClass);

            if (magnitudeClass == 0)
            {
                writer.WriteSymbol((magnitude >> 3) - 1, this.ClassZero);
                return;
            }

            // Remove the class base and the implicit low-bit value 7 plus the final one before coding the remaining
            // whole-sample offset least-significant bit first.
            int magnitudeBase = ClassZeroSize << (magnitudeClass + 2);
            int integerOffset = (magnitude - magnitudeBase - 8) >> 3;

            for (int bit = 0; bit < magnitudeClass; bit++)
            {
                // The decoder reconstructs offsets least-significant bit first, so each adaptive bit model must be
                // updated in the same order during encoding.
                writer.WriteSymbol(((integerOffset >> bit) & 1) != 0, this.OffsetBits[bit]);
            }
        }
    }
}
