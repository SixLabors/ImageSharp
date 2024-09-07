// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Tests.Helpers;

public class ParallelExecutionSettingsTests
{
    [Theory]
    [InlineData(-3, true)]
    [InlineData(-2, true)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    public void Constructor_MaxDegreeOfParallelism_CompatibleWith_ParallelOptions(int maxDegreeOfParallelism, bool throws)
    {
        if (throws)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    _ = new ParallelExecutionSettings(
                        maxDegreeOfParallelism,
                        Configuration.Default.MemoryAllocator);
                });
        }
        else
        {
            ParallelExecutionSettings parallelSettings = new(
                maxDegreeOfParallelism,
                Configuration.Default.MemoryAllocator);
            Assert.Equal(maxDegreeOfParallelism, parallelSettings.MaxDegreeOfParallelism);
        }
    }
}
