// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;

namespace SixLabors.ImageSharp.Memory.Internals
{
    /// <summary>
    /// Since <see cref="WeakReference{T}"/> is finalizable and finalizer order is non-deterministic,
    /// the runtime is free to call finalizer on a <see cref="WeakReference{T}"/>
    /// before the finalizer of the object owning it. To prevent this, we keep the <see cref="WeakReference{T}"/>-s
    /// alive in a global registry.
    /// </summary>
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
