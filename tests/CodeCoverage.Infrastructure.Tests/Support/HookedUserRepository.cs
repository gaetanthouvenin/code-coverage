// ------------------------------------------------------------------------------------------------
// <copyright file="HookedUserRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Infrastructure.Tests.Support;

// The repository of the users, with hooks run before a reading of a given rank and before the first save: a test makes
// another process write the row at an exact point of the work of the code it tests.
internal sealed class HookedUserRepository(IUserRepository inner, UserReadingHook hook) : IUserRepository
{
  #region Methods

  public async Task<AppUser?> FindAsync(long id, CancellationToken cancellationToken)
  {
    await hook.BeforeReadingAsync().ConfigureAwait(false);

    return await inner.FindAsync(id, cancellationToken).ConfigureAwait(false);
  }

  public Task<AppUser?> FindByExternalIdAsync(Forge forge, string externalId, CancellationToken cancellationToken) =>
    inner.FindByExternalIdAsync(forge, externalId, cancellationToken);

  public Task<bool> TryAddAsync(AppUser user, CancellationToken cancellationToken) => inner.TryAddAsync(user, cancellationToken);

  public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken)
  {
    await hook.BeforeSaveAsync().ConfigureAwait(false);

    await inner.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
