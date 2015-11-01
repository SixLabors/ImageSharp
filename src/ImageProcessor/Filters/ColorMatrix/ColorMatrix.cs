// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorMatrix.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Defines a 5x5 matrix that contains the coordinates for the RGBAW color space.
    /// </summary>
    public sealed class ColorMatrix
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColorMatrix"/> class.
        /// </summary>
        public ColorMatrix()
        {
            // Setup the identity matrix by default
            this.Matrix00 = 1.0f;
            this.Matrix11 = 1.0f;
            this.Matrix22 = 1.0f;
            this.Matrix33 = 1.0f;
            this.Matrix44 = 1.0f;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorMatrix"/> class with the
        /// elements in the specified matrix.
        /// </summary>
        /// <param name="colorMatrix">
        /// The elements defining the new Color Matrix.
        /// </param>
        public ColorMatrix(float[][] colorMatrix)
        {
            this.SetMatrix(colorMatrix);
        }

        /// <summary>
        /// Gets or sets the element at the 0th row and 0th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix00 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 0th row and 1st column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix01 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 0th row and 2nd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix02 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 0th row and 3rd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix03 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 0th row and 4th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix04 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 1st row and 0th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix10 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 1st row and 1st column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix11 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 1st row and 2nd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix12 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 1st row and 3rd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix13 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 1st row and 4th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix14 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 2nd row and 0th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix20 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 2nd row and 1st column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix21 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 2nd row and 2nd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix22 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 2nd row and 3rd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix23 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 2nd row and 4th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix24 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 3rd row and 0th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix30 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 3rd row and 1st column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix31 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 3rd row and 2nd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix32 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 3rd row and 3rd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix33 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 3rd row and 4th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix34 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 4th row and 0th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix40 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 4th row and 1st column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix41 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 4th row and 2nd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix42 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 4th row and 3rd column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix43 { get; set; }

        /// <summary>
        /// Gets or sets the element at the 4th row and 4th column of this <see cref="ColorMatrix"/>.
        /// </summary>
        public float Matrix44 { get; set; }

        /// <summary>
        /// Gets or sets the value of the specified element of this <see cref="ColorMatrix"/>.
        /// </summary>
        /// <param name="row">
        /// The row index.
        /// </param>
        /// <param name="column">
        /// The column index.
        /// </param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        public float this[int row, int column]
        {
            get
            {
                return this.GetMatrix()[row][column];
            }

            set
            {
                float[][] tempMatrix = this.GetMatrix();

                tempMatrix[row][column] = value;

                this.SetMatrix(tempMatrix);
            }
        }

        /// <summary>
        /// Sets the values of this <see cref="ColorMatrix"/> to the values contained within the elements.
        /// </summary>
        /// <param name="colorMatrix">
        /// The new color matrix.
        /// </param>
        internal void SetMatrix(float[][] colorMatrix)
        {
            this.Matrix00 = colorMatrix[0][0];
            this.Matrix01 = colorMatrix[0][1];
            this.Matrix02 = colorMatrix[0][2];
            this.Matrix03 = colorMatrix[0][3];
            this.Matrix04 = colorMatrix[0][4];
            this.Matrix10 = colorMatrix[1][0];
            this.Matrix11 = colorMatrix[1][1];
            this.Matrix12 = colorMatrix[1][2];
            this.Matrix13 = colorMatrix[1][3];
            this.Matrix14 = colorMatrix[1][4];
            this.Matrix20 = colorMatrix[2][0];
            this.Matrix21 = colorMatrix[2][1];
            this.Matrix22 = colorMatrix[2][2];
            this.Matrix23 = colorMatrix[2][3];
            this.Matrix24 = colorMatrix[2][4];
            this.Matrix30 = colorMatrix[3][0];
            this.Matrix31 = colorMatrix[3][1];
            this.Matrix32 = colorMatrix[3][2];
            this.Matrix33 = colorMatrix[3][3];
            this.Matrix34 = colorMatrix[3][4];
            this.Matrix40 = colorMatrix[4][0];
            this.Matrix41 = colorMatrix[4][1];
            this.Matrix42 = colorMatrix[4][2];
            this.Matrix43 = colorMatrix[4][3];
            this.Matrix44 = colorMatrix[4][4];
        }

        /// <summary>
        /// Gets this <see cref="ColorMatrix"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="T:float[][]"/>.
        /// </returns>
        internal float[][] GetMatrix()
        {
            float[][] returnMatrix = new float[5][];

            for (int i = 0; i < 5; i++)
            {
                returnMatrix[i] = new float[5];
            }

            returnMatrix[0][0] = this.Matrix00;
            returnMatrix[0][1] = this.Matrix01;
            returnMatrix[0][2] = this.Matrix02;
            returnMatrix[0][3] = this.Matrix03;
            returnMatrix[0][4] = this.Matrix04;
            returnMatrix[1][0] = this.Matrix10;
            returnMatrix[1][1] = this.Matrix11;
            returnMatrix[1][2] = this.Matrix12;
            returnMatrix[1][3] = this.Matrix13;
            returnMatrix[1][4] = this.Matrix14;
            returnMatrix[2][0] = this.Matrix20;
            returnMatrix[2][1] = this.Matrix21;
            returnMatrix[2][2] = this.Matrix22;
            returnMatrix[2][3] = this.Matrix23;
            returnMatrix[2][4] = this.Matrix24;
            returnMatrix[3][0] = this.Matrix30;
            returnMatrix[3][1] = this.Matrix31;
            returnMatrix[3][2] = this.Matrix32;
            returnMatrix[3][3] = this.Matrix33;
            returnMatrix[3][4] = this.Matrix34;
            returnMatrix[4][0] = this.Matrix40;
            returnMatrix[4][1] = this.Matrix41;
            returnMatrix[4][2] = this.Matrix42;
            returnMatrix[4][3] = this.Matrix43;
            returnMatrix[4][4] = this.Matrix44;

            return returnMatrix;
        }
    }
}
