// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.DotDetection;

internal sealed class JxlConnectedComponent
{
    private Stats stats;
    private readonly List<Point> points = [];

    public JxlConnectedComponent(Rectangle bounds, List<Point> points)
    {
        this.stats.bounds = bounds;
        this.points = points;
    }

    public Point Mode => this.stats.mode;

    public Rectangle Bounds => this.stats.bounds;

    public float Score { get; private set; }

    public void ComputeStats(JxlImageF energy, in Rectangle rect, int extra)
    {
        this.stats = default;
        int nIn = 0;
        int nOut = 0;

        for (int sy = -extra; sy < (this.stats.bounds.Height + extra); sy++)
        {
            int y = sy + this.stats.bounds.Y0();

            if (y < 0 || (uint)y >= rect.Height)
            {
                continue;
            }

            Span<float> erow = energy.GetRow(rect, y);

            for (int sx = -extra; sx < (this.stats.bounds.Width + extra); sx++)
            {
                int x = sx + this.stats.bounds.X0();

                if (x < 0 || (uint)x >= rect.Width)
                {
                    continue;
                }

                if (erow[x] > this.stats.maxEnergy)
                {
                    this.stats.maxEnergy = erow[x];
                    this.stats.mode.X = x;
                    this.stats.mode.Y = y;
                }

                if (JxlDotDetectionUtils.PointInRect(this.stats.bounds, new Point(x, y)))
                {
                    this.stats.meanEnergy += erow[x];
                    this.stats.varEnergy += erow[x] * erow[x];
                    nIn++;
                }
                else
                {
                    this.stats.meanBg += erow[x];
                    this.stats.varBg += erow[x] * erow[x];
                    nOut++;
                }
            }
        }

        this.stats.meanEnergy /= nIn;
        this.stats.meanBg /= nOut;
        this.stats.varEnergy = (this.stats.varEnergy / nIn) - (this.stats.meanEnergy * this.stats.meanEnergy);

        this.stats.varBg = (this.stats.varBg / nOut) - (this.stats.meanBg * this.stats.meanBg);
        this.Score = (this.stats.meanEnergy - this.stats.meanBg) / MathF.Sqrt(this.stats.varBg);
    }

    private struct Stats
    {
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
        public Rectangle bounds;
        public float maxEnergy;
        public float meanEnergy;
        public float varEnergy;
        public float meanBg;
        public float varBg;
        public Point mode;
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }
}
