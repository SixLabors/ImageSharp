// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests : MemoryGroupTestsBase
    {
        [Fact]
        public void IsValid_TrueAfterCreation()
        {
            using var g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

            Assert.True(g.IsValid);
        }

        [Fact]
        public void IsValid_FalseAfterDisposal()
        {
            using var g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

            g.Dispose();
            Assert.False(g.IsValid);
        }


        [StructLayout(LayoutKind.Sequential, Size = 5)]
        private struct S5
        {
            public override string ToString() => "S5";
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        private struct S4
        {
            public override string ToString() => "S4";
        }
    }

    public abstract class MemoryGroupTestsBase
    {
        internal readonly TestMemoryAllocator MemoryAllocator = new TestMemoryAllocator();

        internal MemoryGroup<int> CreateTestGroup(long totalLength, int bufferLength, bool fillSequence = false)
        {
            this.MemoryAllocator.BufferCapacity = bufferLength;
            var g = MemoryGroup<int>.Allocate(this.MemoryAllocator, totalLength, bufferLength);

            if (!fillSequence)
            {
                return g;
            }

            int j = 1;
            for (MemoryGroupIndex i = g.MinIndex(); i < g.MaxIndex(); i += 1)
            {
                g.SetElementAt(i, j);
                j++;
            }

            return g;
        }
    }
}
