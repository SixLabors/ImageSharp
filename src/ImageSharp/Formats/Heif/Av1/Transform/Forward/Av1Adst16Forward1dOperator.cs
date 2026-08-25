// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Defines the 16-point AV1 forward asymmetric discrete sine transform operator.
/// </summary>
internal readonly partial struct Av1Adst16Forward1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative 16-point AV1 forward asymmetric discrete sine transform.
    /// </summary>
    /// <param name="input">The sixteen spatial-domain residual values.</param>
    /// <param name="output">The sixteen frequency-domain coefficients.</param>
    /// <param name="step">The sixteen-element stage buffer owned by the containing two-dimensional transform.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        // The range table is consumed by coefficient-range-checking builds of libaom. ImageSharp preserves the same
        // staged arithmetic, while its production path relies on the bit-depth and shift invariants established by
        // the two-dimensional transform configuration.
        _ = stageRange;

        // Reordering and alternating signs express the ADST as progressively wider symmetric butterflies.
        output[0] = input[0];
        output[1] = -input[15];
        output[2] = -input[7];
        output[3] = input[8];
        output[4] = -input[3];
        output[5] = input[12];
        output[6] = input[4];
        output[7] = -input[11];
        output[8] = -input[1];
        output[9] = input[14];
        output[10] = input[6];
        output[11] = -input[9];
        output[12] = input[2];
        output[13] = -input[13];
        output[14] = -input[5];
        output[15] = input[10];

        // Rotate four independent pairs by pi/4 so the following butterflies can double their span.
        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        step[0] = output[0];
        step[1] = output[1];
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], cospi[32], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[32], output[2], -cospi[32], output[3], cosBit);
        step[4] = output[4];
        step[5] = output[5];
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], cospi[32], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[32], output[6], -cospi[32], output[7], cosBit);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], cospi[32], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[32], output[10], -cospi[32], output[11], cosBit);
        step[12] = output[12];
        step[13] = output[13];
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[32], output[14], cospi[32], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[32], output[14], -cospi[32], output[15], cosBit);

        // Combine adjacent rotated pairs into four-sample butterflies.
        output[0] = step[0] + step[2];
        output[1] = step[1] + step[3];
        output[2] = step[0] - step[2];
        output[3] = step[1] - step[3];
        output[4] = step[4] + step[6];
        output[5] = step[5] + step[7];
        output[6] = step[4] - step[6];
        output[7] = step[5] - step[7];
        output[8] = step[8] + step[10];
        output[9] = step[9] + step[11];
        output[10] = step[8] - step[10];
        output[11] = step[9] - step[11];
        output[12] = step[12] + step[14];
        output[13] = step[13] + step[15];
        output[14] = step[12] - step[14];
        output[15] = step[13] - step[15];

        // Rotate the upper half of each eight-sample group by pi/8 and 3pi/8.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[16], output[4], cospi[48], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[48], output[4], -cospi[16], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[6], cospi[16], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[16], output[6], cospi[48], output[7], cosBit);
        step[8] = output[8];
        step[9] = output[9];
        step[10] = output[10];
        step[11] = output[11];
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[16], output[12], cospi[48], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[48], output[12], -cospi[16], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[48], output[14], cospi[16], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[16], output[14], cospi[48], output[15], cosBit);

        // Merge the four-sample groups into two eight-sample butterflies.
        output[0] = step[0] + step[4];
        output[1] = step[1] + step[5];
        output[2] = step[2] + step[6];
        output[3] = step[3] + step[7];
        output[4] = step[0] - step[4];
        output[5] = step[1] - step[5];
        output[6] = step[2] - step[6];
        output[7] = step[3] - step[7];
        output[8] = step[8] + step[12];
        output[9] = step[9] + step[13];
        output[10] = step[10] + step[14];
        output[11] = step[11] + step[15];
        output[12] = step[8] - step[12];
        output[13] = step[9] - step[13];
        output[14] = step[10] - step[14];
        output[15] = step[11] - step[15];

        // Rotate the upper eight coefficients with the pi/16 odd-angle pairs.
        step[0] = output[0];
        step[1] = output[1];
        step[2] = output[2];
        step[3] = output[3];
        step[4] = output[4];
        step[5] = output[5];
        step[6] = output[6];
        step[7] = output[7];
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[8], output[8], cospi[56], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[56], output[8], -cospi[8], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[40], output[10], cospi[24], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[24], output[10], -cospi[40], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(-cospi[56], output[12], cospi[8], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[8], output[12], cospi[56], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(-cospi[24], output[14], cospi[40], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[40], output[14], cospi[24], output[15], cosBit);

        // Merge both eight-sample halves into the complete sixteen-sample butterfly.
        output[0] = step[0] + step[8];
        output[1] = step[1] + step[9];
        output[2] = step[2] + step[10];
        output[3] = step[3] + step[11];
        output[4] = step[4] + step[12];
        output[5] = step[5] + step[13];
        output[6] = step[6] + step[14];
        output[7] = step[7] + step[15];
        output[8] = step[0] - step[8];
        output[9] = step[1] - step[9];
        output[10] = step[2] - step[10];
        output[11] = step[3] - step[11];
        output[12] = step[4] - step[12];
        output[13] = step[5] - step[13];
        output[14] = step[6] - step[14];
        output[15] = step[7] - step[15];

        // Apply the terminal odd-frequency rotations that define the ADST basis vectors.
        step[0] = Av1Transform1dMath.HalfButterfly(cospi[2], output[0], cospi[62], output[1], cosBit);
        step[1] = Av1Transform1dMath.HalfButterfly(cospi[62], output[0], -cospi[2], output[1], cosBit);
        step[2] = Av1Transform1dMath.HalfButterfly(cospi[10], output[2], cospi[54], output[3], cosBit);
        step[3] = Av1Transform1dMath.HalfButterfly(cospi[54], output[2], -cospi[10], output[3], cosBit);
        step[4] = Av1Transform1dMath.HalfButterfly(cospi[18], output[4], cospi[46], output[5], cosBit);
        step[5] = Av1Transform1dMath.HalfButterfly(cospi[46], output[4], -cospi[18], output[5], cosBit);
        step[6] = Av1Transform1dMath.HalfButterfly(cospi[26], output[6], cospi[38], output[7], cosBit);
        step[7] = Av1Transform1dMath.HalfButterfly(cospi[38], output[6], -cospi[26], output[7], cosBit);
        step[8] = Av1Transform1dMath.HalfButterfly(cospi[34], output[8], cospi[30], output[9], cosBit);
        step[9] = Av1Transform1dMath.HalfButterfly(cospi[30], output[8], -cospi[34], output[9], cosBit);
        step[10] = Av1Transform1dMath.HalfButterfly(cospi[42], output[10], cospi[22], output[11], cosBit);
        step[11] = Av1Transform1dMath.HalfButterfly(cospi[22], output[10], -cospi[42], output[11], cosBit);
        step[12] = Av1Transform1dMath.HalfButterfly(cospi[50], output[12], cospi[14], output[13], cosBit);
        step[13] = Av1Transform1dMath.HalfButterfly(cospi[14], output[12], -cospi[50], output[13], cosBit);
        step[14] = Av1Transform1dMath.HalfButterfly(cospi[58], output[14], cospi[6], output[15], cosBit);
        step[15] = Av1Transform1dMath.HalfButterfly(cospi[6], output[14], -cospi[58], output[15], cosBit);

        // Permute the rotated values into AV1 coefficient order.
        output[0] = step[1];
        output[1] = step[14];
        output[2] = step[3];
        output[3] = step[12];
        output[4] = step[5];
        output[5] = step[10];
        output[6] = step[7];
        output[7] = step[8];
        output[8] = step[9];
        output[9] = step[6];
        output[10] = step[11];
        output[11] = step[4];
        output[12] = step[13];
        output[13] = step[2];
        output[14] = step[15];
        output[15] = step[0];
    }
}
