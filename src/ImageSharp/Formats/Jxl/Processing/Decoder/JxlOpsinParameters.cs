// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>Stores operation-wide coefficients for the inverse opsin transform.</summary>
internal sealed class JxlOpsinParameters
{
    /// <summary>Gets the intensity-scaled inverse matrix in the convention used by Vector3.Transform and SimdUtils.Transform3.</summary>
    public Matrix4x4 InverseOpsinMatrix { get; private set; }

    /// <summary>Gets the three negative absorbance biases added after cubing.</summary>
    public Vector3 OpsinBiases { get; private set; }

    /// <summary>Gets the signed cube roots subtracted before cubing.</summary>
    public Vector3 OpsinBiasesCbrt { get; private set; }

    /// <summary>Gets or sets the three coefficient biases and shared numerator used by coefficient reconstruction.</summary>
    public InlineArray4<float> QuantBiases { get; set; }

    /// <summary>Prepares the selected inverse matrix, negative biases and intensity scale before row processing.</summary>
    /// <param name="inverseMatrix">The selected row-major inverse opsin matrix.</param>
    /// <param name="biases">The three negative absorbance biases from the transform header.</param>
    /// <param name="intensityTarget">The positive original intensity target, in nits, established by the image header.</param>
    public void Initialize(in JxlMatrix3x3F inverseMatrix, InlineArray3<float> biases, float intensityTarget)
    {
        // XYB uses absolute intensity. The matrix converts it to relative RGB, where one equals the original intensity target.
        // Transpose the row-major 3 by 3 coefficients once. SIMD broadcasts each scalar; it needs no replicated coefficient bank.
        ReadOnlySpan<float> matrix = inverseMatrix.AsReadOnlySpan();
        float scale = 255F / intensityTarget;
        this.InverseOpsinMatrix = new(
            matrix[0] * scale,
            matrix[3] * scale,
            matrix[6] * scale,
            0F,
            matrix[1] * scale,
            matrix[4] * scale,
            matrix[7] * scale,
            0F,
            matrix[2] * scale,
            matrix[5] * scale,
            matrix[8] * scale,
            0F,
            0F,
            0F,
            0F,
            1F);

        this.OpsinBiases = new(biases[0], biases[1], biases[2]);
        this.OpsinBiasesCbrt = new(MathF.Cbrt(biases[0]), MathF.Cbrt(biases[1]), MathF.Cbrt(biases[2]));
    }
}
