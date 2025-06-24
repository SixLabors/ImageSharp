// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Image encoder for writing an image to a stream as a jpeg.
/// </summary>
internal sealed unsafe partial class JpegEncoderCore
{
    private static JpegFrameConfig[] CreateFrameConfigs()
    {
        JpegHuffmanTableConfig defaultLuminanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 0, HuffmanSpec.LuminanceDC);
        JpegHuffmanTableConfig defaultLuminanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 0, HuffmanSpec.LuminanceAC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanDC = new JpegHuffmanTableConfig(@class: 0, destIndex: 1, HuffmanSpec.ChrominanceDC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanAC = new JpegHuffmanTableConfig(@class: 1, destIndex: 1, HuffmanSpec.ChrominanceAC);

        JpegQuantizationTableConfig defaultLuminanceQuantTable = new JpegQuantizationTableConfig(0, Quantization.LuminanceTable);
        JpegQuantizationTableConfig defaultChrominanceQuantTable = new JpegQuantizationTableConfig(1, Quantization.ChrominanceTable);

        JpegHuffmanTableConfig[] yCbCrHuffmanConfigs = new JpegHuffmanTableConfig[]
        {
            defaultLuminanceHuffmanDC,
            defaultLuminanceHuffmanAC,
            defaultChrominanceHuffmanDC,
            defaultChrominanceHuffmanAC,
        };

        JpegQuantizationTableConfig[] yCbCrQuantTableConfigs = new JpegQuantizationTableConfig[]
        {
            defaultLuminanceQuantTable,
            defaultChrominanceQuantTable,
        };

        return new JpegFrameConfig[]
        {
            // YCbCr 4:4:4
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio444,
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
                JpegColorType.YCbCrRatio422,
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
                JpegColorType.YCbCrRatio420,
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
                JpegColorType.YCbCrRatio411,
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
                JpegColorType.YCbCrRatio410,
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
                JpegColorType.Luminance,
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
                JpegColorType.Rgb,
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
                JpegColorType.Cmyk,
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
                JpegColorType.Ycck,
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
