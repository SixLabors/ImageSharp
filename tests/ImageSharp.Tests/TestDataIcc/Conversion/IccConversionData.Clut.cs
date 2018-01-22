// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataClut
    {
        internal static IccClut Clut3x2 = new IccClut(new float[][]
        {
            new float[] { 0.1f, 0.1f },
            new float[] { 0.2f, 0.2f },
            new float[] { 0.3f, 0.3f },

            new float[] { 0.11f, 0.11f },
            new float[] { 0.21f, 0.21f },
            new float[] { 0.31f, 0.31f },

            new float[] { 0.12f, 0.12f },
            new float[] { 0.22f, 0.22f },
            new float[] { 0.32f, 0.32f },

            new float[] { 0.13f, 0.13f },
            new float[] { 0.23f, 0.23f },
            new float[] { 0.33f, 0.33f },

            new float[] { 0.14f, 0.14f },
            new float[] { 0.24f, 0.24f },
            new float[] { 0.34f, 0.34f },

            new float[] { 0.15f, 0.15f },
            new float[] { 0.25f, 0.25f },
            new float[] { 0.35f, 0.35f },

            new float[] { 0.16f, 0.16f },
            new float[] { 0.26f, 0.26f },
            new float[] { 0.36f, 0.36f },

            new float[] { 0.17f, 0.17f },
            new float[] { 0.27f, 0.27f },
            new float[] { 0.37f, 0.37f },

            new float[] { 0.18f, 0.18f },
            new float[] { 0.28f, 0.28f },
            new float[] { 0.38f, 0.38f },
        }, new byte[] { 3, 3, 3 }, IccClutDataType.Float);

        internal static IccClut Clut2x2 = new IccClut(new float[][]
        {
            new float[] { 0.1f, 0.1f },
            new float[] { 0.2f, 0.2f },
            new float[] { 0.3f, 0.3f },

            new float[] { 0.11f, 0.11f },
            new float[] { 0.21f, 0.21f },
            new float[] { 0.31f, 0.31f },

            new float[] { 0.12f, 0.12f },
            new float[] { 0.22f, 0.22f },
            new float[] { 0.32f, 0.32f },
        }, new byte[] { 3, 3 }, IccClutDataType.Float);

        public static object[][] ClutConversionTestData =
        {
            new object[] { Clut3x2, new Vector4(0.75f, 0.75f, 0.75f, 0), new Vector4(0.31f, 0.31f, 0, 0) },
        };
    }
}
