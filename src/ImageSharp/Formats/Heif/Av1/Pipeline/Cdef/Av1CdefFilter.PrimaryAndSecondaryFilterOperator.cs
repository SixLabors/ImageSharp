// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Enables both directional tap groups and their combined clipping rule.
    /// </summary>
    private readonly struct PrimaryAndSecondaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => true;

        /// <inheritdoc/>
        public static bool EnableSecondary => true;
    }
}
