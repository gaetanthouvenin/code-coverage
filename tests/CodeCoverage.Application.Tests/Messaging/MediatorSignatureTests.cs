// ------------------------------------------------------------------------------------------------
// <copyright file="MediatorSignatureTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Application.Tests.Messaging;

// Freezes the v3 API of martinothamar Mediator (ADR-001): a major version that changes the
// signature of IPipelineBehavior breaks the compilation of this project.
public sealed class MediatorSignatureTests
{
  #region Methods

  [Fact]
  public async Task PipelineBehavior_ShouldCompileAgainstV3Signature()
  {
    var behavior = new SignatureProbeBehavior<PingQuery, string>();
    IPipelineBehavior<PingQuery, string> pipelineBehavior = behavior;
    var received = default(PingQuery);
    MessageHandlerDelegate<PingQuery, string> next = (message, _) =>
    {
      received = message;
      return ValueTask.FromResult("pong");
    };
    var query = new PingQuery();

    var response = await pipelineBehavior.Handle(query, next, TestContext.Current.CancellationToken);

    response.ShouldBe("pong");
    received.ShouldBeSameAs(query);
    behavior.Calls.ShouldBe(1);
  }

  [Fact]
  public void Messages_ShouldUseTheCqrsInterfacesOfV3()
  {
    typeof(PingQuery).IsAssignableTo(typeof(IQuery<string>)).ShouldBeTrue();
    typeof(IQuery<string>).IsAssignableTo(typeof(IMessage)).ShouldBeTrue();
    typeof(ICommandHandler<,>).IsInterface.ShouldBeTrue();
    typeof(IQueryHandler<,>).IsInterface.ShouldBeTrue();
  }

  #endregion
}
