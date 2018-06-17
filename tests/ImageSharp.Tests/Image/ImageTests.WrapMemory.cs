// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.Memory;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class WrapMemory
        {
            [Fact]
            public void ConsumedBuffer_IsMemoryOwner_ReturnsFalse()
            {
                var memory = new Memory<int>(new int[55]);
                var buffer = new ConsumedBuffer<int>(memory);
                Assert.False(buffer.IsMemoryOwner);
            }
        }
    }
}