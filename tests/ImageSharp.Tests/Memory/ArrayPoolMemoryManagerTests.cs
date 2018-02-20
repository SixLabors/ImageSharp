// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using SixLabors.ImageSharp.Memory;

    using Xunit;

    public class ArrayPoolMemoryManagerTests
    {
        private const int MaxPooledBufferSizeInBytes = 2048;
        
        private MemoryManager MemoryManager { get; } = new ArrayPoolMemoryManager(MaxPooledBufferSizeInBytes);
        
        /// <summary>
        /// Rent 'n' buffers -> return all -> re-rent, verify if there is at least one in common.
        /// </summary>
        private bool CheckIsPooled<T>(int size)
            where T : struct
        {
            IBuffer<T> buf1 = this.MemoryManager.Allocate<T>(size);
            IBuffer<T> buf2 = this.MemoryManager.Allocate<T>(size);
            IBuffer<T> buf3 = this.MemoryManager.Allocate<T>(size);

            ref T buf1FirstPrev = ref buf1.DangerousGetPinnableReference();
            ref T buf2FirstPrev = ref buf2.DangerousGetPinnableReference();
            ref T buf3FirstPrev = ref buf3.DangerousGetPinnableReference();

            buf1.Dispose();
            buf2.Dispose();
            buf3.Dispose();

            buf1 = this.MemoryManager.Allocate<T>(size);
            buf2 = this.MemoryManager.Allocate<T>(size);
            buf3 = this.MemoryManager.Allocate<T>(size);

            bool same1 = Unsafe.AreSame(ref buf1FirstPrev, ref buf1.DangerousGetPinnableReference());
            bool same2 = Unsafe.AreSame(ref buf2FirstPrev, ref buf2.DangerousGetPinnableReference());
            bool same3 = Unsafe.AreSame(ref buf3FirstPrev, ref buf3.DangerousGetPinnableReference());

            buf1.Dispose();
            buf2.Dispose();
            buf3.Dispose();

            return same1 || same2 || same3;
        }

        [StructLayout(LayoutKind.Explicit, Size = MaxPooledBufferSizeInBytes / 4)]
        struct LargeStruct
        {
        }

        [Theory]
        [InlineData(32)]
        [InlineData(512)]
        [InlineData(MaxPooledBufferSizeInBytes - 1)]
        public void SmallBuffersArePooled_OfByte(int size)
        {
            Assert.True(this.CheckIsPooled<byte>(size));
        }


        [Theory]
        [InlineData(128 * 1024 * 1024)]
        [InlineData(MaxPooledBufferSizeInBytes + 1)]
        public void LargeBuffersAreNotPooled_OfByte(int size)
        {
            Assert.False(this.CheckIsPooled<byte>(size));
        }

        [Fact]
        public unsafe void SmallBuffersArePooled_OfBigValueType()
        {
            int count = MaxPooledBufferSizeInBytes / sizeof(LargeStruct) - 1;

            Assert.True(this.CheckIsPooled<LargeStruct>(count));
        }

        [Fact]
        public unsafe void LaregeBuffersAreNotPooled_OfBigValueType()
        {
            int count = MaxPooledBufferSizeInBytes / sizeof(LargeStruct) + 1;

            Assert.False(this.CheckIsPooled<LargeStruct>(count));
        }
    }
}