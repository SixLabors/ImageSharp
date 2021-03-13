// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    [Trait("Category", "PixelFormats")]
    public partial class PixelOperationsTests
    {
        public partial class Rgba32_OperationsTests : PixelOperationsTests<Rgba32>
        {
            [Fact(Skip = SkipProfilingBenchmarks)]
            public void Benchmark_ToVector4()
            {
                const int times = 200000;
                const int count = 1024;

                using (IMemoryOwner<Rgba32> source = Configuration.Default.MemoryAllocator.Allocate<Rgba32>(count))
                using (IMemoryOwner<Vector4> dest = Configuration.Default.MemoryAllocator.Allocate<Vector4>(count))
                {
                    this.Measure(
                        times,
                        () => PixelOperations<Rgba32>.Instance.ToVector4(
                            this.Configuration,
                            source.GetSpan(),
                            dest.GetSpan()));
                }
            }
        }
    }
}
