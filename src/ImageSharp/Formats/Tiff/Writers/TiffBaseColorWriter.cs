// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal abstract class TiffBaseColorWriter<TPixel> : IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private bool isDisposed;

        protected TiffBaseColorWriter(ImageFrame<TPixel> image, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
        {
            this.Image = image;
            this.MemoryAllocator = memoryAllocator;
            this.Configuration = configuration;
            this.EntriesCollector = entriesCollector;

            this.BytesPerRow = ((image.Width * this.BitsPerPixel) + 7) / 8;
        }

        public abstract int BitsPerPixel { get; }

        public int BytesPerRow { get; }

        protected ImageFrame<TPixel> Image { get; }

        protected MemoryAllocator MemoryAllocator { get; }

        protected Configuration Configuration { get; }

        protected TiffEncoderEntriesCollector EntriesCollector { get; }

        public virtual void Write(TiffBaseCompressor compressor, int rowsPerStrip)
        {
            DebugGuard.IsTrue(this.BytesPerRow == compressor.BytesPerRow || compressor.BytesPerRow == 0, "Values must be equals");
            int stripsCount = (this.Image.Height + rowsPerStrip - 1) / rowsPerStrip;

            uint[] stripOffsets = new uint[stripsCount];
            uint[] stripByteCounts = new uint[stripsCount];

            int stripIndex = 0;
            compressor.Initialize(rowsPerStrip);
            for (int y = 0; y < this.Image.Height; y += rowsPerStrip)
            {
                long offset = compressor.Output.Position;

                int height = Math.Min(rowsPerStrip, this.Image.Height - y);
                this.EncodeStrip(y, height, compressor);

                long endOffset = compressor.Output.Position;
                stripOffsets[stripIndex] = (uint)offset;
                stripByteCounts[stripIndex] = (uint)(endOffset - offset);
                stripIndex++;
            }

            DebugGuard.IsTrue(stripIndex == stripsCount, "Values must be equals");
            this.AddStripTags(rowsPerStrip, stripOffsets, stripByteCounts);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.Dispose(true);
        }

        protected static Span<T> GetStripPixels<T>(Buffer2D<T> buffer2D, int y, int height)
            where T : struct
            => buffer2D.GetSingleSpan().Slice(y * buffer2D.Width, height * buffer2D.Width);

        protected abstract void EncodeStrip(int y, int height, TiffBaseCompressor compressor);

        /// <summary>
        /// Adds image format information to the specified IFD.
        /// </summary>
        /// <param name="rowsPerStrip">The rows per strip.</param>
        /// <param name="stripOffsets">The strip offsets.</param>
        /// <param name="stripByteCounts">The strip byte counts.</param>
        private void AddStripTags(int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts)
        {
            this.EntriesCollector.Add(new ExifLong(ExifTagValue.RowsPerStrip)
            {
                Value = (uint)rowsPerStrip
            });

            this.EntriesCollector.Add(new ExifLongArray(ExifTagValue.StripOffsets)
            {
                Value = stripOffsets
            });

            this.EntriesCollector.Add(new ExifLongArray(ExifTagValue.StripByteCounts)
            {
                Value = stripByteCounts
            });
        }

        protected abstract void Dispose(bool disposing);
    }
}
