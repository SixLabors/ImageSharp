// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;

internal class Av1InverseQuantizer
{
    private const int QuatizationMatrixTotalSize = 3344;

    private readonly ObuSequenceHeader sequenceHeader;
    private readonly ObuFrameHeader frameHeader;
    private readonly int[][][] inverseQuantizationMatrix;
    private Av1DeQuantizationContext? deQuantsDeltaQ;

    public Av1InverseQuantizer(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;

        this.inverseQuantizationMatrix = new int[Av1Constants.QuantificationMatrixLevelCount][][];
        for (int q = 0; q < Av1Constants.QuantificationMatrixLevelCount; ++q)
        {
            this.inverseQuantizationMatrix[q] = new int[Av1Constants.MaxPlanes][];
            for (int c = 0; c < Av1Constants.MaxPlanes; c++)
            {
                int lumaOrChroma = Math.Min(1, c);
                int current = 0;
                this.inverseQuantizationMatrix[q][c] = new int[(int)Av1TransformSize.AllSizes];
                for (Av1TransformSize t = 0; t < Av1TransformSize.AllSizes; ++t)
                {
                    int size = t.GetSize2d();
                    Av1TransformSize qmTransformSize = t.GetAdjusted();
                    if (q == Av1Constants.QuantificationMatrixLevelCount - 1)
                    {
                        this.inverseQuantizationMatrix[q][c][(int)t] = -1;
                    }
                    else if (t != qmTransformSize)
                    {
                        // Reuse matrices for 'qm_tx_size'
                        this.inverseQuantizationMatrix[q][c][(int)t] = this.inverseQuantizationMatrix[q][c][(int)qmTransformSize];
                    }
                    else
                    {
                        Guard.MustBeLessThanOrEqualTo(current + size, QuatizationMatrixTotalSize, nameof(current));
                        this.inverseQuantizationMatrix[q][c][(int)t] = Av1QuantizationConstants.InverseWT[q][lumaOrChroma][current];
                        current += size;
                    }
                }
            }
        }
    }

    public void UpdateDequant(Av1DeQuantizationContext deQuants, Av1SuperblockInfo superblockInfo)
    {
        Av1BitDepth bitDepth = this.sequenceHeader.ColorConfig.BitDepth;
        Guard.NotNull(deQuants, nameof(deQuants));
        this.deQuantsDeltaQ = deQuants;
        if (this.frameHeader.DeltaQParameters.IsPresent)
        {
            for (int i = 0; i < Av1Constants.MaxSegmentCount; i++)
            {
                int currentQIndex = Av1QuantizationLookup.GetQIndex(this.frameHeader.SegmentationParameters, i, superblockInfo.SuperblockDeltaQ);

                for (Av1Plane plane = 0; (int)plane < Av1Constants.MaxPlanes; plane++)
                {
                    int dcDeltaQ = this.frameHeader.QuantizationParameters.DeltaQDc[(int)plane];
                    int acDeltaQ = this.frameHeader.QuantizationParameters.DeltaQAc[(int)plane];

                    this.deQuantsDeltaQ.SetDc(i, plane, Av1QuantizationLookup.GetDcQuant(currentQIndex, dcDeltaQ, bitDepth));
                    this.deQuantsDeltaQ.SetAc(i, plane, Av1QuantizationLookup.GetAcQuant(currentQIndex, acDeltaQ, bitDepth));
                }
            }
        }
    }

    /// <summary>
    /// SVT: svt_aom_inverse_quantize
    /// </summary>
    public int InverseQuantize(Av1BlockModeInfo mode, Span<int> level, Span<int> qCoefficients, Av1TransformType transformType, Av1TransformSize transformSize, Av1Plane plane)
    {
        Guard.NotNull(this.deQuantsDeltaQ);
        Av1ScanOrder scanOrder = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType);
        short[] scanIndices = scanOrder.Scan;
        int maxValue = (1 << (7 + this.sequenceHeader.ColorConfig.BitDepth.GetBitCount())) - 1;
        int minValue = -(1 << (7 + this.sequenceHeader.ColorConfig.BitDepth.GetBitCount()));
        Av1TransformSize qmTransformSize = transformSize.GetAdjusted();
        bool usingQuantizationMatrix = this.frameHeader.QuantizationParameters.IsUsingQMatrix;
        bool lossless = this.frameHeader.LosslessArray[mode.SegmentId];
        short dequantDc = this.deQuantsDeltaQ.GetDc(mode.SegmentId, plane);
        short dequantAc = this.deQuantsDeltaQ.GetAc(mode.SegmentId, plane);
        int qmLevel = lossless || !usingQuantizationMatrix ? Av1ScanOrderConstants.QuantizationMatrixLevelCount - 1 : this.frameHeader.QuantizationParameters.QMatrix[(int)plane];
        ref int iqMatrix = ref (transformType.ToClass() == Av1TransformClass.Class2D) ?
            ref this.inverseQuantizationMatrix[qmLevel][(int)plane][(int)qmTransformSize]
            : ref this.inverseQuantizationMatrix[Av1Constants.QuantificationMatrixLevelCount - 1][0][(int)qmTransformSize];
        int shift = transformSize.GetScale();

        int coefficientCount = level[0];
        level = level[1..];
        int lev = level[0];
        int qCoefficient;
        if (lev != 0)
        {
            int pos = scanIndices[0];
            qCoefficient = (int)(((long)Math.Abs(lev) * GetDeQuantizedValue(dequantDc, pos, ref iqMatrix)) & 0xffffff);
            qCoefficient >>= shift;

            if (lev < 0)
            {
                qCoefficient = -qCoefficient;
            }

            qCoefficients[0] = Av1Math.Clamp(qCoefficient, minValue, maxValue);
        }

        for (int i = 1; i < coefficientCount; i++)
        {
            lev = level[i];
            if (lev != 0)
            {
                int pos = scanIndices[i];
                qCoefficient = (int)(((long)Math.Abs(lev) * GetDeQuantizedValue(dequantAc, pos, ref iqMatrix)) & 0xffffff);
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

    /// <summary>
    /// SVT: get_dqv
    /// </summary>
    private static int GetDeQuantizedValue(short dequant, int coefficientIndex, ref int iqMatrix)
    {
        const int bias = 1 << (Av1ScanOrderConstants.QuantizationMatrixLevelBitCount - 1);
        int deQuantifiedValue = dequant;

        deQuantifiedValue = ((Unsafe.Add(ref iqMatrix, coefficientIndex) * deQuantifiedValue) + bias) >> Av1ScanOrderConstants.QuantizationMatrixLevelBitCount;
        return deQuantifiedValue;
    }
}
