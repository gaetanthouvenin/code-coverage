// ------------------------------------------------------------------------------------------------
// <copyright file="GetRepositorySettingsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Repositories.Queries.GetRepositorySettings;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Repositories.Queries.GetRepositorySettings;

public sealed class GetRepositorySettingsQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly ICoverageConfigParser _parser = A.Fake<ICoverageConfigParser>();

  private readonly TrackedRepository _repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);

  #endregion

  #region Ctors

  public GetRepositorySettingsQueryHandlerTests()
  {
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, 5L);
    var commit = Commit.Create(5, Head, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 9L);
    var branch = Branch.Create(5, "main", Head, Now);
    branch.Cover(9, Now);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(commit);
    A.CallTo(() => _parser.Parse(A<string>._)).Returns(new ConfigParseResult(ConfigNode.FromMap(), null, []));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The page shows the YAML as it is written, at the commit it was resolved at.
  [Fact]
  public async Task Handle_ShouldGiveTheRepositoryAndTheConfigurationOfItsCoveredHead()
  {
    A.CallTo(() => _configs.FindAsync(5, Head, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Head, ConfigSource.Commit, "codecov.yml", "coverage: {}", [], [], Now));

    var settings = await Handler().Handle(Query(), Ct);

    settings.Repository.ShouldBeSameAs(_repository);
    settings.Config.Source.ShouldBe(ConfigSource.Commit);
    settings.Config.Sha.ShouldBe(Head);
    settings.Config.Yaml.ShouldBe("coverage: {}");
  }

  // The state of the repository is what the switch shows: disabling it never hides it.
  [Fact]
  public async Task Handle_DisabledRepository_ShouldGiveItWithTheMomentItWasDisabled()
  {
    _repository.Disable(Now);

    var settings = await Handler().Handle(Query(), Ct);

    settings.Repository.DisabledAt.ShouldBe(Now);
  }

  // Critical rule 3: a read calls no forge and writes nothing.
  [Fact]
  public async Task Handle_ShouldNotCallTheForgeNorWrite()
  {
    var settings = await Handler().Handle(Query(), Ct);

    settings.Config.Source.ShouldBe(ConfigSource.Builtin);
    A.CallTo(_gateways).MustNotHaveHappened();
    A.CallTo(() => _configs.AddAsync(A<CommitConfig>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Only the administrators of the repository, and of its organization, read the settings.
  [Fact]
  public void Query_ShouldRequireTheAdministrationOfTheRepository() => Query().RequiredPermission.ShouldBe(RepositoryPermission.Admin);

  private static GetRepositorySettingsQuery Query() => new(Forge.GitHub, "octo-org", "app");

  private GetRepositorySettingsQueryHandler Handler()
  {
    var settings = A.Fake<IOrganizationSettingsProvider>();
    A.CallTo(() => settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, null));
    var resolver = new ConfigResolver(_configs, _gateways, _parser, settings, A.Fake<IClock>());

    return new GetRepositorySettingsQueryHandler(new CommitLocator(_repositories, _commits), _branches, _commits, _configs, resolver);
  }

  #endregion
}
