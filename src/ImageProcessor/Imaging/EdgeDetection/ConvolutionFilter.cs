
namespace ImageProcessor.Imaging.EdgeDetection
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;

    using ImageProcessor.Common.Extensions;
    
    /// <summary>
    /// http://pastebin.com/xHHD3pXi
    /// </summary>
    public class ConvolutionFilter
    {
        private readonly IEdgeFilter edgeFilter;

        public ConvolutionFilter(IEdgeFilter edgeFilter)
        {
            this.edgeFilter = edgeFilter;
        }

        public Bitmap ProcessFilter(Bitmap source)
        {
            double[,] horizontalFilter = this.edgeFilter.HorizontalMatrix;
            double[,] verticallFilter = this.edgeFilter.VerticalMatrix;

            int width = source.Width;
            int height = source.Height;
            int maxWidth = width - 1;
            int maxHeight = height - 1;

            Bitmap destination = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                using (FastBitmap destinationBitmap = new FastBitmap(destination))
                {
                    for (int y = 1; y < maxHeight; y++)
                    {
                        for (int x = 1; x < maxWidth; x++)
                        {
                            double newX = 0;
                            double newY = 0;

                            for (int hw = -1; hw < 2; hw++)
                            {
                                for (int wi = -1; wi < 2; wi++)
                                {
                                    double component = sourceBitmap.GetPixel(x + wi, y + hw).B;
                                    newX += horizontalFilter[hw + 1, wi + 1] * component;
                                    newY += verticallFilter[hw + 1, wi + 1] * component;
                                }
                            }

                            byte value = Math.Sqrt((newX * newX) + (newY * newY)).ToByte();
                            Color tempcolor = Color.FromArgb(value, value, value);
                            destinationBitmap.SetPixel(x, y, tempcolor);
                        }
                    }
                }
            }

            return destination;

        }
    }
}
