// ------------------------------------------------------------------------------------------------
// <copyright file="SetupTokenIssuerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Infrastructure.Security;
using CodeCoverage.Infrastructure.Settings;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Settings;

public sealed class SetupTokenIssuerTests
{
  #region Fields

  private readonly WarningRecorder<SetupTokenIssuer> _logger = new();

  private readonly SetupTokenIssuer _issuer;

  #endregion

  #region Ctors

  public SetupTokenIssuerTests()
  {
    _issuer = new SetupTokenIssuer(new Sha256TokenHasher(), _logger);
  }

  #endregion

  #region Methods

  [Fact]
  public void EnsureIssued_ShouldWriteAWarningWhoseTokenIsValid()
  {
    _issuer.EnsureIssued();

    _issuer.IsValid(TokenOf(_logger.Warnings.ShouldHaveSingleItem())).ShouldBeTrue();
  }

  [Fact]
  public void EnsureIssued_Twice_ShouldKeepTheFirstToken()
  {
    _issuer.EnsureIssued();
    _issuer.EnsureIssued();

    _issuer.IsValid(TokenOf(_logger.Warnings.ShouldHaveSingleItem())).ShouldBeTrue();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("wrong")]
  public void IsValid_OtherValue_ShouldRefuse(string? token)
  {
    _issuer.EnsureIssued();

    _issuer.IsValid(token).ShouldBeFalse();
  }

  [Fact]
  public void IsValid_BeforeAnyToken_ShouldRefuse()
  {
    _issuer.IsValid("anything").ShouldBeFalse();
  }

  [Fact]
  public void Revoke_ShouldInvalidateTheToken()
  {
    _issuer.EnsureIssued();
    var token = TokenOf(_logger.Warnings.ShouldHaveSingleItem());

    _issuer.Revoke();

    _issuer.IsValid(token).ShouldBeFalse();
  }

  // Only the hash stays in memory, so a dump of the process does not hold the token.
  [Fact]
  public void EnsureIssued_ShouldNotKeepTheTokenInClear()
  {
    _issuer.EnsureIssued();
    var token = TokenOf(_logger.Warnings.ShouldHaveSingleItem());

    var fields = typeof(SetupTokenIssuer).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.GetValue(_issuer));

    fields.OfType<string>().ShouldNotContain(token);
  }

  private static string TokenOf(string message) => message[(message.LastIndexOf(' ') + 1)..];

  #endregion
}
