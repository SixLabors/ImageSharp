/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file defines the error and message codes for the JPEG library.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// Message codes used in code to signal errors, warning and trace messages.
    /// </summary>
    /// <seealso cref="jpeg_error_mgr"/>
#if EXPOSE_LIBJPEG
    public
#endif
    enum J_MESSAGE_CODE
    {
        /// <summary>
        /// Must be first entry!
        /// </summary>
        JMSG_NOMESSAGE,

        /// <summary>
        /// 
        /// </summary>
        JERR_ARITH_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_BUFFER_MODE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_COMPONENT_ID,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_DCT_COEF,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_DCTSIZE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_HUFF_TABLE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_IN_COLORSPACE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_J_COLORSPACE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_LENGTH,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_MCU_SIZE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_PRECISION,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_PROGRESSION,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_PROG_SCRIPT,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_SAMPLING,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_SCAN_SCRIPT,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_STATE,
        /// <summary>
        /// 
        /// </summary>
        JERR_BAD_VIRTUAL_ACCESS,
        /// <summary>
        /// 
        /// </summary>
        JERR_BUFFER_SIZE,
        /// <summary>
        /// 
        /// </summary>
        JERR_CANT_SUSPEND,
        /// <summary>
        /// 
        /// </summary>
        JERR_CCIR601_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JERR_COMPONENT_COUNT,
        /// <summary>
        /// 
        /// </summary>
        JERR_CONVERSION_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JERR_DHT_INDEX,
        /// <summary>
        /// 
        /// </summary>
        JERR_DQT_INDEX,
        /// <summary>
        /// 
        /// </summary>
        JERR_EMPTY_IMAGE,
        /// <summary>
        /// 
        /// </summary>
        JERR_EOI_EXPECTED,
        /// <summary>
        /// 
        /// </summary>
        JERR_FILE_WRITE,
        /// <summary>
        /// 
        /// </summary>
        JERR_FRACT_SAMPLE_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JERR_HUFF_CLEN_OVERFLOW,
        /// <summary>
        /// 
        /// </summary>
        JERR_HUFF_MISSING_CODE,
        /// <summary>
        /// 
        /// </summary>
        JERR_IMAGE_TOO_BIG,
        /// <summary>
        /// 
        /// </summary>
        JERR_INPUT_EMPTY,
        /// <summary>
        /// 
        /// </summary>
        JERR_INPUT_EOF,
        /// <summary>
        /// 
        /// </summary>
        JERR_MISMATCHED_QUANT_TABLE,
        /// <summary>
        /// 
        /// </summary>
        JERR_MISSING_DATA,
        /// <summary>
        /// 
        /// </summary>
        JERR_MODE_CHANGE,
        /// <summary>
        /// 
        /// </summary>
        JERR_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JERR_NOT_COMPILED,
        /// <summary>
        /// 
        /// </summary>
        JERR_NO_HUFF_TABLE,
        /// <summary>
        /// 
        /// </summary>
        JERR_NO_IMAGE,
        /// <summary>
        /// 
        /// </summary>
        JERR_NO_QUANT_TABLE,
        /// <summary>
        /// 
        /// </summary>
        JERR_NO_SOI,
        /// <summary>
        /// 
        /// </summary>
        JERR_OUT_OF_MEMORY,
        /// <summary>
        /// 
        /// </summary>
        JERR_QUANT_COMPONENTS,
        /// <summary>
        /// 
        /// </summary>
        JERR_QUANT_FEW_COLORS,
        /// <summary>
        /// 
        /// </summary>
        JERR_QUANT_MANY_COLORS,
        /// <summary>
        /// 
        /// </summary>
        JERR_SOF_DUPLICATE,
        /// <summary>
        /// 
        /// </summary>
        JERR_SOF_NO_SOS,
        /// <summary>
        /// 
        /// </summary>
        JERR_SOF_UNSUPPORTED,
        /// <summary>
        /// 
        /// </summary>
        JERR_SOI_DUPLICATE,
        /// <summary>
        /// 
        /// </summary>
        JERR_SOS_NO_SOF,
        /// <summary>
        /// 
        /// </summary>
        JERR_TOO_LITTLE_DATA,
        /// <summary>
        /// 
        /// </summary>
        JERR_UNKNOWN_MARKER,
        /// <summary>
        /// 
        /// </summary>
        JERR_WIDTH_OVERFLOW,
        /// <summary>
        /// 
        /// </summary>
        JTRC_16BIT_TABLES,
        /// <summary>
        /// 
        /// </summary>
        JTRC_ADOBE,
        /// <summary>
        /// 
        /// </summary>
        JTRC_APP0,
        /// <summary>
        /// 
        /// </summary>
        JTRC_APP14,
        /// <summary>
        /// 
        /// </summary>
        JTRC_DHT,
        /// <summary>
        /// 
        /// </summary>
        JTRC_DQT,
        /// <summary>
        /// 
        /// </summary>
        JTRC_DRI,
        /// <summary>
        /// 
        /// </summary>
        JTRC_EOI,
        /// <summary>
        /// 
        /// </summary>
        JTRC_HUFFBITS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_JFIF,
        /// <summary>
        /// 
        /// </summary>
        JTRC_JFIF_BADTHUMBNAILSIZE,
        /// <summary>
        /// 
        /// </summary>
        JTRC_JFIF_EXTENSION,
        /// <summary>
        /// 
        /// </summary>
        JTRC_JFIF_THUMBNAIL,
        /// <summary>
        /// 
        /// </summary>
        JTRC_MISC_MARKER,
        /// <summary>
        /// 
        /// </summary>
        JTRC_PARMLESS_MARKER,
        /// <summary>
        /// 
        /// </summary>
        JTRC_QUANTVALS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_QUANT_3_NCOLORS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_QUANT_NCOLORS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_QUANT_SELECTED,
        /// <summary>
        /// 
        /// </summary>
        JTRC_RECOVERY_ACTION,
        /// <summary>
        /// 
        /// </summary>
        JTRC_RST,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SMOOTH_NOTIMPL,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOF,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOF_COMPONENT,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOI,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOS_COMPONENT,
        /// <summary>
        /// 
        /// </summary>
        JTRC_SOS_PARAMS,
        /// <summary>
        /// 
        /// </summary>
        JTRC_THUMB_JPEG,
        /// <summary>
        /// 
        /// </summary>
        JTRC_THUMB_PALETTE,
        /// <summary>
        /// 
        /// </summary>
        JTRC_THUMB_RGB,
        /// <summary>
        /// 
        /// </summary>
        JTRC_UNKNOWN_IDS,
        /// <summary>
        /// 
        /// </summary>
        JWRN_ADOBE_XFORM,
        /// <summary>
        /// 
        /// </summary>
        JWRN_BOGUS_PROGRESSION,
        /// <summary>
        /// 
        /// </summary>
        JWRN_EXTRANEOUS_DATA,
        /// <summary>
        /// 
        /// </summary>
        JWRN_HIT_MARKER,
        /// <summary>
        /// 
        /// </summary>
        JWRN_HUFF_BAD_CODE,
        /// <summary>
        /// 
        /// </summary>
        JWRN_JFIF_MAJOR,
        /// <summary>
        /// 
        /// </summary>
        JWRN_JPEG_EOF,
        /// <summary>
        /// 
        /// </summary>
        JWRN_MUST_RESYNC,
        /// <summary>
        /// 
        /// </summary>
        JWRN_NOT_SEQUENTIAL,
        /// <summary>
        /// 
        /// </summary>
        JWRN_TOO_MUCH_DATA,
        /// <summary>
        /// 
        /// </summary>
        JMSG_UNKNOWNMSGCODE,
        /// <summary>
        /// 
        /// </summary>
        JMSG_LASTMSGCODE
    }
}
