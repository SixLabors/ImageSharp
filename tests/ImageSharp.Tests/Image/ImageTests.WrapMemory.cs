// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Shapes;
using SixLabors.ImageSharp.Processing;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class WrapMemory
        {
            class BitmapMemoryManager : MemoryManager<Bgra32>
            {
                private System.Drawing.Bitmap bitmap;

                private BitmapData bmpData;

                private int length;

                public BitmapMemoryManager(Bitmap bitmap)
                {
                    if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                    {
                        throw new ArgumentException("bitmap.PixelFormat != PixelFormat.Format32bppArgb", nameof(bitmap));
                    }

                    this.bitmap = bitmap;
                    var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    this.bmpData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    this.length = bitmap.Width * bitmap.Height;
                }

                protected override void Dispose(bool disposing)
                {
                    this.bitmap.UnlockBits(this.bmpData);
                }

                public override unsafe Span<Bgra32> GetSpan()
                {
                    void* ptr = (void*) this.bmpData.Scan0;
                    return new Span<Bgra32>(ptr, this.length);
                }

                public override unsafe MemoryHandle Pin(int elementIndex = 0)
                {
                    void* ptr = (void*)this.bmpData.Scan0;
                    return new MemoryHandle(ptr);
                }

                public override void Unpin()
                {
                }
            }

            [Fact]
            public void WrapSystemDrawingBitmap()
            {
                using (var bmp = new Bitmap(51, 23))
                {
                    using (var memoryManager = new BitmapMemoryManager(bmp))
                    {
                        Memory<Bgra32> memory = memoryManager.Memory;
                        Bgra32 bg = NamedColors<Bgra32>.Red;
                        Bgra32 fg = NamedColors<Bgra32>.Green;

                        using (var image = Image.WrapMemory(memory, bmp.Width, bmp.Height))
                        {
                            image.Mutate(c => c.Fill(bg).Fill(fg, new RectangularPolygon(10, 10, 10, 10)));
                        }
                    }

                    string fn = System.IO.Path.Combine(
                        TestEnvironment.ActualOutputDirectoryFullPath,
                        "WrapSystemDrawingBitmap.bmp");

                    bmp.Save(fn, ImageFormat.Bmp);
                }
            }
        }
    }
}