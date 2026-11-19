// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionCheckTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeConnectionCheckTests
{
  #region Fields

  private static readonly ForgeAccountInfo Account = new("account-1", "fabrikam", "service-1");

  private static readonly Uri BaseAddress = new("https://dev.azure.com/fabrikam/");

  #endregion

  #region Methods

  [Fact]
  public void Verified_ShouldExposeTheAccountAndTheAddress()
  {
    var check = ForgeConnectionCheck.Verified(Account, BaseAddress);

    check.IsRefused.ShouldBeFalse();
    check.Account.ShouldBeSameAs(Account);
    check.BaseAddress.ShouldBe(BaseAddress);
    check.Refusal.ShouldBeNull();
  }

  [Fact]
  public void Verified_NullAccount_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => ForgeConnectionCheck.Verified(null!, BaseAddress));
  }

  [Fact]
  public void Verified_NullAddress_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => ForgeConnectionCheck.Verified(Account, null!));
  }

  [Fact]
  public void Verified_RelativeAddress_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => ForgeConnectionCheck.Verified(Account, new Uri("fabrikam", UriKind.Relative)));
  }

  [Fact]
  public void Refused_ShouldExposeTheReasonOnly()
  {
    var check = ForgeConnectionCheck.Refused("TF400813");

    check.IsRefused.ShouldBeTrue();
    check.Refusal.ShouldBe("TF400813");
    check.Account.ShouldBeNull();
    check.BaseAddress.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void Refused_EmptyReason_ShouldThrow(string refusal)
  {
    Should.Throw<ArgumentException>(() => ForgeConnectionCheck.Refused(refusal));
  }

  [Fact]
  public void Refused_NullReason_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => ForgeConnectionCheck.Refused(null!));
  }

  #endregion
}
