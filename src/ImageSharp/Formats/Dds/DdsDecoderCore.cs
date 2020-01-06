// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp.Formats.Dds.Extensions;
using SixLabors.ImageSharp.Formats.Dds.Internals;
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

                var image = new Image<TPixel>(this.configuration, (int)this.ddsHeader.Width, (int)this.ddsHeader.Height, null);
                Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

                var texture = new Texture<TPixel>
                {
                    Images = new Image<TPixel>[1][]
                };

                texture.Images[0] = new[] { image };

                return texture;
            }
            catch (IndexOutOfRangeException e)
            {
                throw new ImageFormatException("Dds image does not have a valid format.", e);
            }
        }

        /// <summary>
        /// Reads a uncompressed TGA image where each pixels has 24 bit.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> to assign the palette to.</param>
        /// <param name="inverted">Indicates, if the origin of the image is top left rather the bottom left (the default).</param>
        //private void ReadBgr24<TPixel>(int width, int height, Buffer2D<TPixel> pixels, bool inverted)
        //    where TPixel : struct, IPixel<TPixel>
        //{
        //    using (IManagedByteBuffer row = this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, 3, 0))
        //    {
        //        for (int y = 0; y < height; y++)
        //        {
        //            this.currentStream.Read(row);
        //            int newY = Invert(y, height, inverted);
        //            Span<TPixel> pixelSpan = pixels.GetRowSpan(newY);
        //            PixelOperations<TPixel>.Instance.FromBgr24Bytes(
        //                this.configuration,
        //                row.GetSpan(),
        //                pixelSpan,
        //                width);
        //        }
        //    }
        //}

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            int bitsPerPixel = this.ReadFileHeader(stream);
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
        /// <returns>Bits Per Pixel</returns>
        private int ReadFileHeader(Stream stream)
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
                return this.ddsDxt10header.GetBitsPerPixel();
            }

            return 8;
        }
    }
}
