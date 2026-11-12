// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveUploadCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Uploads.Commands.ReceiveUpload;

public sealed class ReceiveUploadCommandHandlerTests
{
  #region Fields

  private const string AccountId = "1001";

  private const string RepositoryExternalId = "2002";

  private const string Key = "the-key";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly RepositorySlug Slug = RepositorySlug.Parse("octo-org/app");

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ITokenHasher _hasher = A.Fake<ITokenHasher>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IInstallationRepository _installations = A.Fake<IInstallationRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRawUploadStore _raw = A.Fake<IRawUploadStore>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ITokenGenerator _generator = A.Fake<ITokenGenerator>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IOptionsMonitor<UploadOptions> _options = A.Fake<IOptionsMonitor<UploadOptions>>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public ReceiveUploadCommandHandlerTests()
  {
    _commit = Commit.Create(1, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    var organization = Organization.CreatePending(Forge.GitHub, AccountId, "octo-org", OrganizationAccountType.Organization, Now);
    typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, 3L);

    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _hasher.Hash(A<string>._)).Returns(Hash);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(null);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._)).Returns(organization);
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _options.CurrentValue).Returns(new UploadOptions { MaxFilesPerUpload = 2 });
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._))
     .Returns(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", Hash, false, "octocat", Now, null));
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _repositories.FindByExternalIdAsync(A<string>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _installations.FindAsync(A<long>._, A<CancellationToken>._)).Returns((Installation?)null);
    A.CallTo(() => _commits.GetOrCreateAsync(A<long>._, Sha, A<DateTimeOffset>._, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _commits.NextUploadSequenceAsync(9, A<CancellationToken>._)).Returns(4);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).Returns(new StoredRawFile(1, Hash, 1, 1));
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _protector.Protect(A<string>._)).Returns("protected");
    A.CallTo(() => _generator.NewBadgeToken()).Returns("badge");
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_TrackedRepo_ShouldStoreThenAddWithItsJob()
  {
    Tracked(true);

    var received = await Handler().Handle(Command(), Ct);

    received.IsReplay.ShouldBeFalse();
    received.Upload.Sequence.ShouldBe(4);
    received.Upload.IdempotencyKey.ShouldBe(Key);
    received.Upload.PrincipalKind.ShouldBe(PrincipalKind.OrganizationToken);
    _commit.State.ShouldBe(CommitState.Pending);
    A.CallTo(() => _raw.WriteAsync(received.Upload.PublicId, A<UploadPart>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(received.Upload, A<JobRequest>.That.Matches(j => j.Type == JobTypes.ProcessUpload && j.PayloadJson.Contains(received.Upload.PublicId.ToString())), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(_gateways).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownRepoWithInstallation_ShouldCreateTrackedRepository()
  {
    Connected(new ForgeConnectionInfo("77", AccountId, RepositoryExternalId));
    Reports();

    await Handler().Handle(Command(), Ct);

    A.CallTo(() => _installations.AddAsync(A<Installation>.That.Matches(i => i.Id == 77 && i.OrganizationId == 3), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repositories.AddAsync(
               A<TrackedRepository>.That.Matches(r => r.ExternalId == RepositoryExternalId && r.IsActive && r.InstallationId == 77 && r.BadgeTokenProtected == "protected"),
               A<CancellationToken>._
             ))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NoInstallation_ShouldThrowNotFound()
  {
    Connected(null);

    await ShouldRefuse<NotFoundException>(RepositoryErrors.NotFound);

    A.CallTo(() => _repositories.AddAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InstallationOfOtherAccount_ShouldThrowNotFound()
  {
    Connected(new ForgeConnectionInfo("77", "9999", RepositoryExternalId));

    await ShouldRefuse<NotFoundException>(RepositoryErrors.NotFound);

    A.CallTo(() => _installations.AddAsync(A<Installation>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InstallationHeldByOtherOrganization_ShouldThrowNotFound()
  {
    Connected(new ForgeConnectionInfo("77", AccountId, RepositoryExternalId));
    Reports();
    A.CallTo(() => _installations.FindAsync(77, A<CancellationToken>._)).Returns(Installation.Create(77, 8, Now));

    await ShouldRefuse<NotFoundException>(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task Handle_InactiveRepo_ShouldThrowUnprocessable()
  {
    Tracked(false);

    await ShouldRefuse<UnprocessableEntityException>(RepositoryErrors.Inactive);

    A.CallTo(() => _commits.GetOrCreateAsync(A<long>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The acceptance of M3.07: an upload to a repository an administrator disabled answers 422.
  [Fact]
  public async Task Handle_DisabledRepo_ShouldThrowUnprocessable()
  {
    Tracked(true).Disable(Now);

    await ShouldRefuse<UnprocessableEntityException>(RepositoryErrors.Inactive);

    A.CallTo(() => _commits.GetOrCreateAsync(A<long>._, A<GitSha>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_SameIdempotencyKey_ShouldReturnExistingAndDeleteOrphanRaw()
  {
    Tracked(true);
    var existing = Upload.Receive(9, 1, Key, [], null, null, null, null, null, null, null, null, PrincipalKind.OrganizationToken, Now);
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._)).Returns(false);
    A.CallTo(() => _uploads.FindByIdempotencyKeyAsync(9, Key, A<CancellationToken>._)).Returns(existing);

    var received = await Handler().Handle(Command(), Ct);

    received.IsReplay.ShouldBeTrue();
    received.Upload.ShouldBeSameAs(existing);
    A.CallTo(() => _raw.DeleteAsync(existing.PublicId, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NoIdempotencyKey_ShouldComputeTheSameKeyForTheSameContent()
  {
    Tracked(true);

    var first = await Handler().Handle(Command(key: null), Ct);
    var second = await Handler().Handle(Command(key: null), Ct);

    first.Upload.IdempotencyKey.ShouldNotBe(Key);
    first.Upload.IdempotencyKey.Length.ShouldBe(64);
    second.Upload.IdempotencyKey.ShouldBe(first.Upload.IdempotencyKey);
  }

  [Fact]
  public async Task Handle_OverLimit_ShouldThrow413AndDeleteTheParts()
  {
    Tracked(true);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).Throws(new UploadTooLargeException("over"));

    await ShouldRefuse<PayloadTooLargeException>(UploadErrors.TooLarge);

    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _reports.TryAddReceivedUploadAsync(A<Upload>._, A<JobRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TooManyReports_ShouldThrowBadRequest()
  {
    Tracked(true);

    var exception = await Should.ThrowAsync<BadRequestException>(async () => await Handler().Handle(Command(parts: 3), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.TooManyFiles);
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_NoReport_ShouldThrowBadRequest()
  {
    Tracked(true);

    var exception = await Should.ThrowAsync<BadRequestException>(async () => await Handler().Handle(Command(parts: 0), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.NoReport);
  }

  // ARCHITECTURE §5.1: network is one optional part; a second one is refused before it is written.
  [Fact]
  public async Task Handle_SecondNetworkPart_ShouldThrowBadRequestBeforeWritingIt()
  {
    Tracked(true);

    var exception = await Should.ThrowAsync<BadRequestException>(
      async () => await Handler().Handle(CommandWith(Report(), Network(), Network()), Ct)
    );

    exception.ErrorCode.ShouldBe(UploadErrors.InvalidPart);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_FileNameTooLong_ShouldThrowBadRequestBeforeWritingIt()
  {
    Tracked(true);
    var part = new UploadPart(UploadPartKind.Report, new string('a', UploadPart.FileNameMaxLength + 1), new MemoryStream([1, 2, 3]));

    var exception = await Should.ThrowAsync<BadRequestException>(async () => await Handler().Handle(CommandWith(part), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.InvalidPart);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_CorruptedGzip_ShouldThrowBadRequestAndDeleteTheParts()
  {
    Tracked(true);
    A.CallTo(() => _raw.WriteAsync(A<Guid>._, A<UploadPart>._, A<CancellationToken>._)).Throws(new InvalidDataException("corrupted"));

    var exception = await Should.ThrowAsync<BadRequestException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.InvalidPart);
    A.CallTo(() => _raw.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // An outdated token is refused like any other credential, before anything is written.
  [Fact]
  public async Task Handle_InvalidCredential_ShouldWriteNothing()
  {
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns((UploadToken?)null);

    await ShouldRefuse<UnauthorizedException>(UploadErrors.TokenInvalid);

    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_raw).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("bad'flag")]
  [InlineData("")]
  public void Validator_InvalidFlag_ShouldFail(string flag)
  {
    var command = Command() with { Metadata = Metadata() with { Flags = [flag] } };

    new ReceiveUploadCommandValidator().Validate(command).Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(UploadErrors.InvalidMetadata);
  }

  [Fact]
  public void Validator_ValidCommand_ShouldPass()
  {
    new ReceiveUploadCommandValidator().Validate(Command()).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void ToString_ShouldNotExposeTheCredential()
  {
    Command().ToString().ShouldNotContain("cvc_abcdefghijklmnopqrstuvwxyz");
  }

  private static UploadMetadata Metadata() =>
    new(Slug, Sha, "main", 12, ["back"], "run", "/repo", "1.0", new CiContext("github-actions", null, "1", "2"));

  private static ReceiveUploadCommand Command(string? key = Key, int parts = 1) =>
    new(new UploadCredential(UploadCredentialKind.UploadToken, "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG"), Metadata(), Parts(parts), key);

  private static ReceiveUploadCommand CommandWith(params UploadPart[] parts) => Command() with { Parts = Listed(parts) };

  private static async IAsyncEnumerable<UploadPart> Listed(UploadPart[] parts)
  {
    await Task.CompletedTask;

    foreach (var part in parts)
    {
      yield return part;
    }
  }

  private static UploadPart Report() => new(UploadPartKind.Report, "coverage.xml", new MemoryStream([1, 2, 3]));

  private static UploadPart Network() => new(UploadPartKind.Network, "network.txt", new MemoryStream("src/A.cs"u8.ToArray()));

  private static async IAsyncEnumerable<UploadPart> Parts(int count)
  {
    await Task.CompletedTask;

    for (var index = 0; index < count; index++)
    {
      yield return new UploadPart(UploadPartKind.Report, "coverage.xml", new MemoryStream([1, 2, 3]));
    }
  }

  private ReceiveUploadCommandHandler Handler()
  {
    var authenticator = new UploadAuthenticator(_organization, _directory, A.Fake<IOidcTokenValidator>(), _tokens, _cache, _repositories, _gateways, _hasher, _clock);

    return new ReceiveUploadCommandHandler(authenticator, _repositories, _directory, _installations, _gateways, _commits, _raw, _uploads, _reports, _generator, _protector, _options, _clock);
  }

  private async Task ShouldRefuse<TException>(string errorCode)
    where TException : Exception
  {
    var exception = await Should.ThrowAsync<TException>(async () => await Handler().Handle(Command(), Ct));

    ((CodeCoverage.Application.Exceptions.ApplicationException)(object)exception).ErrorCode.ShouldBe(errorCode);
  }

  private TrackedRepository Tracked(bool active)
  {
    var repository = TrackedRepository.Create(1, RepositoryExternalId, Slug, "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    if (!active)
    {
      typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.IsActive))!.SetValue(repository, false);
    }

    A.CallTo(() => _repositories.FindBySlugAsync(Slug, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);

    return repository;
  }

  private void Connected(ForgeConnectionInfo? connection) =>
    A.CallTo(() => _gateway.GetRepositoryConnectionAsync("octo-org", "app", A<CancellationToken>._)).Returns(connection);

  private void Reports() =>
    A.CallTo(() => _gateway.GetRepositoryAsync(A<ForgeRepositoryRef>._, A<CancellationToken>._))
     .Returns(new ForgeRepositoryInfo(RepositoryExternalId, "octo-org", string.Empty, "app", "main", true));

  #endregion
}
