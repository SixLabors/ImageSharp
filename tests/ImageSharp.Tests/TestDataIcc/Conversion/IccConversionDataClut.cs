// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataClut
{
    internal static IccClut Clut3x2 = new(
        [
            0.1f, 0.1f,
            0.2f, 0.2f,
            0.3f, 0.3f,

            0.11f, 0.11f,
            0.21f, 0.21f,
            0.31f, 0.31f,

            0.12f, 0.12f,
            0.22f, 0.22f,
            0.32f, 0.32f,

            0.13f, 0.13f,
            0.23f, 0.23f,
            0.33f, 0.33f,

            0.14f, 0.14f,
            0.24f, 0.24f,
            0.34f, 0.34f,

            0.15f, 0.15f,
            0.25f, 0.25f,
            0.35f, 0.35f,

            0.16f, 0.16f,
            0.26f, 0.26f,
            0.36f, 0.36f,

            0.17f, 0.17f,
            0.27f, 0.27f,
            0.37f, 0.37f,

            0.18f, 0.18f,
            0.28f, 0.28f,
            0.38f, 0.38f,
        ],
        [3, 3, 3],
        IccClutDataType.Float,
        outputChannelCount: 2);

    internal static IccClut Clut3x1 = new(
        [
             0.10f,
             0.20f,
             0.30f,

             0.11f,
             0.21f,
             0.31f,

             0.12f,
             0.22f,
             0.32f,

             0.13f,
             0.23f,
             0.33f,

             0.14f,
             0.24f,
             0.34f,

             0.15f,
             0.25f,
             0.35f,

             0.16f,
             0.26f,
             0.36f,

             0.17f,
             0.27f,
             0.37f,

             0.18f,
             0.28f,
             0.38f,
        ],
        [3, 3, 3],
        IccClutDataType.Float,
        outputChannelCount: 1);

    internal static IccClut Clut2x2 = new(
        [
            0.1f, 0.9f,
            0.2f, 0.8f,
            0.3f, 0.7f,

            0.4f, 0.6f,
            0.5f, 0.5f,
            0.6f, 0.4f,

            0.7f, 0.3f,
            0.8f, 0.2f,
            0.9f, 0.1f,
        ],
        [3, 3],
        IccClutDataType.Float,
        outputChannelCount: 2);

    internal static IccClut Clut2x1 = new(
        [
            0.1f,
            0.2f,
            0.3f,

            0.4f,
            0.5f,
            0.6f,

            0.7f,
            0.8f,
            0.9f,
        ],
        [3, 3],
        IccClutDataType.Float,
        outputChannelCount: 1);

    internal static IccClut Clut1x2 = new(
        [
            0f, 0.5f,
            0.25f, 0.75f,
            0.5f, 1f,
        ],
        [3],
        IccClutDataType.Float,
        outputChannelCount: 2);

    internal static IccClut Clut1x1 = new(
        [
            0f,
            0.5f,
            1f,
        ],
        [3],
        IccClutDataType.Float,
        outputChannelCount: 1);

    public static object[][] ClutConversionTestData =
    [
        [Clut3x2, new Vector4(0.75f, 0.75f, 0.75f, 0), new Vector4(0.31f, 0.31f, 0, 0)],
        [Clut3x1, new Vector4(0.2f, 0.6f, 0.8f, 0), new Vector4(0.276f, 0, 0, 0)],
        [Clut3x1, new Vector4(0.75f, 0.75f, 0.75f, 0), new Vector4(0.31f, 0, 0, 0)],
        [Clut2x2, new Vector4(0.2f, 0.6f, 0, 0), new Vector4(0.46f, 0.54f, 0, 0)],
        [Clut2x2, new Vector4(0.25f, 0.75f, 0, 0), new Vector4(0.4f, 0.6f, 0, 0)],
        [Clut2x1, new Vector4(0.25f, 0.75f, 0, 0), new Vector4(0.4f, 0, 0, 0)],
        [Clut1x2, new Vector4(0.25f, 0, 0, 0), new Vector4(0.125f, 0.625f, 0, 0)],
        [Clut1x1, new Vector4(0.25f, 0, 0, 0), new Vector4(0.25f, 0, 0, 0)],
    ];
}
