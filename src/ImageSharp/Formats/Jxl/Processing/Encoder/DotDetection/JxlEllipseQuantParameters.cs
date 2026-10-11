// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal struct JxlEllipseQuantParameters
{
    public int YSize;
    public int XSize;
    public int QPosition;
    public double MinSigma;
    public double MaxSigma;
    public int QSigma;
    public int QAngle;
    public InlineArray3<double> MinIntensity;
    public InlineArray3<double> MaxIntensity;
    public InlineArray3<int> QIntensity;
    public bool SubtractQuantized;
    public float YToX;
    public float YToB;

    public JxlEllipseQuantParameters(int ySize, int xSize, int qPosition, double minSigma, double maxSigma, int qSigma, int qAngle, InlineArray3<double> minIntensity, InlineArray3<double> maxIntensity, InlineArray3<int> qIntensity, bool subtractQuantized, float yToX, float yToB)
    {
        this.YSize = ySize;
        this.XSize = xSize;
        this.QPosition = qPosition;
        this.MinSigma = minSigma;
        this.MaxSigma = maxSigma;
        this.QSigma = qSigma;
        this.QAngle = qAngle;
        this.MinIntensity = minIntensity;
        this.MaxIntensity = maxIntensity;
        this.QIntensity = qIntensity;
        this.SubtractQuantized = subtractQuantized;
        this.YToX = yToX;
        this.YToB = yToB;
    }
}
