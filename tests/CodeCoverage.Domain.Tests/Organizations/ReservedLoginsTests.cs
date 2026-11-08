// ------------------------------------------------------------------------------------------------
// <copyright file="ReservedLoginsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class ReservedLoginsTests
{
  #region Methods

  [Theory]
  [InlineData("admin")]
  [InlineData("ADMIN")]
  [InlineData("Orgs")]
  [InlineData("access-denied")]
  public void IsReserved_AnyCase_ShouldBeTrue(string login)
  {
    ReservedLogins.IsReserved(login).ShouldBeTrue();
  }

  [Theory]
  [InlineData("octo-org")]
  [InlineData("administrator")]
  public void IsReserved_OrdinaryLogin_ShouldBeFalse(string login)
  {
    ReservedLogins.IsReserved(login).ShouldBeFalse();
  }

  #endregion
}
