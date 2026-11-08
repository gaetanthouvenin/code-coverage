// ------------------------------------------------------------------------------------------------
// <copyright file="GetSettingsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.GetSettings;

/// <summary>
///   Gets the effective value of every setting of the instance, with its source; a secret only tells
///   whether it is defined, never its value.
/// </summary>
public sealed record GetSettingsQuery : IQuery<IReadOnlyList<SettingValue>>, IAdminOnly;
