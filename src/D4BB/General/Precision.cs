public static class Precision {
    private static double[] PowersOfTwoPlusOne;

    static Precision()
    {
        PowersOfTwoPlusOne = new double[54];
        for (var i = 0; i < PowersOfTwoPlusOne.Length; i++)
        {
            if (i == 0)
                PowersOfTwoPlusOne[i] = 1; // Special case.
            else
            {
                long two_to_i_plus_one = (1L << i) + 1L;
                PowersOfTwoPlusOne[i] = (double)two_to_i_plus_one;
            }
        }
    }

    public static double RoundBinary(this double d, int binaryDigits)
    {
        double t = d * PowersOfTwoPlusOne[53 - binaryDigits];
        double rounded = t - (t - d);
        return rounded;
    }
}