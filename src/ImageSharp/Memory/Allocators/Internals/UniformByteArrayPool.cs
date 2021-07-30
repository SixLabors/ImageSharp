// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Custom array pool that holds GC arrays of uniform size.
    /// </summary>
    internal partial class UniformByteArrayPool
    {
        // Be more strict about high pressure threshold than ArrayPool<T>.Shared.
        // A 32 bit process can OOM way before reaching HighMemoryLoadThresholdBytes:
        private const float HighPressureThresholdRate = 0.5f;

        // Trim half of the pool on each Gen 2 GC:
        internal const float DefaultTrimRate = 0.5f;

        private readonly byte[][] arrays;
        private int index;

        // The trimRate is configurable for testability
        private readonly float trimRate;

        public UniformByteArrayPool(
            int arrayLength,
            int capacity,
            float trimRate = DefaultTrimRate)
        {
            this.ArrayLength = arrayLength;
            this.arrays = new byte[capacity][];
            this.trimRate = trimRate;

#if NETCORE31COMPATIBLE
            Gen2GcCallback.Register(s => ((UniformByteArrayPool)s).Trim(), this);
#endif
        }

        public int ArrayLength { get; }

        public int Capacity => this.arrays.Length;

        public byte[] Rent(AllocationOptions allocationOptions = AllocationOptions.None)
        {
            byte[][] arraysLocal = this.arrays;

            // Avoid taking the lock if we are over limit:
            if (this.index == arraysLocal.Length)
            {
                return null;
            }

            byte[] array;

            lock (arraysLocal)
            {
                // Check again after taking the lock:
                if (this.index < arraysLocal.Length)
                {
                    array = arraysLocal[this.index];
                    arraysLocal[this.index++] = null;
                }
                else
                {
                    return null;
                }
            }

            if (array == null)
            {
                array = new byte[this.ArrayLength];
            }
            else if (allocationOptions.Has(AllocationOptions.Clean))
            {
                array.AsSpan().Clear();
            }

            return array;
        }

        public byte[][] Rent(int arrayCount, AllocationOptions allocationOptions = AllocationOptions.None)
        {
            byte[][] arraysLocal = this.arrays;

            // Avoid taking the lock if we are over limit:
            if (this.index + arrayCount >= arraysLocal.Length + 1)
            {
                return null;
            }

            byte[][] result;
            lock (arraysLocal)
            {
                // Check again after taking the lock:
                if (this.index + arrayCount <= arraysLocal.Length)
                {
                    result = new byte[arrayCount][];
                    for (int i = 0; i < arrayCount; i++)
                    {
                        result[i] = arraysLocal[this.index];
                        arraysLocal[this.index++] = null;
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
                    result[i] = new byte[this.ArrayLength];
                }
                else if (allocationOptions.Has(AllocationOptions.Clean))
                {
                    result[i].AsSpan().Clear();
                }
            }

            return result;
        }

        public void Return(byte[] array)
        {
            Guard.IsTrue(array.Length == this.ArrayLength, nameof(array), "Incorrect array length, array not rented from pool.");

            lock (this.arrays)
            {
                if (this.index == 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }
                else
                {
                    this.arrays[--this.index] = array;
                }
            }
        }

        public void Return(Span<byte[]> arrays)
        {
            byte[][] arraysLocal = this.arrays;

            lock (arraysLocal)
            {
                if (this.index - arrays.Length + 1 <= 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }
                else
                {
                    for (int i = arrays.Length - 1; i >= 0; i--)
                    {
                        byte[] array = arrays[i];
                        Guard.IsTrue(array.Length == this.ArrayLength, nameof(arrays), "Incorrect array length, array not rented from pool.");
                        arraysLocal[--this.index] = arrays[i];
                    }
                }
            }
        }

        // This indicates a bug in the library, however Return() might be called from a finalizer,
        // therefore we should never throw here in production.
        [Conditional("DEBUG")]
        private static void ThrowReturnedMoreArraysThanRented() =>
            throw new InvalidMemoryOperationException("Returned more arrays then rented");

#if NETCORE31COMPATIBLE
        private bool Trim()
        {
            byte[][] arraysLocal = this.arrays;
            bool isHighPressure = this.IsHighMemoryPressure();

            lock (arraysLocal)
            {
                // Check again after taking the lock:
                if (this.arrays == null)
                {
                    return false;
                }

                if (isHighPressure)
                {
                    // Trim all:
                    for (int i = this.index; i < arraysLocal.Length && arraysLocal[i] != null; i++)
                    {
                        arraysLocal[i] = null;
                    }
                }
                else
                {
                    // Count the arrays in the pool:
                    int retainedCount = 0;
                    for (int i = this.index; i < arraysLocal.Length && arraysLocal[i] != null; i++)
                    {
                        retainedCount++;
                    }

                    // Trim 'trimRate' of 'retainedCount':
                    int trimCount = (int)Math.Ceiling(retainedCount * this.trimRate);
                    int trimStart = this.index + retainedCount - 1;
                    int trimStop = this.index + retainedCount - trimCount;
                    for (int i = trimStart; i >= trimStop; i--)
                    {
                        arraysLocal[i] = null;
                    }
                }
            }

            return true;
        }

        private bool IsHighMemoryPressure()
        {
            GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
            return memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * HighPressureThresholdRate;
        }
#endif
    }
}
