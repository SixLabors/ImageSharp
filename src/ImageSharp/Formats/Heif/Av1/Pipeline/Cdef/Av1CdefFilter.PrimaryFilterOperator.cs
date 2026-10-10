// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Enables only the primary directional taps.
    /// </summary>
    private readonly struct PrimaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => true;

        /// <inheritdoc/>
        public static bool EnableSecondary => false;
    }
}
