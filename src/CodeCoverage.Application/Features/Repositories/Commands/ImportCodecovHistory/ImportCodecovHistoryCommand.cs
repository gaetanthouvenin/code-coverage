// ------------------------------------------------------------------------------------------------
// <copyright file="ImportCodecovHistoryCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Repositories.Commands.ImportCodecovHistory;

/// <summary>Reads the history of a repository from Codecov and writes its commits: the work of the <c>ImportCodecovHistory</c> job.</summary>
/// <param name="ImportId">The identifier of the <c>CodecovImports</c> row, which holds the protected token.</param>
/// <remarks>Sent by the job only, inside the organization of the job (ADR-013).</remarks>
public sealed record ImportCodecovHistoryCommand(long ImportId) : ICommand, ISystemOnly;
