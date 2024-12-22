// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Common;

public class NumericsTests
{
    private ITestOutputHelper Output { get; }

    public NumericsTests(ITestOutputHelper output) => this.Output = output;

    public static TheoryData<int> IsOutOfRangeTestData = new() { int.MinValue, -1, 0, 1, 6, 7, 8, 91, 92, 93, int.MaxValue };

    private static uint DivideCeil_ReferenceImplementation(uint value, uint divisor) => (uint)MathF.Ceiling((float)value / divisor);

    [Fact]
    public void DivideCeil_DivideZero()
    {
        uint expected = 0;
        uint actual = Numerics.DivideCeil(0, 100);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 100)]
    public void DivideCeil_RandomValues(int seed, int count)
    {
        Random rng = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            uint value = (uint)rng.Next();
            uint divisor = (uint)rng.Next();

            uint expected = DivideCeil_ReferenceImplementation(value, divisor);
            uint actual = Numerics.DivideCeil(value, divisor);

            Assert.True(expected == actual, $"Expected: {expected}\nActual: {actual}\n{value} / {divisor} = {expected}");
        }
    }

    private static bool IsOutOfRange_ReferenceImplementation(int value, int min, int max) => value < min || value > max;

    [Theory]
    [MemberData(nameof(IsOutOfRangeTestData))]
    public void IsOutOfRange(int value)
    {
        const int min = 7;
        const int max = 92;

        bool expected = IsOutOfRange_ReferenceImplementation(value, min, max);
        bool actual = Numerics.IsOutOfRange(value, min, max);

        Assert.True(expected == actual, $"IsOutOfRange({value}, {min}, {max})");
    }
}
