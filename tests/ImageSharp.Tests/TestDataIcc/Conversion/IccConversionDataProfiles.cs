// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

/// <summary>
/// Provides independently calculated profile connections that retain intermediate negative XYZ.
/// </summary>
public static class IccConversionDataProfiles
{
    /// <summary>
    /// Gets normalized device samples, including the two colors that exposed intermediate XYZ clipping.
    /// </summary>
    public static List<float[]> Inputs { get; } =
    [
        [0F, 0F, 0F, 0F],
        [1F, 0F, 0F, 0F],
        [0F, 1F, 0F, 0F],
        [0F, 0F, 1F, 0F],
        [0F, 0F, 0F, 1F],
        [1F, 1F, 1F, 1F],
        [0.5F, 0.5F, 0.5F, 0.5F],
        [0.199678659F, 0.67982769F, 0.805381715F, 0.982666492F],
        [0.776568174F, 0.961630166F, 0.31032759F, 0.895294666F],
        [0.25F, 0.75F, 0.5F, 0.25F]
    ];

    /// <summary>
    /// Gets source profiles, target profiles and expected outputs in the same order as <see cref="Inputs"/>.
    /// </summary>
    /// <remarks>
    /// LittleCMS 2.19 calculated these values with unchanged profiles, source-header intent and float RGB or CMYK samples.
    /// Optimization and caching were disabled. CMYK percent output is divided by 100 to give normalized ink amounts.
    /// Default interpolation uses tetrahedral device-input tables and trilinear Lab-input output tables, matching Auto selection.
    /// The values include PCS adjustment and destination encoding. Intermediate XYZ remains unbounded.
    /// </remarks>
    public static TheoryData<string, string, Vector4[]> UnboundedConnections { get; } = new()
    {
        {
            TestIccProfiles.Fogra39,
            TestIccProfiles.StandardRgbV4,
            new Vector4[]
            {
                new Vector4(1.00130033F, 0.998137474F, 0.999276161F, 1F),
                new Vector4(0.112219065F, 0.770237386F, 0.926910818F, 1F),
                new Vector4(0.917270541F, 0.167403936F, 0.558151305F, 1F),
                new Vector4(0.962106705F, 0.996492863F, 0.177499652F, 1F),
                new Vector4(0.0980496481F, 0.0986062959F, 0.0930144414F, 1F),
                new Vector4(0.0259373467F, 0.0172585789F, 0F, 1F),
                new Vector4(0.363397568F, 0.328328222F, 0.310312778F, 1F),
                new Vector4(0.105944052F, 0.0209274441F, 0F, 1F),
                new Vector4(0.0520495847F, 0F, 0.0552377068F, 1F),
                new Vector4(0.619181156F, 0.292398602F, 0.342522949F, 1F),
            }
        },
        {
            TestIccProfiles.Fogra39,
            TestIccProfiles.Cgats21,
            new Vector4[]
            {
                new Vector4(0F, 0F, 1.52590219e-05F, 0F),
                new Vector4(0.940535605F, 0.0571450368F, 0.0141603723F, 0.0146181444F),
                new Vector4(0.0152437631F, 0.965102613F, 0.113160908F, 0.0176394302F),
                new Vector4(0.0156099796F, 0.00910963584F, 0.865110219F, 0F),
                new Vector4(0.761470973F, 0.692088187F, 0.640741587F, 0.807431161F),
                new Vector4(0.891584635F, 0.840192258F, 0.616449237F, 0.996887147F),
                new Vector4(0.535851061F, 0.531807423F, 0.523872733F, 0.390493631F),
                new Vector4(0.559929788F, 0.82796979F, 0.684428155F, 0.933913171F),
                new Vector4(0.773373008F, 0.921629667F, 0.311573982F, 0.932814538F),
                new Vector4(0.169634551F, 0.714732587F, 0.457907975F, 0.286503404F),
            }
        },
        {
            TestIccProfiles.Fogra39,
            TestIccProfiles.StandardRgbV2,
            new Vector4[]
            {
                new Vector4(0.999908447F, 1F, 0.99987793F, 1F),
                new Vector4(0F, 0.620752275F, 0.885420024F, 1F),
                new Vector4(0.913237214F, 0.0173952859F, 0.50490576F, 1F),
                new Vector4(1F, 0.937102318F, 0F, 1F),
                new Vector4(0.104890518F, 0.103395134F, 0.0991226062F, 1F),
                new Vector4(0.0189669635F, 0F, 0F, 1F),
                new Vector4(0.368368059F, 0.327351809F, 0.30983445F, 1F),
                new Vector4(0.113374531F, 0.0154421302F, 0F, 1F),
                new Vector4(0.0802777112F, 0F, 0.0606393516F, 1F),
                new Vector4(0.631815076F, 0.306233317F, 0.338918149F, 1F),
            }
        },
        {
            TestIccProfiles.StandardRgbV2,
            TestIccProfiles.Fogra39,
            new Vector4[]
            {
                new Vector4(0.770153344F, 0.665583253F, 0.546013594F, 0.999984741F),
                new Vector4(0.000259403372F, 0.966460645F, 0.965728223F, 0F),
                new Vector4(0.705821276F, 6.10360876e-05F, 0.999984741F, 0F),
                new Vector4(0.993163884F, 0.798458815F, 0.000732433051F, 0.00032043946F),
                new Vector4(0.770153344F, 0.665583253F, 0.546013594F, 0.999984741F),
                new Vector4(3.05180438e-05F, 9.15541314e-05F, 3.05180438e-05F, 0F),
                new Vector4(0.318989843F, 0.254703581F, 0.259861141F, 0.3857328F),
                new Vector4(0.672587156F, 0.0360265523F, 0.123613335F, 0.0312504768F),
                new Vector4(0.419943541F, 0.000427252613F, 0.921873808F, 0F),
                new Vector4(0.66536963F, 0.00120546273F, 0.657923222F, 0F),
            }
        }
    };
}
