// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;

namespace SixLabors.ImageSharp.Memory.Internals
{
    internal static class WeakReferenceTracker
    {
        private static readonly ConcurrentDictionary<WeakReference<UniformByteArrayPool>, int> AllWeakReferences =
            new ConcurrentDictionary<WeakReference<UniformByteArrayPool>, int>();

        public static void Add(WeakReference<UniformByteArrayPool> weakReference) =>
            AllWeakReferences.TryAdd(weakReference, 0);

        public static void Remove(WeakReference<UniformByteArrayPool> weakReference) =>
            AllWeakReferences.TryRemove(weakReference, out _);
    }
}
