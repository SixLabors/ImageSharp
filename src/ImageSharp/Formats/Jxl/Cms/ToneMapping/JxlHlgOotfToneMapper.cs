// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;

internal struct JxlHlgOotfToneMapper
{
    private readonly float exponent;
    private readonly bool applyOotf;
    private readonly float redY;
    private readonly float greenY;
    private readonly float blueY;

    public JxlHlgOotfToneMapper(float gamma, Vector3 luminances)
    {
        this.exponent = gamma - 1;
        this.redY = luminances.X;
        this.greenY = luminances.Y;
        this.blueY = luminances.Z;
        this.applyOotf = this.exponent is < -0.01f or > 0.01f;
    }

    public JxlHlgOotfToneMapper(float sourceLuminance, float targetLuminance, Vector3 primariesLuminances)
        : this(
              gamma: MathF.Pow(1.111f, MathF.Log2(targetLuminance / sourceLuminance)),
              luminances: primariesLuminances)
    {
    }

    public readonly void Apply(Span<float> rgb)
    {
        if (!this.applyOotf)
        {
            return;
        }

        float luminance = (this.redY * rgb[0]) + (this.greenY * rgb[1]) + (this.blueY * rgb[2]);
        float ratio = MathF.Min(MathF.Pow(luminance, this.exponent), 1e9f);

        rgb[0] *= ratio;
        rgb[1] *= ratio;
        rgb[2] *= ratio;
    }
}
