// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp.Formats.Dds.Emums;
using SixLabors.ImageSharp.Formats.Dds.Extensions;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Dds
{
    internal sealed class DdsDecoderCore
    {
        /// <summary>
        /// The file header containing general information about the image.
        /// </summary>
        private DdsHeader ddsHeader;

        /// <summary>
        /// The dxt10 header if available
        /// </summary>
        private DdsHeaderDxt10 ddsDxt10header;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The stream to decode from.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly IDdsDecoderOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DdsDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public DdsDecoderCore(Configuration configuration, IDdsDecoderOptions options)
        {
            this.configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">
        ///    <para><paramref name="stream"/> is null.</para>
        /// </exception>
        /// <returns>The decoded image.</returns>
        public Texture<TPixel> DecodeTexture<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            try
            {
                this.ReadFileHeader(stream);

                if (this.ddsHeader.Width == 0 || this.ddsHeader.Height == 0)
                {
                    throw new UnknownImageFormatException("Width or height cannot be 0");
                }

                int resourceCount = this.ddsHeader.ShouldHaveDxt10Header() ? (this.ddsDxt10header.ArraySize > 0) ? (int)this.ddsDxt10header.ArraySize : 1 : 1;

                D3dFormat d3dFormat = this.ddsHeader.PixelFormat.GetD3DFormat();
                DxgiFormat dxgiFormat = this.ddsHeader.ShouldHaveDxt10Header() ? this.ddsDxt10header.DxgiFormat : DxgiFormat.Unknown;

                long pitch = DdsTools.ComputePitch(this.ddsHeader.Width, d3dFormat, dxgiFormat, (int)this.ddsHeader.PitchOrLinearSize);
                long linearSize = DdsTools.ComputeLinearSize(this.ddsHeader.Width, this.ddsHeader.Height, d3dFormat, dxgiFormat, (int)this.ddsHeader.PitchOrLinearSize);
                int depth = this.ddsHeader.ComputeDepth();
                int mipMapCount = this.ddsHeader.HasMipmaps() ? (int)this.ddsHeader.MipMapCount : 1;

                if (this.ddsHeader.IsVolumeTexture())
                {
                    return this.ReadVolumeTexture<TPixel>(stream, d3dFormat, dxgiFormat, resourceCount, mipMapCount);
                }
                else if (this.ddsHeader.IsCubemap())
                {
                    return this.ReadCubemap<TPixel>(stream, d3dFormat, dxgiFormat, resourceCount, mipMapCount);
                }
                else
                {
                    return this.ReadFlatTexture<TPixel>(stream, d3dFormat, dxgiFormat, resourceCount, mipMapCount);
                }
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Dds image does not have a valid format.", e);
            }
        }

        private Texture<TPixel> ReadFlatTexture<TPixel>(Stream stream, D3dFormat d3dFormat, DxgiFormat dxgiFormat, int resourceCount, int mipMapCount)
             where TPixel : struct, IPixel<TPixel>
        {
            var texture = new Texture<TPixel>(TextureType.FlatTexture)
            {
                Images = new Image<TPixel>[1][]
            };

            DdsSurfaceType textureType = (this.ddsHeader.Height > 1) ? DdsSurfaceType.Texture2D : DdsSurfaceType.Texture1D;
            int surfaceCount = (mipMapCount == 0) ? 1 : mipMapCount;
            for (int i = 0; i < resourceCount; i++)
            {
                // Flat textures contain only mip-map levels, so read them:
                IEnumerable<Image> surfaces = this.ReadMipMapSurfaces(stream, d3dFormat, dxgiFormat, surfaceCount, textureType, this.ddsHeader.Width, this.ddsHeader.Height);
            }

            return texture;
        }

        private Texture<TPixel> ReadVolumeTexture<TPixel>(Stream stream, D3dFormat d3dFormat, DxgiFormat dxgiFormat, int resourceCount, int mipMapCount)
            where TPixel : struct, IPixel<TPixel>
        {
            var texture = new Texture<TPixel>(TextureType.VolumeTexture)
            {
                Images = new Image<TPixel>[1][]
            };

            int surfaceCount = (mipMapCount == 0) ? 1 : mipMapCount;
            for (int i = 0; i < resourceCount; i++)
            {
                IEnumerable<Image> surfaces = this.ReadMipMapSurfaces(stream, d3dFormat, dxgiFormat, surfaceCount, DdsSurfaceType.Texture3D, this.ddsHeader.Width, this.ddsHeader.Height);
            }

            return texture;
        }

        private Texture<TPixel> ReadCubemap<TPixel>(Stream stream, D3dFormat d3dFormat, DxgiFormat dxgiFormat, int resourceCount, int mipMapCount)
            where TPixel : struct, IPixel<TPixel>
        {
            var texture = new Texture<TPixel>(TextureType.Cubemap)
            {
                Images = new Image<TPixel>[1][]
            };

            int surfaceCount = (mipMapCount == 0) ? 1 : mipMapCount;
            DdsSurfaceType[] faces = this.ddsHeader.GetExistingCubemapFaces();
            if (faces == null)
            {
                return null;
            }

            for (int i = 0; i < resourceCount; i++)
            {
                // Cube maps contain mip-map levels after every cube face.
                // So for every face read all mip-map levels:
                //var surfaces = new List<Surface>(Depth * MipMapCount);
                //for (var face = 0; face < Depth; face++)
                //{
                //    // I'm sure we do not have to swap positive and negative Y faces for OpenGL...
                //    var faceType = faces[face];
                //    surfaces.AddRange(ReadMipMapSurfaces(reader, surfaceCount, faceType, Width, Height));
                //}

                //Textures.Add(new TextureResource(surfaces.ToArray()));
            }

            return texture;
        }

        private IEnumerable<Image> ReadMipMapSurfaces(Stream stream, D3dFormat d3dFormat, DxgiFormat dxgiFormat, int surfaceCount, DdsSurfaceType surfaceType, uint width, uint height, int depth = 1)
        {
            // Load every mip-map level:
            for (int level = 0; level < surfaceCount && (width > 0 || height > 0); ++level)
            {
                int surfaceSize = (int)DdsTools.ComputeLinearSize(width, height, d3dFormat, dxgiFormat, (int)this.ddsHeader.PitchOrLinearSize) * depth;

#if NETCOREAPP2_1
                Span<byte> surfaceBuffer = stackalloc byte[surfaceSize];
#else
                var surfaceBuffer = new byte[surfaceSize];
#endif

                this.currentStream.Read(surfaceBuffer, 0, surfaceSize);

                //yield return Surface.FromBytes(data, surfaceType, level, width, height, FormatD3D, FormatDxgi);
                yield break;

                width /= 2;
                height /= 2;
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            this.ReadFileHeader(stream);

            D3dFormat d3dFormat = this.ddsHeader.PixelFormat.GetD3DFormat();
            DxgiFormat dxgiFormat = this.ddsHeader.ShouldHaveDxt10Header() ? this.ddsDxt10header.DxgiFormat : DxgiFormat.Unknown;
            int bitsPerPixel = DdsTools.GetBitsPerPixel(d3dFormat, dxgiFormat);

            return new ImageInfo(
                new PixelTypeInfo(bitsPerPixel),
                (int)this.ddsHeader.Width,
                (int)this.ddsHeader.Height,
                null);
        }

        /// <summary>
        /// Reads the dds file header from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        private void ReadFileHeader(Stream stream)
        {
            this.currentStream = stream;

#if NETCOREAPP2_1
            Span<byte> magicBuffer = stackalloc byte[4];
#else
            var magicBuffer = new byte[4];
#endif
            this.currentStream.Read(magicBuffer, 0, 4);
            uint magicValue = BinaryPrimitives.ReadUInt32LittleEndian(magicBuffer);
            if (magicValue != DdsFourCc.DdsMagicWord)
            {
                throw new NotSupportedException($"Invalid DDS magic value.");
            }

#if NETCOREAPP2_1
            Span<byte> ddsHeaderBuffer = stackalloc byte[DdsConstants.DdsHeaderSize];
#else
            var ddsHeaderBuffer = new byte[DdsConstants.DdsHeaderSize];
#endif

            this.currentStream.Read(ddsHeaderBuffer, 0, DdsConstants.DdsHeaderSize);
            this.ddsHeader = DdsHeader.Parse(ddsHeaderBuffer);
            this.ddsHeader.Validate();

            if (this.ddsHeader.ShouldHaveDxt10Header())
            {
#if NETCOREAPP2_1
                Span<byte> ddsDxt10headerBuffer = stackalloc byte[DdsConstants.DdsDxt10HeaderSize];
#else
                var ddsDxt10headerBuffer = new byte[DdsConstants.DdsDxt10HeaderSize];
#endif
                this.currentStream.Read(ddsDxt10headerBuffer, 0, DdsConstants.DdsDxt10HeaderSize);
                this.ddsDxt10header = DdsHeaderDxt10.Parse(ddsDxt10headerBuffer);
            }
        }
    }
}
