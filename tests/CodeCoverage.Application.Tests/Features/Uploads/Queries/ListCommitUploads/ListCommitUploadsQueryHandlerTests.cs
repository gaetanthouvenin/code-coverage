// ------------------------------------------------------------------------------------------------
// <copyright file="ListCommitUploadsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads.Queries.ListCommitUploads;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Uploads.Queries.ListCommitUploads;

public sealed class ListCommitUploadsQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_UnknownRepository_ShouldThrowNotFound()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Query(), Ct));
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldBeEmpty()
  {
    Tracked();
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    (await Handler().Handle(Query(), Ct)).ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_KnownCommit_ShouldListItsUploads()
  {
    Tracked();
    var commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);
    A.CallTo(() => _commits.FindByShaAsync(A<long>._, Sha, A<CancellationToken>._)).Returns(commit);
    Upload[] uploads = [Upload.Receive(9, 1, "k", [], null, null, null, null, null, null, null, null, PrincipalKind.GitHubOidc, Now)];
    A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns(uploads);

    (await Handler().Handle(Query(), Ct)).ShouldBe(uploads);
  }

  private static ListCommitUploadsQuery Query() => new(Forge.GitHub, "octo-org", "app", Sha);

  private ListCommitUploadsQueryHandler Handler() => new(_repositories, _commits, _uploads);

  private void Tracked() =>
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._))
     .Returns(TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now));

  #endregion
}
