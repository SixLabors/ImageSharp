// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Performs the OpenExr decoding operation.
    /// </summary>
    internal sealed class ExrDecoderCore : IImageDecoderInternals
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The bitmap decoder options.
        /// </summary>
        private readonly IExrDecoderOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExrDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public ExrDecoderCore(Configuration configuration, IExrDecoderOptions options)
        {
            this.Configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new(this.Width, this.Height);

        private int Width { get; set; }

        private int Height { get; set; }

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel> => throw new NotImplementedException();

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            // Skip over the magick bytes.
            stream.Read(this.buffer, 0, 4);

            // Read version number.
            byte version = (byte)stream.ReadByte();
            if (version != 2)
            {
                ExrThrowHelper.ThrowNotSupportedVersion();
            }

            // Next three bytes contain info's about the image.
            // We ignore those for now.
            stream.Read(this.buffer, 0, 3);

            ExrHeader header = this.ParseHeader(stream);

            if (!header.IsValid())
            {
                ExrThrowHelper.ThrowInvalidImageHeader();
            }

            if (header.Channels.Count != 3)
            {
                ExrThrowHelper.ThrowNotSupported("Only 3 channels are supported!");
            }

            this.Width = header.DataWindow.Value.xMax - header.DataWindow.Value.xMin + 1;
            this.Height = header.DataWindow.Value.yMax - header.DataWindow.Value.yMin + 1;

            // TODO: calculate bits per pixel.
            int bitsPerPixel = 48;

            return new ImageInfo(new PixelTypeInfo(bitsPerPixel), this.Width, this.Height, new ImageMetadata());
        }

        private ExrHeader ParseHeader(BufferedReadStream stream)
        {
            ExrAttribute attribute = this.ReadAttribute(stream);
            var header = new ExrHeader();

            while (!attribute.Equals(ExrAttribute.EmptyAttribute))
            {
                switch (attribute.Name)
                {
                    case "channels":
                        IList<ExrChannelInfo> channels = this.ParseChannelList(stream, attribute.Length);
                        header.Channels = channels;
                        break;
                    case "compression":
                        var compression = (ExrCompression)stream.ReadByte();
                        header.Compression = compression;
                        break;
                    case "dataWindow":
                        ExrBox2i dataWindow = this.ReadBox2i(stream);
                        header.DataWindow = dataWindow;
                        break;
                    case "displayWindow":
                        ExrBox2i displayWindow = this.ReadBox2i(stream);
                        header.DisplayWindow = displayWindow;
                        break;
                    case "lineOrder":
                        var lineOrder = (ExrLineOrder)stream.ReadByte();
                        header.LineOrder = lineOrder;
                        break;
                    case "pixelAspectRatio":
                        float aspectRatio = stream.ReadSingle(this.buffer);
                        header.AspectRatio = aspectRatio;
                        break;
                    case "screenWindowCenter":
                        float screenWindowCenterX = stream.ReadSingle(this.buffer);
                        float screenWindowCenterY = stream.ReadSingle(this.buffer);
                        header.ScreenWindowCenter = new PointF(screenWindowCenterX, screenWindowCenterY);
                        break;
                    case "screenWindowWidth":
                        float screenWindowWidth = stream.ReadSingle(this.buffer);
                        header.ScreenWindowWidth = screenWindowWidth;
                        break;
                    default:
                        // Skip unknown attribute bytes.
                        stream.Skip(attribute.Length);
                        break;
                }

                attribute = this.ReadAttribute(stream);
            }

            return header;
        }

        private ExrAttribute ReadAttribute(BufferedReadStream stream)
        {
            string attributeName = ReadString(stream);
            if (attributeName.Equals(string.Empty))
            {
                return ExrAttribute.EmptyAttribute;
            }

            string attributeType = ReadString(stream);

            stream.Read(this.buffer, 0, 4);
            int attributeSize = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrAttribute(attributeName, attributeType, attributeSize);
        }

        private ExrBox2i ReadBox2i(BufferedReadStream stream)
        {
            stream.Read(this.buffer, 0, 4);
            int xMin = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int yMin = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int xMax = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int yMax = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrBox2i(xMin, yMin, xMax, yMax);
        }

        private List<ExrChannelInfo> ParseChannelList(BufferedReadStream stream, int attributeSize)
        {
            var channels = new List<ExrChannelInfo>();
            while (attributeSize > 1)
            {
                ExrChannelInfo channelInfo = this.ReadChannelInfo(stream, out int bytesRead);
                channels.Add(channelInfo);
                attributeSize -= bytesRead;
            }

            // Last byte should be a null byte.
            int byteRead = stream.ReadByte();

            return channels;
        }

        private ExrChannelInfo ReadChannelInfo(BufferedReadStream stream, out int bytesRead)
        {
            string channelName = ReadString(stream);
            bytesRead = channelName.Length + 1;

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            var pixelType = (ExrPixelType)BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            byte pLinear = (byte)stream.ReadByte();
            byte reserved0 = (byte)stream.ReadByte();
            byte reserved1 = (byte)stream.ReadByte();
            byte reserved2 = (byte)stream.ReadByte();
            bytesRead += 4;

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            int xSampling = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            int ySampling = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrChannelInfo(channelName, pixelType, pLinear, xSampling, ySampling);
        }

        private static string ReadString(BufferedReadStream stream)
        {
            var str = new StringBuilder();
            int character = stream.ReadByte();
            if (character == 0)
            {
                // End of file header reached.
                return string.Empty;
            }

            while (character != 0)
            {
                if (character == -1)
                {
                    ExrThrowHelper.ThrowInvalidImageHeader();
                }

                str.Append((char)character);
                character = stream.ReadByte();
            }

            return str.ToString();
        }
    }
}
