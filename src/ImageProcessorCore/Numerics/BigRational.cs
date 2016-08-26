// <copyright file="Rational.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Text;

    internal struct BigRational : IEquatable<BigRational>
    {
        private bool IsIndeterminate
        {
            get
            {
                if (Denominator != 0)
                    return false;

                return Numerator == 0;
            }
        }

        private bool IsInteger => Denominator == 1;

        private bool IsNegativeInfinity
        {
            get
            {
                if (Denominator != 0)
                    return false;

                return Numerator == -1;
            }
        }

        private bool IsPositiveInfinity
        {
            get
            {
                if (Denominator != 0)
                    return false;

                return Numerator == 1;
            }
        }

        private bool IsZero
        {
            get
            {
                if (Denominator != 1)
                    return false;

                return Numerator == 0;
            }
        }

        private static long GreatestCommonDivisor(long a, long b)
        {
            return b == 0 ? a : GreatestCommonDivisor(b, a % b);
        }

        private void Simplify()
        {
            if (IsIndeterminate)
                return;

            if (IsNegativeInfinity)
                return;

            if (IsPositiveInfinity)
                return;

            if (IsInteger)
                return;

            if (IsZero)
                return;

            if (Numerator == 0)
            {
                Denominator = 0;
                return;
            }

            if (Numerator == Denominator)
            {
                Numerator = 1;
                Denominator = 1;
            }

            long gcd = GreatestCommonDivisor(Math.Abs(Numerator), Math.Abs(Denominator));
            if (gcd > 1)
            {
                Numerator = Numerator / gcd;
                Denominator = Denominator / gcd;
            }
        }

        public BigRational(long numerator, long denominator)
          : this(numerator, denominator, false)
        {
        }

        public BigRational(long numerator, long denominator, bool simplify)
        {
            Numerator = numerator;
            Denominator = denominator;

            if (simplify)
                Simplify();
        }

        public BigRational(double value, bool bestPrecision)
        {
            if (double.IsNaN(value))
            {
                Numerator = Denominator = 0;
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                Numerator = 1;
                Denominator = 0;
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                Numerator = -1;
                Denominator = 0;
                return;
            }

            Numerator = 1;
            Denominator = 1;

            double val = Math.Abs(value);
            double df = Numerator / Denominator;
            double epsilon = bestPrecision ? double.Epsilon : .000001;

            while (Math.Abs(df - val) > epsilon)
            {
                if (df < val)
                    Numerator++;
                else
                {
                    Denominator++;
                    Numerator = (int)(val * Denominator);
                }

                df = Numerator / (double)Denominator;
            }

            if (value < 0.0)
                Numerator *= -1;

            Simplify();
        }

        public long Denominator
        {
            get;
            private set;
        }

        public long Numerator
        {
            get;
            private set;
        }

        public bool Equals(BigRational other)
        {
            if (Denominator == other.Denominator)
                return Numerator == other.Numerator;

            if (Numerator == 0 && Denominator == 0)
                return other.Numerator == 0 && other.Denominator == 0;

            if (other.Numerator == 0 && other.Denominator == 0)
                return Numerator == 0 && Denominator == 0;

            return (Numerator * other.Denominator) == (Denominator * other.Numerator);
        }

        public override int GetHashCode()
        {
            return ((Numerator * 397) ^ Denominator).GetHashCode();
        }

        public string ToString(IFormatProvider provider)
        {
            if (IsIndeterminate)
                return "[ Indeterminate ]";

            if (IsPositiveInfinity)
                return "[ PositiveInfinity ]";

            if (IsNegativeInfinity)
                return "[ NegativeInfinity ]";

            if (IsZero)
                return "0";

            if (IsInteger)
                return Numerator.ToString(provider);

            StringBuilder sb = new StringBuilder();
            sb.Append(Numerator.ToString(provider));
            sb.Append("/");
            sb.Append(Denominator.ToString(provider));

            return sb.ToString();
        }
    }
}