// ------------------------------------------------------------------------------------------------
// <copyright file="AdministratorListTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Tests.Users;

public sealed class AdministratorListTests
{
  #region Methods

  [Theory]
  [InlineData("583231")]
  [InlineData("1234,583231")]
  [InlineData(" 1234 , 583231 ")]
  [InlineData("not-a-number, 583231")]
  [InlineData("0583231")]
  public void Contains_ListNamingTheAccount_ShouldBeTrue(string list)
  {
    AdministratorList.Contains(Forge.GitHub, "583231", list).ShouldBeTrue();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("58323")]
  [InlineData("5832310")]
  [InlineData("+583231")]
  [InlineData("583231a")]
  [InlineData("-583231")]
  public void Contains_ListNotNamingTheAccount_ShouldBeFalse(string? list)
  {
    AdministratorList.Contains(Forge.GitHub, "583231", list).ShouldBeFalse();
  }

  [Fact]
  public void Contains_SecondListNamingTheAccount_ShouldBeTrue()
  {
    AdministratorList.Contains(Forge.GitHub, "583231", "1234", null, "583231").ShouldBeTrue();
  }

  [Fact]
  public void Contains_NoList_ShouldBeFalse()
  {
    AdministratorList.Contains(Forge.GitHub, "583231").ShouldBeFalse();
  }

  // The lists hold GitHub identifiers: the same number on another forge names another account.
  [Fact]
  public void Contains_AccountOfAnotherForge_ShouldBeFalse()
  {
    AdministratorList.Contains(Forge.GitLab, "583231", "583231").ShouldBeFalse();
  }

  [Theory]
  [InlineData("0")]
  [InlineData("not-a-number")]
  public void Contains_AccountIdentifierNotANumber_ShouldBeFalse(string externalId)
  {
    AdministratorList.Contains(Forge.GitHub, externalId, externalId, "0").ShouldBeFalse();
  }

  [Fact]
  public void Contains_NullIdentifier_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => AdministratorList.Contains(Forge.GitHub, null!, "1"));
  }

  #endregion
}
