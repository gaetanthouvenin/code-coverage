// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationScopeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Identity;

[Collection(DatabaseCollection.Name)]
public sealed class OrganizationScopeTests(DatabaseFixture database)
{
  #region Fields

  private const string FirstAccount = "1001";

  private const string SecondAccount = "2002";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_ActiveOrganization_ShouldEnter(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var organization = await SeedActiveAsync(context, Forge.GitHub, FirstAccount, "Octo-Org");
    var current = new OrganizationContext();

    var entered = await Scope(context, current).TryEnterAsync(Forge.GitHub, "OCTO-org", Ct);

    entered.ShouldBeTrue();
    current.Id.ShouldBe(organization.Id);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_SuspendedOrganization_ShouldReturnFalseAndStayUnset(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var organization = Pending(Forge.GitHub, FirstAccount, "octo");
    organization.Admit("admin-user", Now);
    organization.Suspend(SuspensionReason.Administrator, Now);
    context.Organizations.Add(organization);
    await context.SaveChangesAsync(Ct);
    var current = new OrganizationContext();

    var entered = await Scope(context, current).TryEnterAsync(Forge.GitHub, "octo", Ct);

    entered.ShouldBeFalse();
    current.IsKnown.ShouldBeFalse();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_PendingOrganization_ShouldReturnFalseAndStayUnset(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    context.Organizations.Add(Pending(Forge.GitHub, FirstAccount, "octo"));
    await context.SaveChangesAsync(Ct);
    var current = new OrganizationContext();

    var entered = await Scope(context, current).TryEnterAsync(Forge.GitHub, "octo", Ct);

    entered.ShouldBeFalse();
    current.IsKnown.ShouldBeFalse();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_UnknownLogin_ShouldReturnFalse(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var current = new OrganizationContext();

    var entered = await Scope(context, current).TryEnterAsync(Forge.GitHub, "nobody", Ct);

    entered.ShouldBeFalse();
    current.IsKnown.ShouldBeFalse();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_LoginActiveOnOtherForge_ShouldReturnFalse(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    await SeedActiveAsync(context, Forge.GitLab, FirstAccount, "octo");
    var current = new OrganizationContext();

    var entered = await Scope(context, current).TryEnterAsync(Forge.GitHub, "octo", Ct);

    entered.ShouldBeFalse();
    current.IsKnown.ShouldBeFalse();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("~stale-1001")]
  [InlineData("org:project")]
  [InlineData("group/sub")]
  public async Task TryEnter_MalformedLogin_ShouldReturnFalse(string? login)
  {
    var directory = A.Fake<IOrganizationDirectory>();
    var current = new OrganizationContext();

    var entered = await new OrganizationScope(directory, current).TryEnterAsync(Forge.GitHub, login, Ct);

    entered.ShouldBeFalse();
    current.IsKnown.ShouldBeFalse();
    A.CallTo(directory).MustNotHaveHappened();
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task TryEnter_OtherOrganizationOfTheSameScope_ShouldThrow(DatabaseProvider provider)
  {
    var connection = await MigratedDatabaseAsync(provider);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    var first = await SeedActiveAsync(context, Forge.GitHub, FirstAccount, "first-org");
    await SeedActiveAsync(context, Forge.GitHub, SecondAccount, "second-org");
    var current = new OrganizationContext();
    var scope = Scope(context, current);
    await scope.TryEnterAsync(Forge.GitHub, "first-org", Ct);

    await Should.ThrowAsync<InvalidOperationException>(() => scope.TryEnterAsync(Forge.GitHub, "second-org", Ct));

    current.Id.ShouldBe(first.Id);
  }

  private static Organization Pending(Forge forge, string account, string login) =>
    Organization.CreatePending(forge, account, login, OrganizationAccountType.Organization, Now);

  private static async Task<Organization> SeedActiveAsync(
    CodeCoverageDbContext context,
    Forge forge,
    string account,
    string login)
  {
    var organization = Pending(forge, account, login);
    organization.Admit("admin-user", Now);
    context.Organizations.Add(organization);
    await context.SaveChangesAsync(Ct);

    return organization;
  }

  private static OrganizationScope Scope(CodeCoverageDbContext context, OrganizationContext current) =>
    new(new OrganizationDirectory(context, A.Fake<IClock>(), current), current);

  private async Task<string> MigratedDatabaseAsync(DatabaseProvider provider)
  {
    var connection = await database.NewDatabaseAsync(provider, Ct);
    await using var context = DatabaseFixture.CreateContext(provider, connection);
    await context.Database.MigrateAsync(Ct);

    return connection;
  }

  #endregion
}
