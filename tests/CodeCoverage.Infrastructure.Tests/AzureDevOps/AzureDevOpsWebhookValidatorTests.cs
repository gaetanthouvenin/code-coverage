// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsWebhookValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Webhooks;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.AzureDevOps;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsWebhookValidatorTests
{
  #region Fields

  private const string Current = "current-secret";

  private const string Previous = "previous-secret";

  private readonly AzureDevOpsWebhookValidator _validator = new();

  #endregion

  #region Methods

  [Fact]
  public void IsValid_NullArguments_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => _validator.IsValid(null!, [Current]));
    Should.Throw<ArgumentNullException>(() => _validator.IsValid(Request(Current), null!));
  }

  [Fact]
  public void IsValid_CurrentSecret_ShouldAccept() =>
    _validator.IsValid(Request(Current), [Current, Previous]).ShouldBeTrue();

  // The secret was rotated (M7.13) and the subscription still sends the previous one.
  [Fact]
  public void IsValid_PreviousSecret_ShouldAccept() =>
    _validator.IsValid(Request(Previous), [Current, Previous]).ShouldBeTrue();

  [Fact]
  public void IsValid_HeaderNameInAnotherCase_ShouldAccept()
  {
    var request = new ForgeWebhookRequest(
      Forge.AzureDevOps,
      "conn-1",
      "d-1",
      "git.push",
      new Dictionary<string, string> { [AzureDevOpsWebhookHeaders.Secret.ToUpperInvariant()] = Current },
      "{}"u8.ToArray()
    );

    _validator.IsValid(request, [Current]).ShouldBeTrue();
  }

  [Fact]
  public void IsValid_WrongSecret_ShouldRefuse() =>
    _validator.IsValid(Request("stolen-guess"), [Current, Previous]).ShouldBeFalse();

  // A secret that only starts like the right one is not the right one.
  [Fact]
  public void IsValid_PrefixOfTheSecret_ShouldRefuse() =>
    _validator.IsValid(Request("current"), [Current]).ShouldBeFalse();

  [Fact]
  public void IsValid_MissingHeader_ShouldRefuse()
  {
    var request = new ForgeWebhookRequest(Forge.AzureDevOps, "conn-1", "d-1", "git.push", new Dictionary<string, string>(), "{}"u8.ToArray());

    _validator.IsValid(request, [Current, Previous]).ShouldBeFalse();
  }

  [Fact]
  public void IsValid_EmptyHeader_ShouldRefuse() =>
    _validator.IsValid(Request(string.Empty), [Current, string.Empty]).ShouldBeFalse();

  // An empty secret never authenticates, even against a header that would hash the same.
  [Fact]
  public void IsValid_EmptySecret_ShouldBeSkipped()
  {
    _validator.IsValid(Request(Current), [string.Empty]).ShouldBeFalse();
    _validator.IsValid(Request(Current), [string.Empty, Current]).ShouldBeTrue();
  }

  [Fact]
  public void IsValid_NoSecret_ShouldRefuse() =>
    _validator.IsValid(Request(Current), []).ShouldBeFalse();

  private static ForgeWebhookRequest Request(string secret) =>
    new(
      Forge.AzureDevOps,
      "conn-1",
      "d-1",
      "git.push",
      new Dictionary<string, string> { [AzureDevOpsWebhookHeaders.Secret] = secret },
      "{}"u8.ToArray()
    );

  #endregion
}
