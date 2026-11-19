// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations;

/// <summary>
///   The refusals of the organizations, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the message of each.
/// </summary>
internal static class OrganizationGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>
    ///   Throws when the user holds no role in an organization: it is unknown, not active, or the user does not belong
    ///   to it, and the answer does not tell which (ADR-013).
    /// </summary>
    /// <param name="role">The role of the user, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="role" /> is <see langword="null" />.</exception>
    public static void ThrowIfOrganizationNotFound([NotNull] OrganizationRole? role)
    {
      if (role is null)
      {
        throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist or cannot be read.");
      }
    }

    /// <summary>Throws when no organization of the forge was found for a change made by an administrator of the instance.</summary>
    /// <param name="organization">The organization that was looked for, or <see langword="null" />.</param>
    /// <exception cref="NotFoundException"><paramref name="organization" /> is <see langword="null" />.</exception>
    public static void ThrowIfOrganizationNotFound([NotNull] Organization? organization)
    {
      if (organization is null)
      {
        throw new NotFoundException(OrganizationErrors.NotFound, "The organization does not exist.");
      }
    }

    /// <summary>Throws when an organization disappeared while it was changed.</summary>
    /// <param name="organization">The organization after the change, or <see langword="null" /> when its row is gone.</param>
    /// <exception cref="NotFoundException"><paramref name="organization" /> is <see langword="null" />.</exception>
    public static void ThrowIfOrganizationGone([NotNull] Organization? organization)
    {
      if (organization is null)
      {
        throw new NotFoundException(OrganizationErrors.NotFound, "The organization no longer exists.");
      }
    }
  }

  extension(ConflictException)
  {
    /// <summary>Throws when an organization that already exists is not pending: only a pending one can be admitted.</summary>
    /// <param name="existing">The organization of the account, or <see langword="null" /> when there is none yet.</param>
    /// <exception cref="ConflictException"><paramref name="existing" /> exists and is not pending.</exception>
    public static void ThrowIfOrganizationNotPending(Organization? existing)
    {
      if (existing is { State: not OrganizationState.Pending })
      {
        throw new ConflictException(OrganizationErrors.NotPending, $"The organization is {existing.State}: only a pending one can be admitted.");
      }
    }
  }

  extension(BadRequestException)
  {
    /// <summary>
    ///   Throws when an account of a forge other than GitHub is admitted as a GitHub one: it is admitted by its connection
    ///   (<c>ConnectOrganization</c>), which verifies the access before it saves the organization.
    /// </summary>
    /// <param name="forge">The forge of the account.</param>
    /// <exception cref="BadRequestException"><paramref name="forge" /> is not GitHub.</exception>
    public static void ThrowIfConnectionForge(Forge forge)
    {
      if (forge is not Forge.GitHub)
      {
        throw new BadRequestException(OrganizationErrors.UseConnection, $"An account of {forge} is admitted by its connection, not by an installation.");
      }
    }
  }

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when GitHub knows no account of the login an administrator typed.</summary>
    /// <param name="account">The account GitHub described, or <see langword="null" />.</param>
    /// <param name="login">The login that was typed.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="account" /> is <see langword="null" />.</exception>
    public static void ThrowIfUnknownGitHubLogin([NotNull] GitHubAccountInfo? account, string login)
    {
      if (account is null)
      {
        throw new UnprocessableEntityException(OrganizationErrors.UnknownGitHubLogin, $"GitHub knows no account '{login}'.");
      }
    }
  }

  #endregion
}
