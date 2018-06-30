// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Utils
{
    /// <summary>
    /// Optimized quick sort implementation for Span{float} input
    /// </summary>
    internal class QuickSort
    {
        /// <summary>
        /// Sorts the elements of <paramref name="data"/> in ascending order
        /// </summary>
        /// <param name="data">The items to sort</param>
        public static void Sort(Span<float> data)
        {
            if (data.Length < 2)
            {
                return;
            }

            if (data.Length == 2)
            {
                if (data[0] > data[1])
                {
                    Swap(ref data[0], ref data[1]);
                }

                return;
            }

            Sort(ref data[0], 0, data.Length - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref float left, ref float right)
        {
            float tmp = left;
            left = right;
            right = tmp;
        }

        private static void Sort(ref float data0, int lo, int hi)
        {
            if (lo < hi)
            {
                int p = Partition(ref data0, lo, hi);
                Sort(ref data0, lo, p);
                Sort(ref data0, p + 1, hi);
            }
        }

        private static int Partition(ref float data0, int lo, int hi)
        {
            float pivot = Unsafe.Add(ref data0, lo);
            int i = lo - 1;
            int j = hi + 1;
            while (true)
            {
                do
                {
                    i = i + 1;
                }
                while (Unsafe.Add(ref data0, i) < pivot && i < hi);

                do
                {
                    j = j - 1;
                }
                while (Unsafe.Add(ref data0, j) > pivot && j > lo);

                if (i >= j)
                {
                    return j;
                }

                Swap(ref Unsafe.Add(ref data0, i), ref Unsafe.Add(ref data0, j));
            }
        }
    }
}
