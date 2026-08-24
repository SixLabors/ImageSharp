// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Provides shared bounded syntax operations used by HEVC parameter-set readers.
/// </summary>
internal static class HevcParameterSetSyntax
{
    /// <summary>
    /// Gets the horizontal conformance-window unit for an HEVC chroma layout.
    /// </summary>
    /// <param name="chromaFormat">The chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether 4:4:4 components are coded as separate color planes.</param>
    /// <returns>The horizontal unit in luma samples.</returns>
    public static int GetCropUnitWidth(byte chromaFormat, bool separateColorPlane)
        => !separateColorPlane && chromaFormat is 1 or 2 ? 2 : 1;

    /// <summary>
    /// Gets the vertical conformance-window unit for an HEVC chroma layout.
    /// </summary>
    /// <param name="chromaFormat">The chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether 4:4:4 components are coded as separate color planes.</param>
    /// <returns>The vertical unit in luma samples.</returns>
    public static int GetCropUnitHeight(byte chromaFormat, bool separateColorPlane)
        => !separateColorPlane && chromaFormat == 1 ? 2 : 1;

    /// <summary>
    /// Consumes hypothetical-reference-decoder syntax without adding playback state to the still-image model.
    /// </summary>
    /// <param name="reader">The parameter-set raw byte sequence payload reader.</param>
    /// <param name="commonInformationPresent">Whether common HRD flags are coded for this parameter set.</param>
    /// <param name="maxSubLayersMinusOne">The highest declared temporal sublayer index.</param>
    /// <param name="nalHrdParametersPresent">The effective NAL HRD presence flag.</param>
    /// <param name="vclHrdParametersPresent">The effective VCL HRD presence flag.</param>
    /// <param name="subPictureHrdParametersPresent">The effective sub-picture HRD presence flag.</param>
    /// <exception cref="InvalidImageContentException">The HRD syntax is truncated or exceeds its registered bounds.</exception>
    public static void SkipHrdParameters(
        ref HevcBitReader reader,
        bool commonInformationPresent,
        int maxSubLayersMinusOne,
        ref bool nalHrdParametersPresent,
        ref bool vclHrdParametersPresent,
        ref bool subPictureHrdParametersPresent)
    {
        if (commonInformationPresent)
        {
            nalHrdParametersPresent = reader.ReadFlag();
            vclHrdParametersPresent = reader.ReadFlag();
            subPictureHrdParametersPresent = false;
            if (nalHrdParametersPresent || vclHrdParametersPresent)
            {
                subPictureHrdParametersPresent = reader.ReadFlag();
                if (subPictureHrdParametersPresent)
                {
                    reader.ReadBits(8);
                    reader.ReadBits(5);
                    reader.ReadFlag();
                    reader.ReadBits(5);
                }

                reader.ReadBits(4);
                reader.ReadBits(4);
                if (subPictureHrdParametersPresent)
                {
                    reader.ReadBits(4);
                }

                reader.ReadBits(5);
                reader.ReadBits(5);
                reader.ReadBits(5);
            }
        }

        for (int subLayer = 0; subLayer <= maxSubLayersMinusOne; subLayer++)
        {
            bool fixedPictureRateGeneral = reader.ReadFlag();
            bool fixedPictureRateWithinCvs = fixedPictureRateGeneral || reader.ReadFlag();
            bool lowDelayHrd = false;
            if (fixedPictureRateWithinCvs)
            {
                reader.ReadUnsignedExpGolomb();
            }
            else
            {
                lowDelayHrd = reader.ReadFlag();
            }

            uint cpbCountMinusOne = 0;
            if (!lowDelayHrd)
            {
                cpbCountMinusOne = reader.ReadUnsignedExpGolomb();
                if (cpbCountMinusOne > 31)
                {
                    throw new InvalidImageContentException("The HEVC HRD syntax declares too many coded-picture buffers.");
                }
            }

            for (int hrdKind = 0; hrdKind < 2; hrdKind++)
            {
                bool parametersPresent = hrdKind == 0 ? nalHrdParametersPresent : vclHrdParametersPresent;
                if (!parametersPresent)
                {
                    continue;
                }

                for (uint cpbIndex = 0; cpbIndex <= cpbCountMinusOne; cpbIndex++)
                {
                    reader.ReadUnsignedExpGolomb();
                    reader.ReadUnsignedExpGolomb();
                    if (subPictureHrdParametersPresent)
                    {
                        reader.ReadUnsignedExpGolomb();
                        reader.ReadUnsignedExpGolomb();
                    }

                    reader.ReadFlag();
                }
            }
        }
    }
}
