// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// Provides helper methods for performing quad distortion transformations.
/// </summary>
internal static class QuadDistortionHelper
{
    /// <summary>
    /// Computes the projection matrix for a quad distortion transformation.
    /// </summary>
    /// <param name="rectangle">The source rectangle.</param>
    /// <param name="topLeft">The top-left point of the distorted quad.</param>
    /// <param name="topRight">The top-right point of the distorted quad.</param>
    /// <param name="bottomRight">The bottom-right point of the distorted quad.</param>
    /// <param name="bottomLeft">The bottom-left point of the distorted quad.</param>
    /// <returns>The computed projection matrix for the quad distortion.</returns>
    /// <remarks>
    /// This method is based on the algorithm described in the following article:
    /// https://blog.mbedded.ninja/mathematics/geometry/projective-transformations/
    /// </remarks>
    public static Matrix4x4 ComputeQuadDistortMatrix(Rectangle rectangle, PointF topLeft, PointF topRight, PointF bottomRight, PointF bottomLeft)
    {
        PointF p1 = new(rectangle.X, rectangle.Y);
        PointF p2 = new(rectangle.X + rectangle.Width, rectangle.Y);
        PointF p3 = new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
        PointF p4 = new(rectangle.X, rectangle.Y + rectangle.Height);

        PointF q1 = topLeft;
        PointF q2 = topRight;
        PointF q3 = bottomRight;
        PointF q4 = bottomLeft;

        float[][] matrixData =
        [
            [p1.X, p1.Y, 1, 0, 0, 0, -p1.X * q1.X, -p1.Y * q1.X],
            [0, 0, 0, p1.X, p1.Y, 1, -p1.X * q1.Y, -p1.Y * q1.Y],
            [p2.X, p2.Y, 1, 0, 0, 0, -p2.X * q2.X, -p2.Y * q2.X],
            [0, 0, 0, p2.X, p2.Y, 1, -p2.X * q2.Y, -p2.Y * q2.Y],
            [p3.X, p3.Y, 1, 0, 0, 0, -p3.X * q3.X, -p3.Y * q3.X],
            [0, 0, 0, p3.X, p3.Y, 1, -p3.X * q3.Y, -p3.Y * q3.Y],
            [p4.X, p4.Y, 1, 0, 0, 0, -p4.X * q4.X, -p4.Y * q4.X],
            [0, 0, 0, p4.X, p4.Y, 1, -p4.X * q4.Y, -p4.Y * q4.Y],
        ];

        float[] b =
        [
            q1.X,
            q1.Y,
            q2.X,
            q2.Y,
            q3.X,
            q3.Y,
            q4.X,
            q4.Y,
        ];

        GaussianEliminationSolver.Solve(matrixData, b);

#pragma warning disable SA1117
        Matrix4x4 projectionMatrix = new(
            b[0], b[3], 0, b[6],
            b[1], b[4], 0, b[7],
            0,    0,    1, 0,
            b[2], b[5], 0, 1);
#pragma warning restore SA1117

        return projectionMatrix;
    }
}
