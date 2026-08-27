// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Decodes integer intra-block-copy displacement vectors with tile-adaptive AV1 distributions.
/// </summary>
internal sealed class Av1DisplacementVectorContext
{
    /// <summary>
    /// The number of magnitude classes defined by AV1.
    /// </summary>
    private const int MagnitudeClassCount = 11;

    /// <summary>
    /// The number of class-zero integer magnitude bits.
    /// </summary>
    private const int ClassZeroBitCount = 1;

    /// <summary>
    /// The tile-adaptive distribution selecting which vector components are nonzero.
    /// </summary>
    private readonly Av1Distribution joint = new(4096, 11264, 19328);

    /// <summary>
    /// The tile-adaptive vertical component distributions.
    /// </summary>
    private readonly Component vertical = new();

    /// <summary>
    /// The tile-adaptive horizontal component distributions.
    /// </summary>
    private readonly Component horizontal = new();

    /// <summary>
    /// Replaces every displacement-vector distribution with state copied from another context.
    /// </summary>
    /// <param name="source">The displacement-vector context state to copy.</param>
    public void CopyFrom(Av1DisplacementVectorContext source)
    {
        this.joint.CopyFrom(source.joint);
        this.vertical.CopyFrom(source.vertical);
        this.horizontal.CopyFrom(source.horizontal);
    }

    /// <summary>
    /// Resets every observation count used to adapt displacement-vector distributions.
    /// </summary>
    public void ResetUpdateCounts()
    {
        this.joint.ResetUpdateCount();
        this.vertical.ResetUpdateCounts();
        this.horizontal.ResetUpdateCounts();
    }

    /// <summary>
    /// Reads an integer displacement vector relative to a spatially derived reference.
    /// </summary>
    /// <param name="reader">The tile range decoder.</param>
    /// <param name="reference">The reference displacement vector.</param>
    /// <returns>The decoded displacement vector in one-eighth-sample units.</returns>
    public Av1MotionVector Read(ref Av1SymbolReader reader, Av1MotionVector reference)
    {
        int jointType = reader.ReadSymbol(this.joint);

        // Joint values 1 and 3 carry a horizontal delta; values 2 and 3 carry a vertical delta. Intra-block copy
        // fixes precision to whole luma samples, so the component reader consumes no fractional or high-precision CDFs.
        int row = jointType >= 2 ? this.vertical.Read(ref reader) : 0;
        int column = (jointType & 1) != 0 ? this.horizontal.Read(ref reader) : 0;
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
        int jointType = (row != 0 ? 2 : 0) | (column != 0 ? 1 : 0);
        writer.WriteSymbol(jointType, this.joint);

        if (row != 0)
        {
            this.vertical.Write(writer, row);
        }

        if (column != 0)
        {
            this.horizontal.Write(writer, column);
        }
    }

    /// <summary>
    /// Stores the adaptive magnitude distributions for one displacement-vector component.
    /// </summary>
    private sealed class Component
    {
        /// <summary>
        /// The distribution selecting the signed magnitude class.
        /// </summary>
        private readonly Av1Distribution magnitudeClass = new(28672, 30976, 31858, 32320, 32551, 32656, 32740, 32757, 32762, 32767);

        /// <summary>
        /// The distribution selecting the sign of a nonzero component.
        /// </summary>
        private readonly Av1Distribution sign = new(16384);

        /// <summary>
        /// The distribution selecting either of the two class-zero integer magnitudes.
        /// </summary>
        private readonly Av1Distribution classZero = new(27648);

        /// <summary>
        /// The binary distributions that reconstruct larger magnitude offsets from least to most significant bit.
        /// </summary>
        private readonly Av1Distribution[] offsetBits =
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
            this.magnitudeClass.CopyFrom(source.magnitudeClass);
            this.sign.CopyFrom(source.sign);
            this.classZero.CopyFrom(source.classZero);

            for (int bit = 0; bit < this.offsetBits.Length; bit++)
            {
                this.offsetBits[bit].CopyFrom(source.offsetBits[bit]);
            }
        }

        /// <summary>
        /// Resets every observation count used to adapt one component's distributions.
        /// </summary>
        public void ResetUpdateCounts()
        {
            this.magnitudeClass.ResetUpdateCount();
            this.sign.ResetUpdateCount();
            this.classZero.ResetUpdateCount();

            for (int bit = 0; bit < this.offsetBits.Length; bit++)
            {
                this.offsetBits[bit].ResetUpdateCount();
            }
        }

        /// <summary>
        /// Reads one signed integer-precision component.
        /// </summary>
        /// <param name="reader">The tile range decoder.</param>
        /// <returns>The signed component in one-eighth-sample units.</returns>
        public int Read(ref Av1SymbolReader reader)
        {
            bool isNegative = reader.ReadSymbol(this.sign) != 0;
            int magnitudeClass = reader.ReadSymbol(this.magnitudeClass);
            int offset;
            int magnitudeBase;

            if (magnitudeClass == 0)
            {
                offset = reader.ReadSymbol(this.classZero);
                magnitudeBase = 0;
            }
            else
            {
                int bitCount = magnitudeClass + ClassZeroBitCount - 1;
                offset = 0;
                for (int bit = 0; bit < bitCount; bit++)
                {
                    // AV1 transmits the integer offset least-significant bit first, with an independently adapting
                    // distribution for every bit position.
                    offset |= reader.ReadSymbol(this.offsetBits[bit]) << bit;
                }

                magnitudeBase = (1 << ClassZeroBitCount) << (magnitudeClass + 2);
            }

            // Integer precision substitutes the normative fractional values fr=3 and hp=1. The low three bits are
            // consequently all one, and the final increment converts the zero-based magnitude representation.
            int magnitude = magnitudeBase + (offset << 3) + 8;
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

            int magnitudeClass = magnitude <= 16 ? 0 : Av1Math.MostSignificantBit((uint)(magnitude - 1)) - 3;
            DebugGuard.MustBeLessThan(magnitudeClass, MagnitudeClassCount, nameof(magnitudeClass));
            writer.WriteSymbol(value < 0, this.sign);
            writer.WriteSymbol(magnitudeClass, this.magnitudeClass);

            if (magnitudeClass == 0)
            {
                writer.WriteSymbol((magnitude >> 3) - 1, this.classZero);
                return;
            }

            int magnitudeBase = 8 << magnitudeClass;
            int offset = (magnitude - magnitudeBase - 8) >> 3;
            for (int bit = 0; bit < magnitudeClass; bit++)
            {
                // The decoder reconstructs offsets least-significant bit first, so each adaptive bit model must be
                // updated in the same order during encoding.
                writer.WriteSymbol(((offset >> bit) & 1) != 0, this.offsetBits[bit]);
            }
        }
    }
}
