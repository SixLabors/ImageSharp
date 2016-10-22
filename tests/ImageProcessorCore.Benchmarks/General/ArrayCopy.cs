// <copyright file="ArrayCopy.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Benchmarks.General
{
    using System;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    public class ArrayCopy
    {
        [Params(100, 1000, 10000)]
        public int Count { get; set; }

        byte[] source, destination;

        [Setup]
        public void SetUp()
        {
            source = new byte[Count];
            destination = new byte[Count];
        }

        [Benchmark(Baseline = true, Description = "Copy using Array.Copy()")]
        public void CopyArray()
        {
            Array.Copy(source, destination, Count);
        }

        [Benchmark(Description = "Copy using Unsafe<T>")]
        public unsafe void CopyUnsafe()
        {
            fixed (byte* pinnedDestination = destination)
            fixed (byte* pinnedSource = source)
            {
                Unsafe.CopyBlock(pinnedSource, pinnedDestination, (uint)Count);
            }
        }

        [Benchmark(Description = "Copy using Buffer.MemoryCopy<T>")]
        public unsafe void CopyUsingBufferMemoryCopy()
        {
            fixed (byte* pinnedDestination = destination)
            fixed (byte* pinnedSource = source)
            {
                Buffer.MemoryCopy(pinnedSource, pinnedDestination, Count, Count);
            }
        }
    }
}
