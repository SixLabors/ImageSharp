// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Holds one fitted warp model and the points that agree with it.
/// </summary>
/// <remarks>
/// The parameters are held as real numbers here, which is what the fit produces. The encoder rounds
/// them to the precision the bitstream codes only once, when it decides to use the model.
/// Reference: MotionModel.
/// </remarks>
internal sealed class Av1MotionModel
{
    /// <summary>
    /// The parameters that every warp model carries.
    /// </summary>
    /// <remarks>Reference: MAX_PARAMDIM.</remarks>
    public const int ParameterCount = 6;

    private int[] inliers = [];

    /// <summary>
    /// Gets the six model parameters: two of translation, then the four of the warp matrix in row order.
    /// </summary>
    public double[] Parameters { get; } = [0.0, 0.0, 1.0, 0.0, 0.0, 1.0];

    /// <summary>
    /// Gets the positions of the agreeing points, as pairs of a column and a row.
    /// </summary>
    public ReadOnlySpan<int> Inliers => this.inliers.AsSpan(0, this.InlierCount * 2);

    /// <summary>
    /// Gets the points that agree with this model.
    /// </summary>
    public int InlierCount { get; private set; }

    /// <summary>
    /// Returns this model to the one that moves nothing.
    /// </summary>
    /// <remarks>Reference: kIdentityParams.</remarks>
    public void Reset()
    {
        this.Parameters[0] = 0.0;
        this.Parameters[1] = 0.0;
        this.Parameters[2] = 1.0;
        this.Parameters[3] = 0.0;
        this.Parameters[4] = 0.0;
        this.Parameters[5] = 1.0;
        this.InlierCount = 0;
    }

    /// <summary>
    /// Takes one fitted model together with the points that agree with it.
    /// </summary>
    /// <param name="parameters">The six fitted parameters.</param>
    /// <param name="points">Every correspondence that was offered to the fit.</param>
    /// <param name="inlierIndices">The correspondences that agree with the model.</param>
    public void Set(ReadOnlySpan<double> parameters, ReadOnlySpan<Av1Correspondence> points, ReadOnlySpan<int> inlierIndices)
    {
        parameters[..ParameterCount].CopyTo(this.Parameters);
        if (this.inliers.Length < inlierIndices.Length * 2)
        {
            this.inliers = new int[inlierIndices.Length * 2];
        }

        for (int i = 0; i < inlierIndices.Length; i++)
        {
            Av1Correspondence point = points[inlierIndices[i]];
            this.inliers[(2 * i) + 0] = (int)Math.Round(point.X, MidpointRounding.ToEven);
            this.inliers[(2 * i) + 1] = (int)Math.Round(point.Y, MidpointRounding.ToEven);
        }

        this.InlierCount = inlierIndices.Length;
    }
}
