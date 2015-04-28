using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using BitMiracle.LibJpeg.Classic;

namespace BitMiracle.LibJpeg
{
    class BitmapDestination : IDecompressDestination
    {
        /* Target file spec; filled in by djpeg.c after object is created. */
        private Stream m_output;

        private byte[][] m_pixels;

        private int m_rowWidth;       /* physical width of one row in the BMP file */

        private int m_currentRow;  /* next row# to write to virtual array */
        private LoadedImageAttributes m_parameters;

        public BitmapDestination(Stream output)
        {
            m_output = output;
        }

        public Stream Output
        {
            get
            {
                return m_output;
            }
        }

        public void SetImageAttributes(LoadedImageAttributes parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            m_parameters = parameters;
        }

        /// <summary>
        /// Startup: normally writes the file header.
        /// In this module we may as well postpone everything until finish_output.
        /// </summary>
        public void BeginWrite()
        {
            //Determine width of rows in the BMP file (padded to 4-byte boundary).
            m_rowWidth = m_parameters.Width * m_parameters.Components;
            while (m_rowWidth % 4 != 0)
                m_rowWidth++;

            m_pixels = new byte[m_rowWidth][];
            for (int i = 0; i < m_rowWidth; i++)
                m_pixels[i] = new byte[m_parameters.Height];

            m_currentRow = 0;
        }

        /// <summary>
        /// Write some pixel data.
        /// </summary>
        public void ProcessPixelsRow(byte[] row)
        {
            if (m_parameters.Colorspace == Colorspace.Grayscale || m_parameters.QuantizeColors)
            {
                putGrayRow(row);
            }
            else
            {
                if (m_parameters.Colorspace == Colorspace.CMYK)
                    putCmykRow(row);
                else
                    putRgbRow(row);
            }

            ++m_currentRow;
        }

        /// <summary>
        /// Finish up at the end of the file.
        /// Here is where we really output the BMP file.
        /// </summary>
        public void EndWrite()
        {
            writeHeader();
            writePixels();

            /* Make sure we wrote the output file OK */
            m_output.Flush();
        }


        /// <summary>
        /// This version is for grayscale OR quantized color output
        /// </summary>
        private void putGrayRow(byte[] row)
        {
            for (int i = 0; i < m_parameters.Width; ++i)
                m_pixels[i][m_currentRow] = row[i];
        }

        /// <summary>
        /// This version is for writing 24-bit pixels
        /// </summary>
        private void putRgbRow(byte[] row)
        {
            /* Transfer data.  Note destination values must be in BGR order
             * (even though Microsoft's own documents say the opposite).
             */
            for (int i = 0; i < m_parameters.Width; ++i)
            {
                int firstComponent = i * 3;
                byte red = row[firstComponent];
                byte green = row[firstComponent + 1];
                byte blue = row[firstComponent + 2];
                m_pixels[firstComponent][m_currentRow] = blue;
                m_pixels[firstComponent + 1][m_currentRow] = green;
                m_pixels[firstComponent + 2][m_currentRow] = red;
            }
        }

        /// <summary>
        /// This version is for writing 24-bit pixels
        /// </summary>
        private void putCmykRow(byte[] row)
        {
            /* Transfer data.  Note destination values must be in BGR order
             * (even though Microsoft's own documents say the opposite).
             */
            for (int i = 0; i < m_parameters.Width; ++i)
            {
                int firstComponent = i * 4;
                m_pixels[firstComponent][m_currentRow] = row[firstComponent + 2];
                m_pixels[firstComponent + 1][m_currentRow] = row[firstComponent + 1];
                m_pixels[firstComponent + 2][m_currentRow] = row[firstComponent + 0];
                m_pixels[firstComponent + 3][m_currentRow] = row[firstComponent + 3];
            }
        }

        /// <summary>
        /// Write a Windows-style BMP file header, including colormap if needed
        /// </summary>
        private void writeHeader()
        {
            int bits_per_pixel;
            int cmap_entries;

            /* Compute colormap size and total file size */
            if (m_parameters.Colorspace == Colorspace.Grayscale || m_parameters.QuantizeColors)
            {
                bits_per_pixel = 8;
                cmap_entries = 256;
            }
            else
            {
                cmap_entries = 0;

                if (m_parameters.Colorspace == Colorspace.RGB)
                    bits_per_pixel = 24;
                else if (m_parameters.Colorspace == Colorspace.CMYK)
                    bits_per_pixel = 32;
                else
                    throw new InvalidOperationException();
            }

            byte[] infoHeader = null;
            if (m_parameters.Colorspace == Colorspace.RGB)
                infoHeader = createBitmapInfoHeader(bits_per_pixel, cmap_entries);
            else
                infoHeader = createBitmapV4InfoHeader(bits_per_pixel);

            /* File size */
            const int fileHeaderSize = 14;
            int infoHeaderSize = infoHeader.Length;
            int paletteSize = cmap_entries * 4;
            int offsetToPixels = fileHeaderSize + infoHeaderSize + paletteSize; /* Header and colormap */
            int fileSize = offsetToPixels + m_rowWidth * m_parameters.Height;

            byte[] fileHeader = createBitmapFileHeader(offsetToPixels, fileSize);

            m_output.Write(fileHeader, 0, fileHeader.Length);
            m_output.Write(infoHeader, 0, infoHeader.Length);

            if (cmap_entries > 0)
                writeColormap(cmap_entries, 4);
        }

