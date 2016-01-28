/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains compression data destination routines for the case of
 * emitting JPEG data to a file (or any stdio stream).  While these routines
 * are sufficient for most applications, some will want to use a different
 * destination manager.
 * IMPORTANT: we assume that fwrite() will correctly transcribe an array of
 * bytes into 8-bit-wide elements on external storage.  If char is wider
 * than 8 bits on your machine, you may need to do some tweaking.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace BitMiracle.LibJpeg.Classic.Internal
{
    /// <summary>
    /// Expanded data destination object for output to Stream
    /// </summary>
    class my_destination_mgr : jpeg_destination_mgr
    {
        private const int OUTPUT_BUF_SIZE = 4096;   /* choose an efficiently fwrite'able size */

        private jpeg_compress_struct m_cinfo;

        private Stream m_outfile;      /* target stream */
        private byte[] m_buffer;     /* start of buffer */

        public my_destination_mgr(jpeg_compress_struct cinfo, Stream alreadyOpenFile)
        {
            m_cinfo = cinfo;
            m_outfile = alreadyOpenFile;
        }

        /// <summary>
        /// Initialize destination --- called by jpeg_start_compress
        /// before any data is actually written.
        /// </summary>
        public override void init_destination()
        {
            /* Allocate the output buffer --- it will be released when done with image */
            m_buffer = new byte[OUTPUT_BUF_SIZE];
            initInternalBuffer(m_buffer, 0);
        }

        /// <summary>
        /// Empty the output buffer --- called whenever buffer fills up.
        /// 
        /// In typical applications, this should write the entire output buffer
        /// (ignoring the current state of next_output_byte and free_in_buffer),
        /// reset the pointer and count to the start of the buffer, and return true
        /// indicating that the buffer has been dumped.
        /// 
        /// In applications that need to be able to suspend compression due to output
        /// overrun, a false return indicates that the buffer cannot be emptied now.
        /// In this situation, the compressor will return to its caller (possibly with
        /// an indication that it has not accepted all the supplied scanlines).  The
        /// application should resume compression after it has made more room in the
        /// output buffer.  Note that there are substantial restrictions on the use of
        /// suspension --- see the documentation.
        /// 
        /// When suspending, the compressor will back up to a convenient restart point
        /// (typically the start of the current MCU). next_output_byte and free_in_buffer
        /// indicate where the restart point will be if the current call returns false.
        /// Data beyond this point will be regenerated after resumption, so do not
        /// write it out when emptying the buffer externally.
        /// </summary>
        public override bool empty_output_buffer()
        {
            writeBuffer(m_buffer.Length);
            initInternalBuffer(m_buffer, 0);
            return true;
        }

        /// <summary>
        /// Terminate destination --- called by jpeg_finish_compress
        /// after all data has been written.  Usually needs to flush buffer.
        /// 
        /// NB: *not* called by jpeg_abort or jpeg_destroy; surrounding
        /// application must deal with any cleanup that should happen even
        /// for error exit.
        /// </summary>
        public override void term_destination()
        {
            int datacount = m_buffer.Length - freeInBuffer;

            /* Write any data remaining in the buffer */
            if (datacount > 0)
                writeBuffer(datacount);

            m_outfile.Flush();
        }

        private void writeBuffer(int dataCount)
        {
            try
            {
                m_outfile.Write(m_buffer, 0, dataCount);
            }
            catch (IOException e)
            {
                m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, e.Message);
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
            }
            catch (NotSupportedException e)
            {
                m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, e.Message);
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
            }
            catch (ObjectDisposedException e)
            {
                m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JERR_FILE_WRITE, e.Message);
                m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FILE_WRITE);
            }
        }
    }
}
