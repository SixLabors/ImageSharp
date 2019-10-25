// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Decoder for lossless webp images.
    /// </summary>
    internal sealed class WebPLosslessDecoder
    {
        private Vp8LBitReader bitReader;

        public WebPLosslessDecoder(Stream stream)
        {
            this.bitReader = new Vp8LBitReader(stream);
        }

        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height, int imageDataSize)
            where TPixel : struct, IPixel<TPixel>
        {
            //ReadTransformations();
        }

        private void ReadTransformations()
        {
            // Next bit indicates, if a transformation is present.
            bool transformPresent = bitReader.ReadBit();
            int numberOfTransformsPresent = 0;
            while (transformPresent)
            {
                var transformType = (WebPTransformType)bitReader.Read(2);
                switch (transformType)
                {
                    case WebPTransformType.SubtractGreen:
                        // There is no data associated with this transform.
                        break;
                    case WebPTransformType.ColorIndexingTransform:
                        // The transform data contains color table size and the entries in the color table.
                        // 8 bit value for color table size.
                        uint colorTableSize = bitReader.Read(8) + 1;

                        // TODO: color table should follow here?
                        break;

                    case WebPTransformType.PredictorTransform:
                        {
                            // The first 3 bits of prediction data define the block width and height in number of bits.
                            // The number of block columns, block_xsize, is used in indexing two-dimensionally.
                            uint sizeBits = bitReader.Read(3) + 2;
                            int blockWidth = 1 << (int)sizeBits;
                            int blockHeight = 1 << (int)sizeBits;

                            break;
                        }

                    case WebPTransformType.ColorTransform:
                        {
                            // The first 3 bits of the color transform data contain the width and height of the image block in number of bits,
                            // just like the predictor transform:
                            uint sizeBits = bitReader.Read(3) + 2;
                            int blockWidth = 1 << (int)sizeBits;
                            int blockHeight = 1 << (int)sizeBits;
                            break;
                        }
                }

                numberOfTransformsPresent++;
                if (numberOfTransformsPresent == 4)
                {
                    break;
                }

                transformPresent = bitReader.ReadBit();
            }

            // TODO: return transformation in an appropriate form.
        }
    }
}
