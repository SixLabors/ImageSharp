namespace SixLabors.ImageSharp.Tests.Memory
{
    using SixLabors.ImageSharp.Memory;

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