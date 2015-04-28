using System;
using System.Collections.Generic;
using System.Text;

using BitMiracle.LibJpeg.Classic;

namespace BitMiracle.LibJpeg
{
    class DecompressionParameters
    {
        private Colorspace m_outColorspace = Colorspace.Unknown;
        private int m_scaleNumerator = 1;
        private int m_scaleDenominator = 1;
        private bool m_bufferedImage;
        private bool m_rawDataOut;
        private DCTMethod m_dctMethod = (DCTMethod)JpegConstants.JDCT_DEFAULT;
        private DitherMode m_ditherMode = DitherMode.FloydSteinberg;
        private bool m_doFancyUpsampling = true;
        private bool m_doBlockSmoothing = true;
        private bool m_quantizeColors;
        private bool m_twoPassQuantize = true;
        private int m_desiredNumberOfColors = 256;
        private bool m_enableOnePassQuantizer;
        private bool m_enableExternalQuant;
        private bool m_enableTwoPassQuantizer;
        private int m_traceLevel;

        public int TraceLevel
        {
            get
            {
                return m_traceLevel;
            }
            set
            {
                m_traceLevel = value;
            }
        }

        /* Decompression processing parameters --- these fields must be set before
         * calling jpeg_start_decompress().  Note that jpeg_read_header() initializes
         * them to default values.
         */

        // colorspace for output
        public Colorspace OutColorspace
        {
            get
            {
                return m_outColorspace;
            }
            set
            {
                m_outColorspace = value;
            }
        }

        // fraction by which to scale image
        public int ScaleNumerator
        {
            get
            {
                return m_scaleNumerator;
            }
            set
            {
                m_scaleNumerator = value;
            }
        }

        public int ScaleDenominator
        {
            get
            {
                return m_scaleDenominator;
            }
            set
            {
                m_scaleDenominator = value;
            }
        }

        // true=multiple output passes
        public bool BufferedImage
        {
            get
            {
                return m_bufferedImage;
            }
            set
            {
                m_bufferedImage = value;
            }
        }

        // true=downsampled data wanted
        public bool RawDataOut
        {
            get
            {
                return m_rawDataOut;
            }
            set
            {
                m_rawDataOut = value;
            }
        }

        // IDCT algorithm selector
        public DCTMethod DCTMethod
        {
            get
            {
                return m_dctMethod;
            }
            set
            {
                m_dctMethod = value;
            }
        }

        // true=apply fancy upsampling
        public bool DoFancyUpsampling
        {
            get
            {
                return m_doFancyUpsampling;
            }
            set
            {
                m_doFancyUpsampling = value;
            }
        }

        // true=apply interblock smoothing
        public bool DoBlockSmoothing
        {
            get
            {
                return m_doBlockSmoothing;
            }
            set
            {
                m_doBlockSmoothing = value;
            }
        }

        // true=colormapped output wanted
        public bool QuantizeColors
        {
            get
            {
                return m_quantizeColors;
            }
            set
            {
                m_quantizeColors = value;
            }
        }

        /* the following are ignored if not quantize_colors: */

        // type of color dithering to use
        public DitherMode DitherMode
        {
            get
            {
                return m_ditherMode;
            }
            set
            {
                m_ditherMode = value;
            }
        }

        // true=use two-pass color quantization
        public bool TwoPassQuantize
        {
            get
            {
                return m_twoPassQuantize;
            }
            set
            {
                m_twoPassQuantize = value;
            }
        }

        // max # colors to use in created colormap
        public int DesiredNumberOfColors
        {
            get
            {
                return m_desiredNumberOfColors;
            }
            set
            {
                m_desiredNumberOfColors = value;
            }
        }

        /* these are significant only in buffered-image mode: */

        // enable future use of 1-pass quantizer
        public bool EnableOnePassQuantizer
        {
            get
            {
                return m_enableOnePassQuantizer;
            }
            set
            {
                m_enableOnePassQuantizer = value;
            }
        }

        // enable future use of external colormap
        public bool EnableExternalQuant
        {
            get
            {
                return m_enableExternalQuant;
            }
            set
            {
                m_enableExternalQuant = value;
            }
        }

        // enable future use of 2-pass quantizer
        public bool EnableTwoPassQuantizer
        {
            get
            {
                return m_enableTwoPassQuantizer;
            }
            set
            {
                m_enableTwoPassQuantizer = value;
            }
        }
    }
}
