namespace BitMiracle.LibJpeg
{
    using BitMiracle.LibJpeg.Classic;

    /// <summary>
    /// Holds parameters of image for decompression (IDecomressDesination)
    /// </summary>
    class LoadedImageAttributes
    {
        /// <summary>
        /// The m_colorspace.
        /// </summary>
        private Colorspace m_colorspace;

        /// <summary>
        /// The m_quantize colors.
        /// </summary>
        private bool m_quantizeColors;

        /// <summary>
        /// The m_width.
        /// </summary>
        private int m_width;

        /// <summary>
        /// The m_height.
        /// </summary>
        private int m_height;

        /// <summary>
        /// The m_components per sample.
        /// </summary>
        private int m_componentsPerSample;

        /// <summary>
        /// The m_components.
        /// </summary>
        private int m_components;

        /// <summary>
        /// The m_actual number of colors.
        /// </summary>
        private int m_actualNumberOfColors;

        /// <summary>
        /// The m_colormap.
        /// </summary>
        private byte[][] m_colormap;

        /// <summary>
        /// The m_density unit.
        /// </summary>
        private DensityUnit m_densityUnit;

        /// <summary>
        /// The m_density x.
        /// </summary>
        private int m_densityX;

        /// <summary>
        /// The m_density y.
        /// </summary>
        private int m_densityY;

        /* Decompression processing parameters --- these fields must be set before
         * calling jpeg_start_decompress().  Note that jpeg_read_header() initializes
         * them to default values.
         */

        // colorspace for output
        /// <summary>
        /// Gets the colorspace.
        /// </summary>
        public Colorspace Colorspace
        {
            get
            {
                return this.m_colorspace;
            }

            internal set
            {
                this.m_colorspace = value;
            }
        }

        // true=colormapped output wanted
        /// <summary>
        /// Gets a value indicating whether quantize colors.
        /// </summary>
        public bool QuantizeColors
        {
            get
            {
                return this.m_quantizeColors;
            }

            internal set
            {
                this.m_quantizeColors = value;
            }
        }

        /* Description of actual output image that will be returned to application.
         * These fields are computed by jpeg_start_decompress().
         * You can also use jpeg_calc_output_dimensions() to determine these values
         * in advance of calling jpeg_start_decompress().
         */

        // scaled image width
        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width
        {
            get
            {
                return this.m_width;
            }

            internal set
            {
                this.m_width = value;
            }
        }

        // scaled image height
        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height
        {
            get
            {
                return this.m_height;
            }

            internal set
            {
                this.m_height = value;
            }
        }

        // # of color components in out_color_space
        /// <summary>
        /// Gets the components per sample.
        /// </summary>
        public int ComponentsPerSample
        {
            get
            {
                return this.m_componentsPerSample;
            }

            internal set
            {
                this.m_componentsPerSample = value;
            }
        }

        // # of color components returned. it is 1 (a colormap index) when 
        // quantizing colors; otherwise it equals out_color_components.
        /// <summary>
        /// Gets the components.
        /// </summary>
        public int Components
        {
            get
            {
                return this.m_components;
            }

            internal set
            {
                this.m_components = value;
            }
        }

        /* When quantizing colors, the output colormap is described by these fields.
         * The application can supply a colormap by setting colormap non-null before
         * calling jpeg_start_decompress; otherwise a colormap is created during
         * jpeg_start_decompress or jpeg_start_output.
         * The map has out_color_components rows and actual_number_of_colors columns.
         */

        // number of entries in use
        /// <summary>
        /// Gets the actual number of colors.
        /// </summary>
        public int ActualNumberOfColors
        {
            get
            {
                return this.m_actualNumberOfColors;
            }

            internal set
            {
                this.m_actualNumberOfColors = value;
            }
        }

        // The color map as a 2-D pixel array
        /// <summary>
        /// Gets the colormap.
        /// </summary>
        public byte[][] Colormap
        {
            get
            {
                return this.m_colormap;
            }

            internal set
            {
                this.m_colormap = value;
            }
        }

        // These fields record data obtained from optional markers 
        // recognized by the JPEG library.

        // JFIF code for pixel size units
        /// <summary>
        /// Gets the density unit.
        /// </summary>
        public DensityUnit DensityUnit
        {
            get
            {
                return this.m_densityUnit;
            }

            internal set
            {
                this.m_densityUnit = value;
            }
        }

        // Horizontal pixel density
        /// <summary>
        /// Gets the density x.
        /// </summary>
        public int DensityX
        {
            get
            {
                return this.m_densityX;
            }

            internal set
            {
                this.m_densityX = value;
            }
        }

        // Vertical pixel density
        /// <summary>
        /// Gets the density y.
        /// </summary>
        public int DensityY
        {
            get
            {
                return this.m_densityY;
            }

            internal set
            {
                this.m_densityY = value;
            }
        }
    }
}