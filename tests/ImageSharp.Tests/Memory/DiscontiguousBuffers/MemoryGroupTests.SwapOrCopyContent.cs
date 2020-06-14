// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests
    {
        public class SwapOrCopyContent : MemoryGroupTestsBase
        {
            [Fact]
            public void WhenBothAreMemoryOwners_ShouldSwap()
            {
                this.MemoryAllocator.BufferCapacityInBytes = sizeof(int) * 50;
                using MemoryGroup<int> a = this.MemoryAllocator.AllocateGroup<int>(100, 50);
                using MemoryGroup<int> b = this.MemoryAllocator.AllocateGroup<int>(120, 50);

                Memory<int> a0 = a[0];
                Memory<int> a1 = a[1];
                Memory<int> b0 = b[0];
                Memory<int> b1 = b[1];

                bool swap = MemoryGroup<int>.SwapOrCopyContent(a, b);

                Assert.True(swap);
                Assert.Equal(b0, a[0]);
                Assert.Equal(b1, a[1]);
                Assert.Equal(a0, b[0]);
                Assert.Equal(a1, b[1]);
                Assert.NotEqual(a[0], b[0]);
            }

            [Fact]
            public void WhenBothAreMemoryOwners_ShouldReplaceViews()
            {
                using MemoryGroup<int> a = this.MemoryAllocator.AllocateGroup<int>(100, 100);
                using MemoryGroup<int> b = this.MemoryAllocator.AllocateGroup<int>(120, 100);

                a[0].Span[42] = 1;
                b[0].Span[33] = 2;
                MemoryGroupView<int> aView0 = a.View;
                MemoryGroupView<int> bView0 = b.View;

                MemoryGroup<int>.SwapOrCopyContent(a, b);
                Assert.False(aView0.IsValid);
                Assert.False(bView0.IsValid);
                Assert.ThrowsAny<InvalidOperationException>(() => _ = aView0[0].Span);
                Assert.ThrowsAny<InvalidOperationException>(() => _ = bView0[0].Span);

                Assert.True(a.View.IsValid);
                Assert.True(b.View.IsValid);
                Assert.Equal(2, a.View[0].Span[33]);
                Assert.Equal(1, b.View[0].Span[42]);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenDestIsNotAllocated_SameSize_ShouldCopy(bool sourceIsAllocated)
            {
                var data = new Rgba32[21];
                var color = new Rgba32(1, 2, 3, 4);

                using var destOwner = new TestMemoryManager<Rgba32>(data);
                using var dest = MemoryGroup<Rgba32>.Wrap(destOwner.Memory);

                using MemoryGroup<Rgba32> source = this.MemoryAllocator.AllocateGroup<Rgba32>(21, 30);

                source[0].Span[10] = color;

                // Act:
                bool swap = MemoryGroup<Rgba32>.SwapOrCopyContent(dest, source);

                // Assert:
                Assert.False(swap);
                Assert.Equal(color, dest[0].Span[10]);
                Assert.NotEqual(source[0], dest[0]);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenDestIsNotMemoryOwner_DifferentSize_Throws(bool sourceIsOwner)
            {
                var data = new Rgba32[21];
                var color = new Rgba32(1, 2, 3, 4);

                using var destOwner = new TestMemoryManager<Rgba32>(data);
                var dest = MemoryGroup<Rgba32>.Wrap(destOwner.Memory);

                using MemoryGroup<Rgba32> source = this.MemoryAllocator.AllocateGroup<Rgba32>(22, 30);

                source[0].Span[10] = color;

                // Act:
                Assert.ThrowsAny<InvalidOperationException>(() => MemoryGroup<Rgba32>.SwapOrCopyContent(dest, source));

                Assert.Equal(color, source[0].Span[10]);
                Assert.NotEqual(color, dest[0].Span[10]);
            }
        }
    }
}
