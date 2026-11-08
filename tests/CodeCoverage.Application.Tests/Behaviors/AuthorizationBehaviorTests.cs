// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationBehaviorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Behaviors;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Security;
using CodeCoverage.Application.Tests.Behaviors.Support;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Behaviors;

public sealed class AuthorizationBehaviorTests
{
  #region Fields

  private int _nextCalls;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_AdminOnlyWithoutAdmin_ShouldThrowForbidden()
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new AdminOnlyCommand(), Principals.User(isAdmin: false)));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AdminOnlyFromAnonymous_ShouldThrowUnauthorized()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(
                      async () => await SendAsync(new AdminOnlyCommand(), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Unauthenticated);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AdminOnlyFromAdmin_ShouldCallNext()
  {
    var response = await SendAsync(new AdminOnlyCommand(), Principals.User(isAdmin: true));

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Handle_SystemOnlyFromUser_ShouldThrowForbidden(bool isAdmin)
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new SystemOnlyCommand(), Principals.User(isAdmin)));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_SystemOnlyFromAnonymous_ShouldThrowForbidden()
  {
    var exception = await Should.ThrowAsync<ForbiddenException>(
                      async () => await SendAsync(new SystemOnlyCommand(), Principals.Anonymous()));

    exception.ErrorCode.ShouldBe(SecurityErrors.Forbidden);
    _nextCalls.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_AsSystem_ShouldPassEveryCheck()
  {
    var system = Principals.System();

    (await SendAsync(new SystemOnlyCommand(), system)).ShouldBe("handled");
    (await SendAsync(new AdminOnlyCommand(), system)).ShouldBe("handled");
    (await SendAsync(new SampleCommand("main", "token"), system)).ShouldBe("handled");
    _nextCalls.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_WithoutMarker_ShouldCallNextForAnonymous()
  {
    var response = await SendAsync(new SampleCommand("main", "token"), Principals.Anonymous());

    response.ShouldBe("handled");
    _nextCalls.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_WithNullNext_ShouldThrowArgumentNullException()
  {
    var behavior = new AuthorizationBehavior<AdminOnlyCommand, string>(Principals.System());

    await Should.ThrowAsync<ArgumentNullException>(
      async () => await behavior.Handle(new AdminOnlyCommand(), null!, Ct));
  }

  private ValueTask<string> SendAsync<TMessage>(TMessage message, ICurrentUser currentUser)
    where TMessage : IMessage
  {
    var behavior = new AuthorizationBehavior<TMessage, string>(currentUser);

    return behavior.Handle(message, Next, Ct);
  }

  private ValueTask<string> Next<TMessage>(TMessage message, CancellationToken cancellationToken)
  {
    _nextCalls++;

    return ValueTask.FromResult("handled");
  }

  #endregion
}
