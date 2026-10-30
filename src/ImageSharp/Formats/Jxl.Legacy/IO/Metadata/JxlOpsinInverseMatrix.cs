// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Stores the inverse XYB matrix and bias values supplied by the JPEG XL transform header.
/// </summary>
internal sealed class JxlOpsinInverseMatrix : IJxlFields
{
    private InlineArray3<float> opsinBiases;
    private InlineArray4<float> quantBiases;
    private bool allDefault;
    private JxlMatrix3x3F inverseMatrix;

    /// <summary>
    /// Initializes the transform values to the exact bitstream defaults.
    /// </summary>
    public JxlOpsinInverseMatrix() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets the three signed opsin absorbance biases, in channel order.
    /// </summary>
    public InlineArray3<float> OpsinBiases
    {
        get => this.opsinBiases;
        set => this.opsinBiases = value;
    }

    /// <summary>
    /// Gets or sets the three channel quantization biases followed by the shared bias numerator.
    /// </summary>
    public InlineArray4<float> QuantBiases
    {
        get => this.quantBiases;
        set => this.quantBiases = value;
    }

    /// <summary>
    /// Gets or sets whether the wire header uses its one-bit default shortcut.
    /// </summary>
    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    /// <summary>
    /// Gets or sets the inverse opsin absorbance matrix in row-major order.
    /// </summary>
    public JxlMatrix3x3F InverseMatrix
    {
        get => this.inverseMatrix;
        set => this.inverseMatrix = value;
    }

    /// <summary>
    /// Creates the inverse color transform from the parsed matrix, biases and original reference intensity.
    /// </summary>
    /// <param name="intensityTarget">The positive reference intensity established by the image header, in nits.</param>
    /// <returns>The shared transform with the wire matrix transposed to Vector3.Transform order.</returns>
    public XybTransform CreateTransform(float intensityTarget)
    {
        ReadOnlySpan<float> values = this.inverseMatrix.AsReadOnlySpan();
        Matrix4x4 matrix = new(
            values[0], values[3], values[6], 0F,
            values[1], values[4], values[7], 0F,
            values[2], values[5], values[8], 0F,
            0F, 0F, 0F, 1F);

        return new(matrix, new Vector3(this.opsinBiases[0], this.opsinBiases[1], this.opsinBiases[2]), intensityTarget);
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            visitor.SetDefault(this);
            return true;
        }

        // The default matrix is frozen by the format. Recomputing an inverse can produce different rounded coefficients.
        Matrix4x4 defaultInverse = KnownXybTransforms.Default.Inverse;
        Span<float> inverse = this.inverseMatrix.AsSpan();

        for (int i = 0; i < inverse.Length; i++)
        {
            if (!visitor.F16(defaultInverse[i % 3, i / 3], ref inverse[i]))
            {
                return false;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (!visitor.F16(KnownXybTransforms.Default.Biases[i], ref this.opsinBiases[i]))
            {
                return false;
            }
        }

        // The fourth value is shared by all channels. It is serialized after the three channel biases, not an alpha value.
        for (int i = 0; i < 4; i++)
        {
            if (!visitor.F16(JxlQuantizer.DefaultQuantBias[i], ref this.quantBiases[i]))
            {
                return false;
            }
        }

        return true;
    }
}
