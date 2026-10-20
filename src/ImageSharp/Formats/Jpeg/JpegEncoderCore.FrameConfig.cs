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
    /// <summary>
    /// Defines the component order, sampling factors, coding tables and Adobe markers for each supported JPEG output.
    /// </summary>
    /// <remarks>
    /// Each configuration has two selectors. ColorType selects the internal color converter, including the stored CMYK convention.
    /// EncodingColor matches the public encoder option and selects the scan encoder's component layout.
    /// Standalone JPEG selects the first matching EncodingColor. TIFF selects ColorType directly to request its CMYK convention.
    /// The table definitions are created once and reused. JpegFrame owns component buffers; JpegEncoderCore owns quality-scaled quantization tables.
    /// </remarks>
    /// <returns>The frame configurations, with standalone configurations before container-specific configurations sharing the same EncodingColor.</returns>
    private static JpegFrameConfig[] CreateFrameConfigs()
    {
        // Huffman class 0 codes DC differences; class 1 codes AC coefficient runs.
        // YCbCr uses table ID 0 for Y and table ID 1 for Cb/Cr. The other models use table ID 0 for every component.
        JpegHuffmanTableConfig defaultLuminanceHuffmanDC = new(@class: 0, destIndex: 0, HuffmanSpec.LuminanceDC);
        JpegHuffmanTableConfig defaultLuminanceHuffmanAC = new(@class: 1, destIndex: 0, HuffmanSpec.LuminanceAC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanDC = new(@class: 0, destIndex: 1, HuffmanSpec.ChrominanceDC);
        JpegHuffmanTableConfig defaultChrominanceHuffmanAC = new(@class: 1, destIndex: 1, HuffmanSpec.ChrominanceAC);

        // Component quantIndex values refer to these IDs. The encoder scales the base tables for the selected quality later.
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

        // Keep standalone CMYK in a local variable so TIFF can reuse its component and table definitions.
        // The four component IDs identify C, M, Y and K in that order. Sampling factors of 1 retain every sample in each channel.
        // CmykOperator stores inverted inks: 255 means no ink and 0 means full ink. Four components with APP14 transform 0 identify Adobe CMYK.
        JpegFrameConfig cmyk = new(
            JpegColorSpace.Cmyk,
            JpegColorType.Cmyk,
            [
                new JpegComponentConfig(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                new JpegComponentConfig(id: 4, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
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
        };

        // Each component's id is written in the frame and scan headers. quantIndex, dcIndex and acIndex select its coding tables.
        // hsf and vsf give its horizontal and vertical block counts per MCU. Y at 2x2 with Cb/Cr at 1x1 produces 4:2:0 sampling.
        return
        [

            // YCbCr 4:4:4
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio444,
                [
                    new JpegComponentConfig(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:2:2
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio422,
                [
                    new JpegComponentConfig(id: 1, hsf: 2, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:2:0
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio420,
                [
                    new JpegComponentConfig(id: 1, hsf: 2, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:1:1
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio411,
                [
                    new JpegComponentConfig(id: 1, hsf: 4, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // YCbCr 4:1:0
            new JpegFrameConfig(
                JpegColorSpace.YCbCr,
                JpegColorType.YCbCrRatio410,
                [
                    new JpegComponentConfig(id: 1, hsf: 4, vsf: 2, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 1, dcIndex: 1, acIndex: 1)
                ],
                yCbCrHuffmanConfigs,
                yCbCrQuantTableConfigs),

            // Luminance
            new JpegFrameConfig(
                JpegColorSpace.Luminance,
                JpegColorType.Luminance,
                [
                    new JpegComponentConfig(id: 0, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
                ],
                [
                    defaultLuminanceHuffmanDC,
                    defaultLuminanceHuffmanAC
                ],
                [
                    defaultLuminanceQuantTable
                ]),

            // RGB retains three full-resolution planes. IDs 82, 71 and 66 are ASCII R, G and B; APP14 transform 0 identifies direct RGB.
            new JpegFrameConfig(
                JpegColorSpace.RGB,
                JpegColorType.Rgb,
                [
                    new JpegComponentConfig(id: 82, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 71, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 66, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
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

            // Public JpegColorType.Cmyk must resolve to this standalone configuration before the TIFF entry below.
            cmyk,

            // YCCK retains four full-resolution Y, Cb, Cr and K planes. APP14 transform 2 identifies YCCK for the decoder.
            new JpegFrameConfig(
                JpegColorSpace.Ycck,
                JpegColorType.Ycck,
                [
                    new JpegComponentConfig(id: 1, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 2, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 3, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0),
                    new JpegComponentConfig(id: 4, hsf: 1, vsf: 1, quantIndex: 0, dcIndex: 0, acIndex: 0)
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
            },

            // TiffCmyk selects TiffCmykOperator: 0 means no ink and 255 means full ink. TIFF's PhotometricInterpretation=Separated declares CMYK.
            // Share the four component definitions and coding tables above; only the color converter and APP14 choice differ.
            // Leave APP14 unset for this TIFF convention. Keep this entry last because standalone lookup uses the first EncodingColor match.
            new JpegFrameConfig(JpegColorSpace.TiffCmyk, JpegColorType.Cmyk, cmyk.Components, cmyk.HuffmanTables, cmyk.QuantizationTables)
        ];
    }
}
