// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Disables both tap groups so the source block is copied unchanged.
    /// </summary>
    private readonly struct CopyFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => false;

        /// <inheritdoc/>
        public static bool EnableSecondary => false;
    }
}
