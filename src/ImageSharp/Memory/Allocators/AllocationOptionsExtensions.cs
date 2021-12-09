// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Memory
{
    internal static class AllocationOptionsExtensions
    {
        public static bool Has(this AllocationOptions options, AllocationOptions flag) => (options & flag) == flag;
    }
}
