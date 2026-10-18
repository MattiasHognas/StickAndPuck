using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>Sine, cosine, arctangent, exp and log that give the same bits on every machine. MathF and
    /// Math hand these to the C library, and glibc, musl and Windows round them differently, so a
    /// match drifts apart between two computers. Transcendental functions use plain double adds,
    /// multiplies, divides and square roots; interpolation explicitly requests fused rounding. The game's systems use
    /// these in place of MathF; drawing code may keep MathF.</summary>
    public static class SimMath
    {
        private const double HalfPi = 1.5707963267948966;
        private const double PiHi = 3.141592653589793;
        private const double Ln2 = 0.6931471805599453;

        // pi/2 split in three, so x - k * pi/2 stays exact for any angle the game turns through
        private const double HalfPi1 = 1.5707963267341256;
        private const double HalfPi2 = 6.077100506303966e-11;
        private const double HalfPi3 = 2.0222662487111665e-21;

        /// <summary>Interpolates a simulation vector with the same fused rounding on every platform.</summary>
        public static Vector2 Lerp(Vector2 from, Vector2 to, float amount)
        {
            return new(
                MathF.FusedMultiplyAdd(from.X, 1f - amount, to.X * amount),
                MathF.FusedMultiplyAdd(from.Y, 1f - amount, to.Y * amount)
            );
        }

        /// <summary>Gets the sine.</summary>
        public static float Sin(float x)
        {
            return (float)Sin((double)x);
        }

        /// <summary>Gets the sine.</summary>
        public static double Sin(double x)
        {
            if (!double.IsFinite(x))
            {
                return double.NaN;
            }

            (double r, long quarter) = Reduce(x);
            return (quarter & 3) switch
            {
                0 => SinPoly(r),
                1 => CosPoly(r),
                2 => -SinPoly(r),
                _ => -CosPoly(r),
            };
        }

        /// <summary>Gets the cosine.</summary>
        public static float Cos(float x)
        {
            return (float)Cos((double)x);
        }

        /// <summary>Gets the cosine.</summary>
        public static double Cos(double x)
        {
            if (!double.IsFinite(x))
            {
                return double.NaN;
            }

            (double r, long quarter) = Reduce(x);
            return (quarter & 3) switch
            {
                0 => CosPoly(r),
                1 => -SinPoly(r),
                2 => -CosPoly(r),
                _ => SinPoly(r),
            };
        }

        /// <summary>Gets the tangent.</summary>
        public static float Tan(float x)
        {
            return (float)(Sin((double)x) / Cos((double)x));
        }

        /// <summary>Gets the arctangent, -pi/2 to pi/2.</summary>
        public static double Atan(double x)
        {
            if (double.IsNaN(x))
            {
                return double.NaN;
            }

            if (x < 0)
            {
                return -Atan(-x);
            }

            if (double.IsPositiveInfinity(x))
            {
                return HalfPi;
            }

            if (x > 1)
            {
                return HalfPi - Atan(1 / x);
            }

            // two half-angle steps take x below tan(pi/16), where the series is short
            x /= 1 + Math.Sqrt(1 + (x * x));
            x /= 1 + Math.Sqrt(1 + (x * x));
            double x2 = x * x,
                term = x,
                sum = x;
            for (int n = 1; n <= 14; n++)
            {
                term *= -x2;
                sum += term / ((2 * n) + 1);
            }
            return 4 * sum;
        }

        /// <summary>Gets the angle of the point (x, y) from the x axis, -pi to pi, as Math.Atan2.</summary>
        public static float Atan2(float y, float x)
        {
            return (float)Atan2((double)y, (double)x);
        }

        /// <summary>Gets the angle of the point (x, y) from the x axis, -pi to pi, as Math.Atan2.</summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return double.NaN;
            }

            if (x == 0 && y == 0)
            {
                return double.IsNegative(x) ? (double.IsNegative(y) ? -PiHi : PiHi) : y;
            }
            if (x == 0)
            {
                return y > 0 ? HalfPi : -HalfPi;
            }

            double a = Atan(Math.Abs(y / x));
            if (x < 0)
            {
                a = PiHi - a;
            }

            return double.IsNegative(y) ? -a : a;
        }

        /// <summary>Gets e raised to a power.</summary>
        public static float Exp(float x)
        {
            return (float)Exp((double)x);
        }

        /// <summary>Gets e raised to a power.</summary>
        public static double Exp(double x)
        {
            if (double.IsNaN(x))
            {
                return double.NaN;
            }

            if (x > 709)
            {
                return double.PositiveInfinity;
            }

            if (x < -745)
            {
                return 0;
            }

            double k = Math.Round(x / Ln2, MidpointRounding.ToEven);
            double r = x - (k * Ln2);
            double term = 1,
                sum = 1;
            for (int n = 1; n <= 20; n++)
            {
                term *= r / n;
                sum += term;
            }
            return Math.ScaleB(sum, (int)k);
        }

        /// <summary>Gets the natural logarithm.</summary>
        public static float Log(float x)
        {
            return (float)Log((double)x);
        }

        /// <summary>Gets the natural logarithm.</summary>
        public static double Log(double x)
        {
            if (double.IsNaN(x) || x < 0)
            {
                return double.NaN;
            }

            if (x == 0)
            {
                return double.NegativeInfinity;
            }

            if (double.IsPositiveInfinity(x))
            {
                return x;
            }

            int e = Math.ILogB(x);
            double m = Math.ScaleB(x, -e); // 1 to 2
            if (m > 1.4142135623730951)
            {
                m /= 2;
                e++;
            }
            double s = (m - 1) / (m + 1),
                s2 = s * s,
                term = s,
                sum = s;
            for (int n = 1; n <= 16; n++)
            {
                term *= s2;
                sum += term / ((2 * n) + 1);
            }
            return (2 * sum) + (e * Ln2);
        }

        /// <summary>Gets x raised to the power y for x of zero or more.</summary>
        public static float Pow(float x, float y)
        {
            return (float)Pow((double)x, (double)y);
        }

        /// <summary>Gets x raised to the power y for x of zero or more.</summary>
        public static double Pow(double x, double y)
        {
            return (y == 0)
                ? 1
                : (
                    (x == 0)
                        ? (y > 0 ? 0 : double.PositiveInfinity)
                        : ((x < 0) ? double.NaN : Exp(y * Log(x)))
                );
        }

        /// <summary>Takes x down to -pi/4..pi/4 and the number of quarter turns taken off.</summary>
        private static (double R, long Quarter) Reduce(double x)
        {
            double k = Math.Round(x / HalfPi, MidpointRounding.ToEven);
            double r = x - (k * HalfPi1) - (k * HalfPi2) - (k * HalfPi3);
            return (r, (long)k);
        }

        private static double SinPoly(double r)
        {
            double r2 = r * r,
                term = r,
                sum = r;
            for (int n = 1; n <= 9; n++)
            {
                term *= -r2 / (2 * n * ((2 * n) + 1));
                sum += term;
            }
            return sum;
        }

        private static double CosPoly(double r)
        {
            double r2 = r * r,
                term = 1,
                sum = 1;
            for (int n = 1; n <= 9; n++)
            {
                term *= -r2 / (((2 * n) - 1) * (2 * n));
                sum += term;
            }
            return sum;
        }
    }
}
