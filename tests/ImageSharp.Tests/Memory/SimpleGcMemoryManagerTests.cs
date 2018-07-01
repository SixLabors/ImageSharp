namespace SixLabors.ImageSharp.Tests.Memory
{
    using SixLabors.Memory;

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