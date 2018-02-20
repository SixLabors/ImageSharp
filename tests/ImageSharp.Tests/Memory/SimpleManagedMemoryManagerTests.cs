namespace SixLabors.ImageSharp.Tests.Memory
{
    using SixLabors.ImageSharp.Memory;

    public class SimpleManagedMemoryManagerTests
    {
        public class BufferTests : BufferTestSuite
        {
            public BufferTests()
                : base(new SimpleManagedMemoryManager())
            {
            }
        }
    }
}