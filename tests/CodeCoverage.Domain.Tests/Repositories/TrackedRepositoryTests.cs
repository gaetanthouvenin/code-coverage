// ------------------------------------------------------------------------------------------------
// <copyright file="TrackedRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Domain.Tests.Repositories;

public sealed class TrackedRepositoryTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

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

  private static TrackedRepository NewRepository(string slug, string externalId) =>
    TrackedRepository.Create(1, externalId, RepositorySlug.Parse(slug), "main", false, "protected", Now);

  #endregion
}
