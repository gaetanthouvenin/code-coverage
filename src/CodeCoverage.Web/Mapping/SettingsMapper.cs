// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;
using CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;
using CodeCoverage.Application.Features.Settings.Commands.ResetSetting;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Application.Features.Settings.Queries.GetSettings;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.ViewModels.Settings;

namespace CodeCoverage.Web.Mapping;

/// <summary>Converts the settings of the instance between the Domain and the pages.</summary>
internal static class SettingsMapper
{
  #region Methods

  /// <summary>Reads the settings of the instance, by group, without the value of any secret.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The groups, in the order of the catalog.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<SettingGroupViewModel>> GetSettingGroupsViewModelAsync(this ISender sender, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var settings = await sender.Send(new GetSettingsQuery(), cancellationToken).ConfigureAwait(false);

    return [.. settings.GroupBy(static s => s.Definition.Group).Select(static g => new SettingGroupViewModel(g.Key, [.. g.Select(ToViewModel)]))];
  }

  /// <summary>Changes a setting of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="value">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> UpdateSettingAsync(this ISender sender, string key, string value, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new UpdateSettingCommand(key, value), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Replaces a secret of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the secret.</param>
  /// <param name="value">The new secret.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the secret is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ReplaceSecretAsync(this ISender sender, string key, string value, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ReplaceSecretCommand(key, value), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Removes the value saved from the console: back to the default, or a stored secret cleared.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>A task that completes when the setting is reset.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ResetSettingAsync(this ISender sender, string key, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ResetSettingCommand(key), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Reads the settings of an organization: the four keys of its scope, its own value or the default.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the organization.</param>
  /// <param name="login">The login of the organization.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The settings, or <see langword="null" /> when the forge segment is not one the console serves.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<IReadOnlyList<SettingViewModel>?> GetOrganizationSettingViewModelsAsync(
    this ISender sender,
    string forge,
    string login,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    if (!RepositoryMapper.TryParseForge(forge, out var parsed))
    {
      return null;
    }

    var settings = await sender.Send(new GetOrganizationSettingsQuery(parsed, login), cancellationToken).ConfigureAwait(false);

    return [.. settings.Select(static s => ToViewModel(s, SettingOrigin.Organization))];
  }

  /// <summary>Changes a setting of an organization.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the organization.</param>
  /// <param name="login">The login of the organization.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="value">The new value.</param>
  /// <param name="cancellationToken">A token to cancel the change.</param>
  /// <returns><see langword="true" /> once the change is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> UpdateOrganizationSettingAsync(
    this ISender sender,
    string forge,
    string login,
    string key,
    string value,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new UpdateOrganizationSettingCommand(OrganizationForge(forge), login, key, value), cancellationToken).ConfigureAwait(false);

    return true;
  }

  /// <summary>Gives a setting of an organization its default value back.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="forge">The forge segment of the organization.</param>
  /// <param name="login">The login of the organization.</param>
  /// <param name="key">The key of the setting.</param>
  /// <param name="cancellationToken">A token to cancel the change.</param>
  /// <returns><see langword="true" /> once the change is saved.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<bool> ResetOrganizationSettingAsync(this ISender sender, string forge, string login, string key, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    await sender.Send(new ResetOrganizationSettingCommand(OrganizationForge(forge), login, key), cancellationToken).ConfigureAwait(false);

    return true;
  }

  // The page reached the editor only after reading the settings with this segment: it is a forge the console serves.
  private static Forge OrganizationForge(string forge) =>
    RepositoryMapper.TryParseForge(forge, out var parsed) ? parsed : throw new ArgumentException("The forge segment is not one the console serves.", nameof(forge));

  private static SettingViewModel ToViewModel(SettingValue setting) => ToViewModel(setting, SettingOrigin.Database);

  // The value of a secret is dropped again here, even though the query already redacted it.
  private static SettingViewModel ToViewModel(SettingValue setting, SettingOrigin stored)
  {
    var definition = setting.Definition;
    var kind = definition.IsSecret
                 ? SettingEditorKind.Secret
                 : definition.Type switch
                 {
                   SettingType.Boolean => SettingEditorKind.Boolean,
                   SettingType.Yaml => SettingEditorKind.Multiline,
                   var _ => SettingEditorKind.Text,
                 };
    var origin = setting.Source switch
    {
      SettingSource.Environment => SettingOrigin.Environment,
      SettingSource.Store => stored,
      var _ => SettingOrigin.Default,
    };

    return new SettingViewModel(
      setting.Key,
      kind,
      definition.IsSecret ? null : setting.Value ?? definition.Default,
      origin,
      setting.IsDefined,
      setting.IsEditable,
      definition.RequiresRestart
    );
  }

  #endregion
}