        private static byte[] createBitmapFileHeader(int offsetToPixels, int fileSize)
        {
            byte[] bmpfileheader = new byte[14];
            bmpfileheader[0] = 0x42;    /* first 2 bytes are ASCII 'B', 'M' */
            bmpfileheader[1] = 0x4D;
            PUT_4B(bmpfileheader, 2, fileSize);
            /* we leave bfReserved1 & bfReserved2 = 0 */
            PUT_4B(bmpfileheader, 10, offsetToPixels); /* bfOffBits */
            return bmpfileheader;
        }

        private byte[] createBitmapInfoHeader(int bits_per_pixel, int cmap_entries)
        {
            byte[] bmpinfoheader = new byte[40];
            fillBitmapInfoHeader(bits_per_pixel, cmap_entries, bmpinfoheader);
            return bmpinfoheader;
        }

        private void fillBitmapInfoHeader(int bitsPerPixel, int cmap_entries, byte[] infoHeader)
        {
            /* Fill the info header (Microsoft calls this a BITMAPINFOHEADER) */
            PUT_2B(infoHeader, 0, infoHeader.Length);   /* biSize */
            PUT_4B(infoHeader, 4, m_parameters.Width); /* biWidth */
            PUT_4B(infoHeader, 8, m_parameters.Height); /* biHeight */
            PUT_2B(infoHeader, 12, 1);   /* biPlanes - must be 1 */
            PUT_2B(infoHeader, 14, bitsPerPixel); /* biBitCount */
            /* we leave biCompression = 0, for none */
            /* we leave biSizeImage = 0; this is correct for uncompressed data */

            if (m_parameters.DensityUnit == DensityUnit.DotsCm)
            {
                /* if have density in dots/cm, then */
                PUT_4B(infoHeader, 24, m_parameters.DensityX * 100); /* XPels/M */
                PUT_4B(infoHeader, 28, m_parameters.DensityY * 100); /* XPels/M */
            }
            PUT_2B(infoHeader, 32, cmap_entries); /* biClrUsed */
            /* we leave biClrImportant = 0 */
        }

        private byte[] createBitmapV4InfoHeader(int bitsPerPixel)
        {
            byte[] infoHeader = new byte[40 + 68];
            fillBitmapInfoHeader(bitsPerPixel, 0, infoHeader);

            PUT_4B(infoHeader, 56, 0x02); /* CSType == 0x02 (CMYK) */

            return infoHeader;
        }

        /// <summary>
        /// Write the colormap.
        /// Windows uses BGR0 map entries; OS/2 uses BGR entries.
        /// </summary>
        private void writeColormap(int map_colors, int map_entry_size)
        {
            byte[][] colormap = m_parameters.Colormap;
            int num_colors = m_parameters.ActualNumberOfColors;

            int i = 0;
            if (colormap != null)
            {
                if (m_parameters.ComponentsPerSample == 3)
                {
                    /* Normal case with RGB colormap */
                    for (i = 0; i < num_colors; i++)
                    {
                        m_output.WriteByte(colormap[2][i]);
                        m_output.WriteByte(colormap[1][i]);
                        m_output.WriteByte(colormap[0][i]);
                        if (map_entry_size == 4)
                            m_output.WriteByte(0);
                    }
                }
                else
                {
                    /* Grayscale colormap (only happens with grayscale quantization) */
                    for (i = 0; i < num_colors; i++)
                    {
                        m_output.WriteByte(colormap[0][i]);
                        m_output.WriteByte(colormap[0][i]);
                        m_output.WriteByte(colormap[0][i]);
                        if (map_entry_size == 4)
                            m_output.WriteByte(0);
                    }
                }
            }
            else
            {
                /* If no colormap, must be grayscale data.  Generate a linear "map". */
                for (i = 0; i < 256; i++)
                {
                    m_output.WriteByte((byte)i);
                    m_output.WriteByte((byte)i);
                    m_output.WriteByte((byte)i);
                    if (map_entry_size == 4)
                        m_output.WriteByte(0);
                }
            }

            /* Pad colormap with zeros to ensure specified number of colormap entries */
            if (i > map_colors)
                throw new InvalidOperationException("Too many colors");

            for (; i < map_colors; i++)
            {
                m_output.WriteByte(0);
                m_output.WriteByte(0);
                m_output.WriteByte(0);
                if (map_entry_size == 4)
                    m_output.WriteByte(0);
            }
        }

        private void writePixels()
        {
            for (int row = m_parameters.Height - 1; row >= 0; --row)
                for (int col = 0; col < m_rowWidth; ++col)
                    m_output.WriteByte(m_pixels[col][row]);
        }


        private static void PUT_2B(byte[] array, int offset, int value)
        {
            array[offset] = (byte)((value) & 0xFF);
            array[offset + 1] = (byte)(((value) >> 8) & 0xFF);
        }

        private static void PUT_4B(byte[] array, int offset, int value)
        {
            array[offset] = (byte)((value) & 0xFF);
            array[offset + 1] = (byte)(((value) >> 8) & 0xFF);
            array[offset + 2] = (byte)(((value) >> 16) & 0xFF);
            array[offset + 3] = (byte)(((value) >> 24) & 0xFF);
        }
    }
}
