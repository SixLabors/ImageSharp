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
            return QuantizeImage(image, 0, 1);
        }

        public Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader)
        {
            var colorCount = MaxColor;
            ImageBuffer buffer = new ImageBuffer(image);
            var data = BuildHistogram(buffer, alphaThreshold, alphaFader);

            data = CalculateMoments(data);
            var cubes = SplitData(ref colorCount, data);
            var lookups = BuildLookups(cubes, data);
            return GetQuantizedImage(buffer, colorCount, lookups, alphaThreshold);
        }

        private static ColorData BuildHistogram(ImageBuffer sourceImage, int alphaThreshold, int alphaFader)
        {
            ColorData colorData = new ColorData(MaxSideIndex);
            foreach (Pixel pixel in sourceImage.Pixels)
            {
                if (pixel.Alpha >= alphaThreshold)
                {
                    Pixel indexedPixel = pixel;
                    if (indexedPixel.Alpha < 255)
                    {
                        int alpha = pixel.Alpha + (pixel.Alpha % alphaFader);
                        indexedPixel.Alpha = (byte)(alpha > 255 ? 255 : alpha);
                    }

                    indexedPixel.Alpha = (byte)((indexedPixel.Alpha >> 3) + 1);
                    indexedPixel.Red = (byte)((indexedPixel.Red >> 3) + 1);
                    indexedPixel.Green = (byte)((indexedPixel.Green >> 3) + 1);
                    indexedPixel.Blue = (byte)((indexedPixel.Blue >> 3) + 1);
                    colorData.Moments[indexedPixel.Alpha, indexedPixel.Red, indexedPixel.Green, indexedPixel.Blue].Add(pixel);
                }
            }

            return colorData;
        }

        private static ColorData CalculateMoments(ColorData data)
        {
            var xarea = new ColorMoment[SideSize, SideSize];
            var area = new ColorMoment[SideSize];
            var moments = data.Moments;

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
                            line.AddFast(ref moments[alphaIndex, redIndex, greenIndex, blueIndex]);
                            area[blueIndex].AddFast(ref line);
                            xarea[greenIndex, blueIndex].AddFast(ref area[blueIndex]);
                            moments[alphaIndex, redIndex, greenIndex, blueIndex] = moments[alphaIndex - 1, redIndex, greenIndex, blueIndex] + xarea[greenIndex, blueIndex];
                        }
                    }
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

        private List<Pixel> BuildLookups(Box[] cubes, ColorData data)
        {
            List<Pixel> lookups = new List<Pixel>(cubes.Length);

            foreach (var cube in cubes)
            {
                var volume = Volume(cube, data.Moments);

                if (volume.Weight <= 0)
                {
                    continue;
                }

                var lookup = new Pixel
                    {
                        Alpha = (byte)(volume.Alpha / volume.Weight),
                        Red = (byte)(volume.Red / volume.Weight),
                        Green = (byte)(volume.Green / volume.Weight),
                        Blue = (byte)(volume.Blue / volume.Weight)
                    };

                lookups.Add(lookup);
            }

            return lookups;
        }

        internal abstract Image GetQuantizedImage(ImageBuffer image, int colorCount, List<Pixel> lookups, int alphaThreshold);
    }
}