// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Provides helper methods for working with <see cref="AllocationOptions"/>.
/// </summary>
internal static class AllocationOptionsExtensions
{
    /// <summary>
    /// Returns a value indicating whether the specified flag is set on the allocation options.
    /// </summary>
    /// <param name="options">The allocation options to inspect.</param>
    /// <param name="flag">The flag to test for.</param>
    /// <returns><see langword="true"/> if <paramref name="flag"/> is set; otherwise, <see langword="false"/>.</returns>
    public static bool Has(this AllocationOptions options, AllocationOptions flag)
        => (options & flag) == flag;
}
