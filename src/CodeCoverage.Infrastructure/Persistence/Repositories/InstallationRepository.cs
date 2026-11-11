// ------------------------------------------------------------------------------------------------
// <copyright file="InstallationRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Installations</c> table, which is global.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class InstallationRepository(CodeCoverageDbContext context) : IInstallationRepository
{
  #region Fields

  /// <summary>The name of the primary key, which reports two installations added at once.</summary>
  private const string PrimaryKeyName = "PK_Installations";

  #endregion

  #region Implementation of IInstallationRepository

  /// <inheritdoc />
  public async Task<Installation?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.Installations
                 .AsNoTracking()
                 .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<InstallationOwner?> FindOwnerAsync(long id, CancellationToken cancellationToken) =>
    await context.Installations
                 .AsNoTracking()
                 .Where(i => i.Id == id)
                 .Join(context.Organizations, static i => i.OrganizationId, static o => o.Id, static (_, o) => new InstallationOwner(o.Id, o.State))
                 .SingleOrDefaultAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<long>> ListIdsForOrganizationAsync(long organizationId, CancellationToken cancellationToken) =>
    await context.Installations
                 .AsNoTracking()
                 .Where(i => i.OrganizationId == organizationId)
                 .OrderByDescending(static i => i.Id)
                 .Select(static i => i.Id)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task AddAsync(Installation installation, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(installation);

    context.Installations.Add(installation);

    try
    {
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (UniqueIndexViolation.Is(exception, PrimaryKeyName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      context.Entry(installation).State = EntityState.Detached;

      throw new ConflictException(GeneralErrors.ConcurrentChange, "The installation was added in the meantime.", exception);
    }
  }

  #endregion
}
