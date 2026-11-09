// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

public sealed class InstallationTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldTheOrganization()
  {
    var installation = Installation.Create(100, 7, Now);

    installation.Id.ShouldBe(100);
    installation.OrganizationId.ShouldBe(7);
    installation.SuspendedAt.ShouldBeNull();
  }

  [Theory]
  [InlineData(0, 7)]
  [InlineData(100, 0)]
  public void Create_NotPositive_ShouldThrow(long id, long organizationId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => Installation.Create(id, organizationId, Now));
  }

  #endregion
}
