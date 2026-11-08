// ------------------------------------------------------------------------------------------------
// <copyright file="FakePingQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// Stands for the handler at the end of the pipeline: registered after AddApplication(), it is
// the innermost step, records its run and answers without calling the real handler.
internal sealed class FakePingQueryHandler : IPipelineBehavior<PingQuery, string>
{
  #region Fields

  public const string Response = "fake";

  private readonly PipelineTrace _trace;

  #endregion

  #region Ctors

  public FakePingQueryHandler(PipelineTrace trace)
  {
    _trace = trace;
  }

  #endregion

  #region Implementation of IPipelineBehavior<PingQuery,string>

  public ValueTask<string> Handle(
    PingQuery message,
    MessageHandlerDelegate<PingQuery, string> next,
    CancellationToken cancellationToken)
  {
    _trace.Steps.Add("Handler");

    return ValueTask.FromResult(Response);
  }

  #endregion
}
