// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory
{
    internal class TestMemoryAllocator : MemoryAllocator
    {
        private readonly List<AllocationRequest> allocationLog = new List<AllocationRequest>();
        private readonly List<ReturnRequest> returnLog = new List<ReturnRequest>();

        public TestMemoryAllocator(byte dirtyValue = 42)
        {
            this.DirtyValue = dirtyValue;
        }

        /// <summary>
        /// Gets the value to initialize the result buffer with, with non-clean options (<see cref="AllocationOptions.None"/>)
        /// </summary>
        public byte DirtyValue { get; }

        public int BufferCapacityInBytes { get; set; } = int.MaxValue;

        public IReadOnlyList<AllocationRequest> AllocationLog => this.allocationLog;

        public IReadOnlyList<ReturnRequest> ReturnLog => this.returnLog;

        protected internal override int GetBufferCapacityInBytes() => this.BufferCapacityInBytes;

        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            T[] array = this.AllocateArray<T>(length, options);
            return new BasicArrayBuffer<T>(array, length, this);
        }

        public override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
        {
            byte[] array = this.AllocateArray<byte>(length, options);
            return new ManagedByteBuffer(array, this);
        }

        private T[] AllocateArray<T>(int length, AllocationOptions options)
            where T : struct
        {
            var array = new T[length + 42];
            this.allocationLog.Add(AllocationRequest.Create<T>(options, length, array));

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
            this.returnLog.Add(new ReturnRequest(buffer.Array.GetHashCode()));
        }

        public struct AllocationRequest
        {
            private AllocationRequest(Type elementType, AllocationOptions allocationOptions, int length, int lengthInBytes, int hashCodeOfBuffer)
            {
                this.ElementType = elementType;
                this.AllocationOptions = allocationOptions;
                this.Length = length;
                this.LengthInBytes = lengthInBytes;
                this.HashCodeOfBuffer = hashCodeOfBuffer;

                if (elementType == typeof(Vector4))
                {
                }
            }

            public static AllocationRequest Create<T>(AllocationOptions allocationOptions, int length, T[] buffer)
            {
                Type type = typeof(T);
                int elementSize = Marshal.SizeOf(type);
                return new AllocationRequest(type, allocationOptions, length, length * elementSize, buffer.GetHashCode());
            }

            public Type ElementType { get; }

            public AllocationOptions AllocationOptions { get; }

            public int Length { get; }

            public int LengthInBytes { get; }

            public int HashCodeOfBuffer { get; }
        }

        public struct ReturnRequest
        {
            public ReturnRequest(int hashCodeOfBuffer)
            {
                this.HashCodeOfBuffer = hashCodeOfBuffer;
            }

            public int HashCodeOfBuffer { get; }
        }

        /// <summary>
        /// Wraps an array as an <see cref="IManagedByteBuffer"/> instance.
        /// </summary>
        private class BasicArrayBuffer<T> : MemoryManager<T>
            where T : struct
        {
            private readonly TestMemoryAllocator allocator;
            private GCHandle pinHandle;

            public BasicArrayBuffer(T[] array, int length, TestMemoryAllocator allocator)
            {
                this.allocator = allocator;
                DebugGuard.MustBeLessThanOrEqualTo(length, array.Length, nameof(length));
                this.Array = array;
                this.Length = length;
            }

            public BasicArrayBuffer(T[] array, TestMemoryAllocator allocator)
                : this(array, array.Length, allocator)
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

            /// <inheritdoc />
            public override Span<T> GetSpan() => this.Array.AsSpan(0, this.Length);

            public override unsafe MemoryHandle Pin(int elementIndex = 0)
            {
                if (!this.pinHandle.IsAllocated)
                {
                    this.pinHandle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
                }

                void* ptr = (void*)this.pinHandle.AddrOfPinnedObject();
                return new MemoryHandle(ptr, this.pinHandle);
            }

            public override void Unpin()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.allocator.Return(this);
                }
            }
        }

        private class ManagedByteBuffer : BasicArrayBuffer<byte>, IManagedByteBuffer
        {
            public ManagedByteBuffer(byte[] array, TestMemoryAllocator allocator)
                : base(array, allocator)
            {
            }
        }
    }
}
