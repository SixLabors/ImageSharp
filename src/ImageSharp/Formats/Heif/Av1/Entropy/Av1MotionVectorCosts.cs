// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Borrows the motion-vector rate tables retained by one encoder worker.
/// </summary>
internal readonly ref struct Av1MotionVectorCosts
{
    /// <summary>
    /// The largest representable signed component difference, in eighth-sample units.
    /// </summary>
    public const int MaximumComponent = (1 << 14) - 1;

    private const int ComponentCount = (2 * MaximumComponent) + 1;

    /// <summary>
    /// Storage for the joint symbols and two component pairs, one for each fractional precision.
    /// </summary>
    public const int StorageLength = 4 + (4 * ComponentCount);

    private readonly Span<int> joint;
    private readonly Span<int> row;
    private readonly Span<int> column;
    private readonly Av1MotionVectorPrecision precision;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1MotionVectorCosts"/> struct.
    /// </summary>
    /// <param name="storage">Worker-lifetime storage containing both precision tables.</param>
    /// <param name="precision">The precision used by the current frame.</param>
    public Av1MotionVectorCosts(Span<int> storage, Av1MotionVectorPrecision precision)
    {
        // Integer and quarter-sample frames share one pair. The eighth-sample pair remains separate so changing
        // frame precision does not change the worker's allocation or the layout of its other scratch regions.
        int offset = 4 + (precision == Av1MotionVectorPrecision.EighthSample ? 2 * ComponentCount : 0);
        this.joint = storage[..4];
        this.row = storage.Slice(offset, ComponentCount);
        this.column = storage.Slice(offset + ComponentCount, ComponentCount);
        this.precision = precision;
    }

    /// <summary>
    /// Captures component rates from the current tile distributions without adapting them.
    /// </summary>
    /// <param name="context">The tile's current motion-vector distributions.</param>
    public void Fill(Av1MotionVectorContext context)
    {
        for (int i = 0; i < 4; i++)
        {
            this.joint[i] = Av1ProbabilityCost.GetSymbolCost(context.Joint, i);
        }

        FillComponent(this.row, context.Vertical, this.precision);
        FillComponent(this.column, context.Horizontal, this.precision);
    }

    /// <summary>
    /// Measures a candidate against its differential reference using the captured distributions.
    /// </summary>
    /// <param name="value">The candidate vector, in eighth-sample units.</param>
    /// <param name="reference">The differential reference, in eighth-sample units.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    public int GetCost(Av1MotionVector value, Av1MotionVector reference)
    {
        int rowDifference = value.Row - reference.Row;
        int columnDifference = value.Column - reference.Column;
        int jointType = (rowDifference != 0 ? 2 : 0) | (columnDifference != 0 ? 1 : 0);
        return this.joint[jointType] + this.row[MaximumComponent + rowDifference] + this.column[MaximumComponent + columnDifference];
    }

    /// <summary>
    /// Builds a signed component table by reusing the costs of shorter binary magnitudes.
    /// </summary>
    /// <param name="destination">The complete signed component table, centered on zero.</param>
    /// <param name="component">The distributions for this axis.</param>
    /// <param name="precision">The fractional symbols present in the frame.</param>
    private static void FillComponent(Span<int> destination, Av1MotionVectorContext.Component component, Av1MotionVectorPrecision precision)
    {
        Span<int> classCosts = stackalloc int[11];
        Span<int> bitCosts = stackalloc int[20];
        Span<int> fractionalCosts = stackalloc int[4];
        Span<int> highPrecisionCosts = stackalloc int[2];
        Span<int> costOffsets = stackalloc int[10];
        int positiveSignCost = Av1ProbabilityCost.GetSymbolCost(component.Sign, 0);
        int negativeSignCost = Av1ProbabilityCost.GetSymbolCost(component.Sign, 1);
        int signDifference = negativeSignCost - positiveSignCost;

        for (int i = 0; i < classCosts.Length; i++)
        {
            classCosts[i] = Av1ProbabilityCost.GetSymbolCost(component.MagnitudeClass, i);
        }

        for (int i = 0; i < costOffsets.Length; i++)
        {
            bitCosts[2 * i] = Av1ProbabilityCost.GetSymbolCost(component.OffsetBits[i], 0);
            bitCosts[(2 * i) + 1] = Av1ProbabilityCost.GetSymbolCost(component.OffsetBits[i], 1);
        }

        // Omitting fractional symbols gives them zero rate. All entries are assigned explicitly because stack
        // storage is uninitialized, including when integer motion disables both fractional syntax stages.
        for (int i = 0; i < fractionalCosts.Length; i++)
        {
            fractionalCosts[i] = precision == Av1MotionVectorPrecision.Integer ? 0 : Av1ProbabilityCost.GetSymbolCost(component.Fractional, i);
        }

        for (int i = 0; i < highPrecisionCosts.Length; i++)
        {
            highPrecisionCosts[i] = precision == Av1MotionVectorPrecision.EighthSample
                ? Av1ProbabilityCost.GetSymbolCost(component.HighPrecision, i)
                : 0;
        }

        costOffsets[0] = 0;
        for (int i = 1; i < costOffsets.Length; i++)
        {
            // A shorter magnitude's leading one becomes an offset bit in a larger magnitude. Remove its
            // former class rate and insert that bit's rate before adding the new magnitude class below.
            costOffsets[i] = bitCosts[(2 * (i - 1)) + 1] - (i > 1 ? classCosts[i - 1] : 0);
        }

        destination[MaximumComponent] = 0;
        for (int fractional = 0; fractional < 4; fractional++)
        {
            for (int highPrecision = 0; highPrecision < 2; highPrecision++)
            {
                int magnitude = (2 * fractional) + highPrecision + 1;
                destination[MaximumComponent + magnitude] = fractionalCosts[fractional] + highPrecisionCosts[highPrecision] + positiveSignCost;
            }
        }

        // Magnitudes encode value minus one. Each exponent doubles the integer offset range, reusing the
        // previously completed lower half. The first eight entries temporarily carry fractional and sign rates
        // alone; class-zero syntax is installed only after all larger magnitudes have consumed those seeds.
        for (int exponentIndex = 0; exponentIndex < 10; exponentIndex++)
        {
            int exponent = 8 << exponentIndex;
            int classCost = exponentIndex >= 1 ? classCosts[exponentIndex] : 0;
            int mantissa = 0;
            for (int bit = 0; bit <= exponentIndex; bit++)
            {
                for (; mantissa < (8 << bit); mantissa++)
                {
                    int cost = destination[MaximumComponent + mantissa + 1] + classCost + costOffsets[bit];
                    int magnitude = exponent + mantissa + 1;
                    destination[MaximumComponent + magnitude] = cost;
                    destination[MaximumComponent - magnitude] = cost + signDifference;
                }

                // The next exponent introduces one more leading zero in this mantissa group.
                costOffsets[bit] += bitCosts[2 * exponentIndex];
            }
        }

        // The final exponent ends at 16383, one entry before the next power of two. Treat its upper mantissa
        // separately to avoid producing the unrepresentable magnitude 16384 or reading an eleventh offset bit.
        int finalMantissa = 0;
        for (int bit = 0; bit < 10; bit++)
        {
            for (; finalMantissa < (8 << bit); finalMantissa++)
            {
                int cost = destination[MaximumComponent + finalMantissa + 1] + classCosts[10] + costOffsets[bit];
                int magnitude = 8192 + finalMantissa + 1;
                destination[MaximumComponent + magnitude] = cost;
                destination[MaximumComponent - magnitude] = cost + signDifference;
            }
        }

        int finalOffset = bitCosts[19] - classCosts[9];
        for (; finalMantissa < 8191; finalMantissa++)
        {
            int cost = destination[MaximumComponent + finalMantissa + 1] + classCosts[10] + finalOffset;
            int magnitude = 8192 + finalMantissa + 1;
            destination[MaximumComponent + magnitude] = cost;
            destination[MaximumComponent - magnitude] = cost + signDifference;
        }

        for (int integerOffset = 0; integerOffset < 2; integerOffset++)
        {
            int classZeroCost = classCosts[0] + Av1ProbabilityCost.GetSymbolCost(component.ClassZero, integerOffset);
            for (int fractional = 0; fractional < 4; fractional++)
            {
                int cost = classZeroCost;
                if (precision != Av1MotionVectorPrecision.Integer)
                {
                    cost += Av1ProbabilityCost.GetSymbolCost(component.ClassZeroFractional[integerOffset], fractional);
                }

                for (int highPrecision = 0; highPrecision < 2; highPrecision++)
                {
                    int magnitude = (8 * integerOffset) + (2 * fractional) + highPrecision + 1;
                    int fractionalCost = precision == Av1MotionVectorPrecision.EighthSample
                        ? Av1ProbabilityCost.GetSymbolCost(component.ClassZeroHighPrecision, highPrecision)
                        : 0;

                    destination[MaximumComponent + magnitude] = cost + fractionalCost + positiveSignCost;
                    destination[MaximumComponent - magnitude] = cost + fractionalCost + negativeSignCost;
                }
            }
        }
    }
}
