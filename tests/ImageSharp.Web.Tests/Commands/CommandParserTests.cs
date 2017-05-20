namespace ImageSharp.Web.Tests.Commands
{
    using System;
    using System.Globalization;

    using ImageSharp.Processing;
    using ImageSharp.Web.Commands;

    using Xunit;

    public class CommandParserTests
    {
        [Fact]
        public void CommandParsesIntegralValues()
        {
            string param = "1";

            sbyte sb = CommandParser.Instance.ParseValue<sbyte>(param);
            Assert.NotNull(sb);
            Assert.Equal((sbyte)1, sb);

            byte b = CommandParser.Instance.ParseValue<byte>(param);
            Assert.NotNull(b);
            Assert.Equal((byte)1, b);

            short s = CommandParser.Instance.ParseValue<short>(param);
            Assert.NotNull(s);
            Assert.Equal((short)1, s);

            ushort us = CommandParser.Instance.ParseValue<ushort>(param);
            Assert.NotNull(us);
            Assert.Equal((ushort)1, us);

            int i = CommandParser.Instance.ParseValue<int>(param);
            Assert.NotNull(i);
            Assert.Equal(1, i);

            uint ui = CommandParser.Instance.ParseValue<uint>(param);
            Assert.NotNull(ui);
            Assert.Equal(1u, ui);

            long l = CommandParser.Instance.ParseValue<long>(param);
            Assert.NotNull(l);
            Assert.Equal(1L, l);

            ulong ul = CommandParser.Instance.ParseValue<ulong>(param);
            Assert.NotNull(i);
            Assert.Equal(1uL, ul);

            float f = CommandParser.Instance.ParseValue<float>(param);
            Assert.NotNull(f);
            Assert.Equal(1F, f);

            double d = CommandParser.Instance.ParseValue<double>(param);
            Assert.NotNull(d);
            Assert.Equal(1D, d);

            decimal m = CommandParser.Instance.ParseValue<decimal>(param);
            Assert.NotNull(m);
            Assert.Equal(1M, m);
        }

        [Fact]
        public void CommandParsesRealValues()
        {
            // Math.PI.ToString() chops two digits off the end
            const double Pi = 3.14159265358979;
            string param = Pi.ToString(CultureInfo.InvariantCulture);
            double rounded = Math.Round(Pi, MidpointRounding.AwayFromZero);

            sbyte sb = CommandParser.Instance.ParseValue<sbyte>(param);
            Assert.NotNull(sb);
            Assert.Equal((sbyte)rounded, sb);

            byte b = CommandParser.Instance.ParseValue<byte>(param);
            Assert.NotNull(b);
            Assert.Equal((byte)rounded, b);

            short s = CommandParser.Instance.ParseValue<short>(param);
            Assert.NotNull(s);
            Assert.Equal((short)rounded, s);

            ushort us = CommandParser.Instance.ParseValue<ushort>(param);
            Assert.NotNull(us);
            Assert.Equal((ushort)rounded, us);

            int i = CommandParser.Instance.ParseValue<int>(param);
            Assert.NotNull(i);
            Assert.Equal((int)rounded, i);

            uint ui = CommandParser.Instance.ParseValue<uint>(param);
            Assert.NotNull(i);
            Assert.Equal((uint)rounded, ui);

            long l = CommandParser.Instance.ParseValue<long>(param);
            Assert.NotNull(l);
            Assert.Equal((long)rounded, l);

            ulong ul = CommandParser.Instance.ParseValue<ulong>(param);
            Assert.NotNull(i);
            Assert.Equal((ulong)rounded, ul);

            float f = CommandParser.Instance.ParseValue<float>(param);
            Assert.NotNull(f);
            Assert.Equal((float)Pi, f);

            double d = CommandParser.Instance.ParseValue<double>(param);
            Assert.NotNull(d);
            Assert.Equal(Pi, d);

            decimal m = CommandParser.Instance.ParseValue<decimal>(param);
            Assert.NotNull(m);
            Assert.Equal((decimal)Pi, m);
        }

        [Fact]
        public void CommandParsesEnums()
        {
            string param = "max";

            ResizeMode mode = CommandParser.Instance.ParseValue<ResizeMode>(param);

            Assert.NotNull(mode);
            Assert.Equal(ResizeMode.Max, mode);

            // Check default is returned.
            param = "hjbjhbjh";
            mode = CommandParser.Instance.ParseValue<ResizeMode>(param);

            Assert.NotNull(mode);
            Assert.Equal(ResizeMode.Crop, mode);
        }
    }
}