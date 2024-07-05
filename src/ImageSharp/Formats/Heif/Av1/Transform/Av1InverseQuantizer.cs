// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Quantization;

internal class Av1InverseQuantizer
{
    public static int InverseQuantize(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, ObuPartitionInfo part, Av1BlockModeInfo mode, int[] level, int[] qCoefficients, Av1TransformType txType, Av1TransformSize txSize, Av1Plane plane)
    {
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(txSize, txType);
        short[] scanIndices = scanOrder.Scan;
        int maxValue = (1 << (7 + sequenceHeader.ColorConfig.BitDepth)) - 1;
        int minValue = -(1 << (7 + sequenceHeader.ColorConfig.BitDepth));
        bool usingQuantizationMatrix = frameHeader.QuantizationParameters.IsUsingQMatrix;
        bool lossless = frameHeader.LosslessArray[mode.SegmentId];
        short[] dequant = []; // Get from DecModCtx
        int qmLevel = (lossless || !usingQuantizationMatrix) ? Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 : frameHeader.QuantizationParameters.QMatrix[(int)plane];
        byte[] iqMatrix = []; // txType.Is2dTransform() ? Get from DecModCtx
        int shift = txSize.GetScale();

        int coefficientCount = level[0];
        Span<int> levelSpan = level.AsSpan(1);
        int lev = levelSpan[0];
        int qCoefficient;
        if (lev != 0)
        {
            int pos = scanIndices[0];
            qCoefficient = (int)(((long)Math.Abs(lev) * GetDequantizationValue(dequant[0], pos, iqMatrix)) & 0xffffff);
            qCoefficient >>= shift;

            if (lev < 0)
            {
                qCoefficient = -qCoefficient;
            }

            qCoefficients[0] = Av1Math.Clamp(qCoefficient, minValue, maxValue);
        }

        for (int i = 1; i < coefficientCount; i++)
        {
            lev = levelSpan[i];
            if (lev != 0)
            {
                int pos = scanIndices[i];
                qCoefficient = (int)(((long)Math.Abs(lev) * GetDequantizationValue(dequant[1], pos, iqMatrix)) & 0xffffff);
                qCoefficient >>= shift;

                if (lev < 0)
                {
                    qCoefficient = -qCoefficient;
                }

                qCoefficients[pos] = Av1Math.Clamp(qCoefficient, minValue, maxValue);
            }
        }

        return coefficientCount;
    }

    private static int GetDequantizationValue(short dequant, int coefficientIndex, byte[] iqMatrix)
    {
        int dqv = dequant;
        if (iqMatrix != null)
        {
            dqv = ((iqMatrix[coefficientIndex] * dqv) + (1 << (Av1ScanOrderConstants.QuantizationMatrixLevelBitCount - 1))) >> Av1ScanOrderConstants.QuantizationMatrixLevelBitCount;
        }

        return dqv;
    }
}
