// ------------------------------------------------------------------------------------------------
// <copyright file="EnsureForgeHooksCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Webhooks.Commands.EnsureForgeHooks;

/// <summary>Handler for the <see cref="EnsureForgeHooksCommand" /> command.</summary>
/// <param name="connections">The repository of the forge connections.</param>
/// <param name="hooks">The repository of the hooks of the connections.</param>
/// <param name="repositories">The repository of the tracked repositories, which gives the projects and the repositories of the connection.</param>
/// <param name="forges">Resolves the hook manager of the forge.</param>
/// <param name="protector">The secret protector, which gives back the webhook secret for the time of the call.</param>
/// <param name="urls">The public addresses of the hosts, which give the route the hooks deliver to.</param>
/// <param name="organization">The organization of the scope.</param>
/// <param name="clock">The clock.</param>
/// <remarks>
///   <para>
///     The connection is read from a global table: one of another organization, an unknown one or a suspended one is
///     left alone. The scopes come from the kind of hook of the forge: the account of the connection, the projects of its
///     active repositories, or those repositories themselves. Each scope gets the hook last recorded for it, so that the
///     forge is never asked to create it twice.
///   </para>
///   <para>
///     A refusal of the forge is recorded <see cref="ForgeHookState.Forbidden" /> for its scope and the job ends: it is a
///     result, never a failed job. A forge without a hook manager, or an Api without a public address, leaves the hooks
///     as they are, since no hook could deliver anywhere.
///   </para>
/// </remarks>
internal sealed class EnsureForgeHooksCommandHandler(
  IForgeConnectionRepository connections,
  IForgeHookRepository hooks,
  IRepositoryRepository repositories,
  IForgeGatewayProvider forges,
  ISecretProtector protector,
  IOptionsMonitor<UrlOptions> urls,
  ICurrentOrganization organization,
  IClock clock) : ICommandHandler<EnsureForgeHooksCommand>
{
  #region Methods

  /// <summary>Records on the hook of its scope what the hook manager found or left.</summary>
  /// <param name="hook">The hook of the scope.</param>
  /// <param name="result">The result of the hook manager.</param>
  /// <param name="at">The time of the check.</param>
  private static void Apply(ForgeHook hook, ForgeHookResult result, DateTimeOffset at)
  {
    // The factories of ForgeHookResult set the identifier of an active or disabled hook, and the reason of a refusal.
    if (result.State is ForgeHookState.Active)
    {
      hook.MarkActive(result.HookExternalId!, at);
    }
    else if (result.State is ForgeHookState.Disabled)
    {
      hook.MarkDisabled(result.HookExternalId!, result.Error, at);
    }
    else if (result.State is ForgeHookState.Forbidden)
    {
      hook.MarkForbidden(result.Error!, at);
    }
    else
    {
      hook.MarkMissing(at);
    }
  }

  /// <summary>Builds the scopes of the hooks of a connection.</summary>
  /// <param name="kind">What each hook of the forge covers.</param>
  /// <param name="connection">The connection.</param>
  /// <param name="tracked">The tracked repositories of the organization.</param>
  /// <param name="known">The hooks recorded for the connection.</param>
  /// <returns>The scopes, each once, with the hook last recorded for it.</returns>
  private static List<ForgeHookScope> Scopes(ForgeHookScopeKind kind, ForgeConnection connection, IReadOnlyList<TrackedRepository> tracked, IReadOnlyList<ForgeHook> known)
  {
    var served = tracked.Where(r => r.IsActive && string.Equals(r.ConnectionId, connection.Id, StringComparison.Ordinal));

    IEnumerable<string> ids = kind switch
    {
      ForgeHookScopeKind.Connection => [connection.ExternalAccountId],
      ForgeHookScopeKind.Project => served.Select(static r => r.Namespace).Where(static n => !string.IsNullOrWhiteSpace(n)),
      var _ => served.Select(static r => r.ExternalId),
    };

    return ids.Distinct(StringComparer.OrdinalIgnoreCase)
              .Select(id => new ForgeHookScope(kind, id, Find(known, kind, id)?.ExternalId))
              .ToList();
  }

  /// <summary>Finds the hook recorded for a scope.</summary>
  /// <param name="known">The hooks recorded for the connection.</param>
  /// <param name="kind">The kind of the scope.</param>
  /// <param name="externalId">The identifier of the scope, whatever its case.</param>
  /// <returns>The hook, or <see langword="null" /> when none is recorded.</returns>
  private static ForgeHook? Find(IEnumerable<ForgeHook> known, ForgeHookScopeKind kind, string externalId) =>
    known.FirstOrDefault(h => h.ScopeKind == kind && string.Equals(h.ScopeExternalId, externalId, StringComparison.OrdinalIgnoreCase));

  /// <summary>Gets the hook manager of a forge.</summary>
  /// <param name="forge">The forge.</param>
  /// <returns>The hook manager, or <see langword="null" /> when none is implemented for the forge yet.</returns>
  private IForgeHookManager? FindManager(Forge forge)
  {
    try
    {
      return forges.GetHookManager(forge);
    }
    catch (NotSupportedException)
    {
      return null;
    }
  }

  #endregion

  #region Implementation of ICommandHandler<EnsureForgeHooksCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(EnsureForgeHooksCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var connection = await connections.FindAsync(command.ConnectionId, cancellationToken).ConfigureAwait(false);

    // The job names the connection in its payload: only the one of this organization is ever handled here.
    if (connection is null || connection.OrganizationId != organization.Id || connection.Status is ForgeConnectionStatus.Suspended)
    {
      return Unit.Value;
    }

    var manager = FindManager(connection.Forge);

    if (manager is null || !Uri.TryCreate(urls.CurrentValue.ApiPublicUrl, UriKind.Absolute, out var api))
    {
      return Unit.Value;
    }

    var known = await hooks.ListAsync(connection.Id, cancellationToken).ConfigureAwait(false);
    var tracked = await repositories.ListAsync(cancellationToken).ConfigureAwait(false);
    var scopes = Scopes(manager.ScopeKind, connection, tracked, known);

    if (scopes.Count == 0)
    {
      return Unit.Value;
    }

    var now = clock.UtcNow;
    var rows = scopes.Select(s => (Scope: s, Hook: Find(known, s.Kind, s.ExternalId) ?? ForgeHook.Create(connection.Id, s.Kind, s.ExternalId, now))).ToList();
    var callback = ForgeHookCallback.For(api, connection.Forge, connection.Id, protector.Unprotect(connection.WebhookSecretProtected));
    var results = await manager.EnsureAsync(new ForgeConnectionRef(connection.Forge, connection.Id), scopes, callback, cancellationToken).ConfigureAwait(false);

    foreach (var result in results)
    {
      // A result for a scope that was not asked for is not the manager's to record.
      var hook = rows.FirstOrDefault(r => r.Scope.IsSameScope(result.Scope)).Hook;

      if (hook is null)
      {
        continue;
      }

      Apply(hook, result, now);
      await hooks.UpsertAsync(hook, cancellationToken).ConfigureAwait(false);
    }

    return Unit.Value;
  }

  #endregion
}
