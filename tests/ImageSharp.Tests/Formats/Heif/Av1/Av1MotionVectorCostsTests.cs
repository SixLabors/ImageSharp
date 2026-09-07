// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1MotionVectorCostsTests
{
    /// <summary>
    /// Checks the table recurrence against symbol-by-symbol rate accumulation over every legal signed magnitude.
    /// </summary>
    /// <param name="precisionValue">The frame motion precision.</param>
    /// <param name="step">The corresponding increment between representable components.</param>
    /// <param name="adapt">Whether to adapt the entropy distributions before building the table.</param>
    [Theory]
    [InlineData(Av1MotionVectorPrecision.Integer, 8, false)]
    [InlineData(Av1MotionVectorPrecision.QuarterSample, 2, false)]
    [InlineData(Av1MotionVectorPrecision.EighthSample, 1, false)]
    [InlineData(Av1MotionVectorPrecision.Integer, 8, true)]
    [InlineData(Av1MotionVectorPrecision.QuarterSample, 2, true)]
    [InlineData(Av1MotionVectorPrecision.EighthSample, 1, true)]
    public void EveryLegalComponentMatchesIndependentSymbolTraversal(int precisionValue, int step, bool adapt)
    {
        Av1MotionVectorPrecision precision = (Av1MotionVectorPrecision)precisionValue;
        using Av1SymbolEncoder writer = new(Configuration.Default, 65536, 128, updateCdf: true);
        if (adapt)
        {
            // Exercise asymmetric axes and signs, class-zero offsets, fractional symbols, and larger classes.
            // The oracle below walks the syntax bits; it does not reuse the table's magnitude recurrence.
            for (int i = 0; i < 1000; i++)
            {
                int row = ((i * 37) % 1024) * step;
                int column = -((i * 71) % 2048) * step;
                writer.WriteMotionVector(new(row, column), default, precision);
            }
        }

        int[] storage = new int[Av1MotionVectorCosts.StorageLength + 2];
        storage.AsSpan().Fill(-1234567);
        Av1MotionVectorCosts costs = new(storage.AsSpan(1, Av1MotionVectorCosts.StorageLength), precision);
        writer.FillMotionVectorCosts(costs);
        Av1MotionVector reference = new(16, -24);
        int maximum = (Av1MotionVectorCosts.MaximumComponent / step) * step;
        for (int difference = -maximum; difference <= maximum; difference += step)
        {
            Av1MotionVector vertical = new(reference.Row + difference, reference.Column);
            Av1MotionVector horizontal = new(reference.Row, reference.Column + difference);
            Av1MotionVector both = new(reference.Row + difference, reference.Column - difference);
            Assert.Equal(writer.GetMotionVectorCost(vertical, reference, precision), costs.GetCost(vertical, reference));
            Assert.Equal(writer.GetMotionVectorCost(horizontal, reference, precision), costs.GetCost(horizontal, reference));
            Assert.Equal(writer.GetMotionVectorCost(both, reference, precision), costs.GetCost(both, reference));
        }

        Assert.Equal(-1234567, storage[0]);
        Assert.Equal(-1234567, storage[^1]);
    }

    /// <summary>
    /// Verifies that table construction preserves the other precision pair and that adaptation requires an explicit refresh.
    /// </summary>
    [Fact]
    public void SnapshotRetainsRatesUntilExplicitRefreshAndPrecisionPairsDoNotOverlap()
    {
        using Av1SymbolEncoder writer = new(Configuration.Default, 65536, 128, updateCdf: true);
        int[] storage = new int[Av1MotionVectorCosts.StorageLength];
        Av1MotionVectorCosts quarter = new(storage, Av1MotionVectorPrecision.QuarterSample);
        Av1MotionVectorCosts eighth = new(storage, Av1MotionVectorPrecision.EighthSample);
        writer.FillMotionVectorCosts(quarter);
        Av1MotionVector value = new(24, -48);
        int original = quarter.GetCost(value, default);
        writer.FillMotionVectorCosts(eighth);
        Assert.Equal(original, quarter.GetCost(value, default));

        for (int i = 0; i < 100; i++)
        {
            writer.WriteMotionVector(value, default, Av1MotionVectorPrecision.QuarterSample);
        }

        Assert.Equal(original, quarter.GetCost(value, default));
        int adapted = writer.GetMotionVectorCost(value, default, Av1MotionVectorPrecision.QuarterSample);
        Assert.NotEqual(original, adapted);
        writer.FillMotionVectorCosts(quarter);
        Assert.Equal(adapted, quarter.GetCost(value, default));
    }
}
