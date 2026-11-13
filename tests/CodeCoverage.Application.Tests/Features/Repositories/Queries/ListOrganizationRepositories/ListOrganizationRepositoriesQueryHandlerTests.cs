// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationRepositoriesQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.ListOrganizationRepositories;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.ListOrganizationRepositories;

public sealed class ListOrganizationRepositoriesQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldListTheRepositoriesByNameWithTheirLastUpload()
  {
    var served = Repository(5, "web", isActive: true);
    var other = Repository(6, "api", isActive: true);
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).Returns([served, other]);
    A.CallTo(() => _commits.ListLastUploadsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._))
     .Returns(new Dictionary<long, DateTimeOffset> { [5] = Now });

    var repositories = await Handler().Handle(Query(), Ct);

    repositories.Select(static r => r.Repository.Name).ShouldBe(["api", "web"]);
    repositories.Single(static r => r.Repository.Name == "web").LastUploadAt.ShouldBe(Now);
    repositories.Single(static r => r.Repository.Name == "api").LastUploadAt.ShouldBeNull();
  }

  // A repository that left the installation is not listed: only the installation brings it back.
  [Fact]
  public async Task Handle_RepositoryOutOfTheInstallation_ShouldNotListIt()
  {
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).Returns([Repository(5, "web", isActive: true), Repository(6, "gone", isActive: false)]);

    var repositories = await Handler().Handle(Query(), Ct);

    repositories.ShouldHaveSingleItem().Repository.Name.ShouldBe("web");
  }

  // One read for every repository, never one read per repository.
  [Fact]
  public async Task Handle_ShouldReadTheUploadsOfEveryRepositoryAtOnce()
  {
    A.CallTo(() => _repositories.ListAsync(A<CancellationToken>._)).Returns([Repository(5, "web", isActive: true), Repository(6, "api", isActive: true)]);

    await Handler().Handle(Query(), Ct);

    A.CallTo(() => _commits.ListLastUploadsAsync(A<IReadOnlyCollection<long>>.That.Matches(ids => ids.Count == 2 && ids.Contains(5) && ids.Contains(6)), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Query_ShouldRequireAnOrganizationAdministrator() => Query().RequiredRole.ShouldBe(OrganizationRole.Admin);

  private static TrackedRepository Repository(long id, string name, bool isActive)
  {
    var repository = TrackedRepository.Create(1, id.ToString(System.Globalization.CultureInfo.InvariantCulture), RepositorySlug.Parse($"acme-labs/{name}"), "main", false, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);

    if (!isActive)
    {
      repository.Deactivate(Now);
    }

    return repository;
  }

  private static ListOrganizationRepositoriesQuery Query() => new(Forge.GitHub, "acme-labs");

  private ListOrganizationRepositoriesQueryHandler Handler() => new(_repositories, _commits);

  #endregion
}
