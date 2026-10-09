// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Estimates luma residual rate and distortion for fast intra mode selection.
/// </summary>
internal static class Av1IntraModeEstimator
{
    private static ReadOnlySpan<short> Scan8x8 =>
    [
        0, 8, 1, 2, 9, 16, 24, 17, 10, 3, 4, 11, 18, 25, 32, 40,
        33, 26, 19, 12, 5, 6, 13, 20, 27, 34, 41, 48, 56, 49, 42, 35,
        28, 21, 14, 7, 15, 22, 29, 36, 43, 50, 57, 58, 51, 44, 37, 30,
        23, 31, 38, 45, 52, 59, 60, 53, 46, 39, 47, 54, 61, 62, 55, 63
    ];

    private static ReadOnlySpan<short> Scan16x16 =>
    [
        0, 8, 2, 4, 10, 16, 24, 18, 12, 6, 64, 14, 20, 26, 32, 40,
        34, 28, 22, 72, 66, 68, 74, 80, 30, 36, 42, 48, 56, 50, 44, 38,
        88, 82, 76, 70, 128, 78, 84, 90, 96, 46, 52, 58, 1, 9, 3, 60,
        54, 104, 98, 92, 86, 136, 130, 132, 138, 144, 94, 100, 106, 112, 62, 5,
        11, 17, 25, 19, 13, 7, 120, 114, 108, 102, 152, 146, 140, 134, 192, 142,
        148, 154, 160, 110, 116, 122, 65, 15, 21, 27, 33, 41, 35, 29, 23, 73,
        67, 124, 118, 168, 162, 156, 150, 200, 194, 196, 202, 208, 158, 164, 170, 176,
        126, 69, 75, 81, 31, 37, 43, 49, 57, 51, 45, 39, 89, 83, 77, 71,
        184, 178, 172, 166, 216, 210, 204, 198, 206, 212, 218, 224, 174, 180, 186, 129,
        79, 85, 91, 97, 47, 53, 59, 61, 55, 105, 99, 93, 87, 137, 131, 188,
        182, 232, 226, 220, 214, 222, 228, 234, 240, 190, 133, 139, 145, 95, 101, 107,
        113, 63, 121, 115, 109, 103, 153, 147, 141, 135, 248, 242, 236, 230, 238, 244,
        250, 193, 143, 149, 155, 161, 111, 117, 123, 125, 119, 169, 163, 157, 151, 201,
        195, 252, 246, 254, 197, 203, 209, 159, 165, 171, 177, 127, 185, 179, 173, 167,
        217, 211, 205, 199, 207, 213, 219, 225, 175, 181, 187, 189, 183, 233, 227, 221,
        215, 223, 229, 235, 241, 191, 249, 243, 237, 231, 239, 245, 251, 253, 247, 255
    ];

    private static ReadOnlySpan<short> HighBitDepthScan16x16 =>
    [
        0, 4, 2, 8, 6, 16, 20, 18, 12, 10, 64, 14, 24, 22, 32, 36,
        34, 28, 26, 68, 66, 72, 70, 80, 30, 40, 38, 48, 52, 50, 44, 42,
        84, 82, 76, 74, 128, 78, 88, 86, 96, 46, 56, 54, 1, 5, 3, 60,
        58, 100, 98, 92, 90, 132, 130, 136, 134, 144, 94, 104, 102, 112, 62, 9,
        7, 17, 21, 19, 13, 11, 116, 114, 108, 106, 148, 146, 140, 138, 192, 142,
        152, 150, 160, 110, 120, 118, 65, 15, 25, 23, 33, 37, 35, 29, 27, 69,
        67, 124, 122, 164, 162, 156, 154, 196, 194, 200, 198, 208, 158, 168, 166, 176,
        126, 73, 71, 81, 31, 41, 39, 49, 53, 51, 45, 43, 85, 83, 77, 75,
        180, 178, 172, 170, 212, 210, 204, 202, 206, 216, 214, 224, 174, 184, 182, 129,
        79, 89, 87, 97, 47, 57, 55, 61, 59, 101, 99, 93, 91, 133, 131, 188,
        186, 228, 226, 220, 218, 222, 232, 230, 240, 190, 137, 135, 145, 95, 105, 103,
        113, 63, 117, 115, 109, 107, 149, 147, 141, 139, 244, 242, 236, 234, 238, 248,
        246, 193, 143, 153, 151, 161, 111, 121, 119, 125, 123, 165, 163, 157, 155, 197,
        195, 252, 250, 254, 201, 199, 209, 159, 169, 167, 177, 127, 181, 179, 173, 171,
        213, 211, 205, 203, 207, 217, 215, 225, 175, 185, 183, 189, 187, 229, 227, 221,
        219, 223, 233, 231, 241, 191, 245, 243, 237, 235, 239, 249, 247, 253, 251, 255
    ];

