// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Enables only the secondary off-axis taps.
    /// </summary>
    private readonly struct SecondaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => false;

        /// <inheritdoc/>
        public static bool EnableSecondary => true;
    }
}
