// <copyright file="ColorVector.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System.Numerics;

    public struct ColorVector
    {
        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorVector"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="a">
        /// The alpha component of this <see cref="ColorVector"/>.
        /// </param>
        public ColorVector(double b, double g, double r, double a)
            : this((float)b, (float)g, (float)r, (float)a)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorVector"/> struct.
        /// </summary>
        /// <param name="b">
        /// The blue component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="g">
        /// The green component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="r">
        /// The red component of this <see cref="ColorVector"/>.
        /// </param>
        /// <param name="a">
        /// The alpha component of this <see cref="ColorVector"/>.
        /// </param>
        public ColorVector(float b, float g, float r, float a)
            : this()
        {
            this.backingVector.X = b;
            this.backingVector.Y = g;
            this.backingVector.Z = r;
            this.backingVector.W = a;
        }

        /// <summary> The color's blue component, between 0.0 and 1.0 </summary>
        public float B
        {
            get
            {
                return this.backingVector.X;
            }

            set
            {
                this.backingVector.X = value;
            }
        }

        /// <summary> The color's green component, between 0.0 and 1.0 </summary>
        public float G
        {
            get
            {
                return this.backingVector.Y;
            }

            set
            {
                this.backingVector.Y = value;
            }
        }

        /// <summary> The color's red component, between 0.0 and 1.0 </summary>
        public float R
        {
            get
            {
                return this.backingVector.Z;
            }

            set
            {
                this.backingVector.Z = value;
            }
        }

        /// <summary> The color's alpha component, between 0.0 and 1.0 </summary>
        public float A
        {
            get
            {
                return this.backingVector.W;
            }

            set
            {
                this.backingVector.W = value;
            }
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="ColorVector"/> to a
        /// <see cref="Bgra"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="ColorVector"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Bgra"/>.
        /// </returns>
        public static implicit operator ColorVector(Bgra color)
        {
            return new ColorVector(color.B / 255f, color.G / 255f, color.R / 255f, color.A / 255f);
        }
    }
}
