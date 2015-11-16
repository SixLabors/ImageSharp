// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProcessorTestBase.cs" company="James Jackson-South">
//   Copyright © James Jackson-South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using System.Collections.Generic;

    /// <summary>
    /// The processor test base.
    /// </summary>
    public abstract class ProcessorTestBase
    {
        /// <summary>
        /// The collection of image files to test against.
        /// </summary>
        public static readonly List<string> Files = new List<string>
        {
            //"../../TestImages/Formats/Jpg/Backdrop.jpg",
            //"../../TestImages/Formats/Jpg/Calliphora.jpg",
            "TestImages/Formats/Jpg/china.jpg",
            //"../../TestImages/Formats/Jpg/ant.jpg",
            //"../../TestImages/Formats/Jpg/parachute.jpg",
            //"../../TestImages/Formats/Jpg/lomo.jpg",
            //"../../TestImages/Formats/Jpg/shaftesbury.jpg",
            //"../../TestImages/Formats/Jpg/gamma_dalai_lama_gray.jpg",
            //"../../TestImages/Formats/Jpg/greyscale.jpg",
            //"../../TestImages/Formats/Bmp/Car.bmp",
            "TestImages/Formats/Png/cballs.png",
            //"../../TestImages/Formats/Png/cmyk.png",
            //"../../TestImages/Formats/Png/gamma-1.0-or-2.2.png",
            "TestImages/Formats/Png/splash.png",
            //"../../TestImages/Formats/Gif/leaf.gif",
            //"../../TestImages/Formats/Gif/ben2.gif",
            //"../../TestImages/Formats/Gif/rings.gif",
            //"../../TestImages/Formats/Gif/ani2.gif",
            //"../../TestImages/Formats/Gif/giphy.gif"
        };
    }
}
