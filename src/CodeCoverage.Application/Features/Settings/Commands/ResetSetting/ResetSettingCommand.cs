// ------------------------------------------------------------------------------------------------
// <copyright file="ResetSettingCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.ResetSetting;

/// <summary>
///   Removes the value of a setting of the instance saved in the settings store, so that the default
///   of the catalog applies again (a secret becomes undefined); refused when a stronger source provides
///   the value.
/// </summary>
/// <param name="Key">The configuration key of the setting, as <c>Section:Name</c>.</param>
public sealed record ResetSettingCommand(string Key) : ICommand, IAdminOnly;
