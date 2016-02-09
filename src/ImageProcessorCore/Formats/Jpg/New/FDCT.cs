namespace ImageProcessorCore.Formats
{
    using System;

    internal class FDCT
    {
        /// <summary>
        /// The length of the matrices
        /// </summary>
        public const int Length = 8;

        public int[][] Quantum { get; } = new int[2][];

        public double[][] Divisors { get; } = new double[2][];

        // Quantitization Matrix for luminace.
        public double[] DivisorsLuminance = new double[Length * Length];

        // Quantitization Matrix for chrominance.
        public double[] DivisorsChrominance = new double[Length * Length];

        public FDCT(int quality)
        {
            this.Initialize(quality);
        }

        private void Initialize(int quality)
        {
            double[] aanScaleFactor =
            {
                1.0, 1.387039845, 1.306562965, 1.175875602,
                1.0, 0.785694958, 0.541196100, 0.275899379
            };

            int i;
            int j;
            int index;

            // Scale the quality
            if (quality < 50)
            {
                quality = 5000 / quality;
            }
            else
            {
                quality = 200 - (quality * 2);
            }

            int[] scaledLum = JpegQuantizationTable
                                .K1Luminance
                                .GetScaledInstance(quality / 100f, true).Table;

            index = 0;
            for (i = 0; i < 8; i++)
            {
                for (j = 0; j < 8; j++)
                {
                    this.DivisorsLuminance[index] =
                        1.0 /
                        (scaledLum[index] * aanScaleFactor[i] * aanScaleFactor[j] * 8.0);

                    index++;
                }
            }

            // Create the chrominance matrix
            int[] scaledChrom = JpegQuantizationTable
                                  .K2Chrominance
                                  .GetScaledInstance(quality / 100f, true).Table;

            index = 0;
            for (i = 0; i < 8; i++)
            {
                for (j = 0; j < 8; j++)
                {
                    this.DivisorsChrominance[index] = 1.0 / (scaledChrom[index] * aanScaleFactor[i] * aanScaleFactor[j] * 8.0);
                    index++;
                }
            }

            this.Quantum[0] = scaledLum;
            this.Divisors[0] = this.DivisorsLuminance;
            this.Quantum[1] = scaledChrom;
            this.Divisors[1] = this.DivisorsChrominance;
        }

        internal float[] FastFDCT(float[] input)
        {
            float[] output = new float[64];

            float tmp0, tmp1, tmp2, tmp3, tmp4, tmp5, tmp6, tmp7;
            float tmp10, tmp11, tmp12, tmp13;
            float z1, z2, z3, z4, z5, z11, z13;
            int i;

            // Centre the data range around zero.
            for (i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    output[i * 8 + j] = input[i * 8 + j] - 128f;
                }
            }

            // Pass 1: process rows.
            for (i = 0; i < 8; i++)
            {
                tmp0 = output[i * 8 + 0] + output[i * 8 + 7];
                tmp7 = output[i * 8 + 0] - output[i * 8 + 7];
                tmp1 = output[i * 8 + 1] + output[i * 8 + 6];
                tmp6 = output[i * 8 + 1] - output[i * 8 + 6];
                tmp2 = output[i * 8 + 2] + output[i * 8 + 5];
                tmp5 = output[i * 8 + 2] - output[i * 8 + 5];
                tmp3 = output[i * 8 + 3] + output[i * 8 + 4];
                tmp4 = output[i * 8 + 3] - output[i * 8 + 4];

                // Even part
                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                output[i * 8 + 0] = tmp10 + tmp11;
                output[i * 8 + 4] = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781F;
                output[i * 8 + 2] = tmp13 + z1;
                output[i * 8 + 6] = tmp13 - z1;

                // Odd part
                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                // The rotator is modified from fig 4-8 to avoid extra negations.
                z5 = (tmp10 - tmp12) * 0.382683433F;
                z2 = 0.541196100F * tmp10 + z5;
                z4 = 1.306562965F * tmp12 + z5;
                z3 = tmp11 * 0.707106781F;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                output[i * 8 + 5] = z13 + z2;
                output[i * 8 + 3] = z13 - z2;
                output[i * 8 + 1] = z11 + z4;
                output[i * 8 + 7] = z11 - z4;
            }

            // Pass 2: process columns
            for (i = 0; i < 8; i++)
            {
                tmp0 = output[0 * 8 + i] + output[7 * 8 + i];
                tmp7 = output[0 * 8 + i] - output[7 * 8 + i];
                tmp1 = output[1 * 8 + i] + output[6 * 8 + i];
                tmp6 = output[1 * 8 + i] - output[6 * 8 + i];
                tmp2 = output[2 * 8 + i] + output[5 * 8 + i];
                tmp5 = output[2 * 8 + i] - output[5 * 8 + i];
                tmp3 = output[3 * 8 + i] + output[4 * 8 + i];
                tmp4 = output[3 * 8 + i] - output[4 * 8 + i];

                // Even part
                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                output[0 * 8 + i] = tmp10 + tmp11;
                output[4 * 8 + i] = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781F;
                output[2 * 8 + i] = tmp13 + z1;
                output[6 * 8 + i] = tmp13 - z1;

                // Odd part
                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                // The rotator is modified from fig 4-8 to avoid extra negations.
                z5 = (tmp10 - tmp12) * 0.382683433F;
                z2 = 0.541196100F * tmp10 + z5;
                z4 = 1.306562965F * tmp12 + z5;
                z3 = tmp11 * 0.707106781F;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                output[5 * 8 + i] = z13 + z2;
                output[3 * 8 + i] = z13 - z2;
                output[1 * 8 + i] = z11 + z4;
                output[7 * 8 + i] = z11 - z4;
            }

            return output;
        }

        internal int[] QuantizeBlock(float[] inputData, int code)
        {
            int[] result = new int[Length * Length];
            int index = 0;

            for (int i = 0; i < Length; i++)
                for (int j = 0; j < Length; j++)
                {
                    result[index] = (int)Math.Round(inputData[i * 8 + j] * this.Divisors[code][index]);
                    index++;
                }

            return result;
        }
    }
}
