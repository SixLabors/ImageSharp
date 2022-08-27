// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a jpeg.
    /// </summary>
    internal sealed unsafe partial class JpegEncoderCore
    {
        private static JpegFrameConfig[] CreateFrameConfigs()
        {
            var defaultLuminanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 0, HuffmanSpec.LuminanceDC);
            var defaultLuminanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 0, HuffmanSpec.LuminanceAC);
            var defaultChrominanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 1, HuffmanSpec.ChrominanceDC);
            var defaultChrominanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 1, HuffmanSpec.ChrominanceAC);

            var defaultLuminanceQuantTable = new JpegQuantizationTableConfig(0, Quantization.LuminanceTable);
            var defaultChrominanceQuantTable = new JpegQuantizationTableConfig(1, Quantization.ChrominanceTable);

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
                    })
                {
                    AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformUnknown
                },

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
                    })
                {
                    AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformUnknown,
                },

                // YccK
                new JpegFrameConfig(
                    JpegColorSpace.Ycck,
                    JpegEncodingColor.Ycck,
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
                    })
                {
                    AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformYcck,
                },
            };
        }
    }
}