    /// <summary>
    /// Estimates the cost of a predicted luma residual.
    /// </summary>
    /// <param name="workspace">The block workspace, which holds the encoder options.</param>
    /// <param name="estimationRowCoefficients">The coefficients of one row of estimation transforms.</param>
    /// <param name="searchDequantizedCoefficients">The dequantized coefficients of one estimation transform.</param>
    /// <param name="transformWorkspace">The intermediate buffer of the transforms, which also holds the quantized levels.</param>
    /// <param name="residual">The predicted residual samples.</param>
    /// <param name="stride">The residual row stride.</param>
    /// <param name="extent">The included residual dimensions.</param>
    /// <param name="transformSize">The square estimation transform size.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The AC quantizer adjustment.</param>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <param name="rate">The estimated coefficient rate.</param>
    /// <param name="distortion">The normalized coefficient distortion.</param>
    /// <param name="skip">Whether every estimated coefficient is zero.</param>
    public static void Estimate(
        Av1EncoderBlockWorkspace workspace,
        Span<int> estimationRowCoefficients,
        Span<int> searchDequantizedCoefficients,
        Span<int> transformWorkspace,
        ReadOnlySpan<short> residual,
        int stride,
        Size extent,
        Av1TransformSize transformSize,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        out int rate,
        out long distortion,
        out bool skip)
    {
        int width = transformSize.GetWidth();
        int sampleCount = width * width;
        bool highBitDepth = bitDepth != Av1BitDepth.EightBit;
        ReadOnlySpan<short> scan = width switch
        {
            4 => Av1ScanOrderConstants.GetScanOrder(Av1TransformSize.Size4x4, Av1TransformType.DctDct).Scan,
            8 => Scan8x8,
            _ => highBitDepth ? HighBitDepthScan16x16 : Scan16x16
        };

        Span<int> reconstructed = searchDequantizedCoefficients[..sampleCount];
        Span<int> quantized = transformWorkspace[..sampleCount];
        int normalizationShift = (bitDepth.GetBitCount() - 8) * 2;
        int blocksPerRow = (extent.Width + width - 1) / width;
        int magnitudeSum = 0;
        int endOfBlockCost = 0;
        distortion = 0;
        skip = true;
        for (int row = 0; row < extent.Height; row += width)
        {
            // Transform the whole row of blocks first. Then the vector kernels can take several adjacent blocks per step.
            Av1ForwardTransformer.TransformRowForModeEstimation(
                residual[(row * stride)..],
                stride,
                width,
                blocksPerRow,
                estimationRowCoefficients,
                transformWorkspace,
                highBitDepth);

            for (int block = 0; block < blocksPerRow; block++)
            {
                Span<int> coefficients = estimationRowCoefficients.Slice(block * sampleCount, sampleCount);

                // The row transform is complete, so quantization can reuse its intermediate buffer for the levels.
                // Estimation keeps its own scan order and never writes these coefficients to the bitstream.
                ushort endOfBlock = Av1ForwardQuantizer.QuantizeForModeEstimation(
                    coefficients,
                    quantized,
                    reconstructed,
                    transformSize,
                    scan,
                    qIndex,
                    dcDeltaQ,
                    acDeltaQ,
                    bitDepth,
                    workspace.EncoderOptions.Sharpness);

                skip &= endOfBlock == 0;
                endOfBlockCost += BitOperations.Log2((uint)endOfBlock + 1);
                magnitudeSum += TensorPrimitives.SumOfMagnitudes<int>(quantized);

                // Eight-bit estimation keeps the signed 16-bit coefficient representation.
                // High-bit-depth estimation keeps int coefficients and normalizes only after accumulation.
                long squaredError = Av1CoefficientMeasures.SumSquaredDifferences(coefficients, reconstructed, !highBitDepth);

                if (normalizationShift != 0)
                {
                    squaredError = (squaredError + (1L << (normalizationShift - 1))) >> normalizationShift;
                }

                distortion += squaredError >> 2;
            }
        }

        // The rate unit is 1/512 bit. Each unit of quantized magnitude costs four bits (shift 11).
        // Each transform adds log2(end of block + 1) bits for its end position (shift 9). The mode controller adds the prediction and skip rates.
        // The clamp to int.MaxValue / 2 keeps the rate below the invalid-rate value. It leaves space for the mode, vector and chroma rates that callers add.
        rate = (int)Math.Min(((long)magnitudeSum << 11) + ((long)endOfBlockCost << 9), int.MaxValue / 2);
    }
}
