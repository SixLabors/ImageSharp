// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;

internal readonly struct JxlHlgOotfToneMapper
{
    private readonly float exponent;
    private readonly bool applyOotf;
    private readonly Vector3 luminances;

    public JxlHlgOotfToneMapper(float gamma, Vector3 luminances)
    {
        this.exponent = gamma - 1;
        this.luminances = luminances;
        this.applyOotf = this.exponent is < -0.01f or > 0.01f;
    }

    public JxlHlgOotfToneMapper(float sourceLuminance, float targetLuminance, Vector3 primariesLuminances)
        : this(
              gamma: MathF.Pow(1.111f, MathF.Log2(targetLuminance / sourceLuminance)),
              luminances: primariesLuminances)
    {
    }

    public readonly bool WarrantsGamutMapping => this.applyOotf && this.exponent < 0;

    public static JxlHlgOotfToneMapper FromSceneLight(float displayLuminance, Vector3 primariesLuminances) => new(
            gamma: 1.2f * MathF.Pow(1.111f, MathF.Log2(displayLuminance / 1000.0f)),
            luminances: primariesLuminances);

    public static JxlHlgOotfToneMapper ToSceneLight(float displayLuminance, Vector3 primariesLuminances) => new(
            gamma: (1 / 1.2f) * MathF.Pow(1.111f, -MathF.Log2(displayLuminance / 1000.0f)),
            luminances: primariesLuminances);

    public readonly void Apply(Vector3 rgb)
    {
        if (!this.applyOotf)
        {
            return;
        }

        float luminance = (this.luminances.X * rgb[0]) + (this.luminances.Y * rgb[1]) + (this.luminances.Z * rgb[2]);
        float ratio = MathF.Min(MathF.Pow(luminance, this.exponent), 1e9f);

        rgb *= ratio;
    }

    public readonly void Apply(ref Vector<float> red, ref Vector<float> green, ref Vector<float> blue)
    {
        if (!this.applyOotf)
        {
            return;
        }

        Vector<float> luminance = (this.luminances.X * red) + (this.luminances.Y * green) + (this.luminances.Z * blue);
        Vector<float> ratio = Vector.Min(JxlSimdUtils.FastPowf(luminance, Vector.Create(this.exponent)), Vector.Create(1e9f));

        red *= ratio;
        green *= ratio;
        blue *= ratio;
    }
}
