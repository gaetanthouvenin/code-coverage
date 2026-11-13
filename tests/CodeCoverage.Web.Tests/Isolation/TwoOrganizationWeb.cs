// ------------------------------------------------------------------------------------------------
// <copyright file="TwoOrganizationWeb.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Isolation;

// The world of the isolation suite of the console: two organizations on one real database, each with a repository,
// and one signed-in user who belongs to the first one only.
public sealed class TwoOrganizationWeb : IAsyncLifetime
{
  #region Fields

  /// <summary>The organization the user belongs to.</summary>
  public const string FirstOwner = "first-org";

  /// <summary>The organization the user knows nothing about.</summary>
  public const string SecondOwner = "second-org";

  /// <summary>An owner no organization ever had.</summary>
  public const string UnknownOwner = "unknown-org";

  /// <summary>The repository of each organization.</summary>
  public const string RepositoryName = "app";

  private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

  private readonly IsolationDatabase _database;

  private IsolationWebFactory? _factory;

  #endregion

  #region Ctors

  public TwoOrganizationWeb(IsolationDatabase database)
  {
    _database = database;
  }

  #endregion

  #region Properties

  /// <summary>The identifier of the signed-in user of the suite.</summary>
  public long UserId { get; private set; }

  private IsolationWebFactory Factory => _factory ?? throw new InvalidOperationException("The host is not started.");

  #endregion

  #region Methods

  /// <summary>A client of the console, signed in as the user of the first organization.</summary>
  public HttpClient Client()
  {
    var client = Factory.CreateClient();
    client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, UserId.ToString(CultureInfo.InvariantCulture));

    return client;
  }

  /// <inheritdoc />
  public async ValueTask InitializeAsync()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var connectionString = await _database.ConnectionStringAsync(cancellationToken);
    _factory = new IsolationWebFactory(connectionString);

    // The console never migrates: the schema is created here, once, as a deployment creates it.
    await _factory.MigrateAsync(cancellationToken);
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
      var user = AppUser.Create(Forge.GitHub, "583231", TestAuthenticationHandler.Login, null);
      await users.TryAddAsync(user, cancellationToken);
      UserId = (await users.FindByExternalIdAsync(Forge.GitHub, "583231", cancellationToken) ?? user).Id;
    }

    await using (var scope = Factory.Services.CreateAsyncScope())
    {
      await SeedRepositoryAsync(scope.ServiceProvider, FirstOwner, firstId, "3001", readable: true, cancellationToken);
    }

    await using (var scope = Factory.Services.CreateAsyncScope())
    {
      await SeedRepositoryAsync(scope.ServiceProvider, SecondOwner, secondId, "3002", readable: false, cancellationToken);
    }
  }

  private static async Task<Organization> AdmitAsync(IOrganizationDirectory directory, string accountId, string login, CancellationToken cancellationToken)
  {
    await directory.UpsertFromForgeAsync(Forge.GitHub, accountId, login, OrganizationAccountType.Organization, cancellationToken);

    return await directory.ChangeAsync(Forge.GitHub, accountId, organization => organization.Admit("isolation", Now), cancellationToken)
           ?? throw new InvalidOperationException($"The organization '{login}' was not admitted.");
  }

  // The user reads the repository of its own organization only: the access row the forge mirrors says so.
  private async Task SeedRepositoryAsync(
    IServiceProvider services,
    string login,
    long organizationId,
    string externalId,
    bool readable,
    CancellationToken cancellationToken)
  {
    var entered = await services.GetRequiredService<IOrganizationScope>().TryEnterAsync(Forge.GitHub, login, cancellationToken);

    if (!entered)
    {
      throw new InvalidOperationException($"The organization '{login}' could not be entered.");
    }

    var installations = services.GetRequiredService<IInstallationRepository>();
    var installationId = 40 + organizationId;
    await installations.AddAsync(Installation.Create(installationId, organizationId, Now), cancellationToken);

    var repositories = services.GetRequiredService<IRepositoryRepository>();
    var repository = TrackedRepository.Create(
      installationId,
      externalId,
      RepositorySlug.Parse($"{login}/{RepositoryName}"),
      "main",
      isPrivate: !readable,
      services.GetRequiredService<ISecretProtector>().Protect($"badge-{externalId}"),
      Now);

    await repositories.AddAsync(repository, cancellationToken);

    if (readable)
    {
      var accesses = services.GetRequiredService<IUserRepositoryAccessRepository>();
      await accesses.UpsertAsync(UserId, externalId, RepositoryPermission.Admin, Now, cancellationToken);
    }
  }

  #endregion
}
