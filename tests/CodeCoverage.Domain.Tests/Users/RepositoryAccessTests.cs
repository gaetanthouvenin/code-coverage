// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryAccessTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Domain.Tests.Users;

public sealed class RepositoryAccessTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldWhatTheForgeReports()
  {
    var access = RepositoryAccess.Create(5, "ABC", RepositoryPermission.Write, Now);

    access.UserId.ShouldBe(5);
    access.RepositoryExternalId.ShouldBe("abc");
    access.Permission.ShouldBe(RepositoryPermission.Write);
    access.RefreshedAt.ShouldBe(Now);
  }

  [Fact]
  public void Refresh_ShouldApplyThePermissionAndTheDate()
  {
    var access = RepositoryAccess.Create(5, "1", RepositoryPermission.Admin, Now);

    access.Refresh(RepositoryPermission.Read, Now.AddHours(1));

    access.Permission.ShouldBe(RepositoryPermission.Read);
    access.RefreshedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void Create_NotPositiveUser_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => RepositoryAccess.Create(0, "1", RepositoryPermission.Read, Now));
  }

  [Fact]
  public void Create_UndefinedPermission_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => RepositoryAccess.Create(5, "1", (RepositoryPermission)9, Now));
  }

  [Fact]
  public void Create_ExternalIdTooLong_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => RepositoryAccess.Create(5, new string('1', TrackedRepository.ExternalIdMaxLength + 1), RepositoryPermission.Read, Now));
  }

  #endregion
}
