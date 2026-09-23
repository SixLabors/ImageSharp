// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

internal static class JxlSpanHelper
{
    public static T NthElement<T>(this Span<T> span, int n)
        where T : IComparable<T>
    {
        int left = 0;
        int right = span.Length - 1;

        while (true)
        {
            int pivotIndex = Partition(span, left, right);
            if (pivotIndex == n)
            {
                return span[pivotIndex];
            }
            else if (n < pivotIndex)
            {
                right = pivotIndex - 1;
            }
            else
            {
                left = pivotIndex + 1;
            }
        }
    }

    private static int Partition<T>(Span<T> span, int left, int right)
        where T : IComparable<T>
    {
        T pivot = span[right];
        int storeIndex = left;

        for (int i = left; i < right; i++)
        {
            if (span[i].CompareTo(pivot) < 0)
            {
                (span[i], span[storeIndex]) = (span[storeIndex], span[i]);
                storeIndex++;
            }
        }

        (span[storeIndex], span[right]) = (span[right], span[storeIndex]);
        return storeIndex;
    }
}
