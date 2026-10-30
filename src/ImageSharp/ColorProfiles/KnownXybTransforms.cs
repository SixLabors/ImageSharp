// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides the XYB color transform defined by ISO/IEC 18181-1.
/// </summary>
/// <remarks>
/// The standard coefficients are fixed. A JPEG XL image header can supply custom inverse coefficients and biases.
/// See the <see href="https://jpeg.org/jpegxl/workplan.html">JPEG XL specifications</see> and the
/// <see href="https://raw.githubusercontent.com/libjxl/libjxl/main/lib/jxl/cms/opsin_params.h">reference coefficients</see>.
/// </remarks>
public static class KnownXybTransforms
{
    /// <summary>
    /// Gets the ISO/IEC 18181-1 XYB absorbance transform with an intensity target of 255 nits.
    /// </summary>
    public static XybTransform Iso18181 { get; } = new(
        new Matrix4x4(
            0.30F,
            1F - 0.078F - 0.30F,
            0.078F,
            0F,
            0.23F,
            1F - 0.078F - 0.23F,
            0.078F,
            0F,
            0.24342268924547819F,
            0.20476744424496821F,
            1F - 0.24342268924547819F - 0.20476744424496821F,
            0F,
            0F,
            0F,
            0F,
            1F),
        new Matrix4x4(
            11.031566901960783F,
            -9.866943921568629F,
            -0.16462299647058826F,
            0F,
            -3.254147380392157F,
            4.418770392156863F,
            -0.16462299647058826F,
            0F,
            -3.6588512862745097F,
            2.7129230470588235F,
            1.9459282392156863F,
            0F,
            0F,
            0F,
            0F,
            1F),

        // Store negative absorbance biases: forward conversion subtracts them; inverse conversion adds them after cubing.
        new Vector3(-0.0037930732552754493F),
        XybTransform.ReferenceIntensity);
}
