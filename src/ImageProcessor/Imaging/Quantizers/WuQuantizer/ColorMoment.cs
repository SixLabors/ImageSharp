// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorMoment.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The cumulative color moment for holding pixel information.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using ImageProcessor.Imaging.Colors;

    /// <summary>
    /// The cumulative color moment for holding pixel information.
    /// Adapted from <see href="https://github.com/drewnoakes" />
    /// </summary>
    internal struct ColorMoment
    {
        /// <summary>
        /// The alpha component.
        /// </summary>
        public long Alpha;

        /// <summary>
        /// The blue component.
        /// </summary>
        public long Blue;

        /// <summary>
        /// The green component.
        /// </summary>
        public long Green;

        /// <summary>
        /// The moment.
        /// </summary>
        public float Moment;

        /// <summary>
        /// The red.
        /// </summary>
        public long Red;

        /// <summary>
        /// The weight.
        /// </summary>
        public int Weight;

        /// <summary>
        /// Adds one <see cref="ColorMoment"/> to another.
        /// </summary>
        /// <param name="firstAddend">
        /// The first <see cref="ColorMoment"/>.
        /// </param>
        /// <param name="secondAddend">
        /// The second <see cref="ColorMoment"/>.
        /// </param>
        /// <returns>
        /// The <see cref="ColorMoment"/> representing the sum of the addition.
        /// </returns>
        public static ColorMoment operator +(ColorMoment firstAddend, ColorMoment secondAddend)
        {
            firstAddend.Alpha += secondAddend.Alpha;
            firstAddend.Red += secondAddend.Red;
            firstAddend.Green += secondAddend.Green;
            firstAddend.Blue += secondAddend.Blue;
            firstAddend.Weight += secondAddend.Weight;
            firstAddend.Moment += secondAddend.Moment;
            return firstAddend;
        }

        /// <summary>
        /// Subtracts one <see cref="ColorMoment "/> from another.
        /// </summary>
        /// <param name="minuend">
        /// The <see cref="ColorMoment"/> from which the other <see cref="ColorMoment"/> will be subtracted
        /// </param>
        /// <param name="subtrahend">
        /// The <see cref="ColorMoment"/> that is to be subtracted.
        /// </param>
        /// <returns>
        /// The <see cref="ColorMoment"/> representing the difference of the subtraction.
        /// </returns>
        public static ColorMoment operator -(ColorMoment minuend, ColorMoment subtrahend)
        {
            minuend.Alpha -= subtrahend.Alpha;
            minuend.Red -= subtrahend.Red;
            minuend.Green -= subtrahend.Green;
            minuend.Blue -= subtrahend.Blue;
            minuend.Weight -= subtrahend.Weight;
            minuend.Moment -= subtrahend.Moment;
            return minuend;
        }

        /// <summary>
        /// Negates the given <see cref="ColorMoment"/> .
        /// </summary>
        /// <param name="moment">
        /// The <see cref="ColorMoment"/> to negate.
        /// </param>
        /// <returns>
        /// The negated result
        /// </returns>
        public static ColorMoment operator -(ColorMoment moment)
        {
            moment.Alpha = -moment.Alpha;
            moment.Red = -moment.Red;
            moment.Green = -moment.Green;
            moment.Blue = -moment.Blue;
            moment.Weight = -moment.Weight;
            moment.Moment = -moment.Moment;
            return moment;
        }

        /// <summary>
        /// Adds a pixel to the current instance.
        /// </summary>
        /// <param name="pixel">
        /// The pixel to add.
        /// </param>
        public void Add(Color32 pixel)
        {
            byte alpha = pixel.A;
            byte red = pixel.R;
            byte green = pixel.G;
            byte blue = pixel.B;
            this.Alpha += alpha;
            this.Red += red;
            this.Green += green;
            this.Blue += blue;
            this.Weight++;
            this.Moment += (alpha * alpha) + (red * red) + (green * green) + (blue * blue);
        }

        /// <summary>
        /// Adds a color moment to the current instance more quickly.
        /// </summary>
        /// <param name="moment">
        /// The <see cref="ColorMoment"/> to add.
        /// </param>
        public void AddFast(ref ColorMoment moment)
        {
            this.Alpha += moment.Alpha;
            this.Red += moment.Red;
            this.Green += moment.Green;
            this.Blue += moment.Blue;
            this.Weight += moment.Weight;
            this.Moment += moment.Moment;
        }

        /// <summary>
        /// The amplitude.
        /// </summary>
        /// <returns>
        /// The <see cref="long"/> representing the amplitude.
        /// </returns>
        public long Amplitude()
        {
            return (this.Alpha * this.Alpha) + (this.Red * this.Red) + (this.Green * this.Green) + (this.Blue * this.Blue);
        }

        /// <summary>
        /// The variance.
        /// </summary>
        /// <returns>
        /// The <see cref="float"/> representing the variance.
        /// </returns>
        public float Variance()
        {
            float result = this.Moment - ((float)this.Amplitude() / this.Weight);
            return float.IsNaN(result) ? 0.0f : result;
        }

        /// <summary>
        /// The weighted distance.
        /// </summary>
        /// <returns>
        /// The <see cref="long"/>.
        /// </returns>
        public long WeightedDistance()
        {
            return this.Amplitude() / this.Weight;
        }
    }
}