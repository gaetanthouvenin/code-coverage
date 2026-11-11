// ------------------------------------------------------------------------------------------------
// <copyright file="InMemoryOrganizationDirectory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Support;

// The directory of the organizations, kept in memory: the rows change as the real one changes them, so a test
// reads the state an organization ends in instead of the calls that led to it.
internal sealed class InMemoryOrganizationDirectory(DateTimeOffset now) : IOrganizationDirectory
{
  #region Fields

  private readonly List<Organization> _organizations = [];

  private long _nextId = 100;

  #endregion

  #region Properties

  public IReadOnlyList<Organization> All => _organizations;

  public Organization? Current { get; set; }

  public int Upserts { get; private set; }

  #endregion

  #region Methods

  public Organization Add(string accountId, string login, bool admitted = false, OrganizationAccountType accountType = OrganizationAccountType.Organization)
  {
    var organization = Organization.CreatePending(Forge.GitHub, accountId, login, accountType, now);

    if (admitted)
    {
      organization.Admit("tests", now);
    }

    return Add(organization);
  }

  public Organization Add(Organization organization)
  {
    // The identifier is given by the database: a row read from it always has one.
    typeof(Organization).GetProperty(nameof(Organization.Id))!.SetValue(organization, _nextId++);
    _organizations.Add(organization);

    return organization;
  }

  #endregion

  #region Implementation of IOrganizationDirectory

  public Task<Organization?> FindByLoginAsync(Forge forge, string login, CancellationToken cancellationToken) =>
    Task.FromResult(_organizations.SingleOrDefault(o => o.Forge == forge && o.NormalizedLogin == Organization.NormalizeLogin(login)));

  public Task<Organization?> FindByAccountIdAsync(Forge forge, string externalAccountId, CancellationToken cancellationToken) =>
    Task.FromResult(_organizations.SingleOrDefault(o => o.Forge == forge && o.ExternalAccountId == Organization.NormalizeExternalAccountId(externalAccountId)));

  public Task<Organization?> FindCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

  public Task<IReadOnlyList<Organization>> ListActiveAsync(CancellationToken cancellationToken) =>
    Task.FromResult<IReadOnlyList<Organization>>([.. _organizations.Where(static o => o.State is OrganizationState.Active)]);

  public async Task<Organization> UpsertFromForgeAsync(
    Forge forge,
    string externalAccountId,
    string login,
    OrganizationAccountType accountType,
    CancellationToken cancellationToken)
  {
    Upserts++;

    var organization = await FindByAccountIdAsync(forge, externalAccountId, cancellationToken);

    if (organization is null)
    {
      return Add(Organization.CreatePending(forge, externalAccountId, login, accountType, now));
    }

    organization.Rename(login, now);
    organization.ChangeAccountType(accountType, now);

    return organization;
  }

  public async Task<Organization?> ChangeAsync(
    Forge forge,
    string externalAccountId,
    Action<Organization> change,
    CancellationToken cancellationToken)
  {
    var organization = await FindByAccountIdAsync(forge, externalAccountId, cancellationToken);

    if (organization is not null)
    {
      change(organization);
    }

    return organization;
  }

  #endregion
}
