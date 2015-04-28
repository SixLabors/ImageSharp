using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg
{
    /// <summary>
    /// Known color spaces.
    /// </summary>
#if EXPOSE_LIBJPEG
    public
#endif
    enum Colorspace
    {
        /// <summary>
        /// Unspecified colorspace
        /// </summary>
        Unknown,

        /// <summary>
        /// Grayscale
        /// </summary>
        Grayscale,

        /// <summary>
        /// RGB
        /// </summary>
        RGB,

        /// <summary>
        /// YCbCr (also known as YUV)
        /// </summary>
        YCbCr,

        /// <summary>
        /// CMYK
        /// </summary>
        CMYK,

        /// <summary>
        /// YCbCrK
        /// </summary>
        YCCK
    }

    /// <summary>
    /// DCT/IDCT algorithm options.
    /// </summary>
    enum DCTMethod
    {
        IntegerSlow,     /* slow but accurate integer algorithm */
        IntegerFast,     /* faster, less accurate integer method */
        Float            /* floating-point: accurate, fast on fast HW */
    }

    /// <summary>
    /// Dithering options for decompression.
    /// </summary>
    enum DitherMode
    {
        None,               /* no dithering */
        Ordered,            /* simple ordered dither */
        FloydSteinberg      /* Floyd-Steinberg error diffusion dither */
    }
}
