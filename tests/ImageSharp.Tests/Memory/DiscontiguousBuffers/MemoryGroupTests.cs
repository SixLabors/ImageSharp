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
}
