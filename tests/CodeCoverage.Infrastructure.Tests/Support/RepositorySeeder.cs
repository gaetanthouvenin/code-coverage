// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySeeder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Installations, repositories and branches of the two organizations of a TwoOrganizationDatabase.
public static class RepositorySeeder
{
  #region Fields

  public const long FirstInstallation = 101;

  public const long SecondInstallation = 202;

  public static readonly GitSha Head = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  public static readonly GitSha NewHead = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  public static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  public static async Task AddInstallationsAsync(TwoOrganizationDatabase organizations, CancellationToken cancellationToken)
  {
    await using var context = organizations.ContextWithoutOrganization();
    var repository = new InstallationRepository(context);

    await repository.AddAsync(Installation.Create(FirstInstallation, organizations.FirstId, Now), cancellationToken);
    await repository.AddAsync(Installation.Create(SecondInstallation, organizations.SecondId, Now), cancellationToken);
  }

  public static TrackedRepository NewRepository(long installationId, string externalId, string slug) =>
    TrackedRepository.Create(installationId, externalId, RepositorySlug.Parse(slug), "main", false, "protected-token", Now);

  public static async Task<long> AddRepositoryAsync(
    CodeCoverageDbContext context,
    long installationId,
    string externalId,
    string slug,
    CancellationToken cancellationToken)
  {
    var repository = NewRepository(installationId, externalId, slug);

    await new RepositoryRepository(context).AddAsync(repository, cancellationToken);

    return repository.Id;
  }

  #endregion
}
