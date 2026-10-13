// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory;

internal class TestMemoryAllocator : MemoryAllocator
{
    private List<AllocationRequest> allocationLog;
    private List<ReturnRequest> returnLog;
    private int nextAllocationId;

    public TestMemoryAllocator(byte dirtyValue = 42)
    {
        this.DirtyValue = dirtyValue;
    }

    /// <summary>
    /// Gets the value to initialize the result buffer with, with non-clean options (<see cref="AllocationOptions.None"/>)
    /// </summary>
    public byte DirtyValue { get; }

    public int BufferCapacityInBytes { get; set; } = int.MaxValue;

    public IReadOnlyList<AllocationRequest> AllocationLog => this.allocationLog ?? throw new InvalidOperationException("Call TestMemoryAllocator.EnableLogging() first!");

    public IReadOnlyList<ReturnRequest> ReturnLog => this.returnLog ?? throw new InvalidOperationException("Call TestMemoryAllocator.EnableLogging() first!");

    protected internal override int GetBufferCapacityInBytes() => this.BufferCapacityInBytes;

    public void EnableNonThreadSafeLogging()
    {
        this.allocationLog = [];
        this.returnLog = [];
    }

    protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
    {
        int allocationId = ++this.nextAllocationId;
        T[] array = this.AllocateArray<T>(length, options, allocationId);
        return new BasicArrayBuffer<T>(array, length, this, allocationId);
    }

    private T[] AllocateArray<T>(int length, AllocationOptions options, int allocationId)
        where T : struct
    {
        T[] array = new T[length + 42];
        this.allocationLog?.Add(AllocationRequest.Create<T>(options, length, array, allocationId));

        if (options == AllocationOptions.None)
        {
            Span<byte> data = MemoryMarshal.Cast<T, byte>(array.AsSpan());
            data.Fill(this.DirtyValue);
        }

        return array;
    }

    private void Return<T>(BasicArrayBuffer<T> buffer)
        where T : struct
    {
        this.returnLog?.Add(new ReturnRequest(buffer.AllocationId, buffer.Array.GetHashCode()));
    }

    public struct AllocationRequest
    {
        private AllocationRequest(
            Type elementType,
            AllocationOptions allocationOptions,
            int length,
            int lengthInBytes,
            int allocationId,
            int hashCodeOfBuffer)
        {
            this.ElementType = elementType;
            this.AllocationOptions = allocationOptions;
            this.Length = length;
            this.LengthInBytes = lengthInBytes;
            this.AllocationId = allocationId;
            this.HashCodeOfBuffer = hashCodeOfBuffer;

            if (elementType == typeof(Vector4))
            {
            }
        }

        public static AllocationRequest Create<T>(AllocationOptions allocationOptions, int length, T[] buffer, int allocationId)
        {
            Type type = typeof(T);
            int elementSize = Unsafe.SizeOf<T>();
            return new AllocationRequest(type, allocationOptions, length, length * elementSize, allocationId, buffer.GetHashCode());
        }

        public Type ElementType { get; }

        public AllocationOptions AllocationOptions { get; }

        public int Length { get; }

        public int LengthInBytes { get; }

        public int AllocationId { get; }

        public int HashCodeOfBuffer { get; }
    }

    public struct ReturnRequest
    {
        public ReturnRequest(int allocationId, int hashCodeOfBuffer)
        {
            this.AllocationId = allocationId;
            this.HashCodeOfBuffer = hashCodeOfBuffer;
        }

        public int AllocationId { get; }

        public int HashCodeOfBuffer { get; }
    }

    /// <summary>
    /// Wraps an array as an <see cref="IManagedByteBuffer"/> instance.
    /// </summary>
    private class BasicArrayBuffer<T> : AllocationTrackedMemoryManager<T>
        where T : struct
    {
        private readonly TestMemoryAllocator allocator;
        private GCHandle pinHandle;

        public BasicArrayBuffer(T[] array, int length, TestMemoryAllocator allocator, int allocationId)
        {
            this.allocator = allocator;
            DebugGuard.MustBeLessThanOrEqualTo(length, array.Length, nameof(length));
            this.Array = array;
            this.Length = length;
            this.AllocationId = allocationId;
        }

        public BasicArrayBuffer(T[] array, TestMemoryAllocator allocator)
            : this(array, array.Length, allocator, 0)
        {
        }

        /// <summary>
        /// Gets the array.
        /// </summary>
        public T[] Array { get; }

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the stable identity recorded for this allocation.
        /// </summary>
        public int AllocationId { get; }

        /// <inheritdoc />
        public override Span<T> GetSpan() => this.Array.AsSpan(0, this.Length);

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            if (!this.pinHandle.IsAllocated)
            {
                this.pinHandle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
            }

            void* ptr = (void*)this.pinHandle.AddrOfPinnedObject();
            return new MemoryHandle(ptr, pinnable: this);
        }

        public override void Unpin()
        {
            this.pinHandle.Free();
        }

        /// <inheritdoc />
        protected override void DisposeCore(bool disposing)
        {
            if (disposing)
            {
                this.allocator.Return(this);
            }
        }
    }

    private class ManagedByteBuffer : BasicArrayBuffer<byte>, IMemoryOwner<byte>
    {
        public ManagedByteBuffer(byte[] array, TestMemoryAllocator allocator)
            : base(array, allocator)
        {
        }
    }
}
