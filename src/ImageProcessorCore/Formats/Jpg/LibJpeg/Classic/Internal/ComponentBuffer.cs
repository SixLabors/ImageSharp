/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Encapsulates buffer of image samples for one color component
    /// When provided with funny indices (see jpeg_d_main_controller for 
    /// explanation of what it is) uses them for non-linear row access.
    /// </summary>
    class ComponentBuffer
    {
        private byte[][] m_buffer;

        // array of funny indices
        private int[] m_funnyIndices;

        // index of "first funny index" (used because some code uses negative 
        // indices when retrieve rows)
        // see for example my_upsampler.h2v2_fancy_upsample
        private int m_funnyOffset;

        public ComponentBuffer()
        {
        }

        public ComponentBuffer(byte[][] buf, int[] funnyIndices, int funnyOffset)
        {
            SetBuffer(buf, funnyIndices, funnyOffset);
        }

        public void SetBuffer(byte[][] buf, int[] funnyIndices, int funnyOffset)
        {
            m_buffer = buf;
            m_funnyIndices = funnyIndices;
            m_funnyOffset = funnyOffset;
        }

        public byte[] this[int i]
        {
            get
            {
                if (m_funnyIndices == null)
                    return m_buffer[i];

                return m_buffer[m_funnyIndices[i + m_funnyOffset]];
            }
        }
    }
}
