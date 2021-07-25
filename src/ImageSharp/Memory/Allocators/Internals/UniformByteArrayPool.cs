// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal partial class UniformByteArrayPool
    {
        private byte[][] arrays;
        private int index;

        public UniformByteArrayPool(int arrayLength, int capacity)
        {
            this.ArrayLength = arrayLength;
            this.arrays = new byte[capacity][];
        }

        public int ArrayLength { get; }

        public int Capacity => this.arrays.Length;

        public byte[] Rent(AllocationOptions allocationOptions = AllocationOptions.None)
        {
            byte[][] arraysLocal = this.arrays;

            if (arraysLocal == null)
            {
                ThrowReleased();
            }

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

            if (arraysLocal == null)
            {
                ThrowReleased();
            }

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

            byte[][] arraysLocal = this.arrays;
            if (arraysLocal == null)
            {
                // The pool has been released, Return() is NOP
                return;
            }

            lock (arraysLocal)
            {
                if (this.index == 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }

                arraysLocal[--this.index] = array;
            }
        }

        public void Return(Span<byte[]> arrays)
        {
            byte[][] arraysLocal = this.arrays;

            if (arraysLocal == null)
            {
                // The pool has been released, Return() is NOP
                return;
            }

            lock (arraysLocal)
            {
                if (this.index - arrays.Length + 1 <= 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }

                for (int i = arrays.Length - 1; i >= 0; i--)
                {
                    byte[] array = arrays[i];
                    Guard.IsTrue(array.Length == this.ArrayLength, nameof(arrays), "Incorrect array length, array not rented from pool.");
                    arraysLocal[--this.index] = arrays[i];
                }
            }
        }

        public void Release()
        {
            lock (this.arrays)
            {
                this.arrays = null;
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ThrowReturnedMoreArraysThanRented() => throw new InvalidMemoryOperationException("Returned more arrays then rented");

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ThrowReleased() => throw new InvalidMemoryOperationException("UniformByteArrayPool has been released, can not rent anyomre.");
    }
}
