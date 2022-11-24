// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataClut
{
    internal static IccClut Clut3x2 = new(
        new[]
        {
            new[] { 0.1f, 0.1f },
            new[] { 0.2f, 0.2f },
            new[] { 0.3f, 0.3f },

            new[] { 0.11f, 0.11f },
            new[] { 0.21f, 0.21f },
            new[] { 0.31f, 0.31f },

            new[] { 0.12f, 0.12f },
            new[] { 0.22f, 0.22f },
            new[] { 0.32f, 0.32f },

            new[] { 0.13f, 0.13f },
            new[] { 0.23f, 0.23f },
            new[] { 0.33f, 0.33f },

            new[] { 0.14f, 0.14f },
            new[] { 0.24f, 0.24f },
            new[] { 0.34f, 0.34f },

            new[] { 0.15f, 0.15f },
            new[] { 0.25f, 0.25f },
            new[] { 0.35f, 0.35f },

            new[] { 0.16f, 0.16f },
            new[] { 0.26f, 0.26f },
            new[] { 0.36f, 0.36f },

            new[] { 0.17f, 0.17f },
            new[] { 0.27f, 0.27f },
            new[] { 0.37f, 0.37f },

            new[] { 0.18f, 0.18f },
            new[] { 0.28f, 0.28f },
            new[] { 0.38f, 0.38f },
        },
        new byte[] { 3, 3, 3 },
        IccClutDataType.Float);

    internal static IccClut Clut3x1 = new(
        new[]
        {
            new[] { 0.10f },
            new[] { 0.20f },
            new[] { 0.30f },

            new[] { 0.11f },
            new[] { 0.21f },
            new[] { 0.31f },

            new[] { 0.12f },
            new[] { 0.22f },
            new[] { 0.32f },

            new[] { 0.13f },
            new[] { 0.23f },
            new[] { 0.33f },

            new[] { 0.14f },
            new[] { 0.24f },
            new[] { 0.34f },

            new[] { 0.15f },
            new[] { 0.25f },
            new[] { 0.35f },

            new[] { 0.16f },
            new[] { 0.26f },
            new[] { 0.36f },

            new[] { 0.17f },
            new[] { 0.27f },
            new[] { 0.37f },

            new[] { 0.18f },
            new[] { 0.28f },
            new[] { 0.38f },
        },
        new byte[] { 3, 3, 3 },
        IccClutDataType.Float);

    internal static IccClut Clut2x2 = new(
        new[]
        {
            new[] { 0.1f, 0.9f },
            new[] { 0.2f, 0.8f },
            new[] { 0.3f, 0.7f },

            new[] { 0.4f, 0.6f },
            new[] { 0.5f, 0.5f },
            new[] { 0.6f, 0.4f },

            new[] { 0.7f, 0.3f },
            new[] { 0.8f, 0.2f },
            new[] { 0.9f, 0.1f },
        },
        new byte[] { 3, 3 },
        IccClutDataType.Float);

    internal static IccClut Clut2x1 = new(
        new[]
        {
            new[] { 0.1f },
            new[] { 0.2f },
            new[] { 0.3f },

            new[] { 0.4f },
            new[] { 0.5f },
            new[] { 0.6f },

            new[] { 0.7f },
            new[] { 0.8f },
            new[] { 0.9f },
        },
        new byte[] { 3, 3 },
        IccClutDataType.Float);

    internal static IccClut Clut1x2 = new(
        new[]
        {
            new[] { 0f, 0.5f },
            new[] { 0.25f, 0.75f, },
            new[] { 0.5f, 1f },
        },
        new byte[] { 3 },
        IccClutDataType.Float);

    internal static IccClut Clut1x1 = new(
        new[]
        {
            new[] { 0f },
            new[] { 0.5f },
            new[] { 1f },
        },
        new byte[] { 3 },
        IccClutDataType.Float);

    public static object[][] ClutConversionTestData =
    {
        new object[] { Clut3x2, new Vector4(0.75f, 0.75f, 0.75f, 0), new Vector4(0.31f, 0.31f, 0, 0) },
        new object[] { Clut3x1, new Vector4(0.2f, 0.6f, 0.8f, 0), new Vector4(0.276f, 0, 0, 0) },
        new object[] { Clut3x1, new Vector4(0.75f, 0.75f, 0.75f, 0), new Vector4(0.31f, 0, 0, 0) },
        new object[] { Clut2x2, new Vector4(0.2f, 0.6f, 0, 0), new Vector4(0.46f, 0.54f, 0, 0) },
        new object[] { Clut2x2, new Vector4(0.25f, 0.75f, 0, 0), new Vector4(0.4f, 0.6f, 0, 0) },
        new object[] { Clut2x1, new Vector4(0.25f, 0.75f, 0, 0), new Vector4(0.4f, 0, 0, 0) },
        new object[] { Clut1x2, new Vector4(0.25f, 0, 0, 0), new Vector4(0.125f, 0.625f, 0, 0) },
        new object[] { Clut1x1, new Vector4(0.25f, 0, 0, 0), new Vector4(0.25f, 0, 0, 0) },
    };
}
