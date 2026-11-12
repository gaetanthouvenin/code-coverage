// ------------------------------------------------------------------------------------------------
// <copyright file="TrackedRepositoryActivationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

// M3.07: the switch of an administrator, apart from the installation.
public sealed class TrackedRepositoryActivationTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Disable_ShouldStopTrackingAndKeepTheInstallationState()
  {
    var repository = Repository();

    repository.Disable(Now).ShouldBeTrue();

    repository.DisabledAt.ShouldBe(Now);
    repository.IsActive.ShouldBeTrue();
    repository.IsTracked.ShouldBeFalse();
    repository.Version.ShouldBe(1);
  }

  [Fact]
  public void Disable_AlreadyDisabled_ShouldChangeNothing()
  {
    var repository = Repository();
    repository.Disable(Now);

    repository.Disable(Now.AddHours(1)).ShouldBeFalse();

    repository.DisabledAt.ShouldBe(Now);
    repository.Version.ShouldBe(1);
  }

  [Fact]
  public void Enable_ShouldTrackAgain()
  {
    var repository = Repository();
    repository.Disable(Now);

    repository.Enable(Now.AddHours(1)).ShouldBeTrue();

    repository.DisabledAt.ShouldBeNull();
    repository.IsTracked.ShouldBeTrue();
  }

  [Fact]
  public void Enable_NotDisabled_ShouldChangeNothing()
  {
    var repository = Repository();

    repository.Enable(Now).ShouldBeFalse();

    repository.Version.ShouldBe(0);
  }

  // The installation serving the repository again never undoes the choice of an administrator.
  [Fact]
  public void Activate_ByTheInstallation_ShouldKeepTheRepositoryDisabled()
  {
    var repository = Repository();
    repository.Disable(Now);
    repository.Deactivate(Now);

    repository.Activate(78, Now.AddHours(1));

    repository.IsActive.ShouldBeTrue();
    repository.DisabledAt.ShouldBe(Now);
    repository.IsTracked.ShouldBeFalse();
  }

  private static TrackedRepository Repository() => TrackedRepository.Create(77, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);

  #endregion
}
