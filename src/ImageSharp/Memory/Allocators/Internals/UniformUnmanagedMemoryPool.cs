// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformUnmanagedMemoryPool
    {
        private UnmanagedMemoryHandle[] buffers;
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

            // Avoid taking the lock if the pool is released or is over limit:
            if (buffersLocal == null || this.index == buffersLocal.Length)
            {
                return null;
            }

            UnmanagedMemoryHandle array;

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null || this.index == buffersLocal.Length)
                {
                    return null;
                }

                array = buffersLocal[this.index];
                buffersLocal[this.index++] = null;
            }

            if (array == null)
            {
                array = new UnmanagedMemoryHandle(this.BufferLength);
            }

            if (allocationOptions.Has(AllocationOptions.Clean))
            {
                this.GetSpan(array).Clear();
            }

            return array;
        }

        public UnmanagedMemoryHandle[] Rent(int bufferCount, AllocationOptions allocationOptions = AllocationOptions.None)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;

            // Avoid taking the lock if the pool is released or is over limit:
            if (buffersLocal == null || this.index + bufferCount >= buffersLocal.Length + 1)
            {
                return null;
            }

            UnmanagedMemoryHandle[] result;
            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null || this.index + bufferCount >= buffersLocal.Length + 1)
                {
                    return null;
                }

                result = new UnmanagedMemoryHandle[bufferCount];
                for (int i = 0; i < bufferCount; i++)
                {
                    result[i] = buffersLocal[this.index];
                    buffersLocal[this.index++] = null;
                }
            }

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == null)
                {
                    result[i] = new UnmanagedMemoryHandle(this.BufferLength);
                }

                if (allocationOptions.Has(AllocationOptions.Clean))
                {
                    this.GetSpan(result[i]).Clear();
                }
            }

            return result;
        }

        public void Return(UnmanagedMemoryHandle buffer)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            if (buffersLocal == null)
            {
                buffer.Dispose();
                return;
            }

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null)
                {
                    buffer.Dispose();
                    return;
                }

                if (this.index == 0)
                {
                    ThrowReturnedMoreArraysThanRented(); // DEBUG-only exception
                    buffer.Dispose();
                    return;
                }

                this.buffers[--this.index] = buffer;
            }
        }

        public void Return(Span<UnmanagedMemoryHandle> buffers)
        {
            UnmanagedMemoryHandle[] buffersLocal = this.buffers;
            if (buffersLocal == null)
            {
                DisposeAll(buffers);
                return;
            }

            lock (buffersLocal)
            {
                // Check again after taking the lock:
                if (this.buffers == null)
                {
                    DisposeAll(buffers);
                    return;
                }

                if (this.index - buffers.Length + 1 <= 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                    DisposeAll(buffers);
                    return;
                }

                for (int i = buffers.Length - 1; i >= 0; i--)
                {
                    buffersLocal[--this.index] = buffers[i];
                }
            }
        }

        public void Release()
        {
            UnmanagedMemoryHandle[] oldBuffers = Interlocked.Exchange(ref this.buffers, null);
            DebugGuard.NotNull(oldBuffers, nameof(oldBuffers));
            DisposeAll(oldBuffers);
        }

        private static void DisposeAll(Span<UnmanagedMemoryHandle> buffers)
        {
            foreach (UnmanagedMemoryHandle handle in buffers)
            {
                handle?.Dispose();
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
