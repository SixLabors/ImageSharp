// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal class UniformByteArrayPool
    {
        private readonly int arrayLength;
        private readonly byte[][] arrays;
        private int index;

        public UniformByteArrayPool(int arrayLength, int capacity)
        {
            this.arrayLength = arrayLength;
            this.arrays = new byte[capacity][];
        }

        public byte[] Rent()
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
                array = new byte[this.arrayLength];
            }

            return array;
        }

        public byte[][] Rent(int arrayCount)
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
                    result[i] = new byte[this.arrayLength];
                }
            }

            return result;
        }

        public void Return(byte[] array)
        {
            Guard.IsTrue(array.Length == this.arrayLength, nameof(array), "Incorrect array length, array not rented from pool.");

            lock (this.arrays)
            {
                if (this.index == 0)
                {
                    ThrowReturnedMoreArraysThanRented();
                }

                this.arrays[--this.index] = array;
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

                for (int i = arrays.Length - 1; i >= 0; i--)
                {
                    byte[] array = arrays[i];
                    Guard.IsTrue(array.Length == this.arrayLength, nameof(arrays), "Incorrect array length, array not rented from pool.");
                    arraysLocal[--this.index] = arrays[i];
                }
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static void ThrowReturnedMoreArraysThanRented() => throw new InvalidOperationException("Returned more arrays then rented");
    }
}
