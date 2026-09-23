// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal sealed class JxlConnectedComponent
{
    private Rectangle bounds;
    private readonly List<Point> points = [];
    private float maxEnergy;
    private float meanEnergy;
    private float varEnergy;
    private float meanBg;
    private float varBg;
    private Point mode;

    public JxlConnectedComponent(Rectangle bounds, List<Point> points)
    {
        this.bounds = bounds;
        this.points = points;
    }

    public Point Mode => this.mode;

    public Rectangle Bounds => this.bounds;

    public float Score { get; private set; }

    public void ComputeStats(JxlImageF energy, in Rectangle rect, int extra)
    {
        this.maxEnergy = 0.0F;
        this.meanEnergy = 0.0F;
        this.varEnergy = 0.0F;
        this.meanBg = 0.0F;
        this.varBg = 0.0F;
        int nIn = 0;
        int nOut = 0;
        this.mode.X = 0;
        this.mode.Y = 0;

        for (int sy = -extra; sy < (this.bounds.Height + extra); sy++)
        {
            int y = sy + this.bounds.Y0();

            if (y < 0 || (uint)y >= rect.Height)
            {
                continue;
            }

            Span<float> erow = energy.GetRow(rect, y);

            for (int sx = -extra; sx < (this.bounds.Width + extra); sx++)
            {
                int x = sx + this.bounds.X0();

                if (x < 0 || (uint)x >= rect.Width)
                {
                    continue;
                }

                if (erow[x] > this.maxEnergy)
                {
                    this.maxEnergy = erow[x];
                    this.mode.X = x;
                    this.mode.Y = y;
                }

                if (JxlDotDetectionUtils.PointInRect(this.bounds, new Point(x, y)))
                {
                    this.meanEnergy += erow[x];
                    this.varEnergy += erow[x] * erow[x];
                    nIn++;
                }
                else
                {
                    this.meanBg += erow[x];
                    this.varBg += erow[x] * erow[x];
                    nOut++;
                }
            }
        }

        this.meanEnergy /= nIn;
        this.meanBg /= nOut;
        this.varEnergy = (this.varEnergy / nIn) - (this.meanEnergy * this.meanEnergy);

        this.varBg = (this.varBg / nOut) - (this.meanBg * this.meanBg);
        this.Score = (this.meanEnergy - this.meanBg) / MathF.Sqrt(this.varBg);
    }
}
