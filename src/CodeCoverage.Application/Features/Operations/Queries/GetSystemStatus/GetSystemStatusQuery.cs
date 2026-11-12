// ------------------------------------------------------------------------------------------------
// <copyright file="GetSystemStatusQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Application.Features.Operations.Queries.GetSystemStatus;

/// <summary>Reads the state of the job queue, the worker heartbeat and the GitHub quota left.</summary>
public sealed record GetSystemStatusQuery : IQuery<SystemStatus>, IAdminOnly;
