/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains application interface routines that are used for both
 * compression and decompression.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>Base class for both JPEG compressor and decompresor.</summary>
    /// <remarks>
    /// Routines that are to be used by both halves of the library are declared
    /// to receive an instance of this class. There are no actual instances of 
    /// <see cref="jpeg_common_struct"/>, only of <see cref="jpeg_compress_struct"/> 
    /// and <see cref="jpeg_decompress_struct"/>
    /// </remarks>
#if EXPOSE_LIBJPEG
    public
#endif
    abstract class jpeg_common_struct
    {
        internal enum JpegState
        {
            DESTROYED = 0,
            CSTATE_START = 100,     /* after create_compress */
            CSTATE_SCANNING = 101,  /* start_compress done, write_scanlines OK */
            CSTATE_RAW_OK = 102,    /* start_compress done, write_raw_data OK */
            CSTATE_WRCOEFS = 103,   /* jpeg_write_coefficients done */
            DSTATE_START = 200,     /* after create_decompress */
            DSTATE_INHEADER = 201,  /* reading header markers, no SOS yet */
            DSTATE_READY = 202,     /* found SOS, ready for start_decompress */
            DSTATE_PRELOAD = 203,   /* reading multiscan file in start_decompress*/
            DSTATE_PRESCAN = 204,   /* performing dummy pass for 2-pass quant */
            DSTATE_SCANNING = 205,  /* start_decompress done, read_scanlines OK */
            DSTATE_RAW_OK = 206,    /* start_decompress done, read_raw_data OK */
            DSTATE_BUFIMAGE = 207,  /* expecting jpeg_start_output */
            DSTATE_BUFPOST = 208,   /* looking for SOS/EOI in jpeg_finish_output */
            DSTATE_RDCOEFS = 209,   /* reading file in jpeg_read_coefficients */
            DSTATE_STOPPING = 210   /* looking for EOI in jpeg_finish_decompress */
        }

        // Error handler module
        internal jpeg_error_mgr m_err;
        
        // Progress monitor, or null if none
        internal jpeg_progress_mgr m_progress;
        
        internal JpegState m_global_state;     /* For checking call sequence validity */

        /// <summary>
        /// Base constructor.
        /// </summary>
        /// <seealso cref="jpeg_compress_struct"/>
        /// <seealso cref="jpeg_decompress_struct"/>
        public jpeg_common_struct() : this(new jpeg_error_mgr())
        {
        }

        /// <summary>
        /// Base constructor.
        /// </summary>
        /// <param name="errorManager">The error manager.</param>
        /// <seealso cref="jpeg_compress_struct"/>
        /// <seealso cref="jpeg_decompress_struct"/>
        public jpeg_common_struct(jpeg_error_mgr errorManager)
        {
            Err = errorManager;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is Jpeg decompressor.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this is Jpeg decompressor; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsDecompressor
        {
            get;
        }

        /// <summary>
        /// Progress monitor.
        /// </summary>
        /// <value>The progress manager.</value>
        /// <remarks>Default value: <c>null</c>.</remarks>
        public jpeg_progress_mgr Progress
        {
            get
            {
                return m_progress;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                m_progress = value;
            }
        }

        /// <summary>
        /// Error handler module.
        /// </summary>
        /// <value>The error manager.</value>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        public jpeg_error_mgr Err
        {
            get
            {
                return m_err;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                m_err = value;
            }
        }

        /// <summary>
        /// Gets the version of LibJpeg.
        /// </summary>
        /// <value>The version of LibJpeg.</value>
        public static string Version
        {
            get
            {
                Version version = typeof(jpeg_common_struct).GetTypeInfo().Assembly.GetName().Version;
                string versionString = version.Major.ToString(CultureInfo.InvariantCulture) +
                    "." + version.Minor.ToString(CultureInfo.InvariantCulture);

                versionString += "." + version.Build.ToString(CultureInfo.InvariantCulture);
                versionString += "." + version.Revision.ToString(CultureInfo.InvariantCulture);

                return versionString;
            }
        }

        /// <summary>
        /// Gets the LibJpeg's copyright.
        /// </summary>
        /// <value>The copyright.</value>
        public static string Copyright
        {
            get
            {
                return "Copyright (C) 2008-2011, Bit Miracle";
            }
        }

        /// <summary>
        /// Creates the array of samples.
        /// </summary>
        /// <param name="samplesPerRow">The number of samples in row.</param>
        /// <param name="numberOfRows">The number of rows.</param>
        /// <returns>The array of samples.</returns>
        public static jvirt_array<byte> CreateSamplesArray(int samplesPerRow, int numberOfRows)
        {
            return new jvirt_array<byte>(samplesPerRow, numberOfRows, AllocJpegSamples);
        }

        /// <summary>
        /// Creates the array of blocks.
        /// </summary>
        /// <param name="blocksPerRow">The number of blocks in row.</param>
        /// <param name="numberOfRows">The number of rows.</param>
        /// <returns>The array of blocks.</returns>
        /// <seealso cref="JBLOCK"/>
        public static jvirt_array<JBLOCK> CreateBlocksArray(int blocksPerRow, int numberOfRows)
        {
            return new jvirt_array<JBLOCK>(blocksPerRow, numberOfRows, allocJpegBlocks);
        }

        /// <summary>
        /// Creates 2-D sample array.
        /// </summary>
        /// <param name="samplesPerRow">The number of samples per row.</param>
        /// <param name="numberOfRows">The number of rows.</param>
        /// <returns>The array of samples.</returns>
        public static byte[][] AllocJpegSamples(int samplesPerRow, int numberOfRows)
        {
            byte[][] result = new byte[numberOfRows][];
            for (int i = 0; i < numberOfRows; ++i)
                result[i] = new byte[samplesPerRow];

            return result;
        }

        // Creation of 2-D block arrays.
        private static JBLOCK[][] allocJpegBlocks(int blocksPerRow, int numberOfRows)
        {
            JBLOCK[][] result = new JBLOCK[numberOfRows][];
            for (int i = 0; i < numberOfRows; ++i)
            {
                result[i] = new JBLOCK[blocksPerRow];
                for (int j = 0; j < blocksPerRow; ++j)
                    result[i][j] = new JBLOCK();
            }
            return result;
        }

        // Generic versions of jpeg_abort and jpeg_destroy that work on either
        // flavor of JPEG object.  These may be more convenient in some places.

        /// <summary>
        /// Abort processing of a JPEG compression or decompression operation,
        /// but don't destroy the object itself.
        /// 
        /// Closing a data source or destination, if necessary, is the 
        /// application's responsibility.
        /// </summary>
        public void jpeg_abort()
        {
            /* Reset overall state for possible reuse of object */
            if (IsDecompressor)
            {
                m_global_state = JpegState.DSTATE_START;

                /* Try to keep application from accessing now-deleted marker list.
                 * A bit kludgy to do it here, but this is the most central place.
                 */
                jpeg_decompress_struct s = this as jpeg_decompress_struct;
                if (s != null)
                    s.m_marker_list = null;
            }
            else
            {
                m_global_state = JpegState.CSTATE_START;
            }
        }

        /// <summary>
        /// Destruction of a JPEG object. 
        /// 
        /// Closing a data source or destination, if necessary, is the 
        /// application's responsibility.
        /// </summary>
        public void jpeg_destroy()
        {
            // mark it destroyed
            m_global_state = JpegState.DESTROYED;
        }

        // Fatal errors (print message and exit)

        /// <summary>
        /// Used for fatal errors (print message and exit).
        /// </summary>
        /// <param name="code">The message code.</param>
        public void ERREXIT(J_MESSAGE_CODE code)
        {
            ERREXIT((int)code);
        }

        /// <summary>
        /// Used for fatal errors (print message and exit).
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        public void ERREXIT(J_MESSAGE_CODE code, params object[] args)
        {
            ERREXIT((int)code, args);
        }

        /// <summary>
        /// Used for fatal errors (print message and exit).
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        public void ERREXIT(int code, params object[] args)
        {
            m_err.m_msg_code = code;
            m_err.m_msg_parm = args;
            m_err.error_exit();
        }

        // Nonfatal errors (we can keep going, but the data is probably corrupt)


        /// <summary>
        /// Used for non-fatal errors (we can keep going, but the data is probably corrupt).
        /// </summary>
        /// <param name="code">The message code.</param>
        public void WARNMS(J_MESSAGE_CODE code)
        {
            WARNMS((int)code);
        }

        /// <summary>
        /// Used for non-fatal errors (we can keep going, but the data is probably corrupt).
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        public void WARNMS(J_MESSAGE_CODE code, params object[] args)
        {
            WARNMS((int)code, args);
        }

        /// <summary>
        /// Used for non-fatal errors (we can keep going, but the data is probably corrupt).
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        public void WARNMS(int code, params object[] args)
        {
            m_err.m_msg_code = code;
            m_err.m_msg_parm = args;
            m_err.emit_message(-1);
        }

        // Informational/debugging messages

        /// <summary>
        /// Shows informational and debugging messages.
        /// </summary>
        /// <param name="lvl">See <see cref="jpeg_error_mgr.emit_message"/> for description.</param>
        /// <param name="code">The message code.</param>
        /// <seealso cref="jpeg_error_mgr.emit_message"/>
        public void TRACEMS(int lvl, J_MESSAGE_CODE code)
        {
            TRACEMS(lvl, (int)code);
        }

        /// <summary>
        /// Shows informational and debugging messages.
        /// </summary>
        /// <param name="lvl">See <see cref="jpeg_error_mgr.emit_message"/> for description.</param>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        /// <seealso cref="jpeg_error_mgr.emit_message"/>
        public void TRACEMS(int lvl, J_MESSAGE_CODE code, params object[] args)
        {
            TRACEMS(lvl, (int)code, args);
        }

        /// <summary>
        /// Shows informational and debugging messages.
        /// </summary>
        /// <param name="lvl">See <see cref="jpeg_error_mgr.emit_message"/> for description.</param>
        /// <param name="code">The message code.</param>
        /// <param name="args">The parameters of message.</param>
        /// <seealso cref="jpeg_error_mgr.emit_message"/>
        public void TRACEMS(int lvl, int code, params object[] args)
        {
            m_err.m_msg_code = code;
            m_err.m_msg_parm = args;
            m_err.emit_message(lvl);
        }
    }
}