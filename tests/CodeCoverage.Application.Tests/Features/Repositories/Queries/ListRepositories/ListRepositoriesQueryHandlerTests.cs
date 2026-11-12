// ------------------------------------------------------------------------------------------------
// <copyright file="ListRepositoriesQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories.Queries.ListRepositories;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.ListRepositories;

public sealed class ListRepositoriesQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ListRepositoriesQueryHandler _handler;

  #endregion

  #region Ctors

  public ListRepositoriesQueryHandlerTests()
  {
    A.CallTo(() => _currentUser.UserId).Returns(42L);

    _handler = new ListRepositoriesQueryHandler(_currentUser, _repositories, _access);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A repository the user cannot read on its forge is left out, as if it did not exist.
  [Fact]
  public async Task Handle_ShouldReturnOnlyAccessibleRepositories()
  {
    var readable = Repository("100", "octo-org/app");
    var hidden = Repository("200", "octo-org/secret");
    A.CallTo(() => _repositories.SearchActiveAsync("ap", A<CancellationToken>._)).Returns([readable, hidden]);
    A.CallTo(() => _access.ListReadableAsync(42, A<CancellationToken>._)).Returns(new HashSet<string>(["100", "300"], StringComparer.Ordinal));

    var listed = await _handler.Handle(new ListRepositoriesQuery(Forge.GitHub, "octo-org", "ap"), Ct);

    listed.ShouldBe([readable]);
  }

  // The instance administrator sees no more than its own access (ADR-013).
  [Fact]
  public async Task Handle_InstanceAdministratorWithoutAccess_ShouldListNothing()
  {
    A.CallTo(() => _currentUser.IsAdmin).Returns(true);
    A.CallTo(() => _repositories.SearchActiveAsync(null, A<CancellationToken>._)).Returns([Repository("100", "octo-org/app")]);
    A.CallTo(() => _access.ListReadableAsync(42, A<CancellationToken>._)).Returns(new HashSet<string>(StringComparer.Ordinal));

    (await _handler.Handle(new ListRepositoriesQuery(Forge.GitHub, "octo-org", null), Ct)).ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_WithoutUser_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _currentUser.UserId).Returns(null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await _handler.Handle(new ListRepositoriesQuery(Forge.GitHub, "octo-org", null), Ct));
  }

  private static TrackedRepository Repository(string externalId, string slug) =>
    TrackedRepository.Create(1, externalId, RepositorySlug.Parse(slug), "main", true, "protected", Now);

  #endregion
}
