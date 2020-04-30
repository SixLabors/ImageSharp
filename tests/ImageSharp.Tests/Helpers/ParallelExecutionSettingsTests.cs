// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using SixLabors.ImageSharp.Advanced;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
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
                var parallelSettings = new ParallelExecutionSettings(
                    maxDegreeOfParallelism,
                    Configuration.Default.MemoryAllocator);
                Assert.Equal(maxDegreeOfParallelism, parallelSettings.MaxDegreeOfParallelism);
            }
        }
    }
}
