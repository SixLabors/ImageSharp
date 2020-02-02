// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Memory
{
    internal static class MemoryGroupExtensions
    {
        public static void CopyTo<T>(this IMemoryGroup<T> source, IMemoryGroup<T> target)
            where T : struct
        {
            throw new NotImplementedException();
        }
    }
}
