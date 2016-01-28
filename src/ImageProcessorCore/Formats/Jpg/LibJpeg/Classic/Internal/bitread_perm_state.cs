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

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Bitreading state saved across MCUs
    /// </summary>
    struct bitread_perm_state
    {
        public int get_buffer;    /* current bit-extraction buffer */
        public int bits_left;      /* # of unused bits in it */
    }
}
