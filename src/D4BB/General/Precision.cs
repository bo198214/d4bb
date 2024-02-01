
using System;

public static class Precision {
    //https://stackoverflow.com/questions/14285492/efficient-way-to-round-double-precision-numbers-to-a-lower-precision-given-in-nu
    /// <summary>
    /// Round numbers to a specified number of significant binary digits.
    /// 
    /// For example, to 3 places, numbers from zero to seven are unchanged, because they only require 3 binary digits,
    /// but larger numbers lose precision:
    /// 
    ///      8    1000 => 1000   8
    ///      9    1001 => 1010  10
    ///     10    1010 => 1010  10
    ///     11    1011 => 1100  12
    ///     12    1100 => 1100  12
    ///     13    1101 => 1110  14
    ///     14    1110 => 1110  14
    ///     15    1111 =>10000  16
    ///     16   10000 =>10000  16
    ///     
    /// This is different from rounding in that we are specifying the place where rounding occurs as the distance to the right
    /// in binary digits from the highest bit set, not the distance to the left from the zero bit.
    /// </summary>
    /// <param name="d">Number to be rounded.</param>
    /// <param name="digits">Number of binary digits of precision to preserve. </param>
    // public static double AdjustPrecision(this double d, int digits)
    // {
    //     // TODO: Not sure if this will work for both normalized and denormalized doubles. Needs more research.
    //     var shift = 53 - digits; // IEEE 754 doubles have 53 bits of significand, but one bit is "implied" and not stored.
    //     ulong significandMask = (0xffffffffffffffffUL >> shift) << shift;
    //     var local_d = d;
    //     unsafe
    //     {
    //         // double -> fixed point (sorta)
    //         ulong toLong = *(ulong*)(&local_d);
    //         // mask off your least-sig bits
    //         var modLong = toLong & significandMask;
    //         // fixed point -> float (sorta)
    //         local_d = *(double*)(&modLong);
    //     }
    //     return local_d;
    // }
    private static double[] PowersOfTwoPlusOne;
    private static double[] PowersOfTwo;

    static Precision()
    {
        PowersOfTwoPlusOne = new double[54];
        PowersOfTwo = new double[54];
        PowersOfTwo[0] = 1;
        PowersOfTwoPlusOne[0] = 1; // Special case.
        for (var i = 1; i < PowersOfTwoPlusOne.Length; i++)
        {
            long two_to_i = 1L << i;
            PowersOfTwo[i] = (double)two_to_i;
            PowersOfTwoPlusOne[i] = (double)(two_to_i + 1L);
        }
    }
    public static double RoundBinary(this double d, int binaryDigits)
    {
        double t = d * PowersOfTwoPlusOne[53 - binaryDigits];
        double rounded = t - (t - d);
        return rounded;
    }
    public static double TruncateBinaryDigits(this double d, int binaryDigits)
    {
        long bits = BitConverter.DoubleToInt64Bits(d);
        // Note that the shift is sign-extended, hence the test against -1 not 1
        var negative = (bits & (1L << 63)) != 0;
        var exponent = (int) ((bits >> 52) & 0x7ffL);
        var shift = 53 - binaryDigits; // IEEE 754 doubles have 53 bits of significand, but one bit is "implied" and not stored.
        var mantissa = bits &   0xfffffffffffffL;
        ulong significandMask = (0xffffffffffffffffL >> shift) << shift;
        return BitConverter.Int64BitsToDouble((long)((ulong)bits & significandMask));
    }
    public static double TruncateBinary(this double d, int binaryDigits) {
        if (binaryDigits>=0) {
            int i = (int)(d * (1L<<binaryDigits));
            return ((double)i)/(1L<<binaryDigits);
        } else {
            int i = (int)(d / (1L<<-binaryDigits));
            return ((double)i)*(1L<<-binaryDigits);
        }
    }
    public static void MantissaExponent(double d, out long mantissa, out int exponent, out bool negative)
    {
        // Translate the double into sign, exponent and mantissa.
        long bits = BitConverter.DoubleToInt64Bits(d);
        // Note that the shift is sign-extended, hence the test against -1 not 1
        negative = (bits & (1L << 63)) != 0;
        exponent = (int) ((bits >> 52) & 0x7ffL);
        mantissa = bits & 0xfffffffffffffL;

        // Subnormal numbers; exponent is effectively one higher,
        // but there's no extra normalisation bit in the mantissa
        if (exponent==0)
        {
            exponent++;
        }
        // Normal numbers; leave exponent as it is but add extra
        // bit to the front of the mantissa
        else
        {
            mantissa = mantissa | (1L << 52);
        }

        // Bias the exponent. It's actually biased by 1023, but we're
        // treating the mantissa as m.0 rather than 0.m, so we need
        // to subtract another 52 from it.
        exponent -= 1075;

        if (mantissa == 0) 
        {
            return;
        }

        /* Normalize */
        while((mantissa & 1) == 0) 
        {    /*  i.e., Mantissa is even */
            mantissa >>= 1;
            exponent++;
        }
    }
}