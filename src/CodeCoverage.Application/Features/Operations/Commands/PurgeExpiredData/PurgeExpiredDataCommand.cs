// ------------------------------------------------------------------------------------------------
// <copyright file="PurgeExpiredDataCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Operations.Commands.PurgeExpiredData;

/// <summary>
///   Deletes what the retention no longer keeps: the work of the daily <c>PurgeExpiredData</c> job, as a coordinator on the
///   lane of the instance and as a purge in each organization.
/// </summary>
public sealed record PurgeExpiredDataCommand : ICommand, ISystemOnly;
