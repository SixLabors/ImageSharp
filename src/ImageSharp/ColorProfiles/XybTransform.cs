// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Defines the absorbance matrices, signed biases and reference intensity used by an XYB color transform.
/// </summary>
/// <remarks>
/// Only the upper-left three by three coefficients of each supplied matrix are used.
/// Translation and the fourth component do not form part of the absorbance transform.
/// </remarks>
public sealed class XybTransform
{
    /// <summary>
    /// The reference intensity, in nits, at which the unscaled absorbance matrices operate.
    /// </summary>
    internal const float ReferenceIntensity = 255F;

    /// <summary>
    /// The supplied forward matrix, when one is available. Decoder metadata needs only the inverse matrix.
    /// </summary>
    private readonly Matrix4x4? forward;

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> class from an inverse absorbance matrix.
    /// </summary>
    /// <param name="inverse">
    /// The unscaled absorbance-to-linear-RGB coefficients, in <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> order.
    /// Only the upper-left three by three coefficients are used.
    /// </param>
    /// <param name="biases">The signed biases added after cubing during inverse conversion.</param>
    /// <param name="intensityTarget">The positive finite reference intensity, in nits.</param>
    /// <exception cref="ArgumentOutOfRangeException">The reference intensity is not positive and finite.</exception>
    public XybTransform(Matrix4x4 inverse, Vector3 biases, float intensityTarget)
    {
        if (!(intensityTarget > 0F && float.IsFinite(intensityTarget)))
        {
            throw new ArgumentOutOfRangeException(nameof(intensityTarget), "The reference intensity must be positive and finite.");
        }

        // Absorbance has three components and no translation. Complete the unused fourth dimension with an identity axis,
        // so a valid three by three matrix remains invertible even when the caller supplied zero for M44.
        inverse.M14 = inverse.M24 = inverse.M34 = 0F;
        inverse.M41 = inverse.M42 = inverse.M43 = 0F;
        inverse.M44 = 1F;
        this.forward = null;
        this.Inverse = inverse;
        this.Biases = biases;
        this.IntensityTarget = intensityTarget;
        this.CubeRootBiases = new(MathF.Cbrt(biases.X), MathF.Cbrt(biases.Y), MathF.Cbrt(biases.Z));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XybTransform"/> class with both absorbance matrices.
    /// </summary>
    /// <param name="forward">
    /// The unscaled linear-RGB-to-absorbance coefficients, in <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> order.
    /// Only the upper-left three by three coefficients are used.
    /// </param>
    /// <param name="inverse">The unscaled absorbance-to-linear-RGB coefficients in the same three by three matrix convention.</param>
    /// <param name="biases">The signed biases added after cubing during inverse conversion.</param>
    /// <param name="intensityTarget">The positive finite reference intensity, in nits.</param>
    /// <exception cref="ArgumentOutOfRangeException">The reference intensity is not positive and finite.</exception>
    public XybTransform(Matrix4x4 forward, Matrix4x4 inverse, Vector3 biases, float intensityTarget)
        : this(inverse, biases, intensityTarget)
    {
        // Frozen encoder coefficients need not equal a numerical inverse of rounded decoder coefficients.
        // Give the supplied forward coefficients the same translation-free matrix layout as the inverse coefficients.
        forward.M14 = forward.M24 = forward.M34 = 0F;
        forward.M41 = forward.M42 = forward.M43 = 0F;
        forward.M44 = 1F;
        this.forward = forward;
    }

    /// <summary>
    /// Gets the unscaled linear-RGB-to-absorbance matrix.
    /// </summary>
    /// <exception cref="InvalidOperationException">No forward matrix was supplied and the inverse matrix is singular.</exception>
    public Matrix4x4 Forward
    {
        get
        {
            if (this.forward.HasValue)
            {
                return this.forward.Value;
            }

            // An inverse-only source remains valid for decoding, even if its matrix is singular.
            // Only an operation that requests forward conversion needs a mathematical inverse.
            if (!Matrix4x4.Invert(this.Inverse, out Matrix4x4 result))
            {
                throw new InvalidOperationException("The inverse XYB matrix is singular and has no forward transform.");
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the unscaled absorbance-to-linear-RGB matrix.
    /// </summary>
    public Matrix4x4 Inverse { get; }

    /// <summary>
    /// Gets the signed biases added after cubing during inverse conversion.
    /// </summary>
    public Vector3 Biases { get; }

    /// <summary>
    /// Gets the reference intensity, in nits.
    /// </summary>
    public float IntensityTarget { get; }

    /// <summary>
    /// Gets the signed cube roots removed before cubing and added after forward root evaluation.
    /// </summary>
    internal Vector3 CubeRootBiases { get; }

    /// <summary>
    /// Gets the inverse matrix scaled to relative RGB, where one equals the reference intensity.
    /// </summary>
    internal Matrix4x4 ScaledInverse => this.Inverse * (ReferenceIntensity / this.IntensityTarget);

    /// <summary>
    /// Gets the forward matrix scaled from relative RGB to the absolute XYB absorbance domain.
    /// </summary>
    internal Matrix4x4 ScaledForward => this.Forward * (this.IntensityTarget / ReferenceIntensity);
}
