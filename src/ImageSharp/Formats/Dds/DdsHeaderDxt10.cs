// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary>
    /// DDS header extension to handle resource arrays.
    /// </summary>
    /// <remarks>
    /// Use this structure together with a <see cref="DdsHeader" /> to store a resource array in a DDS file.
    /// For more information, see texture arrays.
    /// This header is present if the <see cref="DdsPixelFormat.FourCC" /> member of the
    /// <see cref="DdsPixelFormat" /> structure is set to 'DX10'.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsHeaderDxt10
    {
        public DdsHeaderDxt10(
            DxgiFormat dxgiFormat,
            D3d10ResourceDimension resourceDimension,
            D3d10ResourceMiscFlags miscFlag,
            uint arraySize,
            uint reserved)
        {
            this.DxgiFormat = dxgiFormat;
            this.ResourceDimension = resourceDimension;
            this.MiscFlag = miscFlag;
            this.ArraySize = arraySize;
            this.Reserved = reserved;
        }

        /// <summary>
        /// Gets the surface pixel format.
        /// </summary>
        public DxgiFormat DxgiFormat { get; }

        /// <summary>
        /// Gets identifies the type of resource.
        /// The following values for this member are a subset of the values in the
        /// <see cref="D3d10ResourceDimension" /> enumeration:
        /// <see cref="D3d10ResourceDimension.Texture1D" /> :
        /// Resource is a 1D texture. The <see cref="DdsHeader.Width" /> member of <see cref="DdsHeader" />
        /// specifies the size of the texture. Typically, you set the <see cref="DdsHeader.Height" /> member of
        /// <see cref="DdsHeader" /> to 1; you also must set the <see cref="DdsFlags.Height" /> flag in
        /// the <see cref="DdsHeader.Flags" /> member of <see cref="DdsHeader" />.
        /// <see cref="D3d10ResourceDimension.Texture2D" /> :
        /// Resource is a 2D texture with an area specified by the <see cref="DdsHeader.Width" /> and
        /// <see cref="DdsHeader.Height" /> members of <see cref="DdsHeader" />.
        /// You can also use this type to identify a cube-map texture. For more information about how to identify a
        /// cube-map texture, see <see cref="MiscFlag" /> and <see cref="ArraySize" /> members.
        /// <see cref="D3d10ResourceDimension.Texture3D" /> :
        /// Resource is a 3D texture with a volume specified by the <see cref="DdsHeader.Width" />,
        /// <see cref="DdsHeader.Height" />, and <see cref="DdsHeader.Depth" /> members of
        /// <see cref="DdsHeader" />. You also must set the <see cref="DdsFlags.Depth" /> flag
        /// in the <see cref="DdsHeader.Flags" /> member of <see cref="DdsHeader" />.
        /// </summary>
        public D3d10ResourceDimension ResourceDimension { get; }

        /// <summary>
        /// Gets identifies other, less common options for resources. The following value for this member is a
        /// subset of the values in the <see cref="D3d10ResourceMiscFlags" /> enumeration:
        /// DDS_RESOURCE_MISC_TEXTURECUBE Indicates a 2D texture is a cube-map texture.
        /// </summary>
        public D3d10ResourceMiscFlags MiscFlag { get; }

        /// <summary>
        /// Gets the number of elements in the array.
        /// For a 2D texture that is also a cube-map texture, this number represents the number of cubes.
        /// This number is the same as the number in the NumCubes member of D3D10_TEXCUBE_ARRAY_SRV1 or
        /// D3D11_TEXCUBE_ARRAY_SRV). In this case, the DDS file contains arraySize*6 2D textures.
        /// For more information about this case, see the <see cref="MiscFlag" /> description.
        /// For a 3D texture, you must set this number to 1.
        /// </summary>
        public uint ArraySize { get; }

        /// <summary>
        /// Gets reserved for future use.
        /// </summary>
        public uint Reserved { get; }

        /// <summary>
        /// Writes the header to the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        public void WriteTo(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(0, 4), (uint)this.DxgiFormat);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4, 4), (uint)this.ResourceDimension);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(8, 4), (uint)this.MiscFlag);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(12, 4), this.ArraySize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(16, 4), this.Reserved);
        }

        /// <summary>
        /// Parses the DDS_HEADER_DXT10 from the given data buffer.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parsed DDS_HEADER_DXT10.</returns>
        public static DdsHeaderDxt10 Parse(Span<byte> data)
        {
            return new DdsHeaderDxt10(
                dxgiFormat: (DxgiFormat)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4)),
                resourceDimension: (D3d10ResourceDimension)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)),
                miscFlag: (D3d10ResourceMiscFlags)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4)),
                arraySize: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4)),
                reserved: BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4)));
        }
    }
}
