// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        private readonly UnmanagedMemoryHandle[] buffers;
        private int index;

        public UniformUnmanagedMemoryPool(int bufferLength, int capacity)
        {
            this.Capacity = capacity;
            this.BufferLength = bufferLength;
            this.buffers = new UnmanagedMemoryHandle[capacity];
        }

        public int BufferLength { get; }

        public int Capacity { get; }

        public UnmanagedMemoryHandle Rent(AllocationOptions allocationOptions = AllocationOptions.None)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if we are over limit:
            if (this.index == buffersLocal.Length)
            {
                return null;
            }

            UnmanagedMemoryHandle array;

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.index < buffersLocal.Length)
                {
                    array = buffersLocal[this.index];
                    buffersLocal[this.index++] = null;
                }
                else
                {
                    return null;
                }
            }

            if (array == null)
            {
                array = new UnmanagedMemoryHandle(this.BufferLength);
            }
            else if (allocationOptions.Has(AllocationOptions.Clean))
            {
                this.GetSpan(array).Clear();
            }

            return array;
        }

        public UnmanagedMemoryHandle[] Rent(int bufferCount, AllocationOptions allocationOptions = AllocationOptions.None)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if we are over limit:
            if (this.index + bufferCount >= buffersLocal.Length + 1)
            {
                return null;
            }

            UnmanagedMemoryHandle[] result;
            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.index + bufferCount <= buffersLocal.Length)
                {
                    result = new UnmanagedMemoryHandle[bufferCount];
                    for (int i = 0; i < bufferCount; i++)
                    {
                        result[i] = buffersLocal[this.index];
                        buffersLocal[this.index++] = null;
                    }
                }
                else
                {
                    return null;
                }
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == null)
                {
                    result[i] = new UnmanagedMemoryHandle(this.BufferLength);
                }
                else if (allocationOptions.Has(AllocationOptions.Clean))
                {
                    this.GetSpan(result[i]).Clear();
                }
            }

            return result;
        }

        public void Return(UnmanagedMemoryHandle buffer)
        {
            lock (this.buffers)
            {
                if (this.index == 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }
                else
                {
                    this.buffers[--this.index] = buffer;
                }
            }
        }

        public void Return(Span<UnmanagedMemoryHandle> buffers)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            lock (buffersLocal)
            {
                if (this.index - buffers.Length + 1 <= 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }
                else
                {
                    for (int i = buffers.Length - 1; i >= 0; i--)
                    {
                        buffersLocal[--this.index] = buffers[i];
                    }
                }
            }
        }

        private unsafe Span<byte> GetSpan(UnmanagedMemoryHandle h) =>
            new Span<byte>((byte*)h.DangerousGetHandle(), this.BufferLength);

        // This indicates a bug in the library, however Return() might be called from a finalizer,
        // therefore we should never throw here in production.
        [Conditional("DEBUG")]
        private static void ThrowReturnedMoreArraysThanRented() =>
            throw new InvalidMemoryOperationException("Returned more arrays then rented");
    }
}
