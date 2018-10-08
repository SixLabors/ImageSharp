// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class PlanarColorBufferTests
    {
        private readonly MemoryAllocator allocator;

        public PlanarColorBufferTests()
        {
            this.allocator = ArrayPoolMemoryAllocator.CreateDefault();
        }

        public static readonly TheoryData<int> ConstructLengthData = new TheoryData<int> { 0, 1, 17, 2047 };
        
        [Theory]
        [MemberData(nameof(ConstructLengthData))]
        public void Construct_CreatesCorrectBuffers(int length)
        {
            using (var buffer = new PlanarColorBuffer4F(length, this.allocator, AllocationOptions.None))
            {
                Assert.Equal(4 * length, buffer.Memory.Length);

                buffer.X.Span.Fill(2.0f);
                buffer.Y.Span.Fill(3.0f);
                buffer.Z.Span.Fill(5.0f);
                buffer.W.Span.Fill(7.0f);

                int sum = 0;

                foreach (float val in buffer.Memory.Span)
                {
                    sum += (int)val;
                }

                int expected = (2 + 3 + 5 + 7) * length;
                Assert.Equal(expected, sum);
            }
        }

        [Fact]
        public void Construct_UsesPooledAllocation()
        {
            using (var buffer1 = new PlanarColorBuffer4F(10000, this.allocator, AllocationOptions.None))
            {
                buffer1.Memory.Span[0] = 666f;
            }

            using (var buffer2 = new PlanarColorBuffer4F(10000, this.allocator, AllocationOptions.None))
            {
                Assert.Equal(666f, buffer2.Memory.Span[0]);
            }
        }
    }
}
