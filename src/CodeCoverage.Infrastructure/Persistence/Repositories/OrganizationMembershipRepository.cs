// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationMembershipRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>OrganizationMemberships</c> table, which belongs to no organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <remarks>
///   The table has no query filter and no write guard of an organization: every method names its user, and never
///   reads an organization from anything but its arguments.
/// </remarks>
internal sealed class OrganizationMembershipRepository(CodeCoverageDbContext context) : IOrganizationMembershipRepository
{
  #region Fields

  /// <summary>The message of the refusal of a refresh that lost a race; it is sent to the caller.</summary>
  private const string ConcurrentRefreshMessage = "The memberships of the user were refreshed in the meantime; refresh them again.";

  #endregion

  #region Implementation of IOrganizationMembershipRepository

  /// <inheritdoc />
  public async Task<OrganizationMembership?> FindAsync(long userId, long organizationId, CancellationToken cancellationToken) =>
    await context.OrganizationMemberships
                 .AsNoTracking()
                 .SingleOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  public async Task<IReadOnlyList<OrganizationMembership>> ListOfUserAsync(long userId, CancellationToken cancellationToken) =>
    await context.OrganizationMemberships
                 .AsNoTracking()
                 .Where(m => m.UserId == userId)
                 .OrderBy(static m => m.OrganizationId)
                 .ToListAsync(cancellationToken)
                 .ConfigureAwait(false);

  /// <inheritdoc />
  /// <exception cref="ConflictException">Another refresh of the same user wrote meanwhile.</exception>
  public async Task ReplaceAsync(long userId, IReadOnlyDictionary<long, OrganizationRole> roles, DateTimeOffset at, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(roles);

    var existing = await context.OrganizationMemberships.Where(m => m.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
    var changed = new List<OrganizationMembership>(existing);

    foreach (var membership in existing)
    {
      if (roles.TryGetValue(membership.OrganizationId, out var role))
      {
        membership.Refresh(role, at);
      }
      else
      {
        context.OrganizationMemberships.Remove(membership);
      }
    }

    foreach (var (organizationId, role) in roles.Where(entry => existing.TrueForAll(m => m.OrganizationId != entry.Key)).OrderBy(static entry => entry.Key))
    {
      var added = OrganizationMembership.Create(userId, organizationId, role, at);

      context.OrganizationMemberships.Add(added);
      changed.Add(added);
    }

    try
    {
      // One save, hence one transaction: the memberships of a user are never seen half replaced.
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
                                              || UniqueIndexViolation.Is(exception, OrganizationMembershipConfiguration.MembershipIndexName))
    {
      // Nothing is left for a later save of the scope, which would fail the same way.
      foreach (var membership in changed)
      {
        context.Entry(membership).State = EntityState.Detached;
      }

      throw new ConflictException(GeneralErrors.ConcurrentChange, ConcurrentRefreshMessage, exception);
    }
  }

  #endregion
}
