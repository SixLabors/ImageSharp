// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Memory
{
    internal static class AllocationOptionsExtensions
    {
        public static bool Has(this AllocationOptions options, AllocationOptions flag) => (options & flag) == flag;
    }
}
