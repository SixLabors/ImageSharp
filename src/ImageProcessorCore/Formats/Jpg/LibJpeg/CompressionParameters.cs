using System;
using System.Collections.Generic;
using System.Text;

using BitMiracle.LibJpeg.Classic;

namespace BitMiracle.LibJpeg
{
    /// <summary>
    /// Parameters of compression.
    /// </summary>
    /// <remarks>Being used in <see cref="M:BitMiracle.LibJpeg.JpegImage.WriteJpeg(System.IO.Stream,BitMiracle.LibJpeg.CompressionParameters)"/></remarks>
#if EXPOSE_LIBJPEG
    public
#endif
    class CompressionParameters
    {
        private int m_quality = 75;
        private int m_smoothingFactor;
        private bool m_simpleProgressive;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionParameters"/> class.
        /// </summary>
        public CompressionParameters()
        {
        }

        internal CompressionParameters(CompressionParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            m_quality = parameters.m_quality;
            m_smoothingFactor = parameters.m_smoothingFactor;
            m_simpleProgressive = parameters.m_simpleProgressive;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            CompressionParameters parameters = obj as CompressionParameters;
            if (parameters == null)
                return false;

            return (m_quality == parameters.m_quality &&
                    m_smoothingFactor == parameters.m_smoothingFactor &&
                    m_simpleProgressive == parameters.m_simpleProgressive);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms 
        /// and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Gets or sets the quality of JPEG image.
        /// </summary>
        /// <remarks>Default value: 75<br/>
        /// The quality value is expressed on the 0..100 scale.
        /// </remarks>
        /// <value>The quality of JPEG image.</value>
        public int Quality
        {
            get { return m_quality; }
            set { m_quality = value; }
        }

        /// <summary>
        /// Gets or sets the coefficient of image smoothing.
        /// </summary>
        /// <remarks>Default value: 0<br/>
        /// If non-zero, the input image is smoothed; the value should be 1 for
        /// minimal smoothing to 100 for maximum smoothing.
        /// </remarks>
        /// <value>The coefficient of image smoothing.</value>
        public int SmoothingFactor
        {
            get { return m_smoothingFactor; }
            set { m_smoothingFactor = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to write a progressive-JPEG file.
        /// </summary>
        /// <value>
        /// <c>true</c> for writing a progressive-JPEG file; <c>false</c> 
        /// for non-progressive JPEG files.
        /// </value>
        public bool SimpleProgressive
        {
            get { return m_simpleProgressive; }
            set { m_simpleProgressive = value; }
        }
    }
}