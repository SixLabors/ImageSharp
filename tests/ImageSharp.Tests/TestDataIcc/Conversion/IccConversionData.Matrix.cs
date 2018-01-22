// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataMatrix
    {
        public static float[,] Matrix = { { 0.1f, 0.2f, 0.3f }, { 0.4f, 0.5f, 0.6f }, { 0.7f, 0.8f, 0.9f } };

        public static object[][] MatrixConversionTestData =
        {
        };
    }
}
