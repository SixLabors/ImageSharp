/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains the JPEG system-independent memory management
 * routines. 
 */

/*
 * About virtual array management:
 *
 * Full-image-sized buffers are handled as "virtual" arrays.  The array is still accessed a strip at a
 * time, but the memory manager must save the whole array for repeated
 * accesses.
 *
 * The Access method is responsible for making a specific strip area accessible.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// JPEG virtual array.
    /// </summary>
    /// <typeparam name="T">The type of array's elements.</typeparam>
    /// <remarks>You can't create virtual array manually. For creation use methods
    /// <see cref="jpeg_common_struct.CreateSamplesArray"/> and
    /// <see cref="jpeg_common_struct.CreateBlocksArray"/>.
    /// </remarks>
#if EXPOSE_LIBJPEG
    public
#endif
    class jvirt_array<T>
    {
        internal delegate T[][] Allocator(int width, int height);

        private jpeg_common_struct m_cinfo;

        private T[][] m_buffer;   /* => the in-memory buffer */

        /// <summary>
        /// Request a virtual 2-D array
        /// </summary>
        /// <param name="width">Width of array</param>
        /// <param name="height">Total virtual array height</param>
        /// <param name="allocator">The allocator.</param>
        internal jvirt_array(int width, int height, Allocator allocator)
        {
            m_cinfo = null;
            m_buffer = allocator(width, height);

            Debug.Assert(m_buffer != null);
        }

        /// <summary>
        /// Gets or sets the error processor.
        /// </summary>
        /// <value>The error processor.<br/>
        /// Default value: <c>null</c>
        /// </value>
        /// <remarks>Uses only for calling 
        /// <see cref="M:BitMiracle.LibJpeg.Classic.jpeg_common_struct.ERREXIT(BitMiracle.LibJpeg.Classic.J_MESSAGE_CODE)">jpeg_common_struct.ERREXIT</see>
        /// on error.</remarks>
        public jpeg_common_struct ErrorProcessor
        {
            get { return m_cinfo; }
            set { m_cinfo = value; }
        }

        /// <summary>
        /// Access the part of a virtual array.
        /// </summary>
        /// <param name="startRow">The first row in required block.</param>
        /// <param name="numberOfRows">The number of required rows.</param>
        /// <returns>The required part of virtual array.</returns>
        public T[][] Access(int startRow, int numberOfRows)
        {
            /* debugging check */
            if (startRow + numberOfRows > m_buffer.Length)
            {
                if (m_cinfo != null)
                    m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_VIRTUAL_ACCESS);
                else
                    throw new InvalidOperationException("Bogus virtual array access");
            }

            /* Return proper part of the buffer */
            T[][] ret = new T[numberOfRows][];
            for (int i = 0; i < numberOfRows; i++)
                ret[i] = m_buffer[startRow + i];

            return ret;
        }
    }
}
