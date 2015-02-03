// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Quantizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to calculate the color palette of an image.
//   <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers
{
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// Encapsulates methods to calculate the color palette of an image.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    public unsafe abstract class Quantizer : IQuantizer
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private readonly bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Quantizer"/> class. 
        /// </summary>
        /// <param name="singlePass">
        /// If true, the quantization only needs to loop through the source pixels once
        /// </param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the 'QuantizeImage' function. If two passes are required, the code will call 'InitialQuantizeImage'
        /// and then 'QuantizeImage'.
        /// </remarks>
        protected Quantizer(bool singlePass)
        {
            this.singlePass = singlePass;
        }

        /// <summary>
        /// Quantize an image and return the resulting output bitmap.
        /// </summary>
        /// <param name="source">
        /// The image to quantize.
        /// </param>
        /// <returns>
        /// A quantized version of the image.
        /// </returns>
        public Bitmap Quantize(Image source)
        {
            // Get the size of the source image
            int height = source.Height;
            int width = source.Width;

            // And construct a rectangle from these dimensions
            Rectangle bounds = new Rectangle(0, 0, width, height);

            // First off take a 32bpp copy of the image
            Bitmap copy = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            copy.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            // And construct an 8bpp version
            Bitmap output = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            output.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            // Now lock the bitmap into memory
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.PageUnit = GraphicsUnit.Pixel;

                // Draw the source image onto the copy bitmap,
                // which will effect a widening as appropriate.
                g.DrawImageUnscaled(source, bounds);
            }

            // Define a pointer to the bitmap data
            BitmapData sourceData = null;

            try
            {
                // Get the source image bits and lock into memory
                sourceData = copy.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

                // Call the FirstPass function if not a single pass algorithm.
                // For something like an Octree quantizer, this will run through
                // all image pixels, build a data structure, and create a palette.
                if (!this.singlePass)
                {
                    this.FirstPass(sourceData, width, height);
                }

                // Then set the color palette on the output bitmap. I'm passing in the current palette 
                // as there's no way to construct a new, empty palette.
                output.Palette = this.GetPalette(output.Palette);

                // Then call the second pass which actually does the conversion
                this.SecondPass(sourceData, output, width, height, bounds);
            }
            finally
            {
                // Ensure that the bits are unlocked
                copy.UnlockBits(sourceData);
            }

            // Last but not least, return the output bitmap
            return output;
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="sourceData">
        /// The source data
        /// </param>
        /// <param name="width">
        /// The width in pixels of the image
        /// </param>
        /// <param name="height">
        /// The height in pixels of the image
        /// </param>
        protected virtual void FirstPass(BitmapData sourceData, int width, int height)
        {
            // Define the source data pointers. The source row is a byte to
            // keep addition of the stride value easier (as this is in bytes)
            byte* sourceRow = (byte*)sourceData.Scan0.ToPointer();

            // Loop through each row
            for (int row = 0; row < height; row++)
            {
                // Set the source pixel to the first pixel in this row
                int* sourcePixel = (int*)sourceRow;

                // And loop through each column
                for (int col = 0; col < width; col++, sourcePixel++)
                {
                    // Now I have the pixel, call the FirstPassQuantize function...
                    this.InitialQuantizePixel((Color32*)sourcePixel);
                }

                // Add the stride to the source row
                sourceRow += sourceData.Stride;
            }
        }

        /// <summary>
        /// Execute a second pass through the bitmap
        /// </summary>
        /// <param name="sourceData">
        /// The source bitmap, locked into memory
        /// </param>
        /// <param name="output">
        /// The output bitmap
        /// </param>
        /// <param name="width">
        /// The width in pixels of the image
        /// </param>
        /// <param name="height">
        /// The height in pixels of the image
        /// </param>
        /// <param name="bounds">
        /// The bounding rectangle
        /// </param>
        protected virtual void SecondPass(BitmapData sourceData, Bitmap output, int width, int height, Rectangle bounds)
        {
            BitmapData outputData = null;

            try
            {
                // Lock the output bitmap into memory
                outputData = output.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

                // Define the source data pointers. The source row is a byte to
                // keep addition of the stride value easier (as this is in bytes)
                byte* sourceRow = (byte*)sourceData.Scan0.ToPointer();
                int* sourcePixel = (int*)sourceRow;
                int* previousPixel = sourcePixel;

                // Now define the destination data pointers
                byte* destinationRow = (byte*)outputData.Scan0.ToPointer();
                byte* destinationPixel = destinationRow;

                // And convert the first pixel, so that I have values going into the loop
                byte pixelValue = this.QuantizePixel((Color32*)sourcePixel);

                // Assign the value of the first pixel
                *destinationPixel = pixelValue;

                // Loop through each row
                for (int row = 0; row < height; row++)
                {
                    // Set the source pixel to the first pixel in this row
                    sourcePixel = (int*)sourceRow;

                    // And set the destination pixel pointer to the first pixel in the row
                    destinationPixel = destinationRow;

                    // Loop through each pixel on this scan line
                    for (int col = 0; col < width; col++, sourcePixel++, destinationPixel++)
                    {
                        // Check if this is the same as the last pixel. If so use that value
                        // rather than calculating it again. This is an inexpensive optimization.
                        if (*previousPixel != *sourcePixel)
                        {
                            // Quantize the pixel
                            pixelValue = this.QuantizePixel((Color32*)sourcePixel);

                            // And setup the previous pointer
                            previousPixel = sourcePixel;
                        }

                        // And set the pixel in the output
                        *destinationPixel = pixelValue;
                    }

                    // Add the stride to the source row
                    sourceRow += sourceData.Stride;

                    // And to the destination row
                    destinationRow += outputData.Stride;
                }
            }
            finally
            {
                // Ensure that I unlock the output bits
                output.UnlockBits(outputData);
            }
        }

        /// <summary>
        /// Override this to process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">
        /// The pixel to quantize
        /// </param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(Color32* pixel)
        {
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">
        /// The pixel to quantize
        /// </param>
        /// <returns>
        /// The quantized value
        /// </returns>
        protected abstract byte QuantizePixel(Color32* pixel);

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <param name="original">
        /// Any old palette, this is overwritten
        /// </param>
        /// <returns>
        /// The new color palette
        /// </returns>
        protected abstract ColorPalette GetPalette(ColorPalette original);
    }
}
