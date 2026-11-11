// ------------------------------------------------------------------------------------------------
// <copyright file="AdminCommandLineTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Serilog.Core;

using CodeCoverage.Api.Configuration.Administration;
using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Administration;

// The command line over the real host: the mediator pipeline, the handlers and SystemCommandRunner
// are the real ones, only the ports that reach the database are faked.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class AdminCommandLineTests
{
  #region Fields

  private const string Owner = "octo-org";

  private const string Imported = "0f8a4c1e-7d2b-4e6f-9a3c-5b1d2e3f4a5b";

  private const long OrganizationId = 7;

  private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IAuditLog _audit = A.Fake<IAuditLog>();

  private readonly IJobQueue _jobQueue = A.Fake<IJobQueue>();

  private readonly CapturingLogSink _log = new();

  private readonly StringWriter _output = new();

  private readonly StringWriter _error = new();

  private readonly List<UploadToken> _added = [];

  private readonly List<(long? Organization, string Actor)> _auditedFrom = [];

  #endregion

  #region Ctors

  public AdminCommandLineTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, Owner, A<CancellationToken>._)).Returns(Organization(Owner, OrganizationId, active: true));
    A.CallTo(() => _tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._))
     .Invokes((UploadToken token, CancellationToken _) =>
       {
         SetId(token, 12L);
         _added.Add(token);
       })
     .Returns(true);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Run_CreateUploadToken_ShouldPrintValueOnce()
  {
    var exit = await RunAsync("", "admin", "create-upload-token", "--owner", Owner, "--scope", "organization", "--label", "ci");

    exit.ShouldBe(AdminCommandLine.Success);

    var token = _added.ShouldHaveSingleItem();
    var value = _output.ToString().TrimEnd();

    value.ShouldStartWith("cvc_");
    _output.ToString().ShouldBe(value + Environment.NewLine, "the standard output holds the value and nothing else");
    _error.ToString().ShouldNotContain(value);
    _error.ToString().ShouldContain("Upload token 12 created for github/octo-org");
    _log.Events.ShouldNotContain(e => e.Contains(value, StringComparison.Ordinal));

    token.Scope.ShouldBe(UploadTokenScope.Organization);
    token.RepositoryId.ShouldBeNull();
    token.Label.ShouldBe("ci");
    token.Imported.ShouldBeFalse();
    token.Hint.ShouldBe(value[..8]);
  }

  [Fact]
  public async Task Run_CreateUploadToken_ShouldRunInTheOwnerOrganizationAsTheCommandLine()
  {
    await RunAsync("", "admin", "create-upload-token", "--owner", Owner, "--scope", "organization", "--label", "ci");

    _added.ShouldHaveSingleItem().CreatedBy.ShouldBe(AuditEntry.CommandLineActor);
    _auditedFrom.ShouldBe([(OrganizationId, AuditEntry.CommandLineActor)]);
    AuditEntry.CommandLineActor.ShouldNotBe(AuditEntry.SystemActor);
  }

  [Fact]
  public async Task Run_CreateRepositoryToken_ShouldSendTheRepositoryMessage()
  {
    var repository = TrackedRepository.Create(OrganizationId, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    SetId(repository, 5L);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(repository);

    var exit = await RunAsync("", "admin", "create-upload-token", "--owner", Owner, "--scope", "repository", "--repository", "app", "--label", "ci");

    exit.ShouldBe(AdminCommandLine.Success);

    var token = _added.ShouldHaveSingleItem();

    token.Scope.ShouldBe(UploadTokenScope.Repository);
    token.RepositoryId.ShouldBe(5L);
    _error.ToString().ShouldContain("github/octo-org/app");
  }

  [Fact]
  public async Task Run_CreateRepositoryToken_UnknownRepository_ShouldFail()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    var exit = await RunAsync("", "admin", "create-upload-token", "--owner", Owner, "--scope", "repository", "--repository", "nope", "--label", "ci");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("Repository.NotFound");
    _added.ShouldBeEmpty();
    _output.ToString().ShouldBeEmpty();
  }

  [Fact]
  public async Task Run_ImportFromStdin_ShouldNotEchoValue()
  {
    var exit = await RunAsync(
                 Imported + "\n",
                 "admin",
                 "create-upload-token",
                 "--owner",
                 Owner,
                 "--scope",
                 "organization",
                 "--label",
                 "codecov",
                 "--import-value-stdin"
               );

    exit.ShouldBe(AdminCommandLine.Success);

    var token = _added.ShouldHaveSingleItem();

    token.Imported.ShouldBeTrue();
    token.Hint.ShouldBe(Imported[^4..]);

    _output.ToString().ShouldBeEmpty("an imported value is never written back");
    _error.ToString().ShouldNotContain(Imported);
    _error.ToString().ShouldContain("Upload token 12 imported for github/octo-org");
    _log.Events.ShouldNotBeEmpty("the pipeline logs the command");
    _log.Events.ShouldNotContain(e => e.Contains(Imported, StringComparison.Ordinal));
  }

  [Fact]
  public async Task Run_ImportFromStdin_InvalidValue_ShouldFailWithoutEcho()
  {
    const string notAUuid = "not-a-codecov-token-51f3";

    var exit = await RunAsync(notAUuid, "admin", "create-upload-token", "--owner", Owner, "--scope", "organization", "--label", "x", "--import-value-stdin");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("An imported token is a Codecov token");
    Everything().ShouldNotContain(notAUuid);
    _added.ShouldBeEmpty();
  }

  [Fact]
  public async Task Run_ImportFromStdin_AlreadyImported_ShouldFail()
  {
    A.CallTo(() => _tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._)).Returns(false);

    var exit = await RunAsync(Imported, "admin", "create-upload-token", "--owner", Owner, "--scope", "organization", "--label", "x", "--import-value-stdin");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("UploadToken.AlreadyImported");
    Everything().ShouldNotContain(Imported);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   \n")]
  public async Task Run_ImportFromStdin_NoValue_ShouldRefuseTheUsage(string input)
  {
    var exit = await RunWithoutServicesAsync(
                 input,
                 "admin",
                 "create-upload-token",
                 "--owner",
                 Owner,
                 "--scope",
                 "organization",
                 "--label",
                 "x",
                 "--import-value-stdin"
               );

    exit.ShouldBe(AdminCommandLine.InvalidUsage);
  }

  [Theory]
  [InlineData("--import-value")]
  [InlineData("--value")]
  [InlineData("--token")]
  public async Task Run_ValueGivenAsAnArgument_ShouldRefuseWithoutEcho(string option)
  {
    var exit = await RunWithoutServicesAsync("", "admin", "create-upload-token", "--owner", Owner, "--scope", "organization", "--label", "x", option, Imported);

    exit.ShouldBe(AdminCommandLine.InvalidUsage);
    Everything().ShouldNotContain(Imported);
  }

  [Fact]
  public async Task Run_AdmitOrganization_ShouldCreateActiveOrganization()
  {
    var created = Organization("new-org", 9, active: false);

    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "3003", A<CancellationToken>._)).Returns((Organization?)null);
    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.GitHub, "3003", "new-org", OrganizationAccountType.Organization, A<CancellationToken>._))
     .Returns(created);
    A.CallTo(() => _directory.ChangeAsync(Forge.GitHub, created.ExternalAccountId, A<Action<Organization>>._, A<CancellationToken>._))
     .ReturnsLazily((Forge _, string _, Action<Organization> change, CancellationToken _) =>
       {
         change(created);
         return created;
       });

    var exit = await RunAsync("", "admin", "admit-organization", "--login", "new-org", "--account-id", "3003");

    exit.ShouldBe(AdminCommandLine.Success);
    created.State.ShouldBe(OrganizationState.Active);
    created.AdmittedBy.ShouldBe(AuditEntry.CommandLineActor);
    _auditedFrom.ShouldBe([((long?)null, AuditEntry.CommandLineActor)], "an admission enters no organization");
    _error.ToString().ShouldContain("Organization github/new-org is Active");
    _output.ToString().ShouldBeEmpty();
    // This process runs no worker: the job waits for the Api, which lists the installations of the App.
    A.CallTo(
       () => _jobQueue.EnqueueAsync(A<JobRequest>.That.Matches(j => j.Type == JobTypes.SyncInstallations && j.IsInstance), A<CancellationToken>._)
     )
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Run_AdmitOrganization_OtherForge_ShouldAdmitOnThatForge()
  {
    var created = Organization("team", 9, active: false, Forge.Bitbucket);

    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.Bitbucket, "{abc}", A<CancellationToken>._)).Returns((Organization?)null);
    A.CallTo(() => _directory.UpsertFromForgeAsync(Forge.Bitbucket, "{abc}", "team", A<OrganizationAccountType>._, A<CancellationToken>._)).Returns(created);
    A.CallTo(() => _directory.ChangeAsync(Forge.Bitbucket, A<string>._, A<Action<Organization>>._, A<CancellationToken>._))
     .ReturnsLazily((Forge _, string _, Action<Organization> change, CancellationToken _) =>
       {
         change(created);
         return created;
       });

    var exit = await RunAsync("", "admin", "admit-organization", "--forge", "bitbucket", "--login", "team", "--account-id", "{abc}");

    exit.ShouldBe(AdminCommandLine.Success);
    _error.ToString().ShouldContain("Organization bitbucket/team is Active");
    A.CallTo(_jobQueue).MustNotHaveHappened();
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Run_AdmitOrganization_AlreadyActive_ShouldFail()
  {
    A.CallTo(() => _directory.FindByAccountIdAsync(Forge.GitHub, "1001", A<CancellationToken>._)).Returns(Organization(Owner, OrganizationId, active: true));

    var exit = await RunAsync("", "admin", "admit-organization", "--login", Owner, "--account-id", "1001");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("Organization.NotPending");
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Run_OwnerNotActive_ShouldFail(bool pending)
  {
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, "other", A<CancellationToken>._))
     .Returns(pending ? Organization("other", 8, active: false) : null);

    var exit = await RunAsync("", "admin", "create-upload-token", "--owner", "other", "--scope", "organization", "--label", "ci");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("Organization.NotFound");
    _output.ToString().ShouldBeEmpty();
    A.CallTo(_tokens).MustNotHaveHappened();
    A.CallTo(_audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Run_OwnerOfAnotherForge_ShouldFail()
  {
    // The login is active on GitHub only: the same login on another forge is another account (ADR-014).
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitLab, Owner, A<CancellationToken>._)).Returns((Organization?)null);

    var exit = await RunAsync("", "admin", "create-upload-token", "--forge", "gitlab", "--owner", Owner, "--scope", "organization", "--label", "ci");

    exit.ShouldBe(AdminCommandLine.Refused);
    A.CallTo(_tokens).MustNotHaveHappened();
  }

  [Fact]
  public async Task Run_RevokeUploadToken_ShouldRevokeIt()
  {
    var token = UploadToken.Create(UploadTokenScope.Organization, null, "ci", "cvc_abcd", new byte[UploadToken.HashLength], false, "someone", Now, null);
    SetId(token, 12L);
    A.CallTo(() => _tokens.FindAsync(12, A<CancellationToken>._)).Returns(token);

    var exit = await RunAsync("", "admin", "revoke-upload-token", "--owner", Owner, "--id", "12");

    exit.ShouldBe(AdminCommandLine.Success);
    token.RevokedAt.ShouldNotBeNull();
    A.CallTo(() => _tokens.UpdateAsync(token, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    _auditedFrom.ShouldBe([(OrganizationId, AuditEntry.CommandLineActor)]);
    _error.ToString().ShouldContain("Upload token 12 of github/octo-org is revoked");
  }

  [Fact]
  public async Task Run_RevokeUploadToken_NotInTheOwnerOrganization_ShouldFail()
  {
    // The repository only reads the organization of the scope: the token of another one is not found.
    A.CallTo(() => _tokens.FindAsync(99, A<CancellationToken>._)).Returns((UploadToken?)null);

    var exit = await RunAsync("", "admin", "revoke-upload-token", "--owner", Owner, "--id", "99");

    exit.ShouldBe(AdminCommandLine.Refused);
    _error.ToString().ShouldContain("UploadToken.NotFound");
    A.CallTo(() => _tokens.UpdateAsync(A<UploadToken>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Run_RevokeUploadToken_RepositoryTokenWithoutRepository_ShouldFail()
  {
    var token = UploadToken.Create(UploadTokenScope.Repository, 5, "ci", "cvc_abcd", new byte[UploadToken.HashLength], false, "someone", Now, null);
    A.CallTo(() => _tokens.FindAsync(12, A<CancellationToken>._)).Returns(token);

    var exit = await RunAsync("", "admin", "revoke-upload-token", "--owner", Owner, "--id", "12");

    exit.ShouldBe(AdminCommandLine.Refused);
    token.RevokedAt.ShouldBeNull();
  }

  [Theory]
  [InlineData("admin")]
  [InlineData("admin", "drop-everything")]
  [InlineData("admin", "create-upload-token", "--scope", "organization", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--scope", "organization")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--scope", "instance", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--scope", "repository", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--scope", "organization", "--repository", "app", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--scope", "organization", "--label")]
  [InlineData("admin", "create-upload-token", "--owner", "octo-org", "--owner", "octo-org", "--scope", "organization", "--label", "x")]
  [InlineData("admin", "create-upload-token", "--forge", "svn", "--owner", "octo-org", "--scope", "organization", "--label", "x")]
  [InlineData("admin", "revoke-upload-token", "--owner", "octo-org")]
  [InlineData("admin", "revoke-upload-token", "--owner", "octo-org", "--id", "twelve")]
  [InlineData("admin", "revoke-upload-token", "--owner", "octo-org", "--id", "-1")]
  [InlineData("admin", "revoke-upload-token", "--id", "12")]
  [InlineData("admin", "admit-organization", "--login", "new-org")]
  [InlineData("admin", "admit-organization", "--account-id", "3003")]
  [InlineData("admin", "admit-organization", "--forge", "svn", "--login", "new-org", "--account-id", "3003")]
  public async Task Run_InvalidUsage_ShouldPrintTheUsageAndRunNothing(params string[] arguments)
  {
    var exit = await RunWithoutServicesAsync("", arguments);

    exit.ShouldBe(AdminCommandLine.InvalidUsage);
    _error.ToString().ShouldContain("Usage:");
    _output.ToString().ShouldBeEmpty();
  }

  [Theory]
  [InlineData(true, "admin")]
  [InlineData(true, "admin", "admit-organization")]
  [InlineData(false)]
  [InlineData(false, "--urls", "http://localhost:5000")]
  [InlineData(false, "Admin")]
  [InlineData(false, "--owner", "admin")]
  public void IsRequested_ShouldReadTheFirstArgumentOnly(bool expected, params string[] arguments)
  {
    AdminCommandLine.IsRequested(arguments).ShouldBe(expected);
  }

  private static Organization Organization(string login, long id, bool active, Forge forge = Forge.GitHub)
  {
    var organization = Domain.Organizations.Organization.CreatePending(forge, "1001", login, OrganizationAccountType.Organization, Now);

    SetId(organization, id);

    if (active)
    {
      organization.Admit("someone", Now);
    }

    return organization;
  }

  private static void SetId(object entity, long id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);

  private string Everything() => string.Concat(_output.ToString(), _error.ToString(), string.Join(' ', _log.Events));

  // A refused usage runs nothing: with an empty container, resolving any service would throw.
  private async Task<int> RunWithoutServicesAsync(string input, params string[] arguments)
  {
    await using var services = new ServiceCollection().BuildServiceProvider();
    using var reader = new StringReader(input);
    var commandLine = new AdminCommandLine(services.GetRequiredService<IServiceScopeFactory>(), reader, _output, _error);

    return await commandLine.RunAsync(arguments, Ct);
  }

  private async Task<int> RunAsync(string input, params string[] arguments)
  {
    await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
      services.AddSingleton(_directory);
      services.AddSingleton(_tokens);
      services.AddSingleton(_repositories);
      services.AddSingleton(_jobQueue);
      services.AddSingleton<ILogEventSink>(_log);

      // The audit log of the scope, so that a test reads the organization and the actor it would write.
      services.AddScoped(sp =>
        {
          var user = sp.GetRequiredService<ICurrentUser>();
          var organization = sp.GetRequiredService<ICurrentOrganization>();

          A.CallTo(() => _audit.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
           .Invokes(() => _auditedFrom.Add((organization.IsKnown ? organization.Id : null, user.GetActorName())));

          return _audit;
        });
    }));

    using var reader = new StringReader(input);
    var commandLine = new AdminCommandLine(host.Services.GetRequiredService<IServiceScopeFactory>(), reader, _output, _error);

    return await commandLine.RunAsync(arguments, Ct);
  }

  #endregion
}
