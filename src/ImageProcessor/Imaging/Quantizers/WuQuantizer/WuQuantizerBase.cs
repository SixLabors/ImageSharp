using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace nQuant
{
    public abstract class WuQuantizerBase
    {
        private const int MaxColor = 256;
        protected const byte AlphaColor = 255;
        protected const int Alpha = 3;
        protected const int Red = 2;
        protected const int Green = 1;
        protected const int Blue = 0;
        private const int SideSize = 33;
        private const int MaxSideIndex = 32;

        public Image QuantizeImage(Bitmap image)
        {
            return QuantizeImage(image, 10, 70);
        }

        public Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader)
        {
            var colorCount = MaxColor;
            var data = BuildHistogram(image, alphaThreshold, alphaFader);
            data = CalculateMoments(data);
            var cubes = SplitData(ref colorCount, data);
            var palette = GetQuantizedPalette(colorCount, data, cubes, alphaThreshold);
            return ProcessImagePixels(image, palette);
        }

        private static Bitmap ProcessImagePixels(Image sourceImage, QuantizedPalette palette)
        {
            var result = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format8bppIndexed);
            var newPalette = result.Palette;
            for (var index = 0; index < palette.Colors.Count; index++)
                newPalette.Entries[index] = palette.Colors[index];
            result.Palette = newPalette;

            BitmapData targetData = null;
            try
            {
                var resultHeight = result.Height;
                var resultWidth = result.Width;
                targetData = result.LockBits(Rectangle.FromLTRB(0, 0, resultWidth, resultHeight), ImageLockMode.WriteOnly, result.PixelFormat);
                const byte targetBitDepth = 8;
                var targetByteLength = targetData.Stride < 0 ? -targetData.Stride : targetData.Stride;
                var targetByteCount = Math.Max(1, targetBitDepth >> 3);
                var targetBuffer = new byte[targetByteLength];
                var targetValue = new byte[targetByteCount];
                var pixelIndex = 0;



                for (var y = 0; y < resultHeight; y++)
                {
                    var targetIndex = 0;
                    for (var x = 0; x < resultWidth; x++)
                    {
                        var targetIndexOffset = targetIndex >> 3;
                        targetValue[0] =
                            (byte)
                            (palette.PixelIndex[pixelIndex] == AlphaColor
                                 ? palette.Colors.Count - 1
                                 : palette.PixelIndex[pixelIndex]);
                        pixelIndex++;

                        for (var valueIndex = 0; valueIndex < targetByteCount; valueIndex++)
                        {
                            targetBuffer[valueIndex + targetIndexOffset] = targetValue[valueIndex];
                        }

                        targetIndex += targetBitDepth;
                    }

                    Marshal.Copy(targetBuffer, 0, targetData.Scan0 + (targetByteLength * y), targetByteLength);
                }
            }
            finally
            {
                if (targetData != null)
                {
                    result.UnlockBits(targetData);
                }
            }

            return result;
        }

        private static ColorData BuildHistogram(Bitmap sourceImage, int alphaThreshold, int alphaFader)
        {
            int bitmapWidth = sourceImage.Width;
            int bitmapHeight = sourceImage.Height;

            BitmapData data = sourceImage.LockBits(
                Rectangle.FromLTRB(0, 0, bitmapWidth, bitmapHeight),
                ImageLockMode.ReadOnly,
                sourceImage.PixelFormat);
            ColorData colorData = new ColorData(MaxSideIndex, bitmapWidth, bitmapHeight);

            try
            {
                var bitDepth = Image.GetPixelFormatSize(sourceImage.PixelFormat);
                if (bitDepth != 32)
                    throw new QuantizationException(string.Format("Thie image you are attempting to quantize does not contain a 32 bit ARGB palette. This image has a bit depth of {0} with {1} colors.", bitDepth, sourceImage.Palette.Entries.Length));
                var byteLength = data.Stride < 0 ? -data.Stride : data.Stride;
                var byteCount = Math.Max(1, bitDepth >> 3);
                var buffer = new Byte[byteLength];

                var value = new Byte[byteCount];

                for (int y = 0; y < bitmapHeight; y++)
                {
                    Marshal.Copy(data.Scan0 + (byteLength * y), buffer, 0, buffer.Length);

                    var index = 0;
                    for (int x = 0; x < bitmapWidth; x++)
                    {
                        var indexOffset = index >> 3;

                        for (var valueIndex = 0; valueIndex < byteCount; valueIndex++)
                        {
                            value[valueIndex] = buffer[valueIndex + indexOffset];
                        }

                        Pixel pixelValue = new Pixel(value[Alpha], value[Red], value[Green], value[Blue]);

                        var indexAlpha = (byte)((value[Alpha] >> 3) + 1);
                        var indexRed = (byte)((value[Red] >> 3) + 1);
                        var indexGreen = (byte)((value[Green] >> 3) + 1);
                        var indexBlue = (byte)((value[Blue] >> 3) + 1);

                        if (value[Alpha] > alphaThreshold)
                        {
                            if (value[Alpha] < 255)
                            {
                                var alpha = value[Alpha] + (value[Alpha] % alphaFader);
                                value[Alpha] = (byte)(alpha > 255 ? 255 : alpha);
                                indexAlpha = (byte)((value[Alpha] >> 3) + 1);
                            }

                            colorData.Moments[indexAlpha, indexRed, indexGreen, indexBlue] += pixelValue;
                        }

                        colorData.AddPixel(
                            pixelValue,
                            new Pixel(indexAlpha, indexRed, indexGreen, indexBlue));
                        index += bitDepth;
                    }
                }
            }
            finally
            {
                sourceImage.UnlockBits(data);
            }
            return colorData;
        }

        private static ColorData CalculateMoments(ColorData data)
        {
            var xarea = new ColorMoment[SideSize, SideSize];
            var xPreviousArea = new ColorMoment[SideSize, SideSize];
            var area = new ColorMoment[SideSize];
            for (var alphaIndex = 1; alphaIndex <= MaxSideIndex; ++alphaIndex)
            {
                for (var redIndex = 1; redIndex <= MaxSideIndex; ++redIndex)
                {
                    Array.Clear(area, 0, area.Length);
                    for (var greenIndex = 1; greenIndex <= MaxSideIndex; ++greenIndex)
                    {
                        ColorMoment line = new ColorMoment();
                        for (var blueIndex = 1; blueIndex <= MaxSideIndex; ++blueIndex)
                        {
                            line += data.Moments[alphaIndex, redIndex, greenIndex, blueIndex];
                            area[blueIndex] += line;

                            xarea[greenIndex, blueIndex] = xPreviousArea[greenIndex, blueIndex] + area[blueIndex];
                            data.Moments[alphaIndex, redIndex, greenIndex, blueIndex] = data.Moments[alphaIndex - 1, redIndex, greenIndex, blueIndex] + xarea[greenIndex, blueIndex];
                        }
                    }

                    var temp = xarea;
                    xarea = xPreviousArea;
                    xPreviousArea = temp;
                }
            }
            return data;
        }

        private static ColorMoment Top(Box cube, int direction, int position, ColorMoment[, , ,] moment)
        {
            switch (direction)
            {
                case Alpha:
                    return (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Red:
                    return (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMaximum]) -
                           (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMinimum]);

                case Green:
                    return (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMaximum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMaximum]) -
                           (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMinimum]);

                case Blue:
                    return (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, position] -
                            moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, position] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, position] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, position]) -
                           (moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, position] -
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, position] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, position] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, position]);

                default:
                    return new ColorMoment();
            }
        }

        private static ColorMoment Bottom(Box cube, int direction, ColorMoment[, , ,] moment)
        {
            switch (direction)
            {
                case Alpha:
                    return (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Red:
                    return (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Green:
                    return (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                           (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                case Blue:
                    return (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]) -
                           (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                            moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);

                default:
                    return new ColorMoment();
            }
        }

        private static CubeCut Maximize(ColorData data, Box cube, int direction, byte first, byte last, ColorMoment whole)
        {
            var bottom = Bottom(cube, direction, data.Moments);
            float result = 0.0f;
            byte? cutPoint = null;

            for (byte position = first; position < last; ++position)
            {
                var half = bottom + Top(cube, direction, position, data.Moments);
                if (half.Weight == 0)
                {
                    continue;
                }

                long temp = half.WeightedDistance();
                half = whole - half;
                if (half.Weight != 0)
                {
                    temp += half.WeightedDistance();

                    if (temp > result)
                    {
                        result = temp;
                        cutPoint = position;
                    }
                }
            }

            return new CubeCut(cutPoint, result);
        }

        private bool Cut(ColorData data, ref Box first, ref Box second)
        {
            int direction;
            var whole = Volume(first, data.Moments);
            var maxAlpha = Maximize(data, first, Alpha, (byte)(first.AlphaMinimum + 1), first.AlphaMaximum, whole);
            var maxRed = Maximize(data, first, Red, (byte)(first.RedMinimum + 1), first.RedMaximum, whole);
            var maxGreen = Maximize(data, first, Green, (byte)(first.GreenMinimum + 1), first.GreenMaximum, whole);
            var maxBlue = Maximize(data, first, Blue, (byte)(first.BlueMinimum + 1), first.BlueMaximum, whole);

            if ((maxAlpha.Value >= maxRed.Value) && (maxAlpha.Value >= maxGreen.Value) && (maxAlpha.Value >= maxBlue.Value))
            {
                direction = Alpha;
                if (maxAlpha.Position == null) return false;
            }
            else if ((maxRed.Value >= maxAlpha.Value) && (maxRed.Value >= maxGreen.Value) && (maxRed.Value >= maxBlue.Value))
                direction = Red;
            else
            {
                if ((maxGreen.Value >= maxAlpha.Value) && (maxGreen.Value >= maxRed.Value) && (maxGreen.Value >= maxBlue.Value))
                    direction = Green;
                else
                    direction = Blue;
            }

            second.AlphaMaximum = first.AlphaMaximum;
            second.RedMaximum = first.RedMaximum;
            second.GreenMaximum = first.GreenMaximum;
            second.BlueMaximum = first.BlueMaximum;

            switch (direction)
            {
                case Alpha:
                    second.AlphaMinimum = first.AlphaMaximum = (byte)maxAlpha.Position;
                    second.RedMinimum = first.RedMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Red:
                    second.RedMinimum = first.RedMaximum = (byte)maxRed.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Green:
                    second.GreenMinimum = first.GreenMaximum = (byte)maxGreen.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.RedMinimum = first.RedMinimum;
                    second.BlueMinimum = first.BlueMinimum;
                    break;

                case Blue:
                    second.BlueMinimum = first.BlueMaximum = (byte)maxBlue.Position;
                    second.AlphaMinimum = first.AlphaMinimum;
                    second.RedMinimum = first.RedMinimum;
                    second.GreenMinimum = first.GreenMinimum;
                    break;
            }

            first.Size = (first.AlphaMaximum - first.AlphaMinimum) * (first.RedMaximum - first.RedMinimum) * (first.GreenMaximum - first.GreenMinimum) * (first.BlueMaximum - first.BlueMinimum);
            second.Size = (second.AlphaMaximum - second.AlphaMinimum) * (second.RedMaximum - second.RedMinimum) * (second.GreenMaximum - second.GreenMinimum) * (second.BlueMaximum - second.BlueMinimum);

            return true;
        }

        private static float CalculateVariance(ColorData data, Box cube)
        {
            ColorMoment volume = Volume(cube, data.Moments);
            return volume.Variance();
        }

        private static ColorMoment Volume(Box cube, ColorMoment[, , ,] moment)
        {
            return (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -

                   (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);
        }

        private static float VolumeFloat(Box cube, float[, , ,] moment)
        {
            return (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -

                   (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);
        }

        private Box[] SplitData(ref int colorCount, ColorData data)
        {
            --colorCount;
            var next = 0;
            var volumeVariance = new float[MaxColor];
            var cubes = new Box[MaxColor];
            cubes[0].AlphaMaximum = MaxSideIndex;
            cubes[0].RedMaximum = MaxSideIndex;
            cubes[0].GreenMaximum = MaxSideIndex;
            cubes[0].BlueMaximum = MaxSideIndex;
            for (var cubeIndex = 1; cubeIndex < colorCount; ++cubeIndex)
            {
                if (Cut(data, ref cubes[next], ref cubes[cubeIndex]))
                {
                    volumeVariance[next] = cubes[next].Size > 1 ? CalculateVariance(data, cubes[next]) : 0.0f;
                    volumeVariance[cubeIndex] = cubes[cubeIndex].Size > 1 ? CalculateVariance(data, cubes[cubeIndex]) : 0.0f;
                }
                else
                {
                    volumeVariance[next] = 0.0f;
                    cubeIndex--;
                }

                next = 0;
                var temp = volumeVariance[0];

                for (var index = 1; index <= cubeIndex; ++index)
                {
                    if (volumeVariance[index] <= temp) continue;
                    temp = volumeVariance[index];
                    next = index;
                }

                if (temp > 0.0) continue;
                colorCount = cubeIndex + 1;
                break;
            }
            return cubes.Take(colorCount).ToArray();
        }

        protected Lookup[] BuildLookups(Box[] cubes, ColorData data)
        {
            List<Lookup> lookups = new List<Lookup>(cubes.Length);

            foreach (var cube in cubes)
            {
                var volume = Volume(cube, data.Moments);

                if (volume.Weight <= 0)
                {
                    continue;
                }

                var lookup = new Lookup
                    {
                        Alpha = (int)(volume.Alpha / volume.Weight),
                        Red = (int)(volume.Red / volume.Weight),
                        Green = (int)(volume.Green / volume.Weight),
                        Blue = (int)(volume.Blue / volume.Weight)
                    };

                lookups.Add(lookup);
            }

            return lookups.ToArray();
        }

        protected abstract QuantizedPalette GetQuantizedPalette(int colorCount, ColorData data, Box[] cubes, int alphaThreshold);
    }
}