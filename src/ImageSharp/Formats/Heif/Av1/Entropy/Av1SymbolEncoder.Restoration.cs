// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Writes restoration-unit choices and reference-centered coefficient codes.
/// </content>
internal sealed partial class Av1SymbolEncoder
{
    /// <summary>
    /// Writes one restoration unit and advances the tile's selected coefficient history.
    /// </summary>
    /// <typeparam name="TOperation">The tile symbol operation.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="frameType">The plane's frame restoration mode.</param>
    /// <param name="unit">The selected unit parameters.</param>
    /// <param name="chroma">Whether the plane is chroma.</param>
    /// <param name="reference">The last transmitted Wiener and self-guided parameters in this tile.</param>
    public void WriteRestorationUnit<TOperation>(
        ref Span<byte> output,
        ObuRestorationType frameType,
        Av1LoopRestorationUnit unit,
        bool chroma,
        ref Av1LoopRestorationUnit reference)
        where TOperation : struct, ISymbolOperation
    {
        if (frameType == ObuRestorationType.Switchable)
        {
            _ = TOperation.ProcessSymbol(ref this.writer, ref output, (int)unit.FilterType, this.entropyContext.SwitchableRestoration);
        }
        else
        {
            Av1Distribution distribution = frameType == ObuRestorationType.Wiener
                ? this.entropyContext.WienerRestoration
                : this.entropyContext.SgrProjectionRestoration;

            _ = TOperation.ProcessSymbol(ref this.writer, ref output, unit.FilterType != Av1RestorationFilterType.None, distribution);
        }

        if (unit.FilterType != Av1RestorationFilterType.None)
        {
            _ = this.ProcessRestorationCoefficients<TOperation>(ref output, unit, reference, chroma);
            if (unit.FilterType == Av1RestorationFilterType.Wiener)
            {
                reference.WienerVertical = unit.WienerVertical;
                reference.WienerHorizontal = unit.WienerHorizontal;
            }
            else
            {
                reference.SgrParameterSet = unit.SgrParameterSet;
                reference.SgrProjectionCoefficients = unit.SgrProjectionCoefficients;
            }
        }
    }

    /// <summary>
    /// Gets the fixed-point literal rate of restoration coefficients against the supplied tile history.
    /// </summary>
    /// <param name="unit">The candidate filter parameters.</param>
    /// <param name="reference">The preceding transmitted parameters for this frame mode.</param>
    /// <param name="chroma">Whether the plane is chroma.</param>
    /// <returns>The coefficient rate in probability-cost units.</returns>
    public int GetRestorationCoefficientCost(Av1LoopRestorationUnit unit, Av1LoopRestorationUnit reference, bool chroma)
    {
        // A cost writes nothing, so it needs no tile buffer.
        Span<byte> output = default;
        return this.ProcessRestorationCoefficients<CoefficientCostOperation>(ref output, unit, reference, chroma);
    }

