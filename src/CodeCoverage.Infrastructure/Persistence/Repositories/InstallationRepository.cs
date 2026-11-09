// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Installations</c> table, which is global.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class InstallationRepository(CodeCoverageDbContext context) : IInstallationRepository
{
  #region Implementation of IInstallationRepository

  /// <inheritdoc />
  public async Task<Installation?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.Installations
                 .AsNoTracking()
                 .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task AddAsync(Installation installation, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(installation);

    context.Installations.Add(installation);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}
