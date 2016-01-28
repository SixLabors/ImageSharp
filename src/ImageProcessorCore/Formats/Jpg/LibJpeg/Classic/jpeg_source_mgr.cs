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
    /// Data source object for decompression.
    /// </summary>
#if EXPOSE_LIBJPEG
    public
#endif
    abstract class jpeg_source_mgr
    {
        private byte[] m_next_input_byte;
        private int m_bytes_in_buffer; /* # of bytes remaining (unread) in buffer */
        private int m_position;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public abstract void init_source();

        /// <summary>
        /// Fills input buffer
        /// </summary>
        /// <returns><c>true</c> if operation succeed; otherwise, <c>false</c></returns>
        public abstract bool fill_input_buffer();

        /// <summary>
        /// Initializes the internal buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="size">The size.</param>
        protected void initInternalBuffer(byte[] buffer, int size)
        {
            m_bytes_in_buffer = size;
            m_next_input_byte = buffer;
            m_position = 0;
        }

        /// <summary>
        /// Skip data - used to skip over a potentially large amount of
        /// uninteresting data (such as an APPn marker).
        /// </summary>
        /// <param name="num_bytes">The number of bytes to skip.</param>
        /// <remarks>Writers of suspendable-input applications must note that skip_input_data
        /// is not granted the right to give a suspension return.  If the skip extends
        /// beyond the data currently in the buffer, the buffer can be marked empty so
        /// that the next read will cause a fill_input_buffer call that can suspend.
        /// Arranging for additional bytes to be discarded before reloading the input
        /// buffer is the application writer's problem.</remarks>
        public virtual void skip_input_data(int num_bytes)
        {
            /* Just a dumb implementation for now.  Could use fseek() except
            * it doesn't work on pipes.  Not clear that being smart is worth
            * any trouble anyway --- large skips are infrequent.
            */
            if (num_bytes > 0)
            {
                while (num_bytes > m_bytes_in_buffer)
                {
                    num_bytes -= m_bytes_in_buffer;
                    fill_input_buffer();
                    /* note we assume that fill_input_buffer will never return false,
                    * so suspension need not be handled.
                    */
                }

                m_position += num_bytes;
                m_bytes_in_buffer -= num_bytes;
            }
        }
        
        /// <summary>
        /// This is the default resync_to_restart method for data source 
        /// managers to use if they don't have any better approach.
        /// </summary>
        /// <param name="cinfo">An instance of <see cref="jpeg_decompress_struct"/></param>
        /// <param name="desired">The desired</param>
        /// <returns><c>false</c> if suspension is required.</returns>
        /// <remarks>That method assumes that no backtracking is possible. 
        /// Some data source managers may be able to back up, or may have 
        /// additional knowledge about the data which permits a more 
        /// intelligent recovery strategy; such managers would
        /// presumably supply their own resync method.<br/><br/>
        /// 
        /// read_restart_marker calls resync_to_restart if it finds a marker other than
        /// the restart marker it was expecting.  (This code is *not* used unless
        /// a nonzero restart interval has been declared.)  cinfo.unread_marker is
        /// the marker code actually found (might be anything, except 0 or FF).
        /// The desired restart marker number (0..7) is passed as a parameter.<br/><br/>
        /// 
        /// This routine is supposed to apply whatever error recovery strategy seems
        /// appropriate in order to position the input stream to the next data segment.
        /// Note that cinfo.unread_marker is treated as a marker appearing before
        /// the current data-source input point; usually it should be reset to zero
        /// before returning.<br/><br/>
        /// 
        /// This implementation is substantially constrained by wanting to treat the
        /// input as a data stream; this means we can't back up.  Therefore, we have
        /// only the following actions to work with:<br/>
        /// 1. Simply discard the marker and let the entropy decoder resume at next
        /// byte of file.<br/>
        /// 2. Read forward until we find another marker, discarding intervening
        /// data.  (In theory we could look ahead within the current bufferload,
        /// without having to discard data if we don't find the desired marker.
        /// This idea is not implemented here, in part because it makes behavior
        /// dependent on buffer size and chance buffer-boundary positions.)<br/>
        /// 3. Leave the marker unread (by failing to zero cinfo.unread_marker).
        /// This will cause the entropy decoder to process an empty data segment,
        /// inserting dummy zeroes, and then we will reprocess the marker.<br/>
        /// 
        /// #2 is appropriate if we think the desired marker lies ahead, while #3 is
        /// appropriate if the found marker is a future restart marker (indicating
        /// that we have missed the desired restart marker, probably because it got
        /// corrupted).<br/>
        /// We apply #2 or #3 if the found marker is a restart marker no more than
        /// two counts behind or ahead of the expected one.  We also apply #2 if the
        /// found marker is not a legal JPEG marker code (it's certainly bogus data).
        /// If the found marker is a restart marker more than 2 counts away, we do #1
        /// (too much risk that the marker is erroneous; with luck we will be able to
        /// resync at some future point).<br/>
        /// For any valid non-restart JPEG marker, we apply #3.  This keeps us from
        /// overrunning the end of a scan.  An implementation limited to single-scan
        /// files might find it better to apply #2 for markers other than EOI, since
        /// any other marker would have to be bogus data in that case.</remarks>
        public virtual bool resync_to_restart(jpeg_decompress_struct cinfo, int desired)
        {
            /* Always put up a warning. */
            cinfo.WARNMS(J_MESSAGE_CODE.JWRN_MUST_RESYNC, cinfo.m_unread_marker, desired);

            /* Outer loop handles repeated decision after scanning forward. */
            int action = 1;
            for ( ; ; )
            {
                if (cinfo.m_unread_marker < (int)JPEG_MARKER.SOF0)
                {
                    /* invalid marker */
                    action = 2;
                }
                else if (cinfo.m_unread_marker < (int)JPEG_MARKER.RST0 ||
                    cinfo.m_unread_marker > (int)JPEG_MARKER.RST7)
                {
                    /* valid non-restart marker */
                    action = 3;
                }
                else
                {
                    if (cinfo.m_unread_marker == ((int)JPEG_MARKER.RST0 + ((desired + 1) & 7))
                        || cinfo.m_unread_marker == ((int)JPEG_MARKER.RST0 + ((desired + 2) & 7)))
                    {
                        /* one of the next two expected restarts */
                        action = 3;
                    }
                    else if (cinfo.m_unread_marker == ((int)JPEG_MARKER.RST0 + ((desired - 1) & 7)) ||
                        cinfo.m_unread_marker == ((int)JPEG_MARKER.RST0 + ((desired - 2) & 7)))
                    {
                        /* a prior restart, so advance */
                        action = 2;
                    }
                    else
                    {
                        /* desired restart or too far away */
                        action = 1;
                    }
                }

                cinfo.TRACEMS(4, J_MESSAGE_CODE.JTRC_RECOVERY_ACTION, cinfo.m_unread_marker, action);

                switch (action)
                {
                    case 1:
                        /* Discard marker and let entropy decoder resume processing. */
                        cinfo.m_unread_marker = 0;
                        return true;
                    case 2:
                        /* Scan to the next marker, and repeat the decision loop. */
                        if (!cinfo.m_marker.next_marker())
                            return false;
                        break;
                    case 3:
                        /* Return without advancing past this marker. */
                        /* Entropy decoder will be forced to process an empty segment. */
                        return true;
                }
            }
        }

        /// <summary>
        /// Terminate source - called by jpeg_finish_decompress
        /// after all data has been read.  Often a no-op.
        /// </summary>
        /// <remarks>NB: <b>not</b> called by jpeg_abort or jpeg_destroy; surrounding
        /// application must deal with any cleanup that should happen even
        /// for error exit.</remarks>
        public virtual void term_source()
        {
        }

        /// <summary>
        /// Reads two bytes interpreted as an unsigned 16-bit integer.
        /// </summary>
        /// <param name="V">The result.</param>
        /// <returns><c>true</c> if operation succeed; otherwise, <c>false</c></returns>
        public virtual bool GetTwoBytes(out int V)
        {
            if (!MakeByteAvailable())
            {
                V = 0;
                return false;
            }

            m_bytes_in_buffer--;
            V = m_next_input_byte[m_position] << 8;
            m_position++;

            if (!MakeByteAvailable())
                return false;

            m_bytes_in_buffer--;
            V += m_next_input_byte[m_position];
            m_position++;
            return true;
        }

        /// <summary>
        /// Read a byte into variable V.
        /// If must suspend, take the specified action (typically "return false").
        /// </summary>
        /// <param name="V">The result.</param>
        /// <returns><c>true</c> if operation succeed; otherwise, <c>false</c></returns>
        public virtual bool GetByte(out int V)
        {
            if (!MakeByteAvailable())
            {
                V = 0;
                return false;
            }

            m_bytes_in_buffer--;
            V = m_next_input_byte[m_position];
            m_position++;
            return true;
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        /// <param name="dest">The destination.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>The number of available bytes.</returns>
        public virtual int GetBytes(byte[] dest, int amount)
        {
            int avail = amount;
            if (avail > m_bytes_in_buffer)
                avail = m_bytes_in_buffer;

            for (int i = 0; i < avail; i++)
            {
                dest[i] = m_next_input_byte[m_position];
                m_position++;
                m_bytes_in_buffer--;
            }

            return avail;
        }

        /// <summary>
        /// Functions for fetching data from the data source module.
        /// </summary>
        /// <returns><c>true</c> if operation succeed; otherwise, <c>false</c></returns>
        /// <remarks>At all times, cinfo.src.next_input_byte and .bytes_in_buffer reflect
        /// the current restart point; we update them only when we have reached a
        /// suitable place to restart if a suspension occurs.</remarks>
        public virtual bool MakeByteAvailable()
        {
            if (m_bytes_in_buffer == 0)
            {
                if (!fill_input_buffer())
                    return false;
            }

            return true;
        }
    }
}
