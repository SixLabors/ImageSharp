// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

internal class ArithmeticDecodingTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArithmeticDecodingTable"/> class with the JPEG conditioning defaults.
    /// </summary>
    /// <param name="tableClass">The table class: zero for DC or one for AC.</param>
    /// <param name="identifier">The table identifier from the scan or DAC segment.</param>
    public ArithmeticDecodingTable(byte tableClass, byte identifier)
    {
        this.TableClass = tableClass;
        this.Identifier = identifier;

        // ITU T.81 F.1.4.4.1.4 specifies DC bounds L = 0 and U = 1 when DAC is absent.
        // U occupies the upper nibble, so the encoded DC value is 0x10. F.1.4.4.2.1 specifies AC Kx = 5.
        this.Configure(tableClass == 0 ? (byte)0x10 : (byte)5);
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
