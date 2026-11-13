// ------------------------------------------------------------------------------------------------
// <copyright file="TwoOrganizationApi.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Isolation;

// The world of the isolation suite: two organizations on one real database, each with a repository, and one user who
// belongs to the first one only. Everything is seeded through the ports of the host, as the product writes it.
public sealed class TwoOrganizationApi : IAsyncLifetime
{
  #region Fields

  /// <summary>The owner of the organization the user belongs to.</summary>
  public const string FirstOwner = "first-org";

  /// <summary>The owner of the organization the user knows nothing about.</summary>
  public const string SecondOwner = "second-org";

  /// <summary>An owner no organization ever had: the answer it receives is the one every outsider receives.</summary>
  public const string UnknownOwner = "unknown-org";

  /// <summary>The repository of each organization; the one of the second is private.</summary>
  public const string RepositoryName = "app";

  /// <summary>The personal token of the API of the user, bound to the first organization.</summary>
  public const string ApiTokenValue = "cvp_isolationisolationisolationisolation01";

  /// <summary>The login of the user of the suite.</summary>
  public const string UserLogin = "octocat";

  // The rows are stamped now: an access or a token older than Access:HardExpiryMinutes would be refused, and the
  // suite would prove nothing about the organizations.
  private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

  private readonly IsolationDatabase _database;

  private IsolationApiFactory? _factory;

  #endregion

  #region Ctors

  public TwoOrganizationApi(IsolationDatabase database)
  {
    _database = database;
  }

  #endregion

  #region Properties

  /// <summary>The identifier of the user of the suite.</summary>
  public long UserId { get; private set; }

  /// <summary>The SHA of the commit of the second organization, which the user must never reach.</summary>
  public GitSha SecondSha { get; } = GitSha.Parse("b" + new string('2', GitSha.Length - 1));

  /// <summary>The identifier of the upload of the second organization, which the user must never reach.</summary>
  public Guid SecondUpload { get; private set; }

  /// <summary>The badge token of the private repository of the second organization.</summary>
  public string SecondBadgeToken { get; private set; } = string.Empty;

  private IsolationApiFactory Factory => _factory ?? throw new InvalidOperationException("The host is not started.");

  #endregion

  #region Methods

  /// <summary>A client of the host, with the token of the user when asked.</summary>
  public HttpClient Client(bool authenticated = true)
  {
    var client = Factory.CreateClient();

    if (authenticated)
    {
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenValue);
    }