    /// <summary>
    /// Processes the coefficient syntax shared by writing and candidate rate measurement.
    /// </summary>
    /// <typeparam name="TOperation">The literal operation.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="unit">The filter parameters.</param>
    /// <param name="reference">The preceding parameters.</param>
    /// <param name="chroma">Whether the plane omits the outer Wiener tap.</param>
    /// <returns>The accumulated literal rate.</returns>
    private int ProcessRestorationCoefficients<TOperation>(ref Span<byte> output, Av1LoopRestorationUnit unit, Av1LoopRestorationUnit reference, bool chroma)
        where TOperation : struct, ISymbolOperation
    {
        int cost = 0;
        if (unit.FilterType == Av1RestorationFilterType.Wiener)
        {
            ReadOnlySpan<int> minimum = [-5, -23, -17];
            ReadOnlySpan<int> counts = [16, 32, 64];
            for (int axis = 0; axis < 2; axis++)
            {
                for (int tap = chroma ? 1 : 0; tap < 3; tap++)
                {
                    int current = axis == 0 ? unit.WienerVertical[tap] : unit.WienerHorizontal[tap];
                    int previous = axis == 0 ? reference.WienerVertical[tap] : reference.WienerHorizontal[tap];
                    cost += this.ProcessReferencedSubexponential<TOperation>(
                        ref output, counts[tap], tap + 1, previous - minimum[tap], current - minimum[tap]);
                }
            }
        }
        else if (unit.FilterType == Av1RestorationFilterType.SgrProjection)
        {
            cost = TOperation.ProcessLiteral(ref this.writer, ref output, (uint)unit.SgrParameterSet, 4);
            ReadOnlySpan<int> radii = Av1SelfGuidedFilter.ParameterRadii.Slice(unit.SgrParameterSet * 2, 2);
            for (int coefficient = 0; coefficient < 2; coefficient++)
            {
                if (radii[coefficient] != 0)
                {
                    int minimum = coefficient == 0 ? -96 : -32;
                    cost += this.ProcessReferencedSubexponential<TOperation>(
                        ref output, 128, 4, reference.SgrProjectionCoefficients[coefficient] - minimum, unit.SgrProjectionCoefficients[coefficient] - minimum);
                }
            }
        }

        return cost;
    }

    /// <summary>
    /// Processes a finite subexponential code centered on the preceding transmitted value.
    /// </summary>
    /// <typeparam name="TOperation">The literal operation.</typeparam>
    /// <param name="output">The tile buffer that the caller read once. A write that grows the buffer replaces it.</param>
    /// <param name="count">The finite alphabet size.</param>
    /// <param name="initialBits">The initial group width exponent.</param>
    /// <param name="reference">The reference value within the alphabet.</param>
    /// <param name="value">The value within the alphabet.</param>
    /// <returns>The accumulated literal rate.</returns>
    private int ProcessReferencedSubexponential<TOperation>(ref Span<byte> output, int count, int initialBits, int reference, int value)
        where TOperation : struct, ISymbolOperation
    {
        // Mirror a reference value in the upper half of the alphabet. Then interleave the positive and negative deltas into one nonnegative value.
        // Values near the reference value are the most likely, and this mapping puts them in the short groups.
        if ((reference << 1) > count)
        {
            reference = count - 1 - reference;
            value = count - 1 - value;
        }

        value = value > reference << 1 ? value : value >= reference ? (value - reference) << 1 : ((reference - value) << 1) - 1;
        int cost = 0;
        int consumed = 0;
        int group = 0;
        while (true)
        {
            int bits = group == 0 ? initialBits : initialBits + group - 1;
            int groupSize = 1 << bits;

            // When no more than three groups of this size remain, a truncated binary code writes the rest of the alphabet. The first
            // shortCount values use one bit less than the other values.
            if (count <= consumed + (3 * groupSize))
            {
                int remaining = count - consumed;
                int relative = value - consumed;
                int literalBits = Av1Math.Log2(remaining) + 1;
                int shortCount = (1 << literalBits) - remaining;
                if (relative < shortCount)
                {
                    cost += TOperation.ProcessLiteral(ref this.writer, ref output, (uint)relative, literalBits - 1);
                }
                else
                {
                    int offset = relative - shortCount;
                    cost += TOperation.ProcessLiteral(ref this.writer, ref output, (uint)(shortCount + (offset >> 1)), literalBits - 1);
                    cost += TOperation.ProcessLiteral(ref this.writer, ref output, (uint)(offset & 1), 1);
                }

                return cost;
            }

            bool next = value >= consumed + groupSize;
            cost += TOperation.ProcessLiteral(ref this.writer, ref output, next ? 1U : 0U, 1);
            if (!next)
            {
                return cost + TOperation.ProcessLiteral(ref this.writer, ref output, (uint)(value - consumed), bits);
            }

            consumed += groupSize;
            group++;
        }
    }
}
