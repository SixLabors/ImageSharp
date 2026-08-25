// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the H.273 matrix operation used between encoded planes and RGB components.
/// </summary>
internal enum Av1ColorConversionMode
{
    /// <summary>
    /// A coefficient-based YCbCr matrix conversion.
    /// </summary>
    Coefficients,

    /// <summary>
    /// Direct G, B, and R component mapping from the Y, U, and V planes.
    /// </summary>
    Identity,

    /// <summary>
    /// The reversible-style YCgCo color transform.
    /// </summary>
    YCgCo,

    /// <summary>
    /// The SMPTE ST 2085 YDzDx color transform.
    /// </summary>
    Smpte2085,

    /// <summary>
    /// A constant-luminance transform using the signaled transfer characteristics.
    /// </summary>
    ConstantLuminance,

    /// <summary>
    /// The BT.2100 ICtCp color transform.
    /// </summary>
    ICtCp,
}

/// <summary>
/// Converts normalized component planes between an AV1 color model and RGB.
/// </summary>
internal abstract partial class Av1ColorConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1ColorConverterBase"/> class.
    /// </summary>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma samples.</param>
    protected Av1ColorConverterBase(in Av1ColorConversionParameters parameters, bool isMonochrome)
    {
        this.Parameters = parameters;
        this.IsMonochrome = isMonochrome;
    }

    /// <summary>
    /// Gets the resolved H.273 conversion parameters.
    /// </summary>
    protected Av1ColorConversionParameters Parameters { get; }

    /// <summary>
    /// Gets a value indicating whether the frame contains only luma samples.
    /// </summary>
    protected bool IsMonochrome { get; }

    /// <summary>
    /// Gets the scale used to encode luma components.
    /// </summary>
    public float LumaScale => this.Parameters.LumaScale;

    /// <summary>
    /// Gets the bias used to encode luma components.
    /// </summary>
    public float LumaBias => this.Parameters.LumaBias;

    /// <summary>
    /// Gets the scale used to encode chroma components.
    /// </summary>
    public abstract float ChromaScale { get; }

    /// <summary>
    /// Gets the bias used to encode chroma components.
    /// </summary>
    public abstract float ChromaBias { get; }

    /// <summary>
    /// Converts normalized AV1 components to normalized RGB in place.
    /// </summary>
    /// <param name="component0">The luma or first color component, replaced by red.</param>
    /// <param name="component1">The first chroma or second color component, replaced by green.</param>
    /// <param name="component2">The second chroma or third color component, replaced by blue.</param>
    public abstract void ConvertToRgbInPlace(Span<float> component0, Span<float> component1, Span<float> component2);

    /// <summary>
    /// Converts normalized RGB components to normalized AV1 components in place.
    /// </summary>
    /// <param name="component0">The red component, replaced by luma or the first color component.</param>
    /// <param name="component1">The green component, replaced by the first chroma or second color component.</param>
    /// <param name="component2">The blue component, replaced by the second chroma or third color component.</param>
    /// <param name="maximumValue">The largest value in the RGB component planes.</param>
    public abstract void ConvertFromRgbInPlace(
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        float maximumValue);

    /// <summary>
    /// Creates the converter selected by the resolved H.273 matrix and transfer characteristics.
    /// </summary>
    /// <param name="mode">The resolved matrix conversion mode.</param>
    /// <param name="parameters">The resolved H.273 conversion parameters.</param>
    /// <param name="isMonochrome">Whether the frame contains only luma samples.</param>
    /// <returns>The selected converter.</returns>
    public static Av1ColorConverterBase Create(Av1ColorConversionMode mode, in Av1ColorConversionParameters parameters, bool isMonochrome)
        => mode switch
        {
            Av1ColorConversionMode.Identity => new Av1ColorConverter<Av1IdentityColorOperator>(in parameters, isMonochrome),
            Av1ColorConversionMode.YCgCo => new Av1ColorConverter<Av1YCgCoColorOperator>(in parameters, isMonochrome),
            Av1ColorConversionMode.Smpte2085 => new Av1ColorConverter<Av1Smpte2085ColorOperator>(in parameters, isMonochrome),
            Av1ColorConversionMode.ConstantLuminance => new Av1ColorConverter<Av1ConstantLuminanceColorOperator>(in parameters, isMonochrome),
            Av1ColorConversionMode.ICtCp => new Av1ColorConverter<Av1ICtCpColorOperator>(in parameters, isMonochrome),
            _ => new Av1ColorConverter<Av1CoefficientColorOperator>(in parameters, isMonochrome),
        };
}
