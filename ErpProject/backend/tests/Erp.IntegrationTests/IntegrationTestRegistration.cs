namespace Erp.IntegrationTests;

internal static class IntegrationTestRegistration
{
    private static int _seq;

    internal static string NextTaxId()
    {
        var seed = Interlocked.Increment(ref _seq);
        return GenerateValidCifB(seed);
    }

    /// <summary>Genera CIF tipo B con dígito de control válido (SpanishTaxIdValidator).</summary>
    private static string GenerateValidCifB(int seed)
    {
        var digits = (1_000_000 + (seed % 9_000_000)).ToString("D7");
        var sumEven = 0;
        var sumOdd = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var n = digits[i] - '0';
            if (i % 2 == 0)
            {
                var doubled = n * 2;
                sumOdd += doubled / 10 + doubled % 10;
            }
            else
            {
                sumEven += n;
            }
        }

        var control = (10 - (sumEven + sumOdd) % 10) % 10;
        return $"B{digits}{(char)('0' + control)}";
    }
}
