using EidCharity.Application.Collections;
using EidCharity.Application.Common;
using EidCharity.Domain.Collections;

namespace EidCharity.Tests;

/// <summary>Receipt voucher helpers: amount in words, execution country and the Qatar bank list.</summary>
public class ReceiptTests
{
    [Theory]
    [InlineData(2650, "فقط ألفان وستمائة وخمسون ريال قطري لا غير")]
    [InlineData(1, "فقط واحد ريال قطري لا غير")]
    [InlineData(1000, "فقط ألف ريال قطري لا غير")]
    [InlineData(3500, "فقط ثلاثة آلاف وخمسمائة ريال قطري لا غير")]
    [InlineData(21000, "فقط واحد وعشرون ألف ريال قطري لا غير")]
    [InlineData(103000, "فقط مائة وثلاثة آلاف ريال قطري لا غير")]
    [InlineData(1250000, "فقط مليون ومائتان وخمسون ألف ريال قطري لا غير")]
    [InlineData(150.5, "فقط مائة وخمسون ريال قطري وخمسون درهم لا غير")]
    public void Amount_in_arabic_words(decimal amount, string expected) => Assert.Equal(expected, AmountInWords.Arabic(amount));

    [Theory]
    [InlineData(2650, "Qatari riyals two thousand six hundred fifty only")]
    [InlineData(10.25, "Qatari riyals ten and twenty-five dirhams only")]
    public void Amount_in_english_words(decimal amount, string expected) => Assert.Equal(expected, AmountInWords.English(amount));

    [Theory]
    [InlineData("داخل دولة قطر", "الدوحة", "قطر")]
    [InlineData("", "الوكرة", "قطر")]
    [InlineData("خارج قطر", "اليمن", "اليمن")]
    [InlineData("خارج قطر", "", "خارج قطر")]
    [InlineData("دولي", "اليمن", "اليمن")]
    [InlineData("", "", "—")]
    public void Execution_country(string scope, string location, string expected) =>
        Assert.Equal(expected, DonationService.ExecutionCountry(scope, location).Ar);

    [Fact]
    public void Qatar_banks_are_unique_and_include_the_main_banks()
    {
        Assert.Equal(QatarBanks.All.Count, QatarBanks.All.Select(b => b.NameAr).Distinct().Count());
        foreach (var name in new[] { "QNB", "Commercial Bank", "Doha Bank", "QIB", "Dukhan", "Rayan", "QIIB", "Ahlibank", "Lesha", "HSBC", "Standard Chartered" })
            Assert.Contains(QatarBanks.All, b => b.NameEn.Contains(name));
    }
}
