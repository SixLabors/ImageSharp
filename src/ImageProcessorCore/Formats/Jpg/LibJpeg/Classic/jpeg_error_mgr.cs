/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

/*
 * This file contains simple error-reporting and trace-message routines.
 * Many applications will want to override some or all of these routines.
 *
 * These routines are used by both the compression and decompression code.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Diagnostics;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// Contains simple error-reporting and trace-message routines.
    /// </summary>
    /// <remarks>This class is used by both the compression and decompression code.</remarks>
    /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
#if EXPOSE_LIBJPEG
    public
#endif
    class jpeg_error_mgr
    {
        // The message ID code and any parameters are saved in fields below. 
        internal int m_msg_code;
        internal object[] m_msg_parm;

        internal int m_trace_level;
        internal int m_num_warnings;

        /// <summary>
        /// Initializes a new instance of the <see cref="jpeg_error_mgr"/> class.
        /// </summary>
        public jpeg_error_mgr()
        {
        }

        /// <summary>
        /// Gets or sets the maximum message level that will be displayed.
        /// </summary>
        /// <value>Values are:
        /// -1: recoverable corrupt-data warning, may want to abort.<br/>
        /// 0: important advisory messages (always display to user).<br/>
        /// 1: first level of tracing detail.<br/>
        /// 2, 3, ...: successively more detailed tracing messages.
        /// </value>
        /// <seealso cref="jpeg_error_mgr.emit_message"/>
        public int Trace_level
        {
            get { return m_trace_level; }
            set { m_trace_level = value; }
        }

        /// <summary>
        /// Gets the number of corrupt-data warnings.
        /// </summary>
        /// <value>The num_warnings.</value>
        /// <remarks>For recoverable corrupt-data errors, we emit a warning message, but keep going 
        /// unless <see cref="jpeg_error_mgr.emit_message">emit_message</see> chooses to abort. 
        /// <c>emit_message</c> should count warnings in <c>Num_warnings</c>. The surrounding application 
        /// can check for bad data by seeing if <c>Num_warnings</c> is nonzero at the end of processing.</remarks>
        public int Num_warnings
        {
            get { return m_num_warnings; }
        }

        /// <summary>
        /// Receives control for a fatal error.
        /// </summary>
        /// <remarks>This method calls <see cref="jpeg_error_mgr.output_message">output_message</see> 
        /// and then throws an exception.</remarks>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        public virtual void error_exit()
        {
            // Always display the message
            output_message();

            string buffer = format_message();
            throw new Exception(buffer);
        }

        /// <summary>
        /// Conditionally emit a trace or warning message.
        /// </summary>
        /// <param name="msg_level">The message severity level.<br/>
        /// Values are:<br/>
        /// -1: recoverable corrupt-data warning, may want to abort.<br/>
        /// 0: important advisory messages (always display to user).<br/>
        /// 1: first level of tracing detail.<br/>
        /// 2, 3, ...: successively more detailed tracing messages.
        /// </param>
        /// <remarks>The main reason for overriding this method would be to abort on warnings.
        /// This method calls <see cref="jpeg_error_mgr.output_message">output_message</see> for message showing.<br/>
        /// 
        /// An application might override this method if it wanted to abort on 
        /// warnings or change the policy about which messages to display.
        /// </remarks>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        public virtual void emit_message(int msg_level)
        {
            if (msg_level < 0)
            {
                /* It's a warning message.  Since corrupt files may generate many warnings,
                 * the policy implemented here is to show only the first warning,
                 * unless trace_level >= 3.
                 */
                if (m_num_warnings == 0 || m_trace_level >= 3)
                    output_message();

                /* Always count warnings in num_warnings. */
                m_num_warnings++;
            }
            else
            {
                /* It's a trace message.  Show it if trace_level >= msg_level. */
                if (m_trace_level >= msg_level)
                    output_message();
            }
        }

        /// <summary>
        /// Actual output of any JPEG message.
        /// </summary>
        /// <remarks>Override this to send messages somewhere other than Console. 
        /// Note that this method does not know how to generate a message, only where to send it.
        /// For extending a generation of messages see <see cref="jpeg_error_mgr.format_message">format_message</see>.
        /// </remarks>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        public virtual void output_message()
        {
            // Create the message
            string buffer = format_message();

            // Send it to console, adding a newline */
            Debug.WriteLine(buffer);
        }

        /// <summary>
        /// Constructs a readable error message string.
        /// </summary>
        /// <remarks>This method is called by <see cref="jpeg_error_mgr.output_message">output_message</see>.
        /// Few applications should need to override this method. One possible reason for doing so is to 
        /// implement dynamic switching of error message language.</remarks>
        /// <returns>The formatted message</returns>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        public virtual string format_message()
        {
            string msgtext = GetMessageText(m_msg_code);
            if (msgtext == null)
            {
                m_msg_parm = new object[] { m_msg_code };
                msgtext = GetMessageText(0);
            }

            /* Format the message into the passed buffer */
            return string.Format(CultureInfo.CurrentCulture, msgtext, m_msg_parm);
        }

        /// <summary>
        /// Resets error manager to initial state.
        /// </summary>
        /// <remarks>This is called during compression startup to reset trace/error
        /// processing to default state. An application might possibly want to
        /// override this method if it has additional error processing state.
        /// </remarks>
        public virtual void reset_error_mgr()
        {
            m_num_warnings = 0;

            /* trace_level is not reset since it is an application-supplied parameter */

            // may be useful as a flag for "no error"
            m_msg_code = 0;
        }

        /// <summary>
        /// Gets the actual message texts.
        /// </summary>
        /// <param name="code">The message code. See <see cref="J_MESSAGE_CODE"/> for details.</param>
        /// <returns>The message text associated with <c>code</c>.</returns>
        /// <remarks>It may be useful for an application to add its own message texts that are handled 
        /// by the same mechanism. You can override <c>GetMessageText</c> for this purpose. If you number 
        /// the addon messages beginning at 1000 or so, you won't have to worry about conflicts 
        /// with the library's built-in messages.
        /// </remarks>
        /// <seealso cref="J_MESSAGE_CODE"/>
        /// <seealso href="41dc1a3b-0dea-4594-87d2-c213ab1049e1.htm" target="_self">Error handling</seealso>
        protected virtual string GetMessageText(int code)
        {
            switch ((J_MESSAGE_CODE)code)
            {
                default:
                case J_MESSAGE_CODE.JMSG_NOMESSAGE:
                    return "Bogus message code {0}";

                /* For maintenance convenience, list is alphabetical by message code name */
                case J_MESSAGE_CODE.JERR_ARITH_NOTIMPL:
                    return "Sorry, there are legal restrictions on arithmetic coding";
                case J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE:
                    return "Bogus buffer control mode";
                case J_MESSAGE_CODE.JERR_BAD_COMPONENT_ID:
                    return "Invalid component ID {0} in SOS";
                case J_MESSAGE_CODE.JERR_BAD_DCT_COEF:
                    return "DCT coefficient out of range";
                case J_MESSAGE_CODE.JERR_BAD_DCTSIZE:
                    return "IDCT output block size {0} not supported";
                case J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE:
                    return "Bogus Huffman table definition";
                case J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE:
                    return "Bogus input colorspace";
                case J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE:
                    return "Bogus JPEG colorspace";
                case J_MESSAGE_CODE.JERR_BAD_LENGTH:
                    return "Bogus marker length";
                case J_MESSAGE_CODE.JERR_BAD_MCU_SIZE:
                    return "Sampling factors too large for interleaved scan";
                case J_MESSAGE_CODE.JERR_BAD_PRECISION:
                    return "Unsupported JPEG data precision {0}";
                case J_MESSAGE_CODE.JERR_BAD_PROGRESSION:
                    return "Invalid progressive parameters Ss={0} Se={1} Ah={2} Al={3}";
                case J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT:
                    return "Invalid progressive parameters at scan script entry {0}";
                case J_MESSAGE_CODE.JERR_BAD_SAMPLING:
                    return "Bogus sampling factors";
                case J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT:
                    return "Invalid scan script at entry {0}";
                case J_MESSAGE_CODE.JERR_BAD_STATE:
                    return "Improper call to JPEG library in state {0}";
                case J_MESSAGE_CODE.JERR_BAD_VIRTUAL_ACCESS:
                    return "Bogus virtual array access";
                case J_MESSAGE_CODE.JERR_BUFFER_SIZE:
                    return "Buffer passed to JPEG library is too small";
                case J_MESSAGE_CODE.JERR_CANT_SUSPEND:
                    return "Suspension not allowed here";
                case J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL:
                    return "CCIR601 sampling not implemented yet";
                case J_MESSAGE_CODE.JERR_COMPONENT_COUNT:
                    return "Too many color components: {0}, max {1}";
                case J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL:
                    return "Unsupported color conversion request";
                case J_MESSAGE_CODE.JERR_DHT_INDEX:
                    return "Bogus DHT index {0}";
                case J_MESSAGE_CODE.JERR_DQT_INDEX:
                    return "Bogus DQT index {0}";
                case J_MESSAGE_CODE.JERR_EMPTY_IMAGE:
                    return "Empty JPEG image (DNL not supported)";
                case J_MESSAGE_CODE.JERR_EOI_EXPECTED:
                    return "Didn't expect more than one scan";
                case J_MESSAGE_CODE.JERR_FILE_WRITE:
                    return "Output file write error --- out of disk space?";
                case J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL:
                    return "Fractional sampling not implemented yet";
                case J_MESSAGE_CODE.JERR_HUFF_CLEN_OVERFLOW:
                    return "Huffman code size table overflow";
                case J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE:
                    return "Missing Huffman code table entry";
                case J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG:
                    return "Maximum supported image dimension is {0} pixels";
                case J_MESSAGE_CODE.JERR_INPUT_EMPTY:
                    return "Empty input file";
                case J_MESSAGE_CODE.JERR_INPUT_EOF:
                    return "Premature end of input file";
                case J_MESSAGE_CODE.JERR_MISMATCHED_QUANT_TABLE:
                    return "Cannot transcode due to multiple use of quantization table {0}";
                case J_MESSAGE_CODE.JERR_MISSING_DATA:
                    return "Scan script does not transmit all data";
                case J_MESSAGE_CODE.JERR_MODE_CHANGE:
                    return "Invalid color quantization mode change";
                case J_MESSAGE_CODE.JERR_NOTIMPL:
                    return "Not implemented yet";
                case J_MESSAGE_CODE.JERR_NOT_COMPILED:
                    return "Requested feature was omitted at compile time";
                case J_MESSAGE_CODE.JERR_NO_HUFF_TABLE:
                    return "Huffman table 0x{0:X2} was not defined";
                case J_MESSAGE_CODE.JERR_NO_IMAGE:
                    return "JPEG datastream contains no image";
                case J_MESSAGE_CODE.JERR_NO_QUANT_TABLE:
                    return "Quantization table 0x{0:X2} was not defined";
                case J_MESSAGE_CODE.JERR_NO_SOI:
                    return "Not a JPEG file: starts with 0x{0:X2} 0x{1:X2}";
                case J_MESSAGE_CODE.JERR_OUT_OF_MEMORY:
                    return "Insufficient memory (case {0})";
                case J_MESSAGE_CODE.JERR_QUANT_COMPONENTS:
                    return "Cannot quantize more than {0} color components";
                case J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS:
                    return "Cannot quantize to fewer than {0} colors";
                case J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS:
                    return "Cannot quantize to more than {0} colors";
                case J_MESSAGE_CODE.JERR_SOF_DUPLICATE:
                    return "Invalid JPEG file structure: two SOF markers";
                case J_MESSAGE_CODE.JERR_SOF_NO_SOS:
                    return "Invalid JPEG file structure: missing SOS marker";
                case J_MESSAGE_CODE.JERR_SOF_UNSUPPORTED:
                    return "Unsupported JPEG process: SOF type 0x{0:X2}";
                case J_MESSAGE_CODE.JERR_SOI_DUPLICATE:
                    return "Invalid JPEG file structure: two SOI markers";
                case J_MESSAGE_CODE.JERR_SOS_NO_SOF:
                    return "Invalid JPEG file structure: SOS before SOF";
                case J_MESSAGE_CODE.JERR_TOO_LITTLE_DATA:
                    return "Application transferred too few scanlines";
                case J_MESSAGE_CODE.JERR_UNKNOWN_MARKER:
                    return "Unsupported marker type 0x{0:X2}";
                case J_MESSAGE_CODE.JERR_WIDTH_OVERFLOW:
                    return "Image too wide for this implementation";
                case J_MESSAGE_CODE.JTRC_16BIT_TABLES:
                    return "Caution: quantization tables are too coarse for baseline JPEG";
                case J_MESSAGE_CODE.JTRC_ADOBE:
                    return "Adobe APP14 marker: version {0}, flags 0x{1:X4} 0x{2:X4}, transform {3}";
                case J_MESSAGE_CODE.JTRC_APP0:
                    return "Unknown APP0 marker (not JFIF), length {0}";
                case J_MESSAGE_CODE.JTRC_APP14:
                    return "Unknown APP14 marker (not Adobe), length {0}";
                case J_MESSAGE_CODE.JTRC_DHT:
                    return "Define Huffman Table 0x{0:X2}";
                case J_MESSAGE_CODE.JTRC_DQT:
                    return "Define Quantization Table {0} precision {1}";
                case J_MESSAGE_CODE.JTRC_DRI:
                    return "Define Restart Interval {0}";
                case J_MESSAGE_CODE.JTRC_EOI:
                    return "End Of Image";
                case J_MESSAGE_CODE.JTRC_HUFFBITS:
                    return "        {0:D3} {1:D3} {2:D3} {3:D3} {4:D3} {5:D3} {6:D3} {7:D3}";
                case J_MESSAGE_CODE.JTRC_JFIF:
                    return "JFIF APP0 marker: version {0}.{1:D2}, density {2}x{3}  {4}";
                case J_MESSAGE_CODE.JTRC_JFIF_BADTHUMBNAILSIZE:
                    return "Warning: thumbnail image size does not match data length {0}";
                case J_MESSAGE_CODE.JTRC_JFIF_EXTENSION:
                    return "JFIF extension marker: type 0x{0:X2}, length {1}";
                case J_MESSAGE_CODE.JTRC_JFIF_THUMBNAIL:
                    return "    with {0} x {1} thumbnail image";
                case J_MESSAGE_CODE.JTRC_MISC_MARKER:
                    return "Miscellaneous marker 0x{0:X2}, length {1}";
                case J_MESSAGE_CODE.JTRC_PARMLESS_MARKER:
                    return "Unexpected marker 0x{0:X2}";
                case J_MESSAGE_CODE.JTRC_QUANTVALS:
                    return "        {0:D4} {1:D4} {2:D4} {3:D4} {4:D4} {5:D4} {6:D4} {7:D4}";
                case J_MESSAGE_CODE.JTRC_QUANT_3_NCOLORS:
                    return "Quantizing to {0} = {1}*{2}*{3} colors";
                case J_MESSAGE_CODE.JTRC_QUANT_NCOLORS:
                    return "Quantizing to {0} colors";
                case J_MESSAGE_CODE.JTRC_QUANT_SELECTED:
                    return "Selected {0} colors for quantization";
                case J_MESSAGE_CODE.JTRC_RECOVERY_ACTION:
                    return "At marker 0x{0:X2}, recovery action {1}";
                case J_MESSAGE_CODE.JTRC_RST:
                    return "RST{0}";
                case J_MESSAGE_CODE.JTRC_SMOOTH_NOTIMPL:
                    return "Smoothing not supported with nonstandard sampling ratios";
                case J_MESSAGE_CODE.JTRC_SOF:
                    return "Start Of Frame 0x{0:X2}: width={1}, height={2}, components={3}";
                case J_MESSAGE_CODE.JTRC_SOF_COMPONENT:
                    return "    Component {0}: {1}hx{2}v q={3}";
                case J_MESSAGE_CODE.JTRC_SOI:
                    return "Start of Image";
                case J_MESSAGE_CODE.JTRC_SOS:
                    return "Start Of Scan: {0} components";
                case J_MESSAGE_CODE.JTRC_SOS_COMPONENT:
                    return "    Component {0}: dc={1} ac={2}";
                case J_MESSAGE_CODE.JTRC_SOS_PARAMS:
                    return "  Ss={0}, Se={1}, Ah={2}, Al={3}";
                case J_MESSAGE_CODE.JTRC_THUMB_JPEG:
                    return "JFIF extension marker: JPEG-compressed thumbnail image, length {0}";
                case J_MESSAGE_CODE.JTRC_THUMB_PALETTE:
                    return "JFIF extension marker: palette thumbnail image, length {0}";
                case J_MESSAGE_CODE.JTRC_THUMB_RGB:
                    return "JFIF extension marker: RGB thumbnail image, length {0}";
                case J_MESSAGE_CODE.JTRC_UNKNOWN_IDS:
                    return "Unrecognized component IDs {0} {1} {2}, assuming YCbCr";
                case J_MESSAGE_CODE.JWRN_ADOBE_XFORM:
                    return "Unknown Adobe color transform code {0}";
                case J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION:
                    return "Inconsistent progression sequence for component {0} coefficient {1}";
                case J_MESSAGE_CODE.JWRN_EXTRANEOUS_DATA:
                    return "Corrupt JPEG data: {0} extraneous bytes before marker 0x{1:X2}";
                case J_MESSAGE_CODE.JWRN_HIT_MARKER:
                    return "Corrupt JPEG data: premature end of data segment";
                case J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE:
                    return "Corrupt JPEG data: bad Huffman code";
                case J_MESSAGE_CODE.JWRN_JFIF_MAJOR:
                    return "Warning: unknown JFIF revision number {0}.{1:D2}";
                case J_MESSAGE_CODE.JWRN_JPEG_EOF:
                    return "Premature end of JPEG file";
                case J_MESSAGE_CODE.JWRN_MUST_RESYNC:
                    return "Corrupt JPEG data: found marker 0x{0:X2} instead of RST{1}";
                case J_MESSAGE_CODE.JWRN_NOT_SEQUENTIAL:
                    return "Invalid SOS parameters for sequential JPEG";
                case J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA:
                    return "Application transferred too many scanlines";
                case J_MESSAGE_CODE.JMSG_UNKNOWNMSGCODE:
                    return "Unknown message code (possibly it is an error from application)";
            }
        }
    }
}
