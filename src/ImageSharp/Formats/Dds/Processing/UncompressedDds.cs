using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Emums;
using SixLabors.ImageSharp.Formats.Dds.Extensions;
using SixLabors.ImageSharp.Formats.Dds.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Pfim
{
    /// <summary>
    /// A DirectDraw Surface that is not compressed.  
    /// Thus what is in the input stream gets directly translated to the image buffer.
    /// </summary>
    internal class UncompressedDds : Dds
    {
        private readonly uint? _bitsPerPixel;
        private readonly bool? _rgbSwapped;
        private ImageFormat _format;
        private MipMapOffset[] mipMaps = new MipMapOffset[0];

        internal UncompressedDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10, uint bitsPerPixel, bool rgbSwapped)
            : base(ddsHeader, ddsHeaderDxt10)
        {
            _bitsPerPixel = bitsPerPixel;
            _rgbSwapped = rgbSwapped;
        }

        internal UncompressedDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
        }

        public override int BitsPerPixel => ImageInfo().Depth;
        public override ImageFormat Format => _format;
        public override MipMapOffset[] MipMaps => mipMaps;

        private static void InnerFill(Stream str, byte[] buf, int dataLen, int bufSize = 0x8000, int offset = 0)
        {
            int bufPosition = offset;
            for (int i = dataLen / bufSize; i > 0; i--)
                bufPosition += str.Read(buf, bufPosition, bufSize);
            str.Read(buf, bufPosition, dataLen % bufSize);
        }

        public static void InnerFillUnaligned(Stream str, byte[] buf, int bufLen, int width, int stride, int offset = 0)
        {
            for (int i = offset; i < bufLen + offset; i += stride)
            {
                str.Read(buf, i, width);
            }
        }

        protected override Image[] Decode(Stream stream)
        {
            var imageInfo = ImageInfo();
            _format = imageInfo.Format;

            var DataLen = CalcSize(imageInfo);
            var totalLen = AllocateMipMaps(imageInfo);


            var data = new byte[totalLen];

            var stride = CalcStride((int)this.DdsHeader.Width, BitsPerPixel);
            var width = (int)this.DdsHeader.Width;
            var len = DataLen;

            if (width * BytesPerPixel == stride)
            {
                InnerFill(stream, data, len);
            }
            else
            {
                InnerFillUnaligned(stream, data, len, width * BytesPerPixel, stride);
            }

            foreach (var mip in mipMaps)
            {
                if (mip.Width * BytesPerPixel == mip.Stride)
                {
                    InnerFill(stream, data, mip.DataLen, mip.DataOffset);
                }
                else
                {
                    InnerFillUnaligned(stream, data, mip.DataLen, mip.Width * BytesPerPixel, mip.Stride, mip.DataOffset);
                }
            }

            // Swap the R and B channels
            if (imageInfo.Swap)
            {
                switch (imageInfo.Format)
                {
                    case ImageFormat.Rgba32:
                        for (int i = 0; i < totalLen; i += 4)
                        {
                            byte temp = data[i];
                            data[i] = data[i + 2];
                            data[i + 2] = temp;
                        }
                        break;
                    case ImageFormat.Rgba16:
                        for (int i = 0; i < totalLen; i += 2)
                        {
                            byte temp = (byte)(data[i] & 0xF);
                            data[i] = (byte)((data[i] & 0xF0) + (data[i + 1] & 0XF));
                            data[i + 1] = (byte)((data[i + 1] & 0xF0) + temp);

                        }
                        break;
                    default:
                        throw new Exception($"Do not know how to swap {imageInfo.Format}");
                }
            }

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

        /// <summary>Determine image info from header</summary>
        public DdsLoadInfo ImageInfo()
        {
            bool rgbSwapped = _rgbSwapped ?? this.DdsHeader.PixelFormat.RBitMask < this.DdsHeader.PixelFormat.GBitMask;

            switch (_bitsPerPixel ?? this.DdsHeader.PixelFormat.RGBBitCount)
            {
                case 8:
                    return new DdsLoadInfo(false, rgbSwapped, true, 1, 1, 8, ImageFormat.Rgb8);
                case 16:
                    ImageFormat format = SixteenBitImageFormat();
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 2, 16, format);
                case 24:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 3, 24, ImageFormat.Rgb24);
                case 32:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 4, 32, ImageFormat.Rgba32);
                default:
                    throw new Exception($"Unrecognized rgb bit count: {this.DdsHeader.PixelFormat.RGBBitCount}");
            }
        }

        private ImageFormat SixteenBitImageFormat()
        {
            var pf = this.DdsHeader.PixelFormat;

            if (pf.ABitMask == 0xF000 && pf.RBitMask == 0xF00 && pf.GBitMask == 0xF0 && pf.BBitMask == 0xF)
            {
                return ImageFormat.Rgba16;
            }

            if (pf.Flags.HasFlag(DdsPixelFormatFlags.AlphaPixels))
            {
                return ImageFormat.R5g5b5a1;
            }

            return pf.GBitMask == 0x7e0 ? ImageFormat.R5g6b5 : ImageFormat.R5g5b5;
        }

        /// <summary>Calculates the number of bytes to hold image data</summary>
        private int CalcSize(DdsLoadInfo info)
        {
            int height = (int)Math.Max(info.DivSize, this.DdsHeader.Height);
            return Stride * height;
        }

        private int AllocateMipMaps(DdsLoadInfo info)
        {
            var len = CalcSize(info);

            if (this.DdsHeader.TextureCount() <= 1)
            {
                int width = (int)Math.Max(info.DivSize, (int)this.DdsHeader.Width);
                int height = (int)Math.Max(info.DivSize, this.DdsHeader.Height);
                int stride = CalcStride(width, BitsPerPixel);
                len = stride * height;

                this.mipMaps = new[] { new MipMapOffset(width, height, stride, 0, len) };
                return len;
            }

            mipMaps = new MipMapOffset[this.DdsHeader.TextureCount() - 1];
            var totalLen = len;

            for (int i = 0; i < this.DdsHeader.TextureCount() - 1; i++)
            {
                int width = (int)Math.Max(info.DivSize, (int)(this.DdsHeader.Width / Math.Pow(2, i + 1)));
                int height = (int)Math.Max(info.DivSize, this.DdsHeader.Height / Math.Pow(2, i + 1));
                int stride = CalcStride(width, BitsPerPixel);
                len = stride * height;

                mipMaps[i] = new MipMapOffset(width, height, stride, totalLen, len);
                totalLen += len;
            }

            return totalLen;
        }
    }
}
