// ------------------------------------------------------------------------------------------------
// <copyright file="AbandonCodecovImportCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Repositories.Commands.AbandonCodecovImport;

/// <summary>Deletes an import of the history of Codecov and its credential, once its job ended for good.</summary>
/// <param name="ImportId">The identifier of the <c>CodecovImports</c> row.</param>
/// <remarks>
///   Sent by the worker only, when the <c>ImportCodecovHistory</c> job failed for good or died: the token is never kept
///   for a job that will not run again. An operator who retries the dead job finds no import and nothing happens; the
///   import is started again by its route.
/// </remarks>
public sealed record AbandonCodecovImportCommand(long ImportId) : ICommand, ISystemOnly;
