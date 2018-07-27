// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.Memory.Tests
{
    public class SimpleGcMemoryManagerTests
    {
        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new SimpleGcMemoryAllocator())
            {
            }
        }
    }
}