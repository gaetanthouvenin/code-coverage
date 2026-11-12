// ------------------------------------------------------------------------------------------------
// <copyright file="GetBadgeQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;
using CodeCoverage.Application.Features.Repositories.Queries.GetBadge;
using CodeCoverage.Domain.Badges;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetBadge;

public sealed class GetBadgeQueryHandlerTests
{
  #region Fields

  private const string BadgeToken = "graphing01";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly ICoverageConfigParser _parser = A.Fake<ICoverageConfigParser>();

  private readonly GetBadgeQueryHandler _handler;

  private TrackedRepository _repository;

  #endregion

  #region Ctors

  public GetBadgeQueryHandlerTests()
  {
    var hasher = A.Fake<ITokenHasher>();

    A.CallTo(() => hasher.Hash(A<string>._)).ReturnsLazily((string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    A.CallTo(() => hasher.FixedTimeEquals(A<byte[]>._, A<byte[]>._)).ReturnsLazily((byte[] left, byte[] right) => CryptographicOperations.FixedTimeEquals(left, right));
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _protector.Unprotect("protected")).Returns(BadgeToken);
    _repository = Repository(isPrivate: true);

    var commit = Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);
    typeof(Commit).GetProperty(nameof(Commit.CompletedAt))!.SetValue(commit, (DateTimeOffset?)Now);
    var branch = Branch.Create(5, "main", commit.Sha, Now);
    typeof(Branch).GetProperty(nameof(Branch.CoveredCommitId))!.SetValue(branch, 9L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).ReturnsLazily(() => _repository);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    A.CallTo(() => _reports.ListFlagTotalsAsync(9, A<CancellationToken>._))
     .Returns([
       CommitFlagTotals.Create(commit, CommitFlagTotals.ProjectFlag, Totals(85, 100), false, null),
       CommitFlagTotals.Create(commit, "unit", Totals(50, 100), false, null),
     ]);

    _handler = new GetBadgeQueryHandler(_organization, _repositories, _branches, _reports, _protector, hasher, _parser);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_PrivateWithItsToken_ShouldServeTheCoveredValue()
  {
    var badge = await HandleAsync(token: BadgeToken);

    badge.Coverage.ShouldBe(85m);
    badge.Color.ShouldBe(BadgeColorScale.Color(85m, null));
  }

  // A wrong token on a private repository answers like an unknown one: never a 404 nor a 403.
  [Theory]
  [InlineData("wrong")]
  [InlineData("")]
  [InlineData(null)]
  public async Task Handle_WrongToken_ShouldReturnUnknownNotThrow(string? token)
  {
    (await HandleAsync(token: token)).ShouldBeSameAs(Badge.Unknown);
  }

  [Fact]
  public async Task Handle_PublicRepository_ShouldIgnoreTheToken()
  {
    _repository = Repository(isPrivate: false);

    (await HandleAsync(token: "anything")).Coverage.ShouldBe(85m);
  }

  [Fact]
  public async Task Handle_Flag_ShouldServeItsValueCaseSensitively()
  {
    (await HandleAsync(token: BadgeToken, flag: "unit")).Coverage.ShouldBe(50m);
    (await HandleAsync(token: BadgeToken, flag: "Unit")).ShouldBeSameAs(Badge.Unknown);
  }

  // Decision 23: an inactive repository still serves its last covered value, as Codecov.
  [Fact]
  public async Task Handle_InactiveRepository_ShouldServeItsLastValue()
  {
    _repository = Repository(isPrivate: false);
    _repository.Deactivate(Now);

    (await HandleAsync(token: null)).Coverage.ShouldBe(85m);
  }

  [Fact]
  public async Task Handle_UnknownOrganizationRepositoryOrBranch_ShouldReturnUnknown()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);
    (await HandleAsync(token: BadgeToken)).ShouldBeSameAs(Badge.Unknown);
    A.CallTo(_repositories).MustNotHaveHappened();

    A.CallTo(() => _organization.IsKnown).Returns(true);
    (await HandleAsync(token: BadgeToken, branch: "missing")).ShouldBeSameAs(Badge.Unknown);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    (await HandleAsync(token: BadgeToken)).ShouldBeSameAs(Badge.Unknown);
  }

  [Fact]
  public async Task Handle_LostKeyRing_ShouldReturnUnknownNotThrow()
  {
    A.CallTo(() => _protector.Unprotect(A<string>._)).Throws(new CryptographicException("key ring"));

    (await HandleAsync(token: BadgeToken)).ShouldBeSameAs(Badge.Unknown);
  }

  // Decision 25: coverage.range only when the raw YAML of the default branch names it.
  [Fact]
  public async Task Handle_RangeInTheRawYaml_ShouldColorOverIt()
  {
    _repository = Repository(isPrivate: false);
    _repository.RefreshDefaultBranchYaml("coverage:\n  range: 80..95\n", GitSha.Parse(new string('b', GitSha.Length)), Now);
    var root = ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("range", ConfigNode.FromText("80..95")))));
    A.CallTo(() => _parser.Parse(A<string>._)).Returns(new ConfigParseResult(root, null, []));

    var badge = await HandleAsync(token: null);

    badge.Color.ShouldBe(BadgeColorScale.Color(85m, CoverageRange.Read(ConfigNode.FromText("80..95"))));
    badge.Color.ShouldNotBe(BadgeColorScale.Color(85m, null));
  }

  [Theory]
  [InlineData("abcdEFGH09", true)]
  [InlineData("with_under-score", true)]
  [InlineData("", false)]
  [InlineData("has space", false)]
  [InlineData("café", false)]
  public void ImportValidator_ShouldAcceptOnlyTheCharactersOfCodecov(string value, bool valid)
  {
    var result = new ImportBadgeTokenCommandValidator().Validate(new ImportBadgeTokenCommand(Forge.GitHub, "octo-org", "app", value));

    result.IsValid.ShouldBe(valid);

    if (!valid)
    {
      result.Errors.ShouldAllBe(e => e.ErrorCode == RepositoryErrors.InvalidBadgeToken);
    }
  }

  [Fact]
  public void ImportValidator_TooLong_ShouldRefuse()
  {
    new ImportBadgeTokenCommandValidator().Validate(new ImportBadgeTokenCommand(Forge.GitHub, "octo-org", "app", new string('a', 65))).IsValid.ShouldBeFalse();
  }

  [Fact]
  public void ToString_ShouldNotContainTheTokens()
  {
    new GetBadgeQuery(Forge.GitHub, "octo-org", "app", null, null, BadgeToken).ToString().ShouldNotContain(BadgeToken);
    new ImportBadgeTokenCommand(Forge.GitHub, "octo-org", "app", BadgeToken).ToString().ShouldNotContain(BadgeToken);
  }

  private static CoverageTotals Totals(int hits, int lines) => new(hits, lines - hits, 0, 0, 0);

  private static TrackedRepository Repository(bool isPrivate)
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", isPrivate, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    return repository;
  }

  private async Task<Badge> HandleAsync(string? token, string? flag = null, string? branch = null) =>
    await _handler.Handle(new GetBadgeQuery(Forge.GitHub, "octo-org", "app", branch, flag, token), Ct);

  #endregion
}
