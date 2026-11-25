namespace EidCharity.Application.Common;

/// <summary>Writes a QAR amount in words for receipts ("فقط ... ريال قطري لا غير").</summary>
public static class AmountInWords
{
    private static readonly string[] OnesAr =
    [
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة",
        "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر",
    ];
    private static readonly string[] TensAr = ["", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"];
    private static readonly string[] HundredsAr = ["", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"];

    // (singular, dual, plural 3–10)
    private static readonly (string One, string Two, string Many)[] ScalesAr =
    [
        ("", "", ""),
        ("ألف", "ألفان", "آلاف"),
        ("مليون", "مليونان", "ملايين"),
        ("مليار", "ملياران", "مليارات"),
    ];

    public static string Arabic(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var riyals = (long)Math.Floor(amount);
        var dirhams = (int)((amount - riyals) * 100);
        var parts = new List<string>();
        if (riyals > 0) parts.Add($"{IntegerAr(riyals)} ريال قطري");
        if (dirhams > 0) parts.Add($"{IntegerAr(dirhams)} درهم");
        if (parts.Count == 0) parts.Add("صفر ريال قطري");
        return $"فقط {string.Join(" و", parts)} لا غير";
    }

    public static string IntegerAr(long n)
    {
        if (n == 0) return "صفر";
        var groups = new List<string>();
        for (var scale = ScalesAr.Length - 1; scale >= 0; scale--)
        {
            var size = (long)Math.Pow(1000, scale);
            var g = (int)(n / size % 1000);
            if (g == 0) continue;
            groups.Add(scale == 0 ? Under1000Ar(g) : ScaledAr(g, ScalesAr[scale]));
        }
        return string.Join(" و", groups);
    }

    private static string ScaledAr(int g, (string One, string Two, string Many) s) => g switch
    {
        1 => s.One,
        2 => s.Two,
        <= 10 => $"{Under1000Ar(g)} {s.Many}",
        _ when g % 100 is >= 3 and <= 10 => $"{Under1000Ar(g)} {s.Many}",
        _ => $"{Under1000Ar(g)} {s.One}",
    };

    private static string Under1000Ar(int n)
    {
        var h = n / 100; var r = n % 100;
        var parts = new List<string>();
        if (h > 0) parts.Add(HundredsAr[h]);
        if (r > 0) parts.Add(Under100Ar(r));
        return string.Join(" و", parts);
    }

    private static string Under100Ar(int n)
    {
        if (n < 20) return OnesAr[n];
        var u = n % 10; var t = n / 10;
        return u == 0 ? TensAr[t] : $"{OnesAr[u]} و{TensAr[t]}";
    }

    // ---------------- English ----------------
    private static readonly string[] OnesEn =
    [
        "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];
    private static readonly string[] TensEn = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly string[] ScalesEn = ["", "thousand", "million", "billion"];

    public static string English(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var riyals = (long)Math.Floor(amount);
        var dirhams = (int)((amount - riyals) * 100);
        var s = $"Qatari riyals {IntegerEn(riyals)}";
        if (dirhams > 0) s += $" and {IntegerEn(dirhams)} dirhams";
        return $"{s} only";
    }

    public static string IntegerEn(long n)
    {
        if (n == 0) return "zero";
        var groups = new List<string>();
        for (var scale = ScalesEn.Length - 1; scale >= 0; scale--)
        {
            var size = (long)Math.Pow(1000, scale);
            var g = (int)(n / size % 1000);
            if (g == 0) continue;
            var h = g / 100; var r = g % 100;
            var w = new List<string>();
            if (h > 0) w.Add($"{OnesEn[h]} hundred");
            if (r > 0) w.Add(r < 20 ? OnesEn[r] : r % 10 == 0 ? TensEn[r / 10] : $"{TensEn[r / 10]}-{OnesEn[r % 10]}");
            groups.Add(string.Join(" ", w) + (scale > 0 ? $" {ScalesEn[scale]}" : ""));
        }
        return string.Join(" ", groups);
    }
}
