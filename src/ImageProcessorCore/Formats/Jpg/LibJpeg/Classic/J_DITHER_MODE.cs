/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// Dithering options for decompression.
    /// </summary>
    /// <seealso cref="jpeg_decompress_struct.Dither_mode"/>
#if EXPOSE_LIBJPEG
    public
#endif
    enum J_DITHER_MODE
    {
        /// <summary>
        /// No dithering: fast, very low quality
        /// </summary>
        JDITHER_NONE,

        /// <summary>
        /// Ordered dither: moderate speed and quality
        /// </summary>
        JDITHER_ORDERED,

        /// <summary>
        /// Floyd-Steinberg dither: slow, high quality
        /// </summary>
        JDITHER_FS
    }
}
