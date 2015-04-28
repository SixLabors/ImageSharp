/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// Describes a result of read operation.
    /// </summary>
    /// <seealso cref="jpeg_decompress_struct.jpeg_consume_input"/>
#if EXPOSE_LIBJPEG
    public
#endif
    enum ReadResult
    {
        /// <summary>
        /// Suspended due to lack of input data. Can occur only if a suspending data source is used.
        /// </summary>
        JPEG_SUSPENDED = 0,
        /// <summary>
        /// Found valid image datastream.
        /// </summary>
        JPEG_HEADER_OK = 1,
        /// <summary>
        /// Found valid table-specs-only datastream.
        /// </summary>
        JPEG_HEADER_TABLES_ONLY = 2,
        /// <summary>
        /// Reached a SOS marker (the start of a new scan)
        /// </summary>
        JPEG_REACHED_SOS = 3,
        /// <summary>
        /// Reached the EOI marker (end of image)
        /// </summary>
        JPEG_REACHED_EOI = 4,
        /// <summary>
        /// Completed reading one MCU row of compressed data.
        /// </summary>
        JPEG_ROW_COMPLETED = 5,
        /// <summary>
        /// Completed reading last MCU row of current scan.
        /// </summary>
        JPEG_SCAN_COMPLETED = 6
    }
}
