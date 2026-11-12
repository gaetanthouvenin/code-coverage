// ------------------------------------------------------------------------------------------------
// <copyright file="GetEffectiveConfigQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.GetEffectiveConfig;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetEffectiveConfig;

public sealed class GetEffectiveConfigQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly ICoverageConfigParser _parser = A.Fake<ICoverageConfigParser>();

  #endregion

  #region Ctors

  public GetEffectiveConfigQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var commit = Commit.Create(5, Head, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);
    var branch = Branch.Create(5, "main", Head, Now);
    branch.Cover(9, Now);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _parser.Parse(A<string>._)).Returns(new ConfigParseResult(ConfigNode.FromMap(), null, []));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ConfigKeptForTheCoveredHead_ShouldServeItAtThatSha()
  {
    A.CallTo(() => _configs.FindAsync(5, Head, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Head, ConfigSource.Commit, "codecov.yml", "coverage: {}", [], [], Now));

    var effective = await Handler().Handle(Query(), Ct);

    effective.IsResolvedAtSha.ShouldBeTrue();
    effective.Sha.ShouldBe(Head);
    effective.Source.ShouldBe(ConfigSource.Commit);
    A.CallTo(_gateways).MustNotHaveHappened();
  }

  // Critical rule 3: nothing kept is no reason to call the forge or to write inside a read.
  [Fact]
  public async Task Handle_NothingKept_ShouldNotCallTheForgeNorWrite()
  {
    var effective = await Handler().Handle(Query(), Ct);

    effective.IsResolvedAtSha.ShouldBeFalse();
    effective.Source.ShouldBe(ConfigSource.Builtin);
    A.CallTo(_gateways).MustNotHaveHappened();
    A.CallTo(() => _configs.AddAsync(A<CommitConfig>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_DefaultBranchNeverCovered_ShouldNotBeResolvedAtASha()
  {
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns((Branch?)null);

    var effective = await Handler().Handle(Query(), Ct);

    effective.IsResolvedAtSha.ShouldBeFalse();
    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static GetEffectiveConfigQuery Query() => new(Forge.GitHub, "octo-org", "app");

  private GetEffectiveConfigQueryHandler Handler()
  {
    var settings = A.Fake<IOrganizationSettingsProvider>();
    A.CallTo(() => settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    var resolver = new ConfigResolver(_configs, _gateways, _parser, settings, A.Fake<IClock>());

    return new GetEffectiveConfigQueryHandler(new CommitLocator(_repositories, _commits), _branches, _commits, _configs, resolver);
  }

  #endregion
}
