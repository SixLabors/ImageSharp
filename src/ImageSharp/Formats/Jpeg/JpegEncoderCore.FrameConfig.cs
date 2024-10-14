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
        JpegHuffmanTableConfig defaultLuminanceHuffmanDC = new(@class: 0, destIndex: 0, HuffmanSpec.LuminanceDC);
        JpegHuffmanTableConfig defaultLuminanceHuffmanAC = new(@class: 1, destIndex: 0, HuffmanSpec.LuminanceAC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanDC = new(@class: 0, destIndex: 1, HuffmanSpec.ChrominanceDC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanAC = new(@class: 1, destIndex: 1, HuffmanSpec.ChrominanceAC);

        JpegQuantizationTableConfig defaultLuminanceQuantTable = new(0, Quantization.LuminanceTable);
        JpegQuantizationTableConfig defaultChrominanceQuantTable = new(1, Quantization.ChrominanceTable);

        JpegHuffmanTableConfig[] yCbCrHuffmanConfigs =
        [
            defaultLuminanceHuffmanDC,
            defaultLuminanceHuffmanAC,
            defaultChrominanceHuffmanDC,
            defaultChrominanceHuffmanAC
        ];

        JpegQuantizationTableConfig[] yCbCrQuantTableConfigs =
        [
            defaultLuminanceQuantTable,
            defaultChrominanceQuantTable
        ];

        return
        [
            // YCbCr 4:4:4
            new(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio444,
                [
                    new(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:2:2
            new(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio422,
                [
                    new(id: 1, hsf: 2, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:2:0
            new(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio420,
                [
                    new(id: 1, hsf: 2, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:1:1
            new(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio411,
                [
                    new(id: 1, hsf: 4, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:1:0
            new(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio410,
                [
                    new(id: 1, hsf: 4, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // Luminance
            new(
                JpegColorSpace.Grayscale,
                JpegColorType.Luminance,
                [
                    new(id: 0, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
                ],
                [
                    defaultLuminanceHuffmanDC,
                    defaultLuminanceHuffmanAC
                ],
                [
                    defaultLuminanceQuantTable
                ]),

            // Rgb
            new(
                JpegColorSpace.RGB,
                JpegColorType.Rgb,
                [
                    new(id: 82, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 71, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 66, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
                ],
                [
                    defaultLuminanceHuffmanDC,
                    defaultLuminanceHuffmanAC
                ],
                [
                    defaultLuminanceQuantTable
                ])
            {
                AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformUnknown
            },

            // Cmyk
            new(
                JpegColorSpace.Cmyk,
                JpegColorType.Cmyk,
                [
                    new(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 4, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
                ],
                [
                    defaultLuminanceHuffmanDC,
                    defaultLuminanceHuffmanAC
                ],
                [
                    defaultLuminanceQuantTable
                ])
            {
                AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformUnknown,
            },

            // YccK
            new(
                JpegColorSpace.Ycck,
                JpegColorType.Ycck,
                [
                    new(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 2, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 3, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new(id: 4, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
                ],
                [
                    defaultLuminanceHuffmanDC,
                    defaultLuminanceHuffmanAC
                ],
                [
                    defaultLuminanceQuantTable
                ])
            {
                AdobeColorTransformMarkerFlag = JpegConstants.Adobe.ColorTransformYcck,
            }
        ];
    }
}
