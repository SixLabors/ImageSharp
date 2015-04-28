using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nine.Imaging;

namespace BitMiracle.LibJpeg
{
    using ImageProcessor;

    class DecompressorToJpegImage : IDecompressDestination
    {
        private JpegImage m_jpegImage;

        internal DecompressorToJpegImage(JpegImage jpegImage)
        {
            m_jpegImage = jpegImage;
        }

        public Stream Output
        {
            get
            {
                return null;
            }
        }

        public void SetImageAttributes(LoadedImageAttributes parameters)
        {
            if (parameters.Width > ImageBase.MaxWidth || parameters.Height > ImageBase.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(
                    $"The input jpg '{ parameters.Width }x{ parameters.Height }' is bigger then the max allowed size '{ ImageBase.MaxWidth }x{ ImageBase.MaxHeight }'");
            }

            m_jpegImage.Width = parameters.Width;
            m_jpegImage.Height = parameters.Height;
            m_jpegImage.BitsPerComponent = 8;
            m_jpegImage.ComponentsPerSample = (byte)parameters.ComponentsPerSample;
            m_jpegImage.Colorspace = parameters.Colorspace;
        }

        public void BeginWrite()
        {
        }

        public void ProcessPixelsRow(byte[] row)
        {
            SampleRow samplesRow = new SampleRow(row, m_jpegImage.Width, m_jpegImage.BitsPerComponent, m_jpegImage.ComponentsPerSample);
            m_jpegImage.addSampleRow(samplesRow);
        }

        public void EndWrite()
        {
        }
    }
}
