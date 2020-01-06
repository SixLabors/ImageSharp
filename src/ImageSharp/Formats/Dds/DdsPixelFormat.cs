// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary> Surface pixel format. </summary>
    /// <remarks>
    /// To store DXGI formats such as floating-point data, use a <see cref="Flags" /> of
    /// <see cref="DdsPixelFormatFlags.FourCC" /> and set <see cref="FourCC" /> to
    /// 'D','X','1','0'. Use the <see cref="DdsHeaderDxt10" /> extension header to store the DXGI format in the
    /// <see cref="DdsHeaderDxt10.DxgiFormat" /> member.
    /// Note that there are non-standard variants of DDS files where <see cref="Flags" /> has
    /// <see cref="DdsPixelFormatFlags.FourCC" /> and the <see cref="FourCC" /> value
    /// is set directly to a D3DFORMAT or <see cref="DxgiFormat" /> enumeration value. It is not possible to
    /// disambiguate the D3DFORMAT versus <see cref="DxgiFormat" /> values using this non-standard scheme, so the
    /// DX10 extension header is recommended instead.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = 32, Pack = 1)]
    internal struct DdsPixelFormat
    {
        public DdsPixelFormat(
           uint size,
           DdsPixelFormatFlags flags,
           uint fourCC,
           uint rgbBitCount,
           uint rBitMask,
           uint gBitMask,
           uint bBitMask,
           uint aBitMask)
        {
            this.Size = size;
            this.Flags = flags;
            this.FourCC = fourCC;
            this.RGBBitCount = rgbBitCount;
            this.RBitMask = rBitMask;
            this.GBitMask = gBitMask;
            this.BBitMask = bBitMask;
            this.ABitMask = aBitMask;
        }

        /// <summary>
        /// Gets Structure size; set to 32 (bytes).
        /// </summary>
        public uint Size { get; }

        /// <summary>
        /// Gets Values which indicate what type of data is in the surface.
        /// </summary>
        public DdsPixelFormatFlags Flags { get; }

        /// <summary>
        /// Gets Four-character codes for specifying compressed or custom formats.
        /// Possible values include: DXT1, DXT2, DXT3, DXT4, or DXT5. A FOURCC of DX10 indicates the prescense of
        /// the <see cref="DdsHeaderDxt10" /> extended header, and the
        /// <see cref="DdsHeaderDxt10.DxgiFormat" /> member of that structure indicates the
        /// true format. When using a four-character code, <see cref="Flags" /> must include
        /// <see cref="DdsPixelFormatFlags.FourCC" />.
        /// </summary>
        public uint FourCC { get; }

        /// <summary>
        /// Gets Number of bits in an RGB (possibly including alpha) format. Valid when <see cref="Flags" />
        /// includes <see cref="DdsPixelFormatFlags.RGB" />, or <see cref="DdsPixelFormatFlags.Luminance" />,
        /// or <see cref="DdsPixelFormatFlags.YUV" />.
        /// </summary>
        public uint RGBBitCount { get; }

        /// <summary>
        /// Gets Red (or lumiannce or Y) mask for reading color data. For instance, given the A8R8G8B8 format,
        /// the red mask would be 0x00ff0000.
        /// </summary>
        public uint RBitMask { get; }

        /// <summary>
        /// Gets Green (or U) mask for reading color data. For instance, given the A8R8G8B8 format,
        /// the green mask would be 0x0000ff00.
        /// </summary>
        public uint GBitMask { get; }

        /// <summary>
        /// Gets Blue (or V) mask for reading color data. For instance, given the A8R8G8B8 format,
        /// the blue mask would be 0x000000ff.
        /// </summary>
        public uint BBitMask { get; }

        /// <summary>
        /// Gets Alpha mask for reading alpha data. dwFlags must include
        /// <see cref="DdsPixelFormatFlags.AlphaPixels" /> or <see cref="DdsPixelFormatFlags.Alpha" />.
        /// For instance, given the A8R8G8B8 format, the alpha mask would be 0xff000000.
        /// </summary>
        public uint ABitMask { get; }

        /// <summary>
        /// Writes the header to the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="offset">Offset in the buffer.</param>
        public void WriteTo(Span<byte> buffer, int offset)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), this.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 4, 4), (uint)this.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 8, 4), this.FourCC);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 12, 4), this.RGBBitCount);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 16, 4), this.RBitMask);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 20, 4), this.GBitMask);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 24, 4), this.BBitMask);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 28, 4), this.ABitMask);
        }

        /// <summary>
        /// Parses the DdsPixelFormat from the given data buffer.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <returns>The parsed DdsPixelFormat.</returns>
        public static DdsPixelFormat Parse(Span<byte> data, int offset)
        {
            return new DdsPixelFormat(
                size: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4)),
                flags: (DdsPixelFormatFlags)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4)),
                fourCC: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 8, 4)),
                rgbBitCount: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 12, 4)),
                rBitMask: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 16, 4)),
                gBitMask: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 20, 4)),
                bBitMask: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 24, 4)),
                aBitMask: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 28, 4)));
        }

        /// <summary>
        /// Gets the <see cref="D3dFormat" /> represented by this structure.
        /// </summary>
        /// <returns>
        /// The <see cref="D3dFormat" /> represented by this structure.
        /// </returns>
        public D3dFormat GetD3DFormat()
        {
            if ((this.Flags & DdsPixelFormatFlags.RGBA) == DdsPixelFormatFlags.RGBA)
            {
                switch (this.RGBBitCount)
                {
                    case 32:
                        return this.GetRgba32();
                    case 16:
                        return this.GetRgba16();
                }
            }
            else if ((this.Flags & DdsPixelFormatFlags.RGB) == DdsPixelFormatFlags.RGB)
            {
                switch (this.RGBBitCount)
                {
                    case 32:
                        return this.GetRgb32();
                    case 24:
                        return this.GetRgb24();
                    case 16:
                        return this.GetRgb16();
                }
            }
            else if ((this.Flags & DdsPixelFormatFlags.Alpha) == DdsPixelFormatFlags.Alpha)
            {
                if (this.RGBBitCount == 8)
                {
                    if (this.ABitMask == 0xff)
                    {
                        return D3dFormat.A8;
                    }
                }
            }
            else if ((this.Flags & DdsPixelFormatFlags.Luminance) == DdsPixelFormatFlags.Luminance)
            {
                switch (this.RGBBitCount)
                {
                    case 16:
                        return this.GetLumi16();
                    case 8:
                        return this.GetLumi8();
                }
            }
            else if ((this.Flags & DdsPixelFormatFlags.FourCC) == DdsPixelFormatFlags.FourCC)
            {
                return (D3dFormat)this.FourCC;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetRgba32()
        {
            if (this.RBitMask == 0xff && this.GBitMask == 0xff00 && this.BBitMask == 0xff0000 && this.ABitMask == 0xff000000)
            {
                return D3dFormat.A8B8G8R8;
            }

            if (this.RBitMask == 0xffff && this.GBitMask == 0xffff0000)
            {
                return D3dFormat.G16R16;
            }

            if (this.RBitMask == 0x3ff && this.GBitMask == 0xffc00 && this.BBitMask == 0x3ff00000)
            {
                return D3dFormat.A2B10G10R10;
            }

            if (this.RBitMask == 0xff0000 && this.GBitMask == 0xff00 && this.BBitMask == 0xff && this.ABitMask == 0xff000000)
            {
                return D3dFormat.A8R8G8B8;
            }

            if (this.RBitMask == 0x3ff00000 && this.GBitMask == 0xffc00 && this.BBitMask == 0x3ff && this.ABitMask == 0xc0000000)
            {
                return D3dFormat.A2R10G10B10;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetRgba16()
        {
            if (this.RBitMask == 0x7c00 && this.GBitMask == 0x3e0 && this.BBitMask == 0x1f && this.ABitMask == 0x8000)
            {
                return D3dFormat.A1R5G5B5;
            }

            if (this.RBitMask == 0xf00 && this.GBitMask == 0xf0 && this.BBitMask == 0xf && this.ABitMask == 0xf000)
            {
                return D3dFormat.A4R4G4B4;
            }

            if (this.RBitMask == 0xe0 && this.GBitMask == 0x1c && this.BBitMask == 0x3 && this.ABitMask == 0xff00)
            {
                return D3dFormat.A8R3G3B2;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetRgb32()
        {
            if (this.RBitMask == 0xffff && this.GBitMask == 0xffff0000)
            {
                return D3dFormat.G16R16;
            }

            if (this.RBitMask == 0xff0000 && this.GBitMask == 0xff00 && this.BBitMask == 0xff)
            {
                return D3dFormat.X8R8G8B8;
            }

            if (this.RBitMask == 0xff && this.GBitMask == 0xff00 && this.BBitMask == 0xff0000)
            {
                return D3dFormat.X8B8G8R8;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetRgb24()
        {
            if (this.RBitMask == 0xff0000 && this.GBitMask == 0xff00 && this.BBitMask == 0xff)
            {
                return D3dFormat.R8G8B8;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetRgb16()
        {
            if (this.RBitMask == 0xf800 && this.GBitMask == 0x7e0 && this.BBitMask == 0x1f)
            {
                return D3dFormat.R5G6B5;
            }

            if (this.RBitMask == 0x7c00 && this.GBitMask == 0x3e0 && this.BBitMask == 0x1f)
            {
                return D3dFormat.X1R5G5B5;
            }

            if (this.RBitMask == 0xf00 && this.GBitMask == 0xf0 && this.BBitMask == 0xf)
            {
                return D3dFormat.X4R4G4B4;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetLumi16()
        {
            if (this.RBitMask == 0xff && this.ABitMask == 0xff00)
            {
                return D3dFormat.A8L8;
            }

            if (this.RBitMask == 0xffff)
            {
                return D3dFormat.L16;
            }

            return D3dFormat.Unknown;
        }

        private D3dFormat GetLumi8()
        {
            if (this.RBitMask == 0xf && this.ABitMask == 0xf0)
            {
                return D3dFormat.A4L4;
            }

            if (this.RBitMask == 0xff)
            {
                return D3dFormat.L8;
            }

            return D3dFormat.Unknown;
        }
    }
}
