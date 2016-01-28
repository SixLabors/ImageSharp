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
    /// Operating modes for buffer controllers
    /// </summary>
    enum J_BUF_MODE
    {
        JBUF_PASS_THRU,         /* Plain stripwise operation */

        /* Remaining modes require a full-image buffer to have been created */

        JBUF_SAVE_SOURCE,       /* Run source subobject only, save output */
        JBUF_CRANK_DEST,        /* Run dest subobject only, using saved data */
        JBUF_SAVE_AND_PASS      /* Run both subobjects, save output */
    }
}
