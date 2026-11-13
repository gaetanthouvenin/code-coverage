// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeTokenQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.GetBadgeToken;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetBadgeToken;

public sealed class GetBadgeTokenQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_PrivateRepository_ShouldGiveTheTokenInClear()
  {
    Tracked(isPrivate: true);
    A.CallTo(() => _protector.Unprotect("protected")).Returns("Kq7xT2");

    var token = await Handler().Handle(Query(), Ct);

    token.ShouldBe("Kq7xT2");
  }

  // A public repository serves its badge without a token: there is none to give.
  [Fact]
  public async Task Handle_PublicRepository_ShouldGiveNoToken()
  {
    Tracked(isPrivate: false);

    var token = await Handler().Handle(Query(), Ct);

    token.ShouldBeNull();
    A.CallTo(_protector).MustNotHaveHappened();
  }

  // Decision 3: every reader of the repository reads it, since the badge of a private repository needs it.
  [Fact]
  public void Query_ShouldRequireReadingOnly() => Query().RequiredPermission.ShouldBe(RepositoryPermission.Read);

  private static GetBadgeTokenQuery Query() => new(Forge.GitHub, "octo-org", "app");

  private void Tracked(bool isPrivate)
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", isPrivate, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
  }

  private GetBadgeTokenQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _protector);

  #endregion
}
