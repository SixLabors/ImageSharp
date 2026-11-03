// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Defines the matrices, signed biases and intensity scale for the JPEG XL XYB color transform.
/// </summary>
/// <remarks>
/// <para>
/// The transform is defined by ISO/IEC 18181-1, the JPEG XL core coding system.
/// JPEG XL permits custom inverse coefficients and biases in the image header.
/// <see cref="KnownXybTransforms.Iso18181"/> provides the standard coefficients at an intensity target of 255 nits.
/// See the <see href="https://jpeg.org/jpegxl/workplan.html">JPEG XL specifications</see>.
/// </para>
/// <para>
/// Matrices use row-major coefficient order. Only their upper-left three by three coefficients are used.
/// Translation and the fourth component do not form part of the absorbance transform.
/// </para>
/// <para>
/// The matrices must match the RGB working space used to convert the XYB samples.
/// For conversion from XYB, select that space with <see cref="ColorConversionOptions.SourceRgbWorkingSpace"/>.
/// For conversion to XYB, select it with <see cref="ColorConversionOptions.TargetRgbWorkingSpace"/>.
/// The ISO/IEC 18181-1 coefficients require sRGB primaries and the D65 white point.
/// </para>
/// </remarks>
public readonly struct XybTransform
{
    /// <summary>
    /// The intensity, in nits, at which the standard absorbance coefficients have unit scale.
    /// </summary>
    internal const float ReferenceIntensity = 255F;

    /// <summary>
    /// The supplied or calculated forward matrix. A singular inverse remains usable for decoding.
    /// </summary>
    private readonly Matrix4x4? forward;

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> struct from an inverse absorbance matrix.
    /// </summary>
    /// <param name="inverse">The unscaled absorbance-to-linear-RGB matrix, in row-major coefficient order.</param>
    /// <param name="biases">The signed biases added after cubing during inverse conversion.</param>
    /// <param name="intensityTarget">The intensity represented by a linear RGB component of one, in nits.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The intensity target is zero, negative, NaN or infinite, or 255 divided by it exceeds <see cref="float.MaxValue"/>.
    /// </exception>
    public XybTransform(Matrix4x4 inverse, Vector3 biases, float intensityTarget)
        : this(null, inverse, biases, intensityTarget)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> struct with both absorbance matrices.
    /// </summary>
    /// <param name="forward">The unscaled linear-RGB-to-absorbance matrix, in row-major coefficient order.</param>
    /// <param name="inverse">The unscaled absorbance-to-linear-RGB matrix, in the same coefficient order.</param>
    /// <param name="biases">The signed biases added after cubing during inverse conversion.</param>
    /// <param name="intensityTarget">The intensity represented by a linear RGB component of one, in nits.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The intensity target is zero, negative, NaN or infinite, or 255 divided by it exceeds <see cref="float.MaxValue"/>.
    /// </exception>
    public XybTransform(Matrix4x4 forward, Matrix4x4 inverse, Vector3 biases, float intensityTarget)
        : this((Matrix4x4?)forward, inverse, biases, intensityTarget)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> struct with validated coefficients.
    /// </summary>
    /// <param name="forward">The supplied forward coefficients, or null to derive them from the inverse.</param>
    /// <param name="inverse">The inverse coefficients in row-major order.</param>
    /// <param name="biases">The signed inverse biases.</param>
    /// <param name="intensityTarget">The intensity represented by a linear RGB component of one, in nits.</param>
    private XybTransform(Matrix4x4? forward, Matrix4x4 inverse, Vector3 biases, float intensityTarget)
    {
        Guard.MustBeGreaterThan(intensityTarget, 0F, nameof(intensityTarget));
        Guard.MustBeLessThanOrEqualTo(intensityTarget, float.MaxValue, nameof(intensityTarget));

        // Matrix preparation divides the 255-nit reference by this intensity. Reject an unrepresentable
        // factor here, before finite coefficients or zero can become infinity or NaN during preparation.
        Guard.MustBeLessThanOrEqualTo(ReferenceIntensity / intensityTarget, float.MaxValue, nameof(intensityTarget));

        // XYB has three components and no translation. Complete the unused axis with an identity
        // so a valid three by three matrix remains invertible even when the supplied M44 is zero.
        inverse.M14 = inverse.M24 = inverse.M34 = 0F;
        inverse.M41 = inverse.M42 = inverse.M43 = 0F;
        inverse.M44 = 1F;
        this.Inverse = inverse;

        if (forward.HasValue)
        {
            // Keep supplied encoder coefficients. Rounded decoder coefficients need not invert back
            // to those exact values, so calculating another inverse would lose their precision.
            Matrix4x4 supplied = forward.Value;
            supplied.M14 = supplied.M24 = supplied.M34 = 0F;
            supplied.M41 = supplied.M42 = supplied.M43 = 0F;
            supplied.M44 = 1F;
            this.forward = supplied;
        }
        else
        {
            // Custom JPEG XL headers supply only the inverse. Derive the forward once, not per row.
            // A singular inverse can still decode; only an attempted forward conversion must fail.
            this.forward = Matrix4x4.Invert(inverse, out Matrix4x4 calculated) ? calculated : null;
        }

        this.Biases = biases;
        this.IntensityTarget = intensityTarget;
        this.CubeRootBiases = new(MathF.Cbrt(biases.X), MathF.Cbrt(biases.Y), MathF.Cbrt(biases.Z));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> struct with prepared XYZ connection coefficients and forward row order.
    /// </summary>
    /// <param name="source">The immutable public transform.</param>
    /// <param name="rgbToXyz">The source working space's linear RGB-to-XYZ matrix in row-vector layout.</param>
    /// <param name="xyzToRgb">The target working space's XYZ-to-linear-RGB matrix in row-vector layout.</param>
    private XybTransform(XybTransform source, Matrix4x4 rgbToXyz, Matrix4x4 xyzToRgb)
    {
        // Public matrices contain row-major coefficients. Transpose them for System.Numerics row vectors,
        // then scale relative RGB so one represents IntensityTarget nits. Compose the XYZ connection here,
        // when options prepare the transform, rather than multiplying matrices for every scalar color or bulk block.
        Matrix4x4 inverse = Matrix4x4.Transpose(source.Inverse) * (ReferenceIntensity / source.IntensityTarget);
        this.Inverse = inverse * rgbToXyz;

        if (source.forward.HasValue)
        {
            Matrix4x4 matrix = xyzToRgb * (Matrix4x4.Transpose(source.forward.Value) * (source.IntensityTarget / ReferenceIntensity));

            // Forward absorbance adds the negative bias, then Z, Y and X. Arrange those four coefficient
            // rows once for homogeneous input [1,Z,Y,X]. The shared matrix operation can then retain that
            // evaluation order without a separate dot product or per-color coefficient permutation.
            this.forward = new Matrix4x4(
                -source.Biases.X,
                -source.Biases.Y,
                -source.Biases.Z,
                0F,
                matrix.M31,
                matrix.M32,
                matrix.M33,
                0F,
                matrix.M21,
                matrix.M22,
                matrix.M23,
                0F,
                matrix.M11,
                matrix.M12,
                matrix.M13,
                1F);
        }
        else
        {
            this.forward = null;
        }

        this.Biases = source.Biases;
        this.IntensityTarget = source.IntensityTarget;
        this.CubeRootBiases = source.CubeRootBiases;
    }

    /// <summary>
    /// Gets the unscaled linear-RGB-to-absorbance matrix in row-major coefficient order.
    /// </summary>
    /// <exception cref="InvalidOperationException">The inverse matrix is singular and no forward matrix was supplied.</exception>
    public Matrix4x4 Forward
        => this.forward ?? throw new InvalidOperationException("The inverse XYB matrix is singular and has no forward transform.");

    /// <summary>
    /// Gets the unscaled absorbance-to-linear-RGB matrix in row-major coefficient order.
    /// </summary>
    public Matrix4x4 Inverse { get; }

    /// <summary>
    /// Gets the signed biases added after cubing during inverse conversion.
    /// </summary>
    public Vector3 Biases { get; }

    /// <summary>
    /// Gets the intensity represented by unit linear RGB, in nits.
    /// </summary>
    public float IntensityTarget { get; }

    /// <summary>
    /// Gets the signed cube roots removed before cubing and added after forward root evaluation.
    /// </summary>
    internal Vector3 CubeRootBiases { get; }

    /// <summary>
    /// Prepares both XYZ connection directions in the matrix layout and intensity scale used by shared arithmetic.
    /// </summary>
    /// <param name="rgbToXyz">The source working space's linear RGB-to-XYZ matrix in row-vector layout.</param>
    /// <param name="xyzToRgb">The target working space's XYZ-to-linear-RGB matrix in row-vector layout.</param>
    /// <returns>The prepared immutable transform.</returns>
    internal XybTransform Prepare(Matrix4x4 rgbToXyz, Matrix4x4 xyzToRgb) => new(this, rgbToXyz, xyzToRgb);
}
