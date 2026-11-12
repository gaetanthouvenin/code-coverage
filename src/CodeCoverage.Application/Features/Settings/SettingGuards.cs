// ------------------------------------------------------------------------------------------------
// <copyright file="SettingGuards.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings;

/// <summary>
///   The refusals of the settings of the instance, in the form of
///   <see cref="ArgumentNullException.ThrowIfNull(object?, string?)" />: one place holds the code and the
///   message of each.
/// </summary>
internal static class SettingGuards
{
  #region Methods

  extension(NotFoundException)
  {
    /// <summary>Throws when a key names no setting of the instance: unknown, or a setting of an organization.</summary>
    /// <param name="definition">The definition of the key, or <see langword="null" /> when the catalog has none.</param>
    /// <param name="key">The key asked for.</param>
    /// <exception cref="NotFoundException">The key names no setting of the instance (<see cref="SettingErrors.Unknown" />).</exception>
    public static void ThrowIfNotInstanceSetting([NotNull] SettingDefinition? definition, string key)
    {
      if (definition is not { Scope: SettingScope.Instance })
      {
        throw new NotFoundException(SettingErrors.Unknown, $"The setting '{key}' does not exist on the instance.");
      }
    }
  }

  extension(UnprocessableEntityException)
  {
    /// <summary>Throws when the value of a setting comes from the environment, which the screen cannot change.</summary>
    /// <param name="current">The effective value of the setting.</param>
    /// <exception cref="UnprocessableEntityException">The value is read-only (<see cref="SettingErrors.ProvidedByEnvironment" />).</exception>
    public static void ThrowIfSettingReadOnly(SettingValue current)
    {
      ArgumentNullException.ThrowIfNull(current);

      if (!current.IsEditable)
      {
        throw new UnprocessableEntityException(
          SettingErrors.ProvidedByEnvironment,
          $"The setting '{current.Key}' is provided by the environment: it is read-only."
        );
      }
    }

    /// <summary>Throws when a secret is changed as a plain value.</summary>
    /// <param name="definition">The definition of the setting.</param>
    /// <exception cref="UnprocessableEntityException">The setting is a secret (<see cref="SettingErrors.IsSecret" />).</exception>
    public static void ThrowIfSettingSecret(SettingDefinition definition)
    {
      ArgumentNullException.ThrowIfNull(definition);

      if (definition.IsSecret)
      {
        throw new UnprocessableEntityException(SettingErrors.IsSecret, $"The setting '{definition.Key}' is a secret: replace it instead.");
      }
    }

    /// <summary>Throws when a plain value is replaced as a secret.</summary>
    /// <param name="definition">The definition of the setting.</param>
    /// <exception cref="UnprocessableEntityException">The setting is not a secret (<see cref="SettingErrors.NotSecret" />).</exception>
    public static void ThrowIfSettingNotSecret(SettingDefinition definition)
    {
      ArgumentNullException.ThrowIfNull(definition);

      if (!definition.IsSecret)
      {
        throw new UnprocessableEntityException(SettingErrors.NotSecret, $"The setting '{definition.Key}' is not a secret: change its value instead.");
      }
    }

    /// <summary>Throws when the setup is completed before the GitHub App exists.</summary>
    /// <param name="created">Whether the GitHub App was created.</param>
    /// <exception cref="UnprocessableEntityException"><paramref name="created" /> is <see langword="false" /> (<see cref="SettingErrors.GitHubAppNotCreated" />).</exception>
    public static void ThrowIfGitHubAppNotCreated([DoesNotReturnIf(false)] bool created)
    {
      if (!created)
      {
        throw new UnprocessableEntityException(SettingErrors.GitHubAppNotCreated, "The setup cannot end before the GitHub App exists.");
      }
    }
  }

  extension(ConflictException)
  {
    /// <summary>Throws when the GitHub App was already created.</summary>
    /// <param name="created">Whether the GitHub App was created.</param>
    /// <exception cref="ConflictException"><paramref name="created" /> is <see langword="true" /> (<see cref="SettingErrors.GitHubAppAlreadyCreated" />).</exception>
    public static void ThrowIfGitHubAppAlreadyCreated([DoesNotReturnIf(true)] bool created)
    {
      if (created)
      {
        throw new ConflictException(SettingErrors.GitHubAppAlreadyCreated, "The GitHub App already exists on this instance.");
      }
    }
  }

  extension(BadRequestException)
  {
    /// <summary>Throws when GitHub refuses the code of a manifest.</summary>
    /// <param name="app">What GitHub gave for the code, or <see langword="null" />.</param>
    /// <exception cref="BadRequestException"><paramref name="app" /> is <see langword="null" /> (<see cref="SettingErrors.InvalidManifestCode" />).</exception>
    public static void ThrowIfManifestCodeRejected([NotNull] GitHubAppCredentials? app)
    {
      if (app is null)
      {
        throw new BadRequestException(SettingErrors.InvalidManifestCode, "GitHub does not know this code, or it has expired.");
      }
    }
  }

  extension(UnauthorizedException)
  {
    /// <summary>Throws when a caller does not hold the setup token.</summary>
    /// <param name="valid">Whether the token is valid.</param>
    /// <exception cref="UnauthorizedException"><paramref name="valid" /> is <see langword="false" /> (<see cref="SettingErrors.InvalidSetupToken" />).</exception>
    public static void ThrowIfSetupTokenInvalid([DoesNotReturnIf(false)] bool valid)
    {
      if (!valid)
      {
        throw new UnauthorizedException(SettingErrors.InvalidSetupToken, "The setup token is missing or invalid.");
      }
    }
  }

  #endregion
}
