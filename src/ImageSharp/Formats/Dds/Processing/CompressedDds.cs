using System;
using System.Collections.Generic;
using Pfim;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Dds.Extensions;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Dds.Processing
{
    /// <summary>
    /// Class representing decoding compressed direct draw surfaces
    /// </summary>
    internal abstract class CompressedDds : Dds
    {
        private MipMapOffset[] mipMaps = new MipMapOffset[0];

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressedDds"/> class.
        /// </summary>
        /// <param name="ddsHeader"><see cref="DdsHeader" /></param>
        /// <param name="ddsHeaderDxt10"><see cref="DdsHeaderDxt10" /></param>
        protected CompressedDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
         : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        /// <summary>
        /// Gets the mip map offset data.
        /// </summary>
        public override MipMapOffset[] MipMaps => this.mipMaps;

        /// <summary>
        /// Gets number of bytes for a pixel in the decoded data.
        /// </summary>
        protected abstract byte PixelDepthBytes { get; }

        /// <summary>
        /// Gets the length of a block is in pixels. This mainly affects compressed
        /// images as they are encoded in blocks that are divSize by divSize.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        protected abstract byte DivSize { get; }

        /// <summary>
        /// Gets number of bytes needed to decode block of pixels that is divSize
        /// by divSize.  This takes into account how many bytes it takes to
        /// extract color and alpha information. Uncompressed DDS do not need
        /// this value.
        /// </summary>
        protected abstract byte CompressedBytesPerBlock { get; }

        /// <summary>
        /// Decompress a given block.
        /// </summary>
        protected abstract int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride);


        /// <inheritdoc/>
        public override int Stride => this.DeflatedStrideBytes;

        private int BytesPerStride => this.WidthBlocks * this.CompressedBytesPerBlock;

        private int WidthBlocks => this.CalcBlocks((int)this.DdsHeader.Width);

        private int HeightBlocks => this.CalcBlocks((int)this.DdsHeader.Height);

        private int StridePixels => this.WidthBlocks * this.DivSize;

        private int DeflatedStrideBytes => this.StridePixels * this.PixelDepthBytes;

        private int CalcBlocks(int pixels) => Math.Max(1, (pixels + 3) / 4);

        /// <summary>
        /// Creates data buffer for mip maps.
        /// </summary>
        /// <returns>The allocated size.</returns>
        private int AllocateMipMaps()
        {
            int len = this.HeightBlocks * this.DivSize * this.DeflatedStrideBytes;

            if (this.DdsHeader.TextureCount() <= 1)
            {
                int width = (int)this.DdsHeader.Width;
                int height = (int)this.DdsHeader.Height;
                int widthBlocks = this.CalcBlocks(width);
                int heightBlocks = this.CalcBlocks(height);

                int stridePixels = widthBlocks * this.DivSize;
                int stride = stridePixels * this.PixelDepthBytes;

                this.mipMaps = new[] { new MipMapOffset(width, height, stride, 0, len) };
                return len;
            }

            this.mipMaps = new MipMapOffset[this.DdsHeader.TextureCount() - 1];
            int totalLen = len;

            for (int i = 1; i < this.DdsHeader.TextureCount(); i++)
            {
                int width = (int)(this.DdsHeader.Width / Math.Pow(2, i));
                int height = (int)(this.DdsHeader.Height / Math.Pow(2, i));
                int widthBlocks = this.CalcBlocks(width);
                int heightBlocks = this.CalcBlocks(height);

                int stridePixels = widthBlocks * this.DivSize;
                int stride = stridePixels * this.PixelDepthBytes;

                len = heightBlocks * this.DivSize * stride;
                this.mipMaps[i - 1] = new MipMapOffset(width, height, stride, totalLen, len);
                totalLen += len;
            }

            return totalLen;
        }

        public static int Translate(Stream str, byte[] buf, int bufLen, int bufIndex)
        {
            Buffer.BlockCopy(buf, bufIndex, buf, 0, bufLen - bufIndex);
            int result = str.Read(buf, bufLen - bufIndex, bufIndex);
            return result + bufLen - bufIndex;
        }

        /// <summary>
        /// Decompresses encoded data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="array">encoded data buffer</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        protected override Image[] Decode(Stream stream)
        {
            var totalLen = AllocateMipMaps();
            byte[] data = new byte[totalLen];
            var pixelsLeft = totalLen;
            int dataIndex = 0;

            int imageIndex = 0;
            int divSize = DivSize;
            int stride = DeflatedStrideBytes;
            int blocksPerStride = WidthBlocks;
            int indexPixelsLeft = HeightBlocks * DivSize * stride;
            var stridePixels = StridePixels;
            int bytesPerStride = BytesPerStride;

            int bufferSize;
            byte[] streamBuffer = new byte[0x8000];

            do
            {
                int workingSize;
                bufferSize = workingSize = stream.Read(streamBuffer, 0, 0x8000);
                int bIndex = 0;
                while (workingSize > 0 && indexPixelsLeft > 0)
                {
                    // If there is not enough of the buffer to fill the next
                    // set of 16 square pixels Get the next buffer
                    if (workingSize < bytesPerStride)
                    {
                        bufferSize = workingSize = Translate(stream, streamBuffer, 0x8000, bIndex);
                        bIndex = 0;
                    }

                    var origDataIndex = dataIndex;

                    // Now that we have enough pixels to fill a stride (and
                    // this includes the normally 4 pixels below the stride)
                    for (uint i = 0; i < blocksPerStride; i++)
                    {
                        bIndex = Decode(streamBuffer, data, bIndex, dataIndex, stridePixels);

                        // Advance to the next block, which is (pixel depth *
                        // divSize) bytes away
                        dataIndex += divSize * PixelDepthBytes;
                    }

                    // Each decoded block is divSize by divSize so pixels left
                    // is Width * multiplied by block height
                    workingSize -= bytesPerStride;

                    var filled = stride * divSize;
                    pixelsLeft -= filled;
                    indexPixelsLeft -= filled;

                    // Jump down to the block that is exactly (divSize - 1)
                    // below the current row we are on
                    dataIndex = origDataIndex + filled;

                    if (indexPixelsLeft <= 0 && imageIndex < MipMaps.Length)
                    {
                        var mip = MipMaps[imageIndex];
                        var widthBlocks = CalcBlocks(mip.Width);
                        var heightBlocks = CalcBlocks(mip.Height);
                        stridePixels = widthBlocks * DivSize;
                        stride = stridePixels * PixelDepthBytes;
                        blocksPerStride = widthBlocks;
                        indexPixelsLeft = heightBlocks * DivSize * stride;
                        bytesPerStride = widthBlocks * CompressedBytesPerBlock;
                        imageIndex++;
                    }
                }
            } while (bufferSize != 0 && pixelsLeft > 0);

            var images = new List<Image>();
            foreach (MipMapOffset mipmap in this.mipMaps)
            {
                Span<byte> mipdata = data.AsSpan().Slice(mipmap.DataOffset, mipmap.DataLen);
                if (this.Format == ImageFormat.R5g5b5)
                {
                    // Turn the alpha channel on
                    for (int i = 1; i < mipdata.Length; i += 2)
                    {
                        mipdata[i] |= 128;
                    }

                    images.Add(Image.LoadPixelData<Bgra5551>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.R5g5b5a1)
                {
                    images.Add(Image.LoadPixelData<Bgra5551>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.R5g6b5)
                {
                    images.Add(Image.LoadPixelData<Bgr565>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.Rgb24)
                {
                    images.Add(Image.LoadPixelData<Bgr24>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.Rgb8)
                {
                    images.Add(Image.LoadPixelData<Bgra32>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.Rgba16)
                {
                    images.Add(Image.LoadPixelData<Bgra4444>(mipdata, mipmap.Width, mipmap.Height));
                }
                else if (this.Format == ImageFormat.Rgba32)
                {
                    images.Add(Image.LoadPixelData<Bgra4444>(mipdata, mipmap.Width, mipmap.Height));
                }
                else
                {
                    throw new NotImplementedException($"Unrecognized format: ${this.Format}");
                }
            }
            return images.ToArray();
        }
    }
}
