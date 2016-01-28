namespace ImageProcessorCore.Formats
{
    using System;

    internal class JpegQuantizationTable
    {
        /// <summary>
        /// Construct a new JPEG quantization table.  A copy is created of
        /// the table argument.
        /// </summary>
        /// <param name="table">The 64-element value table, stored in natural order</param>
        public JpegQuantizationTable(int[] table)
            : this(CheckTable(table), true)
        {
        }

        /// <summary>
        /// Private constructor that avoids unnecessary copying and argument checking.
        /// </summary>
        /// <param name="table">the 64-element value table, stored in natural order</param>
        /// <param name="copy">true if a copy should be created of the given table</param>
        private JpegQuantizationTable(int[] table, bool copy)
        {
            this.Table = copy ? (int[])table.Clone() : table;
        }

        /// <summary>
        /// The standard JPEG luminance quantization table.  
        /// Values are stored in natural order.
        /// </summary>
        public static JpegQuantizationTable K1Luminance = new JpegQuantizationTable(new[]
        {
            16, 11, 10, 16,  24,  40,  51,  61,
            12, 12, 14, 19,  26,  58,  60,  55,
            14, 13, 16, 24,  40,  57,  69,  56,
            14, 17, 22, 29,  51,  87,  80,  62,
            18, 22, 37, 56,  68, 109, 103,  77,
            24, 35, 55, 64,  81, 104, 113,  92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103,  99
        }, false);

        /// <summary>
        /// Gets the standard JPEG luminance quantization table, scaled by
        /// one-half.  Values are stored in natural order.
        /// </summary>
        public static JpegQuantizationTable K1Div2Luminance { get; } = K1Luminance.GetScaledInstance(0.5f, true);


        /// <summary>
        /// The standard JPEG chrominance quantization table.  
        /// Values are stored in natural order.
        /// </summary>
        public static JpegQuantizationTable K2Chrominance = new JpegQuantizationTable(new[]
        {
            17, 18, 24, 47, 99, 99, 99, 99,
            18, 21, 26, 66, 99, 99, 99, 99,
            24, 26, 56, 99, 99, 99, 99, 99,
            47, 66, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99
        }, false);

        /// <summary>
        /// The standard JPEG chrominance quantization table, scaled by one-half.  
        /// Values are stored in natural order.
        /// </summary>
        public static JpegQuantizationTable K2Div2Chrominance { get; } = K2Chrominance.GetScaledInstance(0.5f, true);

        /// <summary>
        /// Gets the table entries, stored in natural order.
        /// </summary>
        public int[] Table { get; }

        /// <summary>
        /// Checks the table to ensure it is the correct dimensions.
        /// </summary>
        /// <param name="table">The table to check</param>
        /// <returns>The <see cref="T:int[]"/></returns>
        private static int[] CheckTable(int[] table)
        {
            if (table == null || table.Length != 64)
            {
                throw new ArgumentException("Invalid JPEG quantization table");
            }

            return table;
        }

        /// <summary>
        /// Retrieve a copy of this JPEG quantization table with every value
        /// scaled by the given scale factor, and clamped from 1 to 255
        /// </summary>
        /// <param name="scaleFactor">The factor by which to scale this table</param>
        /// <param name="forceBaseline">
        /// Whether to clamp scaled values to a maximum of 255 or baseline from 1 to 32767 otherwise.
        /// </param>
        /// <returns>new scaled JPEG quantization table</returns>
        public JpegQuantizationTable GetScaledInstance(float scaleFactor, bool forceBaseline)
        {
            int[] scaledTable = (int[])this.Table.Clone();
            int max = forceBaseline ? 255 : 32767;

            for (int i = 0; i < scaledTable.Length; i++)
            {
                scaledTable[i] = (int)Math.Round(scaleFactor * scaledTable[i]);
                if (scaledTable[i] < 1)
                {
                    scaledTable[i] = 1;

                }
                else if (scaledTable[i] > max)
                {
                    scaledTable[i] = max;
                }
            }

            return new JpegQuantizationTable(scaledTable, false);
        }
    }
}
