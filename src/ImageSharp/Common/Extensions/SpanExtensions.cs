// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides partial selection for mutable spans.
/// </summary>
internal static class SpanExtensions
{
    /// <summary>
    /// Partitions a nonempty span and returns the value at the requested position in sorted order.
    /// </summary>
    /// <typeparam name="T">The comparable sample type.</typeparam>
    /// <param name="span">The workspace to partition. Its sample values are retained but their order can change.</param>
    /// <param name="n">The zero-based position, within the supplied span.</param>
    /// <returns>The selected value. Samples before and after it are partitioned rather than fully sorted.</returns>
    public static T NthElement<T>(this Span<T> span, int n)
        where T : IComparable<T>
    {
        int left = 0;
        int right = span.Length - 1;

        while (left < right)
        {
            // Repeated values share one partition. Partition into values less than, equal to,
            // and greater than the pivot. Selecting within the equal range finishes without rescanning each equal sample.
            T pivot = span[left + ((right - left) / 2)];
            int lower = left;
            int current = left;
            int upper = right;

            while (current <= upper)
            {
                int comparison = span[current].CompareTo(pivot);
                if (comparison < 0)
                {
                    (span[lower], span[current]) = (span[current], span[lower]);
                    lower++;
                    current++;
                }
                else if (comparison > 0)
                {
                    // The sample moved from the upper end has not been classified. Examine it before advancing current.
                    (span[current], span[upper]) = (span[upper], span[current]);
                    upper--;
                }
                else
                {
                    current++;
                }
            }

            // Only the partition containing n can affect the result. The equal partition already occupies
            // its final range of sorted positions, even though values on either side are not fully sorted.
            if (n < lower)
            {
                right = lower - 1;
            }
            else if (n > upper)
            {
                left = upper + 1;
            }
            else
            {
                return span[n];
            }
        }

        return span[left];
    }
}
