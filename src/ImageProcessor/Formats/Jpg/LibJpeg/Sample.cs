using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg
{
    using ImageProcessor.Formats;

    /// <summary>
    /// Represents a "sample" (you can understand it as a "pixel") of image.
    /// </summary>
    /// <remarks>It's impossible to create an instance of this class directly, 
    /// but you can use existing samples through <see cref="SampleRow"/> collection. 
    /// Usual scenario is to get row of samples from the <see cref="JpegImage.GetRow"/> method.
    /// </remarks>
#if EXPOSE_LIBJPEG
    public
#endif
    class Sample
    {
        private short[] m_components;
        private byte m_bitsPerComponent;

        internal Sample(BitStream bitStream, byte bitsPerComponent, byte componentCount)
        {
            if (bitStream == null)
                throw new ArgumentNullException("bitStream");

            if (bitsPerComponent <= 0 || bitsPerComponent > 16)
                throw new ArgumentOutOfRangeException("bitsPerComponent");

            if (componentCount <= 0 || componentCount > 5)
                throw new ArgumentOutOfRangeException("componentCount");

            m_bitsPerComponent = bitsPerComponent;

            m_components = new short[componentCount];
            for (short i = 0; i < componentCount; ++i)
                m_components[i] = (short)bitStream.Read(bitsPerComponent);
        }

        internal Sample(short[] components, byte bitsPerComponent)
        {
            if (components == null)
                throw new ArgumentNullException("components");

            if (components.Length == 0 || components.Length > 5)
                throw new ArgumentException("components must be not empty and contain less than 5 elements");

            if (bitsPerComponent <= 0 || bitsPerComponent > 16)
                throw new ArgumentOutOfRangeException("bitsPerComponent");

            m_bitsPerComponent = bitsPerComponent;

            m_components = new short[components.Length];
            Buffer.BlockCopy(components, 0, m_components, 0, components.Length * sizeof(short));
        }

        /// <summary>
        /// Gets the number of bits per color component.
        /// </summary>
        /// <value>The number of bits per color component.</value>
        public byte BitsPerComponent
        {
            get
            {
                return m_bitsPerComponent;
            }
        }

        /// <summary>
        /// Gets the number of color components.
        /// </summary>
        /// <value>The number of color components.</value>
        public byte ComponentCount
        {
            get
            {
                return (byte)m_components.Length;
            }
        }

        /// <summary>
        /// Gets the color component at the specified index.
        /// </summary>
        /// <param name="componentNumber">The number of color component.</param>
        /// <returns>Value of color component.</returns>
        public short this[int componentNumber]
        {
            get
            {
                return m_components[componentNumber];
            }
        }

        /// <summary>
        /// Gets the required color component.
        /// </summary>
        /// <param name="componentNumber">The number of color component.</param>
        /// <returns>Value of color component.</returns>
        public short GetComponent(int componentNumber)
        {
            return m_components[componentNumber];
        }
    }
}
