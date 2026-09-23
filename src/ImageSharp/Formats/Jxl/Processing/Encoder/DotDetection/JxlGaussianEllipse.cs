// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal struct JxlGaussianEllipse
{
    // Make all of these fields, for consistency with
    // the InlineArrays. InlineArrays can't be updated
    // when they're a property.

    /// <summary>
    /// Position in X
    /// </summary>
    public double X;

    /// <summary>
    /// Position in Y
    /// </summary>
    public double Y;

    /// <summary>
    /// Scale in X
    /// </summary>
    public double SigmaX;

    /// <summary>
    /// Scale in Y
    /// </summary>
    public double SigmaY;

    /// <summary>
    /// Ellipse rotation in radians
    /// </summary>
    public double Angle;

    /// <summary>
    /// Intensity in each channel
    /// </summary>
    public InlineArray3<double> Intensity;

    /// <summary>
    /// Error after the Gaussian was fit
    /// </summary>
    public double L2Loss;

    public double L1Loss;

    /// <summary>
    /// The L2Loss + regularization term
    /// </summary>
    public double RidgeLoss;

    /// <summary>
    /// Experimental custom loss
    /// </summary>
    public double CustomLoss;

    /// <summary>
    /// Best background color
    /// </summary>
    public InlineArray3<double> BackgroundColor;

    /// <summary>
    /// Number of negative pixels when subtracting dot
    /// </summary>
    public int NegativePixels;

    /// <summary>
    /// Debt due to channel truncation
    /// </summary>
    public InlineArray3<double> NegativeValue;
}
