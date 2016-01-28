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
    /// JPEG marker codes.
    /// </summary>
    /// <seealso href="81c88818-a5d7-4550-9ce5-024a768f7b1e.htm" target="_self">Special markers</seealso>
#if EXPOSE_LIBJPEG
    public
#endif
    enum JPEG_MARKER
    {
        /// <summary>
        /// 
        /// </summary>
        SOF0 = 0xc0,
        /// <summary>
        /// 
        /// </summary>
        SOF1 = 0xc1,
        /// <summary>
        /// 
        /// </summary>
        SOF2 = 0xc2,
        /// <summary>
        /// 
        /// </summary>
        SOF3 = 0xc3,
        /// <summary>
        /// 
        /// </summary>
        SOF5 = 0xc5,
        /// <summary>
        /// 
        /// </summary>
        SOF6 = 0xc6,
        /// <summary>
        /// 
        /// </summary>
        SOF7 = 0xc7,
        /// <summary>
        /// 
        /// </summary>
        JPG = 0xc8,
        /// <summary>
        /// 
        /// </summary>
        SOF9 = 0xc9,
        /// <summary>
        /// 
        /// </summary>
        SOF10 = 0xca,
        /// <summary>
        /// 
        /// </summary>
        SOF11 = 0xcb,
        /// <summary>
        /// 
        /// </summary>
        SOF13 = 0xcd,
        /// <summary>
        /// 
        /// </summary>
        SOF14 = 0xce,
        /// <summary>
        /// 
        /// </summary>
        SOF15 = 0xcf,
        /// <summary>
        /// 
        /// </summary>
        DHT = 0xc4,
        /// <summary>
        /// 
        /// </summary>
        DAC = 0xcc,
        /// <summary>
        /// 
        /// </summary>
        RST0 = 0xd0,
        /// <summary>
        /// 
        /// </summary>
        RST1 = 0xd1,
        /// <summary>
        /// 
        /// </summary>
        RST2 = 0xd2,
        /// <summary>
        /// 
        /// </summary>
        RST3 = 0xd3,
        /// <summary>
        /// 
        /// </summary>
        RST4 = 0xd4,
        /// <summary>
        /// 
        /// </summary>
        RST5 = 0xd5,
        /// <summary>
        /// 
        /// </summary>
        RST6 = 0xd6,
        /// <summary>
        /// 
        /// </summary>
        RST7 = 0xd7,
        /// <summary>
        /// 
        /// </summary>
        SOI = 0xd8,
        /// <summary>
        /// 
        /// </summary>
        EOI = 0xd9,
        /// <summary>
        /// 
        /// </summary>
        SOS = 0xda,
        /// <summary>
        /// 
        /// </summary>
        DQT = 0xdb,
        /// <summary>
        /// 
        /// </summary>
        DNL = 0xdc,
        /// <summary>
        /// 
        /// </summary>
        DRI = 0xdd,
        /// <summary>
        /// 
        /// </summary>
        DHP = 0xde,
        /// <summary>
        /// 
        /// </summary>
        EXP = 0xdf,
        /// <summary>
        /// 
        /// </summary>
        APP0 = 0xe0,
        /// <summary>
        /// 
        /// </summary>
        APP1 = 0xe1,
        /// <summary>
        /// 
        /// </summary>
        APP2 = 0xe2,
        /// <summary>
        /// 
        /// </summary>
        APP3 = 0xe3,
        /// <summary>
        /// 
        /// </summary>
        APP4 = 0xe4,
        /// <summary>
        /// 
        /// </summary>
        APP5 = 0xe5,
        /// <summary>
        /// 
        /// </summary>
        APP6 = 0xe6,
        /// <summary>
        /// 
        /// </summary>
        APP7 = 0xe7,
        /// <summary>
        /// 
        /// </summary>
        APP8 = 0xe8,
        /// <summary>
        /// 
        /// </summary>
        APP9 = 0xe9,
        /// <summary>
        /// 
        /// </summary>
        APP10 = 0xea,
        /// <summary>
        /// 
        /// </summary>
        APP11 = 0xeb,
        /// <summary>
        /// 
        /// </summary>
        APP12 = 0xec,
        /// <summary>
        /// 
        /// </summary>
        APP13 = 0xed,
        /// <summary>
        /// 
        /// </summary>
        APP14 = 0xee,
        /// <summary>
        /// 
        /// </summary>
        APP15 = 0xef,
        /// <summary>
        /// 
        /// </summary>
        JPG0 = 0xf0,
        /// <summary>
        /// 
        /// </summary>
        JPG13 = 0xfd,
        /// <summary>
        /// 
        /// </summary>
        COM = 0xfe,
        /// <summary>
        /// 
        /// </summary>
        TEM = 0x01,
        /// <summary>
        /// 
        /// </summary>
        ERROR = 0x100
    }
}
