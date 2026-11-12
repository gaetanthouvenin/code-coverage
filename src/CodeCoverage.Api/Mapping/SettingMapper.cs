// ------------------------------------------------------------------------------------------------
// <copyright file="SettingMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Api.Contracts.Settings;
using CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;
using CodeCoverage.Application.Features.Settings.Commands.ResetSetting;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Application.Features.Settings.Queries.GetSettings;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Api.Mapping;

/// <summary>Converts the settings of the instance between the Domain and the API.</summary>
internal static class SettingMapper
{
  #region Methods

  /// <summary>Reads the settings of the instance and converts them for the API, without the value of any secret.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The settings.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<SettingResponse>> GetSettingsResponseAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var settings = await sender.Send(new GetSettingsQuery(), cancellationToken).ConfigureAwait(false);

    return [.. settings.Select(ToResponse)];
  }

  /// <summary>Changes a setting of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="request">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task UpdateSettingAsync(this ISender sender, string key, UpdateSettingRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    await sender.Send(new UpdateSettingCommand(key, request.Value), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Replaces a secret of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the secret.</param>
  /// <param name="request">The new secret.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the secret is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> or <paramref name="request" /> is <see langword="null" />.</exception>
  public static async Task ReplaceSecretAsync(this ISender sender, string key, ReplaceSecretRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);
    ArgumentNullException.ThrowIfNull(request);

    await sender.Send(new ReplaceSecretCommand(key, request.Value), cancellationToken).ConfigureAwait(false);
  }

  /// <summary>Puts a setting of the instance back to its default; a secret saved from the console is cleared.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is reset.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task ResetSettingAsync(this ISender sender, string key, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ResetSettingCommand(key), cancellationToken).ConfigureAwait(false);
  }

  // The value of a secret is dropped again here, even though the query already redacted it.
  private static SettingResponse ToResponse(SettingValue setting)
  {
    var definition = setting.Definition;

    return new SettingResponse(
      setting.Key,
      definition.Group,
      definition.Type.ToString(),
      definition.IsSecret ? null : setting.Value,
      definition.IsSecret ? null : definition.Default,
      setting.Source.ToString(),
      definition.IsSecret,
      setting.IsDefined,
      setting.IsEditable,
      definition.RequiresRestart,
      setting.UpdatedAt,
      setting.UpdatedBy
    );
  }

  #endregion
}
