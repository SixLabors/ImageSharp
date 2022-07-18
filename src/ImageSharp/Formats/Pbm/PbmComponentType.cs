// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// The data type of the components of the pixels.
    /// </summary>
    public enum PbmComponentType : byte
    {
        /// <summary>
        /// Single bit per pixel, exclusively for <see cref="PbmColorType.BlackAndWhite"/>.
        /// </summary>
        Bit = 0,

        /// <summary>
        /// 8 bits unsigned integer per component.
        /// </summary>
        Byte = 1,

        /// <summary>
        /// 16 bits unsigned integer per component.
        /// </summary>
        Short = 2
    }
}
