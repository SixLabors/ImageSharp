// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Allows the approximate comparison of color profile component values.
/// </summary>
internal readonly struct ApproximateColorProfileComparer :
    IEqualityComparer<CieLab>,
    IEqualityComparer<CieXyz>,
    IEqualityComparer<Lms>,
    IEqualityComparer<CieLch>,
    IEqualityComparer<Rgb>,
    IEqualityComparer<YCbCr>,
    IEqualityComparer<CieLchuv>,
    IEqualityComparer<CieLuv>,
    IEqualityComparer<CieXyy>,
    IEqualityComparer<Cmyk>,
    IEqualityComparer<Hsl>,
    IEqualityComparer<Hsv>,
    IEqualityComparer<HunterLab>,
    IEqualityComparer<Y>
{
    private readonly float epsilon;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproximateColorProfileComparer"/> struct.
    /// </summary>
    /// <param name="epsilon">The comparison error difference epsilon to use.</param>
    public ApproximateColorProfileComparer(float epsilon = 1f) => this.epsilon = epsilon;

    public bool Equals(CieLab x, CieLab y) => this.Equals(x.L, y.L) && this.Equals(x.A, y.A) && this.Equals(x.B, y.B);

    public bool Equals(CieXyz x, CieXyz y) => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y) && this.Equals(x.Z, y.Z);

    public bool Equals(Lms x, Lms y) => this.Equals(x.L, y.L) && this.Equals(x.M, y.M) && this.Equals(x.S, y.S);

    public bool Equals(CieLch x, CieLch y) => this.Equals(x.L, y.L) && this.Equals(x.C, y.C) && this.Equals(x.H, y.H);

    public bool Equals(Rgb x, Rgb y) => this.Equals(x.R, y.R) && this.Equals(x.G, y.G) && this.Equals(x.B, y.B);

    public bool Equals(YCbCr x, YCbCr y) => this.Equals(x.Y, y.Y) && this.Equals(x.Cb, y.Cb) && this.Equals(x.Cr, y.Cr);

    public bool Equals(CieLchuv x, CieLchuv y) => this.Equals(x.L, y.L) && this.Equals(x.C, y.C) && this.Equals(x.H, y.H);

    public bool Equals(CieLuv x, CieLuv y) => this.Equals(x.L, y.L) && this.Equals(x.U, y.U) && this.Equals(x.V, y.V);

    public bool Equals(CieXyy x, CieXyy y) => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y) && this.Equals(x.Yl, y.Yl);

    public bool Equals(Cmyk x, Cmyk y) => this.Equals(x.C, y.C) && this.Equals(x.M, y.M) && this.Equals(x.Y, y.Y) && this.Equals(x.K, y.K);

    public bool Equals(Hsl x, Hsl y) => this.Equals(x.H, y.H) && this.Equals(x.S, y.S) && this.Equals(x.L, y.L);

    public bool Equals(Hsv x, Hsv y) => this.Equals(x.H, y.H) && this.Equals(x.S, y.S) && this.Equals(x.V, y.V);

    public bool Equals(HunterLab x, HunterLab y) => this.Equals(x.L, y.L) && this.Equals(x.A, y.A) && this.Equals(x.B, y.B);

    public bool Equals(Y x, Y y) => this.Equals(x.L, y.L);

    public int GetHashCode([DisallowNull] CieLab obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] CieXyz obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Lms obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] CieLch obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Rgb obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] YCbCr obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] CieLchuv obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] CieLuv obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] CieXyy obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Cmyk obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Hsl obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Hsv obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] HunterLab obj) => obj.GetHashCode();

    public int GetHashCode([DisallowNull] Y obj) => obj.GetHashCode();

    private bool Equals(float x, float y)
    {
        float d = x - y;
        return d >= -this.epsilon && d <= this.epsilon;
    }
}
