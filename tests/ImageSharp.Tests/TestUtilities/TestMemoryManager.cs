// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.ImageSharp.Tests
{
    public class TestMemoryManager<T> : MemoryManager<T>
        where T : struct
    {
        public TestMemoryManager(T[] pixelArray)
        {
            this.PixelArray = pixelArray;
        }

        public T[] PixelArray { get; private set; }

        public bool IsDisposed { get; private set; }

        public override Span<T> GetSpan()
        {
            return this.PixelArray;
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            throw new NotImplementedException();
        }

        public override void Unpin()
        {
            throw new NotImplementedException();
        }

        public static TestMemoryManager<T> CreateAsCopyOf(Span<T> copyThisBuffer)
        {
            var pixelArray = new T[copyThisBuffer.Length];
            copyThisBuffer.CopyTo(pixelArray);
            return new TestMemoryManager<T>(pixelArray);
        }

        protected override void Dispose(bool disposing)
        {
            this.IsDisposed = true;
            this.PixelArray = null;
        }
    }
}
