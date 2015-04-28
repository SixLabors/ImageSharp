using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BitMiracle.LibJpeg.Classic;

namespace BitMiracle.LibJpeg
{
    /// <summary>
    /// Common interface for processing of decompression.
    /// </summary>
    interface IDecompressDestination
    {
        /// <summary>
        /// Strean with decompressed data
        /// </summary>
        Stream Output
        {
            get;
        }

        /// <summary>
        /// Implementor of this interface should process image properties received from decompressor.
        /// </summary>
        /// <param name="parameters">Image properties</param>
        void SetImageAttributes(LoadedImageAttributes parameters);

        /// <summary>
        /// Called before decompression
        /// </summary>
        void BeginWrite();

        /// <summary>
        /// It called during decompression - pass row of pixels from JPEG
        /// </summary>
        /// <param name="row"></param>
        void ProcessPixelsRow(byte[] row);

        /// <summary>
        /// Called after decompression
        /// </summary>
        void EndWrite();
    }

    /// <summary>
    /// Holds parameters of image for decompression (IDecomressDesination)
    /// </summary>
    class LoadedImageAttributes
    {
        private Colorspace m_colorspace;
        private bool m_quantizeColors;
        private int m_width;
        private int m_height;
        private int m_componentsPerSample;
        private int m_components;
        private int m_actualNumberOfColors;
        private byte[][] m_colormap;
        private DensityUnit m_densityUnit;
        private int m_densityX;
        private int m_densityY;

        /* Decompression processing parameters --- these fields must be set before
         * calling jpeg_start_decompress().  Note that jpeg_read_header() initializes
         * them to default values.
         */

        // colorspace for output
        public Colorspace Colorspace
        {
            get
            {
                return m_colorspace;
            }
            internal set
            {
                m_colorspace = value;
            }
        }

        // true=colormapped output wanted
        public bool QuantizeColors
        {
            get
            {
                return m_quantizeColors;
            }
            internal set
            {
                m_quantizeColors = value;
            }
        }

        /* Description of actual output image that will be returned to application.
         * These fields are computed by jpeg_start_decompress().
         * You can also use jpeg_calc_output_dimensions() to determine these values
         * in advance of calling jpeg_start_decompress().
         */

        // scaled image width
        public int Width
        {
            get
            {
                return m_width;
            }
            internal set
            {
                m_width = value;
            }
        }

        // scaled image height
        public int Height
        {
            get
            {
                return m_height;
            }
            internal set
            {
                m_height = value;
            }
        }

        // # of color components in out_color_space
        public int ComponentsPerSample
        {
            get
            {
                return m_componentsPerSample;
            }
            internal set
            {
                m_componentsPerSample = value;
            }
        }

        // # of color components returned. it is 1 (a colormap index) when 
        // quantizing colors; otherwise it equals out_color_components.
        public int Components
        {
            get
            {
                return m_components;
            }
            internal set
            {
                m_components = value;
            }
        }

        /* When quantizing colors, the output colormap is described by these fields.
         * The application can supply a colormap by setting colormap non-null before
         * calling jpeg_start_decompress; otherwise a colormap is created during
         * jpeg_start_decompress or jpeg_start_output.
         * The map has out_color_components rows and actual_number_of_colors columns.
         */

        // number of entries in use
        public int ActualNumberOfColors
        {
            get
            {
                return m_actualNumberOfColors;
            }
            internal set
            {
                m_actualNumberOfColors = value;
            }
        }

        // The color map as a 2-D pixel array
        public byte[][] Colormap
        {
            get
            {
                return m_colormap;
            }
            internal set
            {
                m_colormap = value;
            }
        }

        // These fields record data obtained from optional markers 
        // recognized by the JPEG library.

        // JFIF code for pixel size units
        public DensityUnit DensityUnit
        {
            get
            {
                return m_densityUnit;
            }
            internal set
            {
                m_densityUnit = value;
            }
        }

        // Horizontal pixel density
        public int DensityX
        {
            get
            {
                return m_densityX;
            }
            internal set
            {
                m_densityX = value;
            }
        }

        // Vertical pixel density
        public int DensityY
        {
            get
            {
                return m_densityY;
            }
            internal set
            {
                m_densityY = value;
            }
        }
    }
}
