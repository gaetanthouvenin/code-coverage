// ------------------------------------------------------------------------------------------------
// <copyright file="TrackedRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

public sealed class TrackedRepositoryTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset Later = Now.AddDays(1);

  #endregion

  #region Methods

  [Fact]
  public void Create_ShouldBeActiveAndNormalized()
  {
    var repository = NewRepository("Group/Sub/Repo", externalId: "{ABC-DEF}");

    repository.IsActive.ShouldBeTrue();
    repository.ExternalId.ShouldBe("{abc-def}");
    repository.Owner.ShouldBe("Group");
    repository.Namespace.ShouldBe("Sub");
    repository.NormalizedNamespace.ShouldBe("sub");
    repository.Name.ShouldBe("Repo");
    repository.NormalizedName.ShouldBe("repo");
    repository.Version.ShouldBe(0);
  }

  [Fact]
  public void Create_LocalTime_ShouldBeUtc()
  {
    var local = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(2));

    var repository = TrackedRepository.Create(1, "42", RepositorySlug.Parse("owner/repo"), "main", false, "protected", local);

    repository.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    repository.CreatedAt.ShouldBe(local);
  }

  [Fact]
  public void Create_ExternalIdOverTheLimit_ShouldThrow()
  {
    var externalId = new string('1', TrackedRepository.ExternalIdMaxLength + 1);

    Should.Throw<ArgumentException>(() => NewRepository("owner/repo", externalId));
  }

  [Fact]
  public void Create_InstallationNotPositive_ShouldThrow()
  {
    var slug = RepositorySlug.Parse("owner/repo");

    Should.Throw<ArgumentOutOfRangeException>(() => TrackedRepository.Create(0, "42", slug, "main", false, "protected", Now));
  }

  [Fact]
  public void Rename_ShouldKeepExternalId()
  {
    var repository = NewRepository("owner/old-name", externalId: "42");

    repository.Rename(RepositorySlug.Parse("New-Owner/New-Name"), Now.AddHours(1));

    repository.ExternalId.ShouldBe("42");
    repository.Owner.ShouldBe("New-Owner");
    repository.Name.ShouldBe("New-Name");
    repository.NormalizedName.ShouldBe("new-name");
    repository.UpdatedAt.ShouldBe(Now.AddHours(1));
    repository.Version.ShouldBe(1);
  }

  [Fact]
  public void Rename_SameName_ShouldNotChangeTheVersion()
  {
    var repository = NewRepository("owner/repo", externalId: "42");

    repository.Rename(RepositorySlug.Parse("owner::::repo"), Now.AddHours(1));

    repository.Version.ShouldBe(0);
    repository.UpdatedAt.ShouldBe(Now);
  }

  [Fact]
  public void Deactivate_ShouldStopTrackingOnce()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.Deactivate(Later);
    repository.Deactivate(Later.AddHours(1));

    repository.IsActive.ShouldBeFalse();
    repository.Version.ShouldBe(1);
    repository.UpdatedAt.ShouldBe(Later);
  }

  // A transfer closes the row: no longer tracked, another badge token, and the mark that tells it from a row that is
  // only inactive.
  [Fact]
  public void CloseAsTransferred_ShouldStopTrackingReplaceTheBadgeTokenAndMarkTheRow()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.TransferredAt.ShouldBeNull();

    repository.CloseAsTransferred("other", Later.ToOffset(TimeSpan.FromHours(2)));

    repository.IsActive.ShouldBeFalse();
    repository.BadgeTokenProtected.ShouldBe("other");
    repository.TransferredAt.ShouldBe(Later);
    repository.TransferredAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    repository.Version.ShouldBe(1);
    repository.UpdatedAt.ShouldBe(Later);
  }

  // Deactivated by an installation that stopped serving it: the row is inactive, not closed, and a transfer closes it.
  [Fact]
  public void CloseAsTransferred_RowOnlyInactive_ShouldCloseIt()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.Deactivate(Now);
    repository.TransferredAt.ShouldBeNull();

    repository.CloseAsTransferred("other", Later);

    repository.IsActive.ShouldBeFalse();
    repository.TransferredAt.ShouldBe(Later);
    repository.BadgeTokenProtected.ShouldBe("other");
  }

  [Fact]
  public void CloseAsTransferred_Twice_ShouldThrowAndKeepTheFirstClosing()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.CloseAsTransferred("other", Later);

    Should.Throw<InvalidOperationException>(() => repository.CloseAsTransferred("third", Later.AddHours(1)));

    repository.BadgeTokenProtected.ShouldBe("other");
    repository.TransferredAt.ShouldBe(Later);
    repository.Version.ShouldBe(1);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void CloseAsTransferred_EmptyToken_ShouldThrowAndCloseNothing(string token)
  {
    var repository = NewRepository("owner/repo", "42");

    Should.Throw<ArgumentException>(() => repository.CloseAsTransferred(token, Later));

    repository.IsActive.ShouldBeTrue();
    repository.TransferredAt.ShouldBeNull();
    repository.Version.ShouldBe(0);
  }

  // The repository comes back: its row is open again, so that leaving once more closes it once more. The badge token
  // that the transfer replaced stays replaced.
  [Fact]
  public void Activate_RowClosedByATransfer_ShouldOpenItAgain()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.CloseAsTransferred("other", Now);

    repository.Activate(9, Later);

    repository.IsActive.ShouldBeTrue();
    repository.TransferredAt.ShouldBeNull();
    repository.BadgeTokenProtected.ShouldBe("other");

    repository.CloseAsTransferred("third", Later.AddHours(1));
    repository.TransferredAt.ShouldBe(Later.AddHours(1));
  }

  [Fact]
  public void Activate_Inactive_ShouldTrackAgainUnderTheGivenInstallation()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.Deactivate(Now);

    repository.Activate(9, Later);

    repository.IsActive.ShouldBeTrue();
    repository.InstallationId.ShouldBe(9);
    repository.Version.ShouldBe(2);
  }

  [Fact]
  public void Activate_ActiveUnderTheSameInstallation_ShouldChangeNothing()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.Activate(1, Later);

    repository.Version.ShouldBe(0);
    repository.UpdatedAt.ShouldBe(Now);
  }

  [Fact]
  public void Activate_ActiveUnderAnotherInstallation_ShouldMoveIt()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.Activate(9, Later);

    repository.InstallationId.ShouldBe(9);
    repository.Version.ShouldBe(1);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Activate_InstallationNotPositive_ShouldThrow(long installationId)
  {
    Should.Throw<ArgumentOutOfRangeException>(() => NewRepository("owner/repo", "42").Activate(installationId, Later));
  }

  [Fact]
  public void ChangeVisibility_ShouldCountOnlyAChange()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.ChangeVisibility(false, Later);
    repository.Version.ShouldBe(0);

    repository.ChangeVisibility(true, Later);
    repository.IsPrivate.ShouldBeTrue();
    repository.Version.ShouldBe(1);
  }

  [Fact]
  public void ChangeDefaultBranch_ShouldCountOnlyAChange()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.ChangeDefaultBranch("main", Later);
    repository.Version.ShouldBe(0);

    repository.ChangeDefaultBranch("trunk", Later);
    repository.DefaultBranch.ShouldBe("trunk");
    repository.Version.ShouldBe(1);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void ChangeDefaultBranch_Empty_ShouldThrow(string branch)
  {
    Should.Throw<ArgumentException>(() => NewRepository("owner/repo", "42").ChangeDefaultBranch(branch, Later));
  }

  [Fact]
  public void ChangeDefaultBranch_OverTheLimit_ShouldThrow()
  {
    var branch = new string('b', Branch.NameMaxLength + 1);

    Should.Throw<ArgumentException>(() => NewRepository("owner/repo", "42").ChangeDefaultBranch(branch, Later));
  }

  [Fact]
  public void RefreshDefaultBranchYaml_ShouldKeepTheContentAndTheCommitItWasReadFrom()
  {
    var repository = NewRepository("owner/repo", "42");
    var sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

    repository.RefreshDefaultBranchYaml("coverage:\n  precision: 1\n", sha, Later);

    repository.DefaultBranchYaml.ShouldBe("coverage:\n  precision: 1\n");
    repository.DefaultBranchYamlSha.ShouldBe(sha);
    repository.UpdatedAt.ShouldBe(Later);
    repository.Version.ShouldBe(1);
  }

  [Fact]
  public void RefreshDefaultBranchYaml_ShouldCountOnlyAChange()
  {
    var repository = NewRepository("owner/repo", "42");
    var first = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var second = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    repository.RefreshDefaultBranchYaml("yaml", first, Later);

    repository.RefreshDefaultBranchYaml("yaml", first, Later.AddHours(1));
    repository.Version.ShouldBe(1);

    // The same content at another commit still moves the commit it was read from.
    repository.RefreshDefaultBranchYaml("yaml", second, Later.AddHours(1));
    repository.DefaultBranchYamlSha.ShouldBe(second);
    repository.Version.ShouldBe(2);

    repository.RefreshDefaultBranchYaml("other", second, Later.AddHours(2));
    repository.DefaultBranchYaml.ShouldBe("other");
    repository.Version.ShouldBe(3);
  }

  [Fact]
  public void RefreshDefaultBranchYaml_Null_ShouldThrow()
  {
    var repository = NewRepository("owner/repo", "42");
    var sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

    Should.Throw<ArgumentNullException>(() => repository.RefreshDefaultBranchYaml(null!, sha, Later));
    Should.Throw<ArgumentNullException>(() => repository.RefreshDefaultBranchYaml("yaml", null!, Later));
  }

  [Fact]
  public void RotateBadgeToken_ShouldReplaceTheValue()
  {
    var repository = NewRepository("owner/repo", "42");

    repository.RotateBadgeToken("other", Later);

    repository.BadgeTokenProtected.ShouldBe("other");
    repository.Version.ShouldBe(1);
    repository.UpdatedAt.ShouldBe(Later);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void RotateBadgeToken_Empty_ShouldThrow(string token)
  {
    Should.Throw<ArgumentException>(() => NewRepository("owner/repo", "42").RotateBadgeToken(token, Later));
  }

  [Fact]
  public void ReleaseName_Inactive_ShouldTakeANameNoForgeGives()
  {
    var repository = NewRepository("owner/Repo", "42");
    repository.Deactivate(Now);

    repository.ReleaseName(Later);
    repository.ReleaseName(Later.AddHours(1));

    repository.Name.ShouldBe("Repo~42");
    repository.NormalizedName.ShouldBe("repo~42");
    repository.Version.ShouldBe(2);
    repository.UpdatedAt.ShouldBe(Later);
    RepositorySlug.TryFromAddress("owner", repository.Name, out _).ShouldBeTrue();
  }

  [Fact]
  public void ReleaseName_LongestName_ShouldStayWithinTheColumn()
  {
    var repository = NewRepository("owner/" + new string('r', TrackedRepository.NameMaxLength), "42");
    repository.Deactivate(Now);

    repository.ReleaseName(Later);

    repository.Name.Length.ShouldBe(TrackedRepository.NameMaxLength);
    repository.Name.ShouldEndWith("~42");
  }

  [Fact]
  public void ReleaseName_Active_ShouldThrow()
  {
    Should.Throw<InvalidOperationException>(() => NewRepository("owner/repo", "42").ReleaseName(Later));
  }

  [Fact]
  public void Rename_AfterAReleasedName_ShouldTakeTheNameOfTheForgeAgain()
  {
    var repository = NewRepository("owner/repo", "42");
    repository.Deactivate(Now);
    repository.ReleaseName(Now);

    repository.Activate(1, Later);
    repository.Rename(RepositorySlug.Parse("owner/repo"), Later);

    repository.Name.ShouldBe("repo");
  }

  private static TrackedRepository NewRepository(string slug, string externalId) =>
    TrackedRepository.Create(1, externalId, RepositorySlug.Parse(slug), "main", false, "protected", Now);

  #endregion
}
