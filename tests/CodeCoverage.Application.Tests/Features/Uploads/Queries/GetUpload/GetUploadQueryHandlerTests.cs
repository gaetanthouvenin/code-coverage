// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Queries.GetUpload;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Uploads.Queries.GetUpload;

public sealed class GetUploadQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly Guid UploadId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly Upload _upload = Upload.Receive(9, 1, "key", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now, UploadId);

  #endregion

  #region Ctors

  public GetUploadQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _uploads.FindByPublicIdAsync(UploadId, A<CancellationToken>._)).Returns(_upload);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_UploadOfTheRepository_ShouldReturnIt()
  {
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(CommitOf(5));

    (await Handler().Handle(Query(UploadId), Ct)).ShouldBeSameAs(_upload);
  }

  // Another repository of the same organization, read by the same user: the upload stays invisible from this address.
  [Fact]
  public async Task Handle_UploadOfAnotherRepository_ShouldThrowNotFound()
  {
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(CommitOf(6));

    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(UploadId), Ct).AsTask());

    exception.ErrorCode.ShouldBe(UploadErrors.NotFound);
  }

  [Fact]
  public async Task Handle_UnknownUpload_ShouldThrowNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(() => Handler().Handle(Query(Guid.NewGuid()), Ct).AsTask());

    exception.ErrorCode.ShouldBe(UploadErrors.NotFound);
  }

  private static Commit CommitOf(long repositoryId) => Commit.Create(repositoryId, GitSha.Parse(new string('a', GitSha.Length)), Now);

  private static GetUploadQuery Query(Guid id) => new(Forge.GitHub, "octo-org", "app", id);

  private GetUploadQueryHandler Handler() => new(new CommitLocator(_repositories, _commits), _uploads, _commits);

  #endregion
}
