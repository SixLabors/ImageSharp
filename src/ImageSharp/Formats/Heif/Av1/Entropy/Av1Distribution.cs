// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Class representing the probability distribution used for symbol coding.
/// </summary>
internal class Av1Distribution
{
    internal const int ProbabilityTop = 1 << ProbabilityBitCount;
    internal const int ProbabilityMinimum = 4;
    internal const int CdfShift = 15 - ProbabilityBitCount;
    internal const int ProbabilityShift = 6;

    private const int ProbabilityBitCount = 15;

    private readonly uint[] probabilities;
    private readonly int speed;
    private int updateCount;

    public Av1Distribution(uint p0)
        : this([p0, 0], 1)
    {
    }

    public Av1Distribution(uint p0, uint p1)
        : this([p0, p1, 0], 1)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2)
        : this([p0, p1, p2, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3)
        : this([p0, p1, p2, p3, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4)
        : this([p0, p1, p2, p3, p4, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5)
        : this([p0, p1, p2, p3, p4, p5, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6)
        : this([p0, p1, p2, p3, p4, p5, p6, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11, uint p12)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, 0], 2)
    {
    }

    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11, uint p12, uint p13, uint p14)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, 0], 2)
    {
    }

    private Av1Distribution(uint[] props, int speed)
    {
        // this.probabilities = props;
        this.probabilities = new uint[props.Length];
        for (int i = 0; i < props.Length - 1; i++)
        {
            this.probabilities[i] = ProbabilityTop - props[i];
        }

        this.NumberOfSymbols = props.Length;
        this.speed = speed;
    }

    public int NumberOfSymbols { get; }

    public uint this[int index] => this.probabilities[index];

    public void Update(int value)
    {
        int rate15 = this.updateCount > 15 ? 1 : 0;
        int rate31 = this.updateCount > 31 ? 1 : 0;
        int rate = 3 + rate15 + rate31 + this.speed; // + get_msb(nsymbs);
        int tmp = ProbabilityTop;

        // Single loop (faster)
        for (int i = 0; i < this.NumberOfSymbols - 1; i++)
        {
            tmp = i == value ? 0 : tmp;
            uint p = this.probabilities[i];
            if (tmp < p)
            {
                this.probabilities[i] -= (ushort)(p - tmp >> rate);
            }
            else
            {
                this.probabilities[i] += (ushort)(tmp - p >> rate);
            }
        }

        int rate32 = this.updateCount < 32 ? 1 : 0;
        this.updateCount += rate32;
    }
}
