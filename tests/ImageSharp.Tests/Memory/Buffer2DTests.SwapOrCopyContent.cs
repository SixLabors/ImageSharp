// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Memory;

public partial class Buffer2DTests
{
    public class SwapOrCopyContent
    {
        private readonly TestMemoryAllocator memoryAllocator = new TestMemoryAllocator();

        [Fact]
        public void SwapOrCopyContent_WhenBothAllocated()
        {
            using (Buffer2D<int> a = this.memoryAllocator.Allocate2D<int>(10, 5, AllocationOptions.Clean))
            using (Buffer2D<int> b = this.memoryAllocator.Allocate2D<int>(3, 7, AllocationOptions.Clean))
            {
                a[1, 3] = 666;
                b[1, 3] = 444;

                Memory<int> aa = a.FastMemoryGroup.Single();
                Memory<int> bb = b.FastMemoryGroup.Single();

                Buffer2D<int>.SwapOrCopyContent(a, b);

                Assert.Equal(bb, a.FastMemoryGroup.Single());
                Assert.Equal(aa, b.FastMemoryGroup.Single());

                Assert.Equal(new Size(3, 7), a.Size());
                Assert.Equal(new Size(10, 5), b.Size());

                Assert.Equal(666, b[1, 3]);
                Assert.Equal(444, a[1, 3]);
            }
        }

        [Fact]
        public void SwapOrCopyContent_WhenDestinationIsOwned_ShouldNotSwapInDisposedSourceBuffer()
        {
            using MemoryGroup<int> destData = MemoryGroup<int>.Wrap(new int[100]);
            using Buffer2D<int> dest = new Buffer2D<int>(destData, 10, 10);

            using (Buffer2D<int> source = this.memoryAllocator.Allocate2D<int>(10, 10, AllocationOptions.Clean))
            {
                source[0, 0] = 1;
                dest[0, 0] = 2;

                Buffer2D<int>.SwapOrCopyContent(dest, source);
            }

            int actual1 = dest.DangerousGetRowSpan(0)[0];
            int actual2 = dest.DangerousGetRowSpan(0)[0];
            int actual3 = dest.GetSafeRowMemory(0).Span[0];
            int actual5 = dest[0, 0];

            Assert.Equal(1, actual1);
            Assert.Equal(1, actual2);
            Assert.Equal(1, actual3);
            Assert.Equal(1, actual5);
        }

        [Fact]
        public void WhenBothAreMemoryOwners_ShouldSwap()
        {
            this.memoryAllocator.BufferCapacityInBytes = sizeof(int) * 50;
            using Buffer2D<int> a = this.memoryAllocator.Allocate2D<int>(48, 2);
            using Buffer2D<int> b = this.memoryAllocator.Allocate2D<int>(50, 2);

            Memory<int> a0 = a.FastMemoryGroup[0];
            Memory<int> a1 = a.FastMemoryGroup[1];
            Memory<int> b0 = b.FastMemoryGroup[0];
            Memory<int> b1 = b.FastMemoryGroup[1];

            bool swap = Buffer2D<int>.SwapOrCopyContent(a, b);
            Assert.True(swap);

            Assert.Equal(b0, a.FastMemoryGroup[0]);
            Assert.Equal(b1, a.FastMemoryGroup[1]);
            Assert.Equal(a0, b.FastMemoryGroup[0]);
            Assert.Equal(a1, b.FastMemoryGroup[1]);
            Assert.NotEqual(a.FastMemoryGroup[0], b.FastMemoryGroup[0]);
        }

        [Fact]
        public void WhenBothAreMemoryOwners_ShouldReplaceViews()
        {
            using Buffer2D<int> a = this.memoryAllocator.Allocate2D<int>(100, 1);
            using Buffer2D<int> b = this.memoryAllocator.Allocate2D<int>(100, 2);

            a.FastMemoryGroup[0].Span[42] = 1;
            b.FastMemoryGroup[0].Span[33] = 2;
            MemoryGroupView<int> aView0 = (MemoryGroupView<int>)a.MemoryGroup;
            MemoryGroupView<int> bView0 = (MemoryGroupView<int>)b.MemoryGroup;

            Buffer2D<int>.SwapOrCopyContent(a, b);
            Assert.False(aView0.IsValid);
            Assert.False(bView0.IsValid);
            Assert.ThrowsAny<InvalidOperationException>(() => _ = aView0[0].Span);
            Assert.ThrowsAny<InvalidOperationException>(() => _ = bView0[0].Span);

            Assert.True(a.MemoryGroup.IsValid);
            Assert.True(b.MemoryGroup.IsValid);
            Assert.Equal(2, a.MemoryGroup[0].Span[33]);
            Assert.Equal(1, b.MemoryGroup[0].Span[42]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDestIsNotAllocated_SameSize_ShouldCopy(bool sourceIsAllocated)
        {
            Rgba32[] data = new Rgba32[21];
            Rgba32 color = new Rgba32(1, 2, 3, 4);

            using TestMemoryManager<Rgba32> destOwner = new TestMemoryManager<Rgba32>(data);
            using Buffer2D<Rgba32> dest = new Buffer2D<Rgba32>(MemoryGroup<Rgba32>.Wrap(destOwner.Memory), 21, 1);

            using Buffer2D<Rgba32> source = this.memoryAllocator.Allocate2D<Rgba32>(21, 1);

            source.FastMemoryGroup[0].Span[10] = color;

            // Act:
            bool swap = Buffer2D<Rgba32>.SwapOrCopyContent(dest, source);

            // Assert:
            Assert.False(swap);
            Assert.Equal(color, dest.MemoryGroup[0].Span[10]);
            Assert.NotEqual(source.FastMemoryGroup[0], dest.FastMemoryGroup[0]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenDestIsNotMemoryOwner_DifferentSize_Throws(bool sourceIsOwner)
        {
            Rgba32[] data = new Rgba32[21];
            Rgba32 color = new Rgba32(1, 2, 3, 4);

            using TestMemoryManager<Rgba32> destOwner = new TestMemoryManager<Rgba32>(data);
            using Buffer2D<Rgba32> dest = new Buffer2D<Rgba32>(MemoryGroup<Rgba32>.Wrap(destOwner.Memory), 21, 1);

            using Buffer2D<Rgba32> source = this.memoryAllocator.Allocate2D<Rgba32>(22, 1);

            source.FastMemoryGroup[0].Span[10] = color;

            // Act:
            Assert.ThrowsAny<InvalidOperationException>(() => Buffer2D<Rgba32>.SwapOrCopyContent(dest, source));

            Assert.Equal(color, source.MemoryGroup[0].Span[10]);
            Assert.NotEqual(color, dest.MemoryGroup[0].Span[10]);
        }
    }
}
