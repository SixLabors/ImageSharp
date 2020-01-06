// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary>
    /// Describes a DDS file header.
    /// </summary>
    /// <remarks>
    /// Note  When you write .dds files, you should set the  <see cref="DdsFlags.Caps" /> and
    /// <see cref="DdsFlags.PixelFormat" /> flags, and for mipmapped textures you should also set the
    /// <see cref="DdsFlags.MipMapCount" /> flag.
    /// However, when you read a .dds file, you should not rely on the <see cref="DdsFlags.Caps" />,
    /// <see cref="DdsFlags.PixelFormat" />, and <see cref="DdsFlags.MipMapCount" />
    /// flags being set because some writers of such a file might not set these flags.
    /// Include flags in <see cref="Flags" /> for the members of the structure that contain valid data. Use this
    /// structure in combination with a <see cref="DdsHeaderDxt10" /> to store a resource array in a DDS file.
    /// For more information, see texture arrays.
    /// <see cref="DdsHeader" /> is identical to the DirectDraw DDSURFACEDESC2 structure without DirectDraw
    /// dependencies.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsHeader
    {
        public DdsHeader(
            uint size,
            DdsFlags flags,
            uint height,
            uint width,
            uint pitchOrLinearSize,
            uint depth,
            uint mipMapCount,
            uint[] reserved1,
            DdsPixelFormat pixelFormat,
            DdsCaps1 caps1,
            DdsCaps2 caps2,
            uint caps3,
            uint caps4,
            uint reserved2)
        {
            this.Size = size;
            this.Flags = flags;
            this.Height = height;
            this.Width = width;
            this.PitchOrLinearSize = pitchOrLinearSize;
            this.Depth = depth;
            this.MipMapCount = mipMapCount;
            this.Reserved1 = reserved1;
            this.PixelFormat = pixelFormat;
            this.Caps1 = caps1;
            this.Caps2 = caps2;
            this.Caps3 = caps3;
            this.Caps4 = caps4;
            this.Reserved2 = reserved2;
        }

        /// <summary>
        /// Gets size of structure. This member must be set to 124.
        /// </summary>
        public uint Size { get; }

        /// <summary>
        /// Gets flags to indicate which members contain valid data.
        /// </summary>
        /// <remarks>
        /// When you write .dds files, you should set the <see cref="DdsFlags.Caps" /> and
        /// <see cref="DdsFlags.PixelFormat" /> flags,
        /// and for mipmapped textures you should also set the <see cref="DdsFlags.MipMapCount" /> flag.
        /// However, when you read a .dds file, you should not rely on the <see cref="DdsFlags.Caps" />,
        /// <see cref="DdsFlags.PixelFormat" />, and <see cref="DdsFlags.MipMapCount" />
        /// flags being set because some writers of such a file might not set these flags.
        /// </remarks>
        public DdsFlags Flags { get; }

        /// <summary>
        /// Gets surface height (in pixels).
        /// </summary>
        public uint Height { get; }

        /// <summary>
        /// Gets surface width (in pixels).
        /// </summary>
        public uint Width { get; }

        /// <summary>
        /// Gets the pitch or number of bytes per scan line in an uncompressed texture;
        /// the total number of bytes in the top level texture for a compressed texture.
        /// </summary>
        public uint PitchOrLinearSize { get; }

        /// <summary>
        /// Gets depth of a volume texture (in pixels), otherwise unused.
        /// </summary>
        public uint Depth { get; }

        /// <summary>
        /// Gets number of mipmap levels, otherwise unused.
        /// </summary>
        public uint MipMapCount { get; }

        /// <summary>
        /// Gets unused.
        /// </summary>
        public uint[] Reserved1 { get; }

        /// <summary>
        /// Gets the pixel format.
        /// </summary>
        public DdsPixelFormat PixelFormat { get; }

        /// <summary>
        /// Gets specifies the complexity of the surfaces stored.
        /// </summary>
        /// <remarks>
        /// When you write .dds files, you should set the <see cref="DdsCaps1.Texture" /> flag,
        /// and for multiple surfaces you should also set the <see cref="DdsCaps1.Complex" /> flag.
        /// However, when you read a .dds file, you should not rely on the <see cref="DdsCaps1.Texture" /> and
        /// <see cref="DdsCaps1.Complex" /> flags being set because some file writers might not set these flags.
        /// </remarks>
        public DdsCaps1 Caps1 { get; }

        /// <summary>
        /// Gets defines additional capabilities of the surface.
        /// </summary>
        public DdsCaps2 Caps2 { get; }

        /// <summary>
        /// Gets unused.
        /// </summary>
        public uint Caps3 { get; }

        /// <summary>
        /// Gets unused.
        /// </summary>
        public uint Caps4 { get; }

        /// <summary>
        /// Gets unused.
        /// </summary>
        public uint Reserved2 { get; }

        /// <summary>
        /// Validates the dds header.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown if the image does not pass validation.
        /// </exception>
        public void Validate()
        {
            bool incorrectSize = (this.Size != DdsConstants.DdsHeaderSize) || (this.PixelFormat.Size != DdsConstants.DdsPixelFormatSize);
            if (incorrectSize)
            {
                throw new NotSupportedException($"Invalid structure size.");
            }

            bool requiredFlagsMissing = (this.Flags & DdsFlags.Caps) == 0 || (this.Flags & DdsFlags.PixelFormat) == 0 || (this.Caps1 & DdsCaps1.Texture) == 0;
            if (requiredFlagsMissing)
            {
                throw new NotSupportedException($"Required flags missing.");
            }

            bool hasInvalidCompression = (this.Flags & DdsFlags.Pitch) != 0 && (this.Flags & DdsFlags.LinearSize) != 0;
            if (hasInvalidCompression)
            {
                throw new NotSupportedException($"Invalid compression.");
            }
        }

        public void WriteTo(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0, 4), this.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), (uint)this.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), this.Height);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), this.Width);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), this.PitchOrLinearSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(20, 4), this.Depth);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(24, 4), this.MipMapCount);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(28, 4), this.Reserved1[0]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(32, 4), this.Reserved1[1]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(36, 4), this.Reserved1[2]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(40, 4), this.Reserved1[3]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(44, 4), this.Reserved1[4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(48, 4), this.Reserved1[5]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(52, 4), this.Reserved1[6]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(56, 4), this.Reserved1[7]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(60, 4), this.Reserved1[8]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(64, 4), this.Reserved1[9]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(68, 4), this.Reserved1[10]);
            this.PixelFormat.WriteTo(buffer, 72);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(104, 4), (uint)this.Caps1);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(108, 4), (uint)this.Caps2);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(112, 4), this.Caps3);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(116, 4), this.Caps4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(120, 4), this.Reserved2);
        }

        public static DdsHeader Parse(Span<byte> data)
        {
            uint[] reserved1 = new uint[]
            {
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(36, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(40, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(44, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(48, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(52, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(56, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(60, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(64, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(68, 4))
            };

            return new DdsHeader(
                size: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4)),
                flags: (DdsFlags)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)),
                height: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4)),
                width: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4)),
                pitchOrLinearSize: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4)),
                depth: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4)),
                mipMapCount: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(24, 4)),
                reserved1: reserved1,
                pixelFormat: DdsPixelFormat.Parse(data, 72),
                caps1: (DdsCaps1)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(104, 4)),
                caps2: (DdsCaps2)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(108, 4)),
                caps3: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(112, 4)),
                caps4: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(116, 4)),
                reserved2: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(120, 4)));
        }
    }
}
