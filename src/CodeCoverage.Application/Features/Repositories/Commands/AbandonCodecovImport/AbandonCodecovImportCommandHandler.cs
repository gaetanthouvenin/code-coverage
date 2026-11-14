// ------------------------------------------------------------------------------------------------
// <copyright file="AbandonCodecovImportCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.AbandonCodecovImport;

/// <summary>Handler for the <see cref="AbandonCodecovImportCommand" /> command.</summary>
/// <param name="imports">The imports of the history of Codecov.</param>
internal sealed class AbandonCodecovImportCommandHandler(ICodecovImportRepository imports) : ICommandHandler<AbandonCodecovImportCommand>
{
  #region Implementation of ICommandHandler<AbandonCodecovImportCommand>

  /// <inheritdoc />
  public async ValueTask<Unit> Handle(AbandonCodecovImportCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    await imports.DeleteAsync(command.ImportId, cancellationToken).ConfigureAwait(false);

    return Unit.Value;
  }

  #endregion
}
