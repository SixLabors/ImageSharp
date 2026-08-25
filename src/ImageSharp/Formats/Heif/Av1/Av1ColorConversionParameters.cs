// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Stores the resolved H.273 values shared by every scalar and SIMD lane.
/// </summary>
internal readonly struct Av1ColorConversionParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ColorConversionParameters"/> struct.
    /// </summary>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kg">The green luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="constantLuminanceScales">The constant-luminance chroma scales.</param>
    /// <param name="lumaBias">The encoded luma bias.</param>
    /// <param name="lumaScale">The encoded luma range.</param>
    /// <param name="chromaBias">The encoded chroma midpoint.</param>
    /// <param name="chromaScale">The encoded chroma range.</param>
    public Av1ColorConversionParameters(
        float kr,
        float kg,
        float kb,
        ObuTransferCharacteristics transferCharacteristics,
        in Av1ConstantLuminanceScales constantLuminanceScales,
        float lumaBias,
        float lumaScale,
        float chromaBias,
        float chromaScale)
    {
        this.Kr = kr;
        this.Kg = kg;
        this.Kb = kb;
        this.RedChromaScale = 2F * (1F - kr);
        this.BlueChromaScale = 2F * (1F - kb);
        this.GreenRedChromaScale = 2F * kr * (1F - kr) / kg;
        this.GreenBlueChromaScale = 2F * kb * (1F - kb) / kg;
        this.TransferCharacteristics = transferCharacteristics;
        this.ConstantLuminanceScales = constantLuminanceScales;
        this.LumaBias = lumaBias;
        this.LumaScale = lumaScale;
        this.ChromaBias = chromaBias;
        this.ChromaScale = chromaScale;
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
    /// Gets the signaled transfer characteristics.
    /// </summary>
    public ObuTransferCharacteristics TransferCharacteristics { get; }

    /// <summary>
    /// Gets the constant-luminance chroma scales.
    /// </summary>
    public Av1ConstantLuminanceScales ConstantLuminanceScales { get; }

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
}

/// <summary>
/// Stores the H.273 chroma normalization constants for constant-luminance conversion.
/// </summary>
internal readonly struct Av1ConstantLuminanceScales
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ConstantLuminanceScales"/> struct.
    /// </summary>
    /// <param name="transferCharacteristics">The signaled transfer characteristics.</param>
    /// <param name="kr">The red luma coefficient.</param>
    /// <param name="kb">The blue luma coefficient.</param>
    public Av1ConstantLuminanceScales(ObuTransferCharacteristics transferCharacteristics, float kr, float kb)
    {
        this.NegativeBlue = Av1TransferFunctions.ToGamma(transferCharacteristics, 1F - kb);
        this.PositiveBlue = 1F - Av1TransferFunctions.ToGamma(transferCharacteristics, kb);
        this.NegativeRed = Av1TransferFunctions.ToGamma(transferCharacteristics, 1F - kr);
        this.PositiveRed = 1F - Av1TransferFunctions.ToGamma(transferCharacteristics, kr);
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
