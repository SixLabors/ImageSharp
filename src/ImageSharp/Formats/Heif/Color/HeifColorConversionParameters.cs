// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Heif.Color;

/// <summary>
/// Stores the resolved H.273 values shared by every scalar and SIMD lane.
/// </summary>
internal readonly struct HeifColorConversionParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifColorConversionParameters"/> struct.
    /// </summary>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="matrixCoefficients">The signaled H.273 matrix-coefficient code point.</param>
    /// <param name="isFullRange">Whether encoded components use their complete numeric range.</param>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="constantLuminanceScales">The constant-luminance chroma scales.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    /// <param name="lumaSampleMaximum">The largest encoded luma sample value.</param>
    /// <param name="chromaSampleMaximum">The largest encoded chroma sample value.</param>
    /// <param name="rgbBias">The reversible transform's RGB code-value bias.</param>
    /// <param name="rgbScale">The reversible transform's RGB code-value range.</param>
    /// <param name="rgbSampleMaximum">The largest reversible transform RGB code value.</param>
    public HeifColorConversionParameters(
        float kr,
        float kg,
        float kb,
        CicpMatrixCoefficients matrixCoefficients,
        bool isFullRange,
        CicpTransferCharacteristics transferCharacteristics,
        in HeifConstantLuminanceScales constantLuminanceScales,
        float lumaBias,
        float lumaScale,
        float chromaBias,
        float chromaScale,
        float lumaSampleMaximum,
        float chromaSampleMaximum,
        float rgbBias,
        float rgbScale,
        float rgbSampleMaximum)
    {
        this.Kr = kr;
        this.Kg = kg;
        this.Kb = kb;
        this.RedChromaScale = 2F * (1F - kr);
        this.BlueChromaScale = 2F * (1F - kb);
        this.GreenRedChromaScale = 2F * kr * (1F - kr) / kg;
        this.GreenBlueChromaScale = 2F * kb * (1F - kb) / kg;
        this.MatrixCoefficients = matrixCoefficients;
        this.IsFullRange = isFullRange;
        this.TransferCharacteristics = transferCharacteristics;
        this.ConstantLuminanceScales = constantLuminanceScales;
        this.LumaBias = lumaBias;
        this.LumaScale = lumaScale;
        this.ChromaBias = chromaBias;
        this.ChromaScale = chromaScale;
        this.LumaSampleMaximum = lumaSampleMaximum;
        this.ChromaSampleMaximum = chromaSampleMaximum;
        this.RgbBias = rgbBias;
        this.RgbScale = rgbScale;
        this.RgbSampleMaximum = rgbSampleMaximum;
    }

    /// <summary>
    /// Gets the red luma coefficient.
    /// </summary>
    public float Kr { get; }

    /// <summary>
    /// Gets the green luma coefficient.
    /// </summary>
    public float Kg { get; }

    /// <summary>
    /// Gets the blue luma coefficient.
    /// </summary>
    public float Kb { get; }

    /// <summary>
    /// Gets the red contribution from the red-difference component.
    /// </summary>
    public float RedChromaScale { get; }

    /// <summary>
    /// Gets the blue contribution from the blue-difference component.
    /// </summary>
    public float BlueChromaScale { get; }

    /// <summary>
    /// Gets the red-difference subtraction from green.
    /// </summary>
    public float GreenRedChromaScale { get; }

    /// <summary>
    /// Gets the blue-difference subtraction from green.
    /// </summary>
    public float GreenBlueChromaScale { get; }

    /// <summary>
    /// Gets the signaled H.273 matrix-coefficient code point.
    /// </summary>
    public CicpMatrixCoefficients MatrixCoefficients { get; }

    /// <summary>
    /// Gets a value indicating whether encoded components use their complete numeric range.
    /// </summary>
    public bool IsFullRange { get; }

    /// <summary>
    /// Gets the signaled transfer characteristics.
    /// </summary>
    public CicpTransferCharacteristics TransferCharacteristics { get; }

    /// <summary>
    /// Gets the constant-luminance chroma scales.
    /// </summary>
    public HeifConstantLuminanceScales ConstantLuminanceScales { get; }

    /// <summary>
    /// Gets the encoded luma bias.
    /// </summary>
    public float LumaBias { get; }

    /// <summary>
    /// Gets the encoded luma range.
    /// </summary>
    public float LumaScale { get; }

    /// <summary>
    /// Gets the encoded chroma midpoint.
    /// </summary>
    public float ChromaBias { get; }

    /// <summary>
    /// Gets the encoded chroma range.
    /// </summary>
    public float ChromaScale { get; }

    /// <summary>
    /// Gets the largest encoded luma sample value.
    /// </summary>
    public float LumaSampleMaximum { get; }

    /// <summary>
    /// Gets the largest encoded chroma sample value.
    /// </summary>
    public float ChromaSampleMaximum { get; }

    /// <summary>
    /// Gets the reversible transform's RGB code-value bias.
    /// </summary>
    public float RgbBias { get; }

    /// <summary>
    /// Gets the reversible transform's RGB code-value range.
    /// </summary>
    public float RgbScale { get; }

    /// <summary>
    /// Gets the largest reversible transform RGB code value.
    /// </summary>
    public float RgbSampleMaximum { get; }

    /// <summary>
    /// Resolves the H.273 matrix coefficients and sample ranges shared by HEVC and AV1 image items.
    /// </summary>
    /// <param name="colorPrimaries">The H.273 color-primary code point.</param>
    /// <param name="transferCharacteristics">The H.273 transfer-characteristic code point.</param>
    /// <param name="matrixCoefficients">The H.273 matrix-coefficient code point.</param>
    /// <param name="isFullRange">Whether encoded components use their complete numeric range.</param>
    /// <param name="lumaBitDepth">The encoded luma precision in bits.</param>
    /// <param name="chromaBitDepth">The encoded chroma precision in bits.</param>
    /// <param name="isMonochrome">Whether the image contains only luma samples.</param>
    /// <param name="hasFullResolutionChroma">Whether both chroma planes have luma resolution.</param>
    /// <param name="mode">The resolved conversion operation.</param>
    /// <returns>The immutable values used by each scalar and SIMD conversion lane.</returns>
    public static HeifColorConversionParameters Create(
        CicpColorPrimaries colorPrimaries,
        CicpTransferCharacteristics transferCharacteristics,
        CicpMatrixCoefficients matrixCoefficients,
        bool isFullRange,
        int lumaBitDepth,
        int chromaBitDepth,
        bool isMonochrome,
        bool hasFullResolutionChroma,
        out HeifColorConversionMode mode)
    {
        mode = HeifColorConversionMode.Coefficients;
        float kr = 0F;
        float kb = 0F;

        // H.273 assigns one closed arithmetic operation to each non-reserved matrix code point. Resolving the
        // operation once keeps both codec wrappers and every vector lane free from per-sample format dispatch.
        switch (matrixCoefficients)
        {
            case CicpMatrixCoefficients.Identity:
                mode = HeifColorConversionMode.Identity;
                break;
            case CicpMatrixCoefficients.ItuRBt709_6:
                kr = 0.2126F;
                kb = 0.0722F;
                break;
            case CicpMatrixCoefficients.Fcc47:
                kr = 0.30F;
                kb = 0.11F;
                break;
            case CicpMatrixCoefficients.ItuRBt601_7_625:
            case CicpMatrixCoefficients.ItuRBt601_7_525:
            case CicpMatrixCoefficients.Unspecified:
                kr = 0.299F;
                kb = 0.114F;
                break;
            case CicpMatrixCoefficients.SmpteSt240:
                kr = 0.212F;
                kb = 0.087F;
                break;
            case CicpMatrixCoefficients.YCgCo:
                mode = HeifColorConversionMode.YCgCo;
                break;
            case CicpMatrixCoefficients.ItuRBt2020_2_Ncl:
                kr = 0.2627F;
                kb = 0.0593F;
                break;
            case CicpMatrixCoefficients.ItuRBt2020_2_Cl:
                mode = HeifColorConversionMode.ConstantLuminance;
                kr = 0.2627F;
                kb = 0.0593F;
                break;
            case CicpMatrixCoefficients.SmpteSt2085:
                mode = HeifColorConversionMode.Smpte2085;
                break;
            case CicpMatrixCoefficients.ChromaDerivedNcl:
                GetChromaticityDerivedCoefficients(colorPrimaries, out kr, out kb);
                break;
            case CicpMatrixCoefficients.ChromaDerivedCl:
                mode = HeifColorConversionMode.ConstantLuminance;
                GetChromaticityDerivedCoefficients(colorPrimaries, out kr, out kb);
                break;
            case CicpMatrixCoefficients.ICtCp:
                mode = HeifColorConversionMode.ICtCp;
                break;
            case CicpMatrixCoefficients.IptC2:
                mode = HeifColorConversionMode.IptC2;
                break;
            case CicpMatrixCoefficients.YCgCoRe:
            case CicpMatrixCoefficients.YCgCoRo:
                mode = HeifColorConversionMode.YCgCoReversible;
                break;
            default:
                throw new InvalidImageContentException($"The image declares reserved H.273 matrix coefficients '{(byte)matrixCoefficients}'.");
        }

        bool requiresFullResolutionChroma = mode is HeifColorConversionMode.Identity or HeifColorConversionMode.YCgCoReversible;
        if (requiresFullResolutionChroma && !isMonochrome && !hasFullResolutionChroma)
        {
            throw new InvalidImageContentException($"H.273 matrix coefficients '{matrixCoefficients}' require 4:4:4 sampling.");
        }

        bool requiresEqualBitDepth = mode is HeifColorConversionMode.Identity or HeifColorConversionMode.YCgCoReversible;
        if (requiresEqualBitDepth && !isMonochrome && lumaBitDepth != chromaBitDepth)
        {
            throw new InvalidImageContentException($"H.273 matrix coefficients '{matrixCoefficients}' require equal component bit depths.");
        }

        float kg = 1F - kr - kb;
        int lumaDepthScale = 1 << (lumaBitDepth - 8);
        int chromaDepthScale = 1 << (chromaBitDepth - 8);
        float lumaSampleMaximum = (1 << lumaBitDepth) - 1;
        float chromaSampleMaximum = (1 << chromaBitDepth) - 1;
        float chromaBias = 128F * chromaDepthScale;
        float lumaBias = isFullRange ? 0F : 16F * lumaDepthScale;
        float lumaScale = isFullRange ? lumaSampleMaximum : 219F * lumaDepthScale;

        // Limited-range YCgCo first range-adjusts RGB through the luma range, so its difference components use
        // 219 codes. Conventional YCbCr uses the independently specified 224-code chroma excursion.
        float chromaScale = isFullRange
            ? chromaSampleMaximum
            : (mode == HeifColorConversionMode.YCgCo ? 219F : 224F) * chromaDepthScale;

        float rgbBias = 0F;
        float rgbScale = 1F;
        float rgbSampleMaximum = 1F;
        if (mode == HeifColorConversionMode.YCgCoReversible)
        {
            int bitOffset = matrixCoefficients == CicpMatrixCoefficients.YCgCoRe ? 2 : 1;
            int rgbBitDepth = lumaBitDepth - bitOffset;
            float rgbDepthScale = MathF.ScaleB(1F, rgbBitDepth - 8);
            rgbSampleMaximum = (1 << rgbBitDepth) - 1;
            rgbBias = isFullRange ? 0F : 16F * rgbDepthScale;
            rgbScale = isFullRange ? rgbSampleMaximum : 219F * rgbDepthScale;

            // The reversible lifting transform works on integer code values. It performs RGB range adjustment
            // internally, while the row traversal normalizes all three encoded components to their full code range.
            lumaBias = 0F;
            lumaScale = lumaSampleMaximum;
            chromaScale = chromaSampleMaximum;
        }

        HeifConstantLuminanceScales constantLuminanceScales = mode == HeifColorConversionMode.ConstantLuminance
            ? new HeifConstantLuminanceScales(transferCharacteristics, kr, kb)
            : default;

        return new HeifColorConversionParameters(
            kr,
            kg,
            kb,
            matrixCoefficients,
            isFullRange,
            transferCharacteristics,
            in constantLuminanceScales,
            lumaBias,
            lumaScale,
            chromaBias,
            chromaScale,
            lumaSampleMaximum,
            chromaSampleMaximum,
            rgbBias,
            rgbScale,
            rgbSampleMaximum);
    }

    /// <summary>
    /// Computes the luma coefficients defined by an H.273 primary-chromaticity code point.
    /// </summary>
    /// <param name="colorPrimaries">The H.273 color-primary code point.</param>
    /// <param name="kr">The resulting red luma coefficient.</param>
    /// <param name="kb">The resulting blue luma coefficient.</param>
    private static void GetChromaticityDerivedCoefficients(CicpColorPrimaries colorPrimaries, out float kr, out float kb)
    {
        float redX;
        float redY;
        float greenX;
        float greenY;
        float blueX;
        float blueY;
        float whiteX;
        float whiteY;

        switch (colorPrimaries)
        {
            case CicpColorPrimaries.ItuRBt470_6M:
                redX = 0.67F;
                redY = 0.33F;
                greenX = 0.21F;
                greenY = 0.71F;
                blueX = 0.14F;
                blueY = 0.08F;
                whiteX = 0.310F;
                whiteY = 0.316F;
                break;
            case CicpColorPrimaries.ItuRBt601_7_625:
                redX = 0.64F;
                redY = 0.33F;
                greenX = 0.29F;
                greenY = 0.60F;
                blueX = 0.15F;
                blueY = 0.06F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case CicpColorPrimaries.ItuRBt601_7_525:
            case CicpColorPrimaries.SmpteSt240:
                redX = 0.630F;
                redY = 0.340F;
                greenX = 0.310F;
                greenY = 0.595F;
                blueX = 0.155F;
                blueY = 0.070F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case CicpColorPrimaries.GenericFilm:
                redX = 0.681F;
                redY = 0.319F;
                greenX = 0.243F;
                greenY = 0.692F;
                blueX = 0.145F;
                blueY = 0.049F;
                whiteX = 0.310F;
                whiteY = 0.316F;
                break;
            case CicpColorPrimaries.ItuRBt2020_2:
                redX = 0.708F;
                redY = 0.292F;
                greenX = 0.170F;
                greenY = 0.797F;
                blueX = 0.131F;
                blueY = 0.046F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case CicpColorPrimaries.SmpteSt428_1:
                redX = 1F;
                redY = 0F;
                greenX = 0F;
                greenY = 1F;
                blueX = 0F;
                blueY = 0F;
                whiteX = 1F / 3F;
                whiteY = 1F / 3F;
                break;
            case CicpColorPrimaries.SmpteRp431_2:
                redX = 0.680F;
                redY = 0.320F;
                greenX = 0.265F;
                greenY = 0.690F;
                blueX = 0.150F;
                blueY = 0.060F;
                whiteX = 0.314F;
                whiteY = 0.351F;
                break;
            case CicpColorPrimaries.SmpteEg432_1:
                redX = 0.680F;
                redY = 0.320F;
                greenX = 0.265F;
                greenY = 0.690F;
                blueX = 0.150F;
                blueY = 0.060F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            case CicpColorPrimaries.EbuTech3213E:
                redX = 0.630F;
                redY = 0.340F;
                greenX = 0.295F;
                greenY = 0.605F;
                blueX = 0.155F;
                blueY = 0.077F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
            default:
                // Unspecified primaries cannot define a chromaticity-derived matrix. The established libavif
                // behavior supplies BT.709/D65 so the image has one deterministic interpretation.
                redX = 0.64F;
                redY = 0.33F;
                greenX = 0.30F;
                greenY = 0.60F;
                blueX = 0.15F;
                blueY = 0.06F;
                whiteX = 0.3127F;
                whiteY = 0.3290F;
                break;
        }

        float redZ = 1F - (redX + redY);
        float greenZ = 1F - (greenX + greenY);
        float blueZ = 1F - (blueX + blueY);
        float whiteZ = 1F - (whiteX + whiteY);

        // H.273 equations 39 and 40 solve the RGB-to-XYZ primary matrix at the signaled white point. Expanding the
        // determinant avoids a general matrix inversion and gives both codec paths the same rounding sequence.
        float denominator = whiteY *
            ((redX * ((greenY * blueZ) - (blueY * greenZ))) +
             (greenX * ((blueY * redZ) - (redY * blueZ))) +
             (blueX * ((redY * greenZ) - (greenY * redZ))));

        kr = (redY *
            ((whiteX * ((greenY * blueZ) - (blueY * greenZ))) +
             (whiteY * ((blueX * greenZ) - (greenX * blueZ))) +
             (whiteZ * ((greenX * blueY) - (blueX * greenY))))) / denominator;

        kb = (blueY *
            ((whiteX * ((redY * greenZ) - (greenY * redZ))) +
             (whiteY * ((greenX * redZ) - (redX * greenZ))) +
             (whiteZ * ((redX * greenY) - (greenX * redY))))) / denominator;
    }
}

/// <summary>
/// Stores the H.273 chroma normalization constants for constant-luminance conversion.
/// </summary>
internal readonly struct HeifConstantLuminanceScales
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifConstantLuminanceScales"/> struct.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    public HeifConstantLuminanceScales(CicpTransferCharacteristics transferCharacteristics, float kr, float kb)
    {
        this.NegativeBlue = HeifTransferFunctions.ToGamma(transferCharacteristics, 1F - kb);
        this.PositiveBlue = 1F - HeifTransferFunctions.ToGamma(transferCharacteristics, kb);
        this.NegativeRed = HeifTransferFunctions.ToGamma(transferCharacteristics, 1F - kr);
        this.PositiveRed = 1F - HeifTransferFunctions.ToGamma(transferCharacteristics, kr);
    }

    /// <summary>
    /// Gets the negative blue-difference scale.
    /// </summary>
    public float NegativeBlue { get; }

    /// <summary>
    /// Gets the positive blue-difference scale.
    /// </summary>
    public float PositiveBlue { get; }

    /// <summary>
    /// Gets the negative red-difference scale.
    /// </summary>
    public float NegativeRed { get; }

    /// <summary>
    /// Gets the positive red-difference scale.
    /// </summary>
    public float PositiveRed { get; }
}
