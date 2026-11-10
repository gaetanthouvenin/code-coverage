// ------------------------------------------------------------------------------------------------
// <copyright file="SystemCommandRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;
using CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;
using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Administration;

/// <summary>Runs a named command as the system, for the administration command line.</summary>
/// <param name="scopeFactory">The factory of the scope of each command.</param>
/// <remarks>
///   <para>
///     One of the doors of ADR-013, and the reason <see cref="CurrentUserContext.SetSystem" /> can stay
///     <see langword="internal" />: the hosts get a list of commands to run, never a system principal.
///     What it runs is audited under <see cref="Domain.Operations.AuditEntry.CommandLineActor" />, not
///     under the actor of the jobs.
///   </para>
///   <para>Deliberately not registered in the container: a build rule keeps its use in the administration command line.</para>
/// </remarks>
public sealed class SystemCommandRunner(IServiceScopeFactory scopeFactory)
{
  #region Fields

  private static readonly HashSet<Type> AllowedCommands =
  [
    typeof(AdmitOrganizationCommand),
    typeof(SuspendOrganizationCommand),
    typeof(CreateOrganizationUploadTokenCommand),
    typeof(CreateRepositoryUploadTokenCommand),
    typeof(RevokeOrganizationUploadTokenCommand),
    typeof(RevokeRepositoryUploadTokenCommand),
  ];

  #endregion

  #region Methods

  /// <summary>Runs a command as the system, in a new scope.</summary>
  /// <typeparam name="TResponse">The type of the response.</typeparam>
  /// <param name="command">The command, which must be on the list.</param>
  /// <param name="forge">The forge of the organization to enter; ignored without <paramref name="login" />.</param>
  /// <param name="login">The login of the organization to enter, or <see langword="null" /> to enter none.</param>
  /// <param name="cancellationToken">A token to cancel the command.</param>
  /// <returns>The response of the command.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="command" /> is <see langword="null" />.</exception>
  /// <exception cref="InvalidOperationException">The command is not on the list.</exception>
  /// <exception cref="NotFoundException">No active organization of this forge holds the login.</exception>
  public async Task<TResponse> RunAsync<TResponse>(
    ICommand<TResponse> command,
    Forge forge,
    string? login,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    InvalidOperationException.ThrowIf(
      !AllowedCommands.Contains(command.GetType()),
      $"{command.GetType().Name} cannot be run from the command line."
    );

    var scope = scopeFactory.CreateAsyncScope();

    await using (scope.ConfigureAwait(false))
    {
      var services = scope.ServiceProvider;

      services.GetRequiredService<CurrentUserContext>().SetCommandLine();

      if (login is not null)
      {
        await EnterAsync(services, forge, login, cancellationToken).ConfigureAwait(false);
      }

      return await services.GetRequiredService<ISender>().Send(command, cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Makes a scope enter an active organization.</summary>
  /// <param name="services">The services of the scope.</param>
  /// <param name="forge">The forge of the account.</param>
  /// <param name="login">The login of the account.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>A task that completes when the scope has entered the organization.</returns>
  /// <exception cref="NotFoundException">No active organization of this forge holds the login.</exception>
  private static async Task EnterAsync(IServiceProvider services, Forge forge, string login, CancellationToken cancellationToken)
  {
    var organization = await services.GetRequiredService<IOrganizationDirectory>()
                                     .FindByLoginAsync(forge, login, cancellationToken)
                                     .ConfigureAwait(false);

    if (organization is not { State: OrganizationState.Active })
    {
      throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or is not active.");
    }

    services.GetRequiredService<OrganizationContext>().Enter(organization.Id);
  }

  #endregion
}
