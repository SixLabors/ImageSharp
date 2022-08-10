// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Provides enumeration of the various TIFF photometric interpretation implementation types.
    /// </summary>
    internal enum TiffColorType
    {
        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white.
        /// </summary>
        BlackIsZero,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for bilevel images.
        /// </summary>
        BlackIsZero1,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 4-bit images.
        /// </summary>
        BlackIsZero4,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 8-bit images.
        /// </summary>
        BlackIsZero8,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 16-bit images.
        /// </summary>
        BlackIsZero16,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 24-bit images.
        /// </summary>
        BlackIsZero24,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 32-bit images.
        /// </summary>
        BlackIsZero32,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Pixel data is 32-bit float.
        /// </summary>
        BlackIsZero32Float,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black.
        /// </summary>
        WhiteIsZero,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for bilevel images.
        /// </summary>
        WhiteIsZero1,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 4-bit images.
        /// </summary>
        WhiteIsZero4,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 8-bit images.
        /// </summary>
        WhiteIsZero8,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 16-bit images.
        /// </summary>
        WhiteIsZero16,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 24-bit images.
        /// </summary>
        WhiteIsZero24,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 32-bit images.
        /// </summary>
        WhiteIsZero32,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Pixel data is 32-bit float.
        /// </summary>
        WhiteIsZero32Float,

        /// <summary>
        /// Palette-color.
        /// </summary>
        PaletteColor,

        /// <summary>
        /// RGB Full Color.
        /// </summary>
        Rgb,

        /// <summary>
        /// RGB color image with 2 bits for each channel.
        /// </summary>
        Rgb222,

        /// <summary>
        /// RGBA color image with 2 bits for each channel.
        /// </summary>
        Rgba2222,

        /// <summary>
        /// RGB color image with 3 bits for each channel.
        /// </summary>
        Rgb333,

        /// <summary>
        /// RGBA color image with 3 bits for each channel.
        /// </summary>
        Rgba3333,

        /// <summary>
        /// RGB color image with 4 bits for each channel.
        /// </summary>
        Rgb444,

        /// <summary>
        /// RGBA color image with 4 bits for each channel.
        /// </summary>
        Rgba4444,

        /// <summary>
        /// RGB color image with 5 bits for each channel.
        /// </summary>
        Rgb555,

        /// <summary>
        /// RGBA color image with 5 bits for each channel.
        /// </summary>
        Rgba5555,

        /// <summary>
        /// RGB color image with 6 bits for each channel.
        /// </summary>
        Rgb666,

        /// <summary>
        /// RGBA color image with 6 bits for each channel.
        /// </summary>
        Rgba6666,

        /// <summary>
        /// RGB Full Color. Optimized implementation for 8-bit images.
        /// </summary>
        Rgb888,

        /// <summary>
        /// RGBA Full Color with 8-bit for each channel.
        /// </summary>
        Rgba8888,

        /// <summary>
        /// RGB color image with 10 bits for each channel.
        /// </summary>
        Rgb101010,

        /// <summary>
        /// RGBA color image with 10 bits for each channel.
        /// </summary>
        Rgba10101010,

        /// <summary>
        /// RGB color image with 12 bits for each channel.
        /// </summary>
        Rgb121212,

        /// <summary>
        /// RGBA color image with 12 bits for each channel.
        /// </summary>
        Rgba12121212,

        /// <summary>
        /// RGB color image with 14 bits for each channel.
        /// </summary>
        Rgb141414,

        /// <summary>
        /// RGBA color image with 14 bits for each channel.
        /// </summary>
        Rgba14141414,

        /// <summary>
        /// RGB color image with 16 bits for each channel.
        /// </summary>
        Rgb161616,

        /// <summary>
        /// RGBA color image with 16 bits for each channel.
        /// </summary>
        Rgba16161616,

        /// <summary>
        /// RGB color image with 24 bits for each channel.
        /// </summary>
        Rgb242424,

        /// <summary>
        /// RGBA color image with 24 bits for each channel.
        /// </summary>
        Rgba24242424,

        /// <summary>
        /// RGB color image with 32 bits for each channel.
        /// </summary>
        Rgb323232,

        /// <summary>
        /// RGBA color image with 32 bits for each channel.
        /// </summary>
        Rgba32323232,

        /// <summary>
        /// RGB color image with 32 bits floats for each channel.
        /// </summary>
        RgbFloat323232,

        /// <summary>
        /// RGBA color image with 32 bits floats for each channel.
        /// </summary>
        RgbaFloat32323232,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 8 Bit per color channel.
        /// </summary>
        Rgb888Planar,

        /// <summary>
        /// RGBA color image with an alpha channel. Planar configuration of data. 8 Bit per color channel.
        /// </summary>
        Rgba8888Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 16 Bit per color channel.
        /// </summary>
        Rgb161616Planar,

        /// <summary>
        /// RGB Color with an alpha channel. Planar configuration of data. 16 Bit per color channel.
        /// </summary>
        Rgba16161616Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 24 Bit per color channel.
        /// </summary>
        Rgb242424Planar,

        /// <summary>
        /// RGB Color with an alpha channel. Planar configuration of data. 24 Bit per color channel.
        /// </summary>
        Rgba24242424Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 32 Bit per color channel.
        /// </summary>
        Rgb323232Planar,

        /// <summary>
        /// RGB Color with an alpha channel. Planar configuration of data. 32 Bit per color channel.
        /// </summary>
        Rgba32323232Planar,

        /// <summary>
        /// The pixels are stored in YCbCr format.
        /// </summary>
        YCbCr,

        /// <summary>
        /// The pixels are stored in YCbCr format as planar.
        /// </summary>
        YCbCrPlanar,

        /// <summary>
        /// The pixels are stored in CieLab format.
        /// </summary>
        CieLab,

        /// <summary>
        /// The pixels are stored in CieLab format as planar.
        /// </summary>
        CieLabPlanar,
    }
}
