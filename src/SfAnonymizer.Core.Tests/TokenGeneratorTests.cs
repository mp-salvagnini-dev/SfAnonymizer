using SfAnonymizer.Core.Models;
using SfAnonymizer.Core.Services;
using Xunit;

namespace SfAnonymizer.Core.Tests;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _sut = new();

    // ── Pass-through for blank input ──────────────────────────────────────────

    [Fact]
    public void GetToken_EmptyString_ReturnsInputUnchanged()
    {
        var result = _sut.GetToken(string.Empty, SensitiveDataCategory.CustomerCompany);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void GetToken_WhitespaceOnly_ReturnsInputUnchanged(string input)
    {
        var result = _sut.GetToken(input, SensitiveDataCategory.CustomerCompany);
        Assert.Equal(input, result);
    }

    // ── Determinism within a session ─────────────────────────────────────────

    [Fact]
    public void GetToken_SameInput_ReturnsSameTokenWithinSession()
    {
        const string value = "Acme Corporation";
        var first  = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);
        var second = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetToken_DifferentInputs_ReturnDifferentTokens()
    {
        var t1 = _sut.GetToken("Acme Corp", SensitiveDataCategory.CustomerCompany);
        var t2 = _sut.GetToken("Globex Inc", SensitiveDataCategory.CustomerCompany);
        Assert.NotEqual(t1, t2);
    }

    // ── Built-in category prefixes ────────────────────────────────────────────

    [Theory]
    [InlineData(SensitiveDataCategory.CustomerCompany, "CUST")]
    [InlineData(SensitiveDataCategory.SerialNumber,    "SN")]
    [InlineData(SensitiveDataCategory.PersonName,      "PERSON")]
    [InlineData(SensitiveDataCategory.MachineType,     "MACH")]
    [InlineData(SensitiveDataCategory.Custom,          "CUSTOM")]
    public void GetToken_BuiltInCategory_HasExpectedPrefix(SensitiveDataCategory category, string expectedPrefix)
    {
        var token = _sut.GetToken("some value", category);
        Assert.StartsWith(expectedPrefix + "-", token);
    }

    // ── Custom category prefix handling ───────────────────────────────────────

    [Fact]
    public void GetToken_CustomCategoryWithPrefix_UsesCustomPrefix()
    {
        var custom = new CustomCategoryDefinition { Name = "MyCategory", UsePrefix = true, Prefix = "MYCAT" };
        var token = _sut.GetToken("some value", SensitiveDataCategory.Custom, custom);
        Assert.StartsWith("MYCAT-", token);
    }

    [Fact]
    public void GetToken_CustomCategoryWithoutPrefix_ReturnsRawHash()
    {
        var custom = new CustomCategoryDefinition { Name = "MyCategory", UsePrefix = false, Prefix = "MYCAT" };
        var token = _sut.GetToken("some value", SensitiveDataCategory.Custom, custom);
        Assert.DoesNotContain("-", token);
    }

    [Fact]
    public void GetToken_CustomCategoryWithEmptyPrefix_ReturnsRawHash()
    {
        var custom = new CustomCategoryDefinition { Name = "MyCategory", UsePrefix = true, Prefix = "" };
        var token = _sut.GetToken("some value", SensitiveDataCategory.Custom, custom);
        Assert.DoesNotContain("-", token);
    }

    [Fact]
    public void GetToken_CustomCategoryOverridesBuiltInPrefix()
    {
        // Even when the enum is CustomerCompany, a custom definition takes priority
        var custom = new CustomCategoryDefinition { Name = "VIP", UsePrefix = true, Prefix = "VIP" };
        var token = _sut.GetToken("Acme Corp", SensitiveDataCategory.CustomerCompany, custom);
        Assert.StartsWith("VIP-", token);
        Assert.DoesNotContain("CUST", token);
    }

    // ── Hash format ───────────────────────────────────────────────────────────

    [Fact]
    public void GetToken_HashPortion_Is6CharHex()
    {
        var token = _sut.GetToken("test value", SensitiveDataCategory.CustomerCompany);
        // Token format: "CUST-xxxxxx"
        var hash = token.Split('-').Last();
        Assert.Equal(6, hash.Length);
        Assert.Matches("^[0-9A-F]{6}$", hash);
    }

    // ── Reset behavior ────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ChangesTokensForSameInput()
    {
        const string value = "Acme Corporation";
        var before = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);
        _sut.Reset();
        var after = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);

        // With overwhelming probability the new key yields a different hash.
        // (A collision would require HMAC-SHA256 to produce the same 6-char output — ~1 in 16M)
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Reset_MaintainsDeterminismAfterReset()
    {
        _sut.Reset();
        const string value = "Acme Corporation";
        var first  = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);
        var second = _sut.GetToken(value, SensitiveDataCategory.CustomerCompany);
        Assert.Equal(first, second);
    }
}
