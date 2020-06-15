// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataClut
    {
        internal static IccClut Clut3x2 = new IccClut(
            new float[][]
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
            },
            new byte[] { 3, 3, 3 },
            IccClutDataType.Float);

        internal static IccClut Clut3x1 = new IccClut(
            new float[][]
            {
                new float[] { 0.10f },
                new float[] { 0.20f },
                new float[] { 0.30f },

                new float[] { 0.11f },
                new float[] { 0.21f },
                new float[] { 0.31f },

                new float[] { 0.12f },
                new float[] { 0.22f },
                new float[] { 0.32f },

                new float[] { 0.13f },
                new float[] { 0.23f },
                new float[] { 0.33f },

                new float[] { 0.14f },
                new float[] { 0.24f },
                new float[] { 0.34f },

                new float[] { 0.15f },
                new float[] { 0.25f },
                new float[] { 0.35f },

                new float[] { 0.16f },
                new float[] { 0.26f },
                new float[] { 0.36f },

                new float[] { 0.17f },
                new float[] { 0.27f },
                new float[] { 0.37f },

                new float[] { 0.18f },
                new float[] { 0.28f },
                new float[] { 0.38f },
            },
            new byte[] { 3, 3, 3 },
            IccClutDataType.Float);

        internal static IccClut Clut2x2 = new IccClut(
            new float[][]
            {
                new float[] { 0.1f, 0.9f },
                new float[] { 0.2f, 0.8f },
                new float[] { 0.3f, 0.7f },

                new float[] { 0.4f, 0.6f },
                new float[] { 0.5f, 0.5f },
                new float[] { 0.6f, 0.4f },

                new float[] { 0.7f, 0.3f },
                new float[] { 0.8f, 0.2f },
                new float[] { 0.9f, 0.1f },
            },
            new byte[] { 3, 3 },
            IccClutDataType.Float);

        internal static IccClut Clut2x1 = new IccClut(
            new float[][]
            {
                new float[] { 0.1f },
                new float[] { 0.2f },
                new float[] { 0.3f },

                new float[] { 0.4f },
                new float[] { 0.5f },
                new float[] { 0.6f },

                new float[] { 0.7f },
                new float[] { 0.8f },
                new float[] { 0.9f },
            },
            new byte[] { 3, 3 },
            IccClutDataType.Float);

        internal static IccClut Clut1x2 = new IccClut(
            new float[][]
            {
                new float[] { 0f, 0.5f },
                new float[] { 0.25f, 0.75f, },
                new float[] { 0.5f, 1f },
            },
            new byte[] { 3 },
            IccClutDataType.Float);

        internal static IccClut Clut1x1 = new IccClut(
            new float[][]
            {
                new float[] { 0f },
                new float[] { 0.5f },
                new float[] { 1f },
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
}
