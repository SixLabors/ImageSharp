// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Sets the frame quantizer: the base index, the chroma delta quantizers and the quantization matrix levels.
/// </summary>
internal static class Av1FrameQuantizer
{
    /// <summary>
    /// Sets the quantization parameters of a frame.
    /// </summary>
    /// <param name="quantization">The frame quantization parameters.</param>
    /// <param name="colorConfig">The color configuration of the sequence.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="options">The encoder configuration.</param>
    /// <param name="isAllIntra">Whether the encoder runs in all-intra usage.</param>
    public static void SetQuantizer(
        ObuQuantizationParameters quantization,
        ObuColorConfig colorConfig,
        int qIndex,
        Av1EncoderOptions options,
        bool isAllIntra)
    {
        // The frame quantizer starts from the configured index. The delta quantizer syntax needs a nonzero base index.
        // The caller sets that index when the frame decides to use delta quantizers.
        quantization.BaseQIndex = qIndex;
        quantization.DeltaQDc[(int)Av1Plane.Y] = 0;
        quantization.DeltaQAc[(int)Av1Plane.Y] = 0;

        // Lossless coding keeps every delta at zero.
        int chromaDcDeltaQ = 0;
        int chromaAcDeltaQ = 0;
        bool subsamplingX = colorConfig.IsMonochrome || colorConfig.SubSamplingX;
        bool subsamplingY = colorConfig.IsMonochrome || colorConfig.SubSamplingY;
        bool imageTune = options.Tuning.IsImageTuning();
        bool ssimulacra2Tune = options.Tuning == Av1Tuning.Ssimulacra2;
        if (options.EnableChromaDeltaQ && qIndex != 0)
        {
            if (imageTune)
            {
                if (subsamplingX && subsamplingY)
                {
                    // 4:2:0 chroma gets a finer quantizer, by up to 20 steps for the SSIMULACRA 2 tune and 16 for the image tune.
                    // The decrease ramps down to zero at low quantizers.
                    int offset = ssimulacra2Tune ? 20 : 16;
                    chromaDcDeltaQ = -Math.Clamp((qIndex / 2) - 14, 0, offset);
                    chromaAcDeltaQ = chromaDcDeltaQ;
                }
                else if (subsamplingX)
                {
                    // 4:2:2 chroma gets a coarser AC quantizer, by up to 6 steps.
                    chromaAcDeltaQ = Math.Clamp(qIndex / 2, 0, 6);
                }
                else if (!subsamplingY)
                {
                    // 4:4:4 chroma gets a coarser AC quantizer, by up to 24 steps.
                    chromaAcDeltaQ = Math.Clamp(qIndex / 2, 0, 24);
                }
            }
            else
            {
                chromaDcDeltaQ = 2;
                chromaAcDeltaQ = 2;
            }
        }

        quantization.DeltaQDc[(int)Av1Plane.U] = chromaDcDeltaQ;
        quantization.DeltaQAc[(int)Av1Plane.U] = chromaAcDeltaQ;
        quantization.DeltaQDc[(int)Av1Plane.V] = chromaDcDeltaQ;
        quantization.DeltaQAc[(int)Av1Plane.V] = chromaAcDeltaQ;
        quantization.HasSeparateUvDelta = false;

        // Select the luma and chroma matrix formulas from the usage and the tune.
        int minimum = options.QuantizationMatrixMinimum;
        int maximum = options.QuantizationMatrixMaximum;
        int luma;
        int chroma;
        if (imageTune)
        {
            luma = ssimulacra2Tune ? GetSsimulacra2LumaLevel(qIndex, minimum, maximum) : GetAllIntraLevel(qIndex, minimum, maximum);
            chroma = !subsamplingX && !subsamplingY
                ? Get444ChromaLevel(qIndex + chromaAcDeltaQ, minimum, maximum)
                : GetAllIntraLevel(qIndex + chromaAcDeltaQ, minimum, maximum);
        }
        else if (isAllIntra)
        {
            luma = GetAllIntraLevel(qIndex, minimum, maximum);
            chroma = GetAllIntraLevel(qIndex + chromaAcDeltaQ, minimum, maximum);
        }
        else
        {
            luma = GetLevel(qIndex, minimum, maximum);
            chroma = GetLevel(qIndex + chromaAcDeltaQ, minimum, maximum);
        }

        // The frame uses the matrix levels only when the options enable quantization matrices.
        quantization.IsUsingQMatrix = options.EnableQuantizationMatrices;
        quantization.QMatrix[(int)Av1Plane.Y] = luma;
        quantization.QMatrix[(int)Av1Plane.U] = chroma;
        quantization.QMatrix[(int)Av1Plane.V] = chroma;
    }

    /// <summary>
    /// Gets the matrix level that grows linearly with the quantizer index.
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="first">The lowest allowed matrix level.</param>
    /// <param name="last">The highest allowed matrix level.</param>
    /// <returns>The matrix level.</returns>
    private static int GetLevel(int qIndex, int first, int last)
        => first + ((qIndex * (last + 1 - first)) / (Av1Constants.MaxQ + 1));

    /// <summary>
    /// Gets the matrix level of all-intra coding, which falls as the quantizer index grows.
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="first">The lowest allowed matrix level.</param>
    /// <param name="last">The highest allowed matrix level.</param>
    /// <returns>The matrix level.</returns>
    private static int GetAllIntraLevel(int qIndex, int first, int last)
    {
        int level = qIndex switch
        {
            <= 40 => 10,
            <= 100 => 9,
            <= 160 => 8,
            <= 200 => 7,
            <= 220 => 6,
            <= 240 => 5,
            _ => 4
        };

        return Math.Clamp(level, first, last);
    }

    /// <summary>
    /// Gets the luma matrix level of the SSIMULACRA 2 tune. It falls faster than the all-intra level as the quantizer index grows,
    /// and it reaches steeper matrices.
    /// </summary>
    /// <param name="qIndex">The quantizer index.</param>
    /// <param name="first">The lowest allowed matrix level.</param>
    /// <param name="last">The highest allowed matrix level.</param>
    /// <returns>The matrix level.</returns>
    private static int GetSsimulacra2LumaLevel(int qIndex, int first, int last)
    {
        int level = qIndex switch
        {
            <= 40 => 10,
            <= 60 => 9,
            <= 90 => 8,
            <= 120 => 7,
            <= 130 => 6,
            <= 140 => 5,
            <= 160 => 4,
            <= 200 => 3,
            _ => 2
        };

        return Math.Clamp(level, first, last);
    }

    /// <summary>
    /// Gets the chroma matrix level of 4:4:4 coding, which has four times the chroma coefficients of 4:2:0.
    /// </summary>
    /// <param name="qIndex">The chroma quantizer index.</param>
    /// <param name="first">The lowest allowed matrix level.</param>
    /// <param name="last">The highest allowed matrix level.</param>
    /// <returns>The matrix level.</returns>
    private static int Get444ChromaLevel(int qIndex, int first, int last)
    {
        int level = qIndex switch
        {
            <= 12 => 10,
            <= 24 => 9,
            <= 32 => 8,
            <= 36 => 7,
            <= 44 => 6,
            <= 48 => 5,
            <= 56 => 4,
            <= 88 => 3,
            _ => 2
        };

        return Math.Clamp(level, first, last);
    }
}
