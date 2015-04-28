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
    /// Huffman coding table.
    /// </summary>
#if EXPOSE_LIBJPEG
    public
#endif
    class JHUFF_TBL
    {
        /* These two fields directly represent the contents of a JPEG DHT marker */
        private readonly byte[] m_bits = new byte[17];     /* bits[k] = # of symbols with codes of */
        
        /* length k bits; bits[0] is unused */
        private readonly byte[] m_huffval = new byte[256];     /* The symbols, in order of incr code length */
        
        private bool m_sent_table;        /* true when table has been output */


        internal JHUFF_TBL()
        {
        }

        internal byte[] Bits
        {
            get { return m_bits; }
        }

        internal byte[] Huffval
        {
            get { return m_huffval; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the table has been output to file.
        /// </summary>
        /// <value>It's initialized <c>false</c> when the table is created, and set 
        /// <c>true</c> when it's been output to the file. You could suppress output 
        /// of a table by setting this to <c>true</c>.
        /// </value>
        /// <remarks>This property is used only during compression. It's initialized
        /// <c>false</c> when the table is created, and set <c>true</c> when it's been
        /// output to the file. You could suppress output of a table by setting this to
        /// <c>true</c>. (See jpeg_suppress_tables for an example.)</remarks>
        /// <seealso cref="jpeg_compress_struct.jpeg_suppress_tables"/>
        public bool Sent_table
        {
            get { return m_sent_table; }
            set { m_sent_table = value; }
        }
    }
}