    return client;
  }

  /// <inheritdoc />
  public async ValueTask InitializeAsync()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var connectionString = await _database.ConnectionStringAsync(cancellationToken);
    _factory = new IsolationApiFactory(connectionString);

    // The first request migrates the database and settles the host.
    using (var client = Factory.CreateClient())
    {
      using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), cancellationToken);
    }

    await SeedAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync()
  {
    if (_factory is not null)
    {
      await _factory.DisposeAsync();
    }
  }

  private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

  // A scope enters one organization and no other (ADR-013), so each one is seeded in a scope of its own.
  private async Task SeedAsync(CancellationToken cancellationToken)
  {
    long firstId;
    long secondId;

    await using (var scope = Factory.Services.CreateAsyncScope())
    {
      var services = scope.ServiceProvider;
      var directory = services.GetRequiredService<IOrganizationDirectory>();

      firstId = (await AdmitAsync(directory, "1001", FirstOwner, cancellationToken)).Id;
      secondId = (await AdmitAsync(directory, "2002", SecondOwner, cancellationToken)).Id;

      var users = services.GetRequiredService<IUserRepository>();
      var user = AppUser.Create(Forge.GitHub, "583231", UserLogin, null);
      await users.TryAddAsync(user, cancellationToken);
      user = await users.FindByExternalIdAsync(Forge.GitHub, "583231", cancellationToken) ?? user;
      UserId = user.Id;

      var tokens = services.GetRequiredService<IApiTokenRepository>();
      await tokens.AddAsync(ApiToken.Create(UserId, firstId, "isolation", ApiTokenValue[..8], Hash(ApiTokenValue), Now, null), cancellationToken);
    }

    await using (var scope = Factory.Services.CreateAsyncScope())
    {
      await SeedFirstAsync(scope.ServiceProvider, firstId, cancellationToken);
    }

    await using (var scope = Factory.Services.CreateAsyncScope())
    {
      await SeedSecondAsync(scope.ServiceProvider, secondId, cancellationToken);
    }
  }

  private static async Task<Organization> AdmitAsync(IOrganizationDirectory directory, string accountId, string login, CancellationToken cancellationToken)
  {
    await directory.UpsertFromForgeAsync(Forge.GitHub, accountId, login, OrganizationAccountType.Organization, cancellationToken);

    return await directory.ChangeAsync(Forge.GitHub, accountId, organization => organization.Admit("isolation", Now), cancellationToken)
           ?? throw new InvalidOperationException($"The organization '{login}' was not admitted.");
  }

  // The user reads the repository of its own organization: the access row the forge mirrors says so.
  private async Task SeedFirstAsync(IServiceProvider services, long organizationId, CancellationToken cancellationToken)
  {
    await EnterAsync(services, FirstOwner, cancellationToken);

    var installationId = await InstallAsync(services, organizationId, cancellationToken);
    var repositories = services.GetRequiredService<IRepositoryRepository>();
    var repository = TrackedRepository.Create(
      installationId,
      "3001",
      RepositorySlug.Parse($"{FirstOwner}/{RepositoryName}"),
      "main",
      isPrivate: false,
      Protected(services, "badge-first"),
      Now);

    await repositories.AddAsync(repository, cancellationToken);

    var accesses = services.GetRequiredService<IUserRepositoryAccessRepository>();
    await accesses.UpsertAsync(UserId, "3001", RepositoryPermission.Admin, Now, cancellationToken);
  }

  // The second organization holds a private repository, a commit and an upload the user must never reach.
  private async Task SeedSecondAsync(IServiceProvider services, long organizationId, CancellationToken cancellationToken)
  {
    await EnterAsync(services, SecondOwner, cancellationToken);

    var installationId = await InstallAsync(services, organizationId, cancellationToken);
    var repositories = services.GetRequiredService<IRepositoryRepository>();
    SecondBadgeToken = "badge-second";
    var repository = TrackedRepository.Create(
      installationId,
      "3002",
      RepositorySlug.Parse($"{SecondOwner}/{RepositoryName}"),
      "main",
      isPrivate: true,
      Protected(services, SecondBadgeToken),
      Now);

    await repositories.AddAsync(repository, cancellationToken);

    var commits = services.GetRequiredService<ICommitRepository>();
    var commit = await commits.GetOrCreateAsync(repository.Id, SecondSha, Now, cancellationToken);
    commit.MarkPending("main", null, Now);
    await commits.UpdateAsync(commit, cancellationToken);

    var uploads = services.GetRequiredService<IUploadRepository>();
    var upload = Upload.Receive(commit.Id, 1, "isolation", ["unit"], null, null, null, null, null, null, null, null, PrincipalKind.RepositoryToken, Now);
    await uploads.AddAsync(upload, cancellationToken);
    SecondUpload = upload.PublicId;
  }

  // A repository belongs to the installation that serves it, which belongs to the organization.
  private static async Task<long> InstallAsync(IServiceProvider services, long organizationId, CancellationToken cancellationToken)
  {
    var installations = services.GetRequiredService<IInstallationRepository>();
    var installationId = 40 + organizationId;

    await installations.AddAsync(Installation.Create(installationId, organizationId, Now), cancellationToken);

    return installationId;
  }

  private static string Protected(IServiceProvider services, string value) =>
    services.GetRequiredService<ISecretProtector>().Protect(value);

  private static async Task EnterAsync(IServiceProvider services, string login, CancellationToken cancellationToken)
  {
    var entered = await services.GetRequiredService<IOrganizationScope>().TryEnterAsync(Forge.GitHub, login, cancellationToken);

    if (!entered)
    {
      throw new InvalidOperationException($"The organization '{login}' could not be entered.");
    }
  }

  #endregion
}
