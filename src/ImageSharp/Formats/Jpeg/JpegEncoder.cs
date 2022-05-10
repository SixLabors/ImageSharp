// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Encoder for writing the data image to a stream in jpeg format.
    /// </summary>
    public sealed class JpegEncoder : IImageEncoder, IJpegEncoderOptions
    {
        /// <summary>
        /// The available encodable frame configs.
        /// </summary>
        private static readonly JpegFrameConfig[] FrameConfigs = CreateFrameConfigs();

        /// <inheritdoc/>
        public int? Quality { get; set; }

        /// <summary>
        /// Sets jpeg color for encoding.
        /// </summary>
        public JpegEncodingColor ColorType
        {
            set
            {
                JpegFrameConfig frameConfig = Array.Find(
                    FrameConfigs,
                    cfg => cfg.EncodingColor == value);

                if (frameConfig is null)
                {
                    throw new ArgumentException(nameof(value));
                }

                this.FrameConfig = frameConfig;
            }
        }

        internal JpegFrameConfig FrameConfig { get; set; }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new JpegEncoderCore(this, this.FrameConfig);
            encoder.Encode(image, stream);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new JpegEncoderCore(this, this.FrameConfig);
            return encoder.EncodeAsync(image, stream, cancellationToken);
        }

        private static JpegFrameConfig[] CreateFrameConfigs()
        {
            var defaultLuminanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 0, HuffmanSpec.TheHuffmanSpecs[0]);
            var defaultLuminanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 0, HuffmanSpec.TheHuffmanSpecs[1]);
            var defaultChrominanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 1, HuffmanSpec.TheHuffmanSpecs[2]);
            var defaultChrominanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 1, HuffmanSpec.TheHuffmanSpecs[3]);

            var defaultLuminanceQuantTable = new JpegQuantizationTableConfig(0, Block8x8.Load(Quantization.LuminanceTable));
            var defaultChrominanceQuantTable = new JpegQuantizationTableConfig(1, Block8x8.Load(Quantization.ChrominanceTable));

            var yCbCrHuffmanConfigs = new JpegHuffmanTableConfig[]
            {
                defaultLuminanceHuffmanDC,
                defaultLuminanceHuffmanAC,
                defaultChrominanceHuffmanDC,
                defaultChrominanceHuffmanAC,
            };

            var yCbCrQuantTableConfigs = new JpegQuantizationTableConfig[]
            {
                defaultLuminanceQuantTable,
                defaultChrominanceQuantTable,
            };

            return new JpegFrameConfig[]
            {
                // YCbCr 4:4:4
                new JpegFrameConfig(
                    JpegColorSpace.YCbCr,
                    JpegEncodingColor.YCbCrRatio444,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    },
                    yCbCrHuffmanConfigs,
                    yCbCrQuantTableConfigs),

                // YCbCr 4:2:2
                new JpegFrameConfig(
                    JpegColorSpace.YCbCr,
                    JpegEncodingColor.YCbCrRatio422,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 2, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    },
                    yCbCrHuffmanConfigs,
                    yCbCrQuantTableConfigs),

                // YCbCr 4:2:0
                new JpegFrameConfig(
                    JpegColorSpace.YCbCr,
                    JpegEncodingColor.YCbCrRatio420,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 2, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    },
                    yCbCrHuffmanConfigs,
                    yCbCrQuantTableConfigs),

                // YCbCr 4:1:1
                new JpegFrameConfig(
                    JpegColorSpace.YCbCr,
                    JpegEncodingColor.YCbCrRatio411,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 4, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    },
                    yCbCrHuffmanConfigs,
                    yCbCrQuantTableConfigs),

                // YCbCr 4:1:0
                new JpegFrameConfig(
                    JpegColorSpace.YCbCr,
                    JpegEncodingColor.YCbCrRatio410,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 4, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    },
                    yCbCrHuffmanConfigs,
                    yCbCrQuantTableConfigs),

                // Luminance
                new JpegFrameConfig(
                    JpegColorSpace.Grayscale,
                    JpegEncodingColor.Luminance,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 0, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    },
                    new JpegHuffmanTableConfig[]
                    {
                        defaultLuminanceHuffmanDC,
                        defaultLuminanceHuffmanAC
                    },
                    new JpegQuantizationTableConfig[]
                    {
                        defaultLuminanceQuantTable
                    }),

                // Rgb
                new JpegFrameConfig(
                    JpegColorSpace.RGB,
                    JpegEncodingColor.Rgb,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 82, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 71, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 66, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    },
                    new JpegHuffmanTableConfig[]
                    {
                        defaultLuminanceHuffmanDC,
                        defaultLuminanceHuffmanAC
                    },
                    new JpegQuantizationTableConfig[]
                    {
                        defaultLuminanceQuantTable
                    }),

                // Cmyk
                new JpegFrameConfig(
                    JpegColorSpace.Cmyk,
                    JpegEncodingColor.Cmyk,
                    new JpegComponentConfig[]
                    {
                        new JpegComponentConfig(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                        new JpegComponentConfig(id: 4, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    },
                    new JpegHuffmanTableConfig[]
                    {
                        defaultLuminanceHuffmanDC,
                        defaultLuminanceHuffmanAC
                    },
                    new JpegQuantizationTableConfig[]
                    {
                        defaultLuminanceQuantTable
                    }),
            };
        }
    }

    internal class JpegFrameConfig
    {
        public JpegFrameConfig(JpegColorSpace colorType, JpegEncodingColor encodingColor, JpegComponentConfig[] components, JpegHuffmanTableConfig[] huffmanTables, JpegQuantizationTableConfig[] quantTables)
        {
            this.ColorType = colorType;
            this.EncodingColor = encodingColor;
            this.Components = components;
            this.HuffmanTables = huffmanTables;
            this.QuantizationTables = quantTables;

            this.MaxHorizontalSamplingFactor = components[0].HorizontalSampleFactor;
            this.MaxVerticalSamplingFactor = components[0].VerticalSampleFactor;
            for (int i = 1; i < components.Length; i++)
            {
                JpegComponentConfig component = components[i];
                this.MaxHorizontalSamplingFactor = Math.Max(this.MaxHorizontalSamplingFactor, component.HorizontalSampleFactor);
                this.MaxVerticalSamplingFactor = Math.Max(this.MaxVerticalSamplingFactor, component.VerticalSampleFactor);
            }
        }

        public JpegColorSpace ColorType { get; }

        public JpegEncodingColor EncodingColor { get; }

        public JpegComponentConfig[] Components { get; }

        public JpegHuffmanTableConfig[] HuffmanTables { get; }

        public JpegQuantizationTableConfig[] QuantizationTables { get; }

        public int MaxHorizontalSamplingFactor { get; }

        public int MaxVerticalSamplingFactor { get; }
    }

    internal class JpegComponentConfig
    {
        public JpegComponentConfig(byte id, int hsf, int vsf, int quantIndex, int dcIndex, int acIndex)
        {
            this.Id = id;
            this.HorizontalSampleFactor = hsf;
            this.VerticalSampleFactor = vsf;
            this.QuantizatioTableIndex = quantIndex;
            this.DcTableSelector = dcIndex;
            this.AcTableSelector = acIndex;
        }

        public byte Id { get; }

        public int HorizontalSampleFactor { get; }

        public int VerticalSampleFactor { get; }

        public int QuantizatioTableIndex { get; }

        public int DcTableSelector { get; }

        public int AcTableSelector { get; }
    }

    internal class JpegHuffmanTableConfig
    {
        public JpegHuffmanTableConfig(int @class, int destIndex, HuffmanSpec table)
        {
            this.Class = @class;
            this.DestinationIndex = destIndex;
            this.Table = table;
        }

        public int Class { get; }

        public int DestinationIndex { get; }

        public HuffmanSpec Table { get; }
    }

    internal class JpegQuantizationTableConfig
    {
        public JpegQuantizationTableConfig(int destIndex, Block8x8 table)
        {
            this.DestinationIndex = destIndex;
            this.Table = table;
        }

        public int DestinationIndex { get; }

        public Block8x8 Table { get; }
    }
}
