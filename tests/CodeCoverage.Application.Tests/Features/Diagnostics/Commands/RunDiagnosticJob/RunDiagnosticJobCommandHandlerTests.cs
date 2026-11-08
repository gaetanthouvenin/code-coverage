// ------------------------------------------------------------------------------------------------
// <copyright file="RunDiagnosticJobCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Tests.Features.Diagnostics.Commands.RunDiagnosticJob;

public sealed class RunDiagnosticJobCommandHandlerTests
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldComplete()
  {
    var handler = new RunDiagnosticJobCommandHandler(NullLogger<RunDiagnosticJobCommandHandler>.Instance);

    (await handler.Handle(new RunDiagnosticJobCommand(42), Ct)).ShouldBe(Unit.Value);
  }

  [Fact]
  public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
  {
    var handler = new RunDiagnosticJobCommandHandler(NullLogger<RunDiagnosticJobCommandHandler>.Instance);

    await Should.ThrowAsync<ArgumentNullException>(async () => await handler.Handle(null!, Ct));
  }

  // Only the worker may send it: no user can trigger a job through the mediator.
  [Fact]
  public void Command_ShouldBeSystemOnly()
  {
    new RunDiagnosticJobCommand(42).ShouldBeAssignableTo<ISystemOnly>();
  }

  #endregion
}
