// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;

/// <summary>
///   Changes the value of a setting of the instance that is not a secret, saved in the settings store
///   and applied by every host; refused when a stronger source provides the value.
/// </summary>
/// <param name="Key">The configuration key of the setting, as <c>Section:Name</c>.</param>
/// <param name="Value">The new value, written with the invariant culture.</param>
public sealed record UpdateSettingCommand(string Key, string Value) : ICommand, IAdminOnly;
