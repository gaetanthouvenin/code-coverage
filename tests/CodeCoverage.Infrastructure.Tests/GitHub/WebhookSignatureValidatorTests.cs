// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookSignatureValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Diagnostics;
using CodeCoverage.Infrastructure.GitHub;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class WebhookSignatureValidatorTests : IDisposable
{
  #region Fields

  private const string Current = "current-secret";

  private const string Previous = "previous-secret";

  private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"zen":"Keep it logically awesome."}""");

  private readonly TestMeterFactory _meters = new();

  private readonly MeasurementRecorder _recorder;

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IOptionsMonitor<GitHubOptions> _options = A.Fake<IOptionsMonitor<GitHubOptions>>();

  private readonly GitHubOptions _settings = new() { WebhookSecret = "protected:" + Current, WebhookSecretPrevious = "protected:" + Previous };

  #endregion

  #region Ctors

  public WebhookSignatureValidatorTests()
  {
    _recorder = new MeasurementRecorder(_meters);
    A.CallTo(() => _options.CurrentValue).ReturnsLazily(() => _settings);
    A.CallTo(() => _protector.Unprotect(A<string>._)).ReturnsLazily((string value) => value["protected:".Length..]);
  }

  #endregion

  #region Methods

  public void Dispose()
  {
    _recorder.Dispose();
    _meters.Dispose();
  }

  [Fact]
  public void IsValid_CurrentSecret_ShouldAccept()
  {
    Validator().IsValid(Body, Sign(Current, Body)).ShouldBeTrue();
    Failures.ShouldBe(0);
  }

  [Fact]
  public void IsValid_PreviousSecret_ShouldAccept()
  {
    Validator().IsValid(Body, Sign(Previous, Body)).ShouldBeTrue();
    Failures.ShouldBe(0);
  }

  [Fact]
  public void IsValid_UpperCaseHexadecimal_ShouldAccept()
  {
    var signature = Sign(Current, Body);

    Validator().IsValid(Body, $"sha256={signature["sha256=".Length..].ToUpperInvariant()}").ShouldBeTrue();
  }

  [Fact]
  public void IsValid_OtherSecret_ShouldRefuseAndCount()
  {
    Validator().IsValid(Body, Sign("attacker", Body)).ShouldBeFalse();
    Failures.ShouldBe(1);
  }

  [Fact]
  public void IsValid_ChangedBody_ShouldRefuse()
  {
    Validator().IsValid([.. Body, 0x20], Sign(Current, Body)).ShouldBeFalse();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("sha256=")]
  [InlineData("sha256=abc")]
  [InlineData("sha1=0123456789012345678901234567890123456789")]
  [InlineData("sha256=zzzz456789012345678901234567890123456789012345678901234567890123")]
  public void IsValid_MalformedHeader_ShouldRefuseAndCount(string? signature)
  {
    Validator().IsValid(Body, signature).ShouldBeFalse();
    Failures.ShouldBe(1);
  }

  // An empty key would give a signature that anyone can compute.
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void IsValid_NoSecretConfigured_ShouldRefuseTheSignatureOfAnEmptyKey(string? secret)
  {
    _settings.WebhookSecret = secret;
    _settings.WebhookSecretPrevious = secret;

    Validator().IsValid(Body, Sign(string.Empty, Body)).ShouldBeFalse();
    A.CallTo(_protector).MustNotHaveHappened();
  }

  [Fact]
  public void IsValid_ProtectedEmptySecret_ShouldRefuseTheSignatureOfAnEmptyKey()
  {
    _settings.WebhookSecret = "protected:";
    _settings.WebhookSecretPrevious = null;

    Validator().IsValid(Body, Sign(string.Empty, Body)).ShouldBeFalse();
  }

  [Fact]
  public void IsValid_RotatedSecret_ShouldApplyWithoutANewValidator()
  {
    var validator = Validator();
    _settings.WebhookSecret = "protected:rotated";

    validator.IsValid(Body, Sign("rotated", Body)).ShouldBeTrue();
    validator.IsValid(Body, Sign(Current, Body)).ShouldBeFalse();
  }

  private double Failures => _recorder.Sum(CodeCoverageMeter.WebhookSignatureFailuresTotal);

  private static string Sign(string secret, byte[] body) =>
    "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

  private WebhookSignatureValidator Validator() => new(_options, _protector, new CodeCoverageMeter(_meters));

  #endregion
}
