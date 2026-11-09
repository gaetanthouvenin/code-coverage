// ------------------------------------------------------------------------------------------------
// <copyright file="BranchTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

public sealed class BranchTests
{
  #region Fields

  private static readonly GitSha First = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Second = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldHoldTheHead()
  {
    var branch = Branch.Create(1, "main", First, Now);

    branch.HeadSha.ShouldBe(First);
    branch.HeadUpdatedAt.ShouldBe(Now);
    branch.CoveredCommitId.ShouldBeNull();
    branch.Version.ShouldBe(0);
  }

  [Fact]
  public void Create_NameOverTheLimit_ShouldThrow()
  {
    var name = new string('a', Branch.NameMaxLength + 1);

    Should.Throw<ArgumentException>(() => Branch.Create(1, name, First, Now));
  }

  [Fact]
  public void Create_RepositoryNotPositive_ShouldThrow()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => Branch.Create(0, "main", First, Now));
  }

  [Fact]
  public void MoveHead_NewSha_ShouldChangeTheVersion()
  {
    var branch = Branch.Create(1, "main", First, Now);

    branch.MoveHead(Second, Now.AddHours(1));

    branch.HeadSha.ShouldBe(Second);
    branch.HeadUpdatedAt.ShouldBe(Now.AddHours(1));
    branch.Version.ShouldBe(1);
  }

  [Fact]
  public void MoveHead_SameSha_ShouldDoNothing()
  {
    var branch = Branch.Create(1, "main", First, Now);

    branch.MoveHead(First, Now.AddHours(1));

    branch.HeadUpdatedAt.ShouldBe(Now);
    branch.Version.ShouldBe(0);
  }

  #endregion
}
