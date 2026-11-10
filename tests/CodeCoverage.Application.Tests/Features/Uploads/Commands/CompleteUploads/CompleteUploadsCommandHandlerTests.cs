// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteUploadsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Uploads.Commands.CompleteUploads;

public sealed class CompleteUploadsCommandHandlerTests
{
  #region Fields

  private const string Value = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly List<JobRequest> _queued = [];

  private readonly Commit _commit = Commit.Create(5, Sha, Now);

  #endregion

  #region Ctors

  public CompleteUploadsCommandHandlerTests()
  {
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _jobQueue.EnqueueAsync(A<JobRequest>._, A<CancellationToken>._))
     .Invokes((JobRequest job, CancellationToken _) => _queued.Add(job))
     .Returns(1L);
    Uploads(UploadState.Processed);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldQueueTheForcedNotificationOfTheCommit()
  {
    Uploads(UploadState.Processed, UploadState.Processed, UploadState.Failed);

    var completion = await Handler().Handle(Command(), Ct);

    completion.ShouldBe(new UploadCompletion(3, 2, 0, 1));

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true}""");
    job.LockKey.ShouldBe("commit:9");
    // The organization of the scope: the address named it, never the body.
    job.OrganizationId.ShouldBeNull();
    job.IsInstance.ShouldBeFalse();
  }

  // Uploads still in progress: the notification is queued all the same, and waits for them.
  [Fact]
  public async Task Handle_UploadsInProgress_ShouldCountThemAndStillQueue()
  {
    Uploads(UploadState.Processed, UploadState.Received, UploadState.Processing);

    var completion = await Handler().Handle(Command(), Ct);

    completion.ShouldBe(new UploadCompletion(3, 1, 2, 0));
    _queued.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Handle_CommitWithoutUpload_ShouldQueueNothing()
  {
    Uploads();

    (await Handler().Handle(Command(), Ct)).ShouldBe(new UploadCompletion(0, 0, 0, 0));
    _queued.ShouldBeEmpty();
  }

  // The uploads sent with the SHA of a synthetic merge commit are moved to the head of the pull request (§1.4-2). The
  // request cannot ask the forge which commit that is: the job is queued all the same, and finds the head.
  [Fact]
  public async Task Handle_CommitOfAPullRequestWithoutUpload_ShouldQueueTheNotificationThatFindsTheHead()
  {
    _commit.MarkPending("feature", 12, Now);
    Uploads();

    (await Handler().Handle(Command(), Ct)).ShouldBe(new UploadCompletion(0, 0, 0, 0));

    var job = _queued.ShouldHaveSingleItem();
    job.Type.ShouldBe(JobTypes.NotifyCommit);
    job.PayloadJson.ShouldBe("""{"CommitId":9,"Force":true}""");
    A.CallTo(_gateways).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CredentialOfOtherOrganization_ShouldThrowUnauthorized()
  {
    // The token exists, in another organization: under the filter of this one it is unknown.
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(null);
    A.CallTo(() => _tokens.FindByHashAsync(A<byte[]>._, A<CancellationToken>._)).Returns((UploadToken?)null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await Handler().Handle(Command(), Ct));

    _queued.ShouldBeEmpty();
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_uploads).MustNotHaveHappened();
  }

  // The owner of the address is not an active organization: the same refusal, which says nothing of it.
  [Fact]
  public async Task Handle_OwnerThatIsNotAnActiveOrganization_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);

    await Should.ThrowAsync<UnauthorizedException>(async () => await Handler().Handle(Command(), Ct));

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_TokenOfAnotherRepository_ShouldThrowForbidden()
  {
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Repository, 6, null));

    await Should.ThrowAsync<ForbiddenException>(async () => await Handler().Handle(Command(), Ct));

    _queued.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFound()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    (await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct))).ErrorCode.ShouldBe(CommitErrors.NotFound);
    _queued.ShouldBeEmpty();
  }

  // A repository that is not tracked is never created by this call, and the forge is never asked.
  [Fact]
  public async Task Handle_RepositoryNotTracked_ShouldThrowNotFoundWithoutCallingTheForge()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    A.CallTo(_gateways).MustNotHaveHappened();
    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("not-a-sha")]
  [InlineData("abc1234")]
  [InlineData("")]
  public async Task Handle_ShaThatIsNotAFullSha_ShouldThrowNotFoundBeforeAnyReading(string sha)
  {
    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(sha: sha), Ct));

    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_commits).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("", "app")]
  [InlineData("octo-org", "")]
  [InlineData("octo org", "app")]
  public async Task Handle_AddressThatIsNotARepository_ShouldThrowNotFound(string owner, string name)
  {
    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(owner, name), Ct));
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Handler().Handle(null!, Ct));
  }

  private static CompleteUploadsCommand Command(string owner = "octo-org", string name = "app", string? sha = null) =>
    new(new UploadCredential(UploadCredentialKind.UploadToken, Value), Forge.GitHub, owner, name, sha ?? Sha.Value);

  private void Uploads(params UploadState[] states)
  {
    var uploads = states.Select(
                          static (state, index) =>
                          {
                            var upload = Upload.Receive(9, index + 1, $"key-{index}", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

                            if (state is UploadState.Processing)
                            {
                              // Taken by a worker: the transition is a guarded update of the repository, not a method.
                              typeof(Upload).GetProperty(nameof(Upload.State))!.SetValue(upload, UploadState.Processing);
                            }
                            else if (state is UploadState.Processed)
                            {
                              upload.MarkProcessed(1, 1, 0, default, ProcessingLog.Empty, Now);
                            }
                            else if (state is UploadState.Failed)
                            {
                              upload.MarkFailed("Upload.ParseFailed", "unreadable", 1, 0, 0, ProcessingLog.Empty, Now);
                            }

                            return upload;
                          }
                        )
                        .ToList();

    A.CallTo(() => _uploads.ListByCommitAsync(9, A<CancellationToken>._)).Returns(uploads);
  }

  private CompleteUploadsCommandHandler Handler()
  {
    var hasher = A.Fake<ITokenHasher>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => hasher.Hash(A<string>._)).Returns(Hash);
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var authenticator = new UploadAuthenticator(_organization, A.Fake<IOrganizationDirectory>(), A.Fake<IOidcTokenValidator>(), _tokens, _cache, _repositories, _gateways, hasher, clock);

    return new CompleteUploadsCommandHandler(authenticator, _commits, _uploads, _jobQueue);
  }

  #endregion
}