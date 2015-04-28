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
    /// Representation of special JPEG marker.
    /// </summary>
    /// <remarks>You can't create instance of this class manually.
    /// Concrete objects are instantiated by library and you can get them
    /// through <see cref="jpeg_decompress_struct.Marker_list">Marker_list</see> property.
    /// </remarks>
    /// <seealso cref="jpeg_decompress_struct.Marker_list"/>
    /// <seealso href="81c88818-a5d7-4550-9ce5-024a768f7b1e.htm" target="_self">Special markers</seealso>
#if EXPOSE_LIBJPEG
    public
#endif
    class jpeg_marker_struct
    {
        private byte m_marker;           /* marker code: JPEG_COM, or JPEG_APP0+n */
        private int m_originalLength;   /* # bytes of data in the file */
        private byte[] m_data;       /* the data contained in the marker */

        internal jpeg_marker_struct(byte marker, int originalDataLength, int lengthLimit)
        {
            m_marker = marker;
            m_originalLength = originalDataLength;
            m_data = new byte[lengthLimit];
        }

        /// <summary>
        /// Gets the special marker.
        /// </summary>
        /// <value>The marker value.</value>
        public byte Marker
        {
            get
            {
                return m_marker;
            }
        }

        /// <summary>
        /// Gets the full length of original data associated with the marker.
        /// </summary>
        /// <value>The length of original data associated with the marker.</value>
        /// <remarks>This length excludes the marker length word, whereas the stored representation 
        /// within the JPEG file includes it. (Hence the maximum data length is really only 65533.)
        /// </remarks>
        public int OriginalLength
        {
            get
            {
                return m_originalLength;
            }
        }

        /// <summary>
        /// Gets the data associated with the marker.
        /// </summary>
        /// <value>The data associated with the marker.</value>
        /// <remarks>The length of this array doesn't exceed <c>length_limit</c> for the particular marker type.
        /// Note that this length excludes the marker length word, whereas the stored representation 
        /// within the JPEG file includes it. (Hence the maximum data length is really only 65533.)
        /// </remarks>
        public byte[] Data
        {
            get
            {
                return m_data;
            }
        }
    }
}
