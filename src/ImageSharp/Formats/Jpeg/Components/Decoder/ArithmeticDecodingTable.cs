// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal class ArithmeticDecodingTable
    {
        public ArithmeticDecodingTable(byte tableClass, byte identifier)
        {
            this.TableClass = tableClass;
            this.Identifier = identifier;
        }

        public byte TableClass { get; }

        public byte Identifier { get; }

        public byte ConditioningTableValue { get; private set; }

        public int DcL { get; private set; }

        public int DcU { get; private set; }

        public int AcKx { get; private set; }

        public void Configure(byte conditioningTableValue)
        {
            this.ConditioningTableValue = conditioningTableValue;
            if (this.TableClass == 0)
            {
                this.DcL = conditioningTableValue & 0x0F;
                this.DcU = conditioningTableValue >> 4;
                this.AcKx = 0;
            }
            else
            {
                this.DcL = 0;
                this.DcU = 0;
                this.AcKx = conditioningTableValue;
            }
        }
    }
}
