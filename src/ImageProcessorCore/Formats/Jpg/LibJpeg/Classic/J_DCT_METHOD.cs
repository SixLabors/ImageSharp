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
    /// Algorithm used for the DCT step.
    /// </summary>
    /// <remarks>The <c>FLOAT</c> method is very slightly more accurate than the <c>ISLOW</c> method, 
    /// but may give different results on different machines due to varying roundoff behavior. 
    /// The integer methods should give the same results on all machines. On machines with 
    /// sufficiently fast hardware, the floating-point method may also be the fastest. 
    /// The <c>IFAST</c> method is considerably less accurate than the other two; its use is not recommended 
    /// if high quality is a concern.</remarks>
    /// <seealso cref="jpeg_compress_struct.Dct_method"/>
    /// <seealso cref="jpeg_decompress_struct.Dct_method"/>
#if EXPOSE_LIBJPEG
    public
#endif
    enum J_DCT_METHOD
    {
        /// <summary>
        /// Slow but accurate integer algorithm.
        /// </summary>
        JDCT_ISLOW,

        /// <summary>
        /// Faster, less accurate integer method.
        /// </summary>
        JDCT_IFAST,

        /// <summary>
        /// Floating-point method.
        /// </summary>
        JDCT_FLOAT
    }
}
