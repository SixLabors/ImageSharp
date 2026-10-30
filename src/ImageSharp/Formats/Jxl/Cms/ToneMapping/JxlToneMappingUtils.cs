// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;

internal static class JxlToneMappingUtils
{
    public static void GamutMapScalar(Span<float> rgb, Vector3 primariesLuminances, float preserveSaturation = 0.1f)
    {
        float luminance = (primariesLuminances.X * rgb[0]) +
            (primariesLuminances.Y * rgb[1]) +
            (primariesLuminances.Z * rgb[2]);

        // Desaturate out-of-gamut pixels. This is done by mixing each pixel
        // with just enough gray of the target luminance to make all
        // components non-negative.
        //
        //   - For saturation preservation, if a component is still larger than
        //     1 then the pixel is normalized to have a maximum component of 1.
        //     That will reduce its luminance.
        //
        //   - For luminance preservation, getting all components below 1 is
        //     done by mixing in yet more gray. That will desaturate it further.
        float grayMixSaturation = 0f;
        float grayMixLuminance = 0f;

        for (int idx = 0; idx < 3; idx++)
        {
            float value = rgb[idx];
            float valMinusGray = value - luminance;
            float invValMinusGray = 1.0f / ((valMinusGray == 0.0f) ? 1.0f : valMinusGray);
            float valOverValMinusGray = valMinusGray * invValMinusGray;

            grayMixSaturation = valMinusGray >= 0.0f
                ? grayMixSaturation
                : MathF.Max(grayMixSaturation, valOverValMinusGray);
            grayMixLuminance = MathF.Max(grayMixLuminance, (valMinusGray <= 0.0f) ? grayMixSaturation : valOverValMinusGray - invValMinusGray);
        }

        float grayMix = Math.Clamp(
            (preserveSaturation * (grayMixSaturation - grayMixLuminance)) + grayMixLuminance,
            0.0f,
            1.0f);

        for (int idx = 0; idx < 3; idx++)
        {
            ref float val = ref rgb[idx];
            val = (grayMix * (luminance - val)) + val;
        }

        float maxColor = MathF.Max(1.0f, MathF.Max(rgb[0], MathF.Max(rgb[1], rgb[2])));
        float normalizer = 1.0f / maxColor;

        rgb[0] *= normalizer;
        rgb[1] *= normalizer;
        rgb[2] *= normalizer;
    }

    public static void GamutMap(ref Vector<float> red, ref Vector<float> green, ref Vector<float> blue, Vector3 primariesLuminances, float preserveSaturation = 0.1f)
    {
        Vector<float> luminance =
            (new Vector<float>(primariesLuminances.X) * red) +
            (new Vector<float>(primariesLuminances.Y) * green) +
            (new Vector<float>(primariesLuminances.Z) * blue);

        // Desaturate out-of-gamut pixels by mixing each pixel with gray
        // of the target luminance until all components are non-negative.
        Vector<float> zero = Vector<float>.Zero;
        Vector<float> one = Vector<float>.One;

        Vector<float> grayMixSaturation = zero;
        Vector<float> grayMixLuminance = zero;

        Span<Vector<float>> channels = [red, green, blue];

        foreach (Vector<float> val in channels)
        {
            Vector<float> valMinusGray = val - luminance;

            Vector<float> invValMinusGray = one / Vector.ConditionalSelect(
                Vector.Equals(valMinusGray, zero),
                one,
                valMinusGray);

            Vector<float> valOverValMinusGray = val * invValMinusGray;

            grayMixSaturation = Vector.ConditionalSelect(
                Vector.GreaterThanOrEqual(valMinusGray, zero),
                grayMixSaturation,
                Vector.Max(grayMixSaturation, valOverValMinusGray));

            grayMixLuminance = Vector.Max(
                grayMixLuminance,
                Vector.ConditionalSelect(
                    Vector.LessThanOrEqual(valMinusGray, zero),
                    grayMixSaturation,
                    valOverValMinusGray - invValMinusGray));
        }

        Vector<float> grayMix = Vector.Min(
            one,
            Vector.Max(
                zero,
                (new Vector<float>(preserveSaturation) * (grayMixSaturation - grayMixLuminance)) + grayMixLuminance));

        red = (grayMix * (luminance - red)) + red;
        green = (grayMix * (luminance - green)) + green;
        blue = (grayMix * (luminance - blue)) + blue;

        Vector<float> maxClr = Vector.Max(
            Vector.Max(one, red),
            Vector.Max(green, blue));

        Vector<float> normalizer = one / maxClr;

        red *= normalizer;
        green *= normalizer;
        blue *= normalizer;
    }
}
