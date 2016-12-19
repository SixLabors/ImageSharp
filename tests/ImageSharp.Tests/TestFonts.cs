// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    /// <summary>
    /// Class that contains all the test images.
    /// </summary>
    public static class TestFonts
    {
        public static class Ttf
        {
            private static readonly string folder = "TestFonts/Formats/TTF/";

            public static string OpenSans_Regular => folder + "OpenSans-Regular.ttf";
        }
    }
}
