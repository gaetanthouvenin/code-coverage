// ------------------------------------------------------------------------------------------------
// <copyright file="ScopedSenderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Infrastructure;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Messaging;

// The sender of each scope is a fake that answers ProbeQuery with the ScopeProbe of its scope.
public sealed class ScopedSenderTests : IAsyncDisposable
{
  #region Fields

  private readonly ServiceProvider _services;

  private readonly AsyncServiceScope _circuit;

  #endregion

  #region Ctors

  public ScopedSenderTests()
  {
    var services = new ServiceCollection();
    services.AddInfrastructure();
    services.AddUiMessaging();
    services.AddScoped<ScopeProbe>();
    services.AddScoped(static sp => ProbeSender(sp.GetRequiredService<ScopeProbe>()));

    _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    _circuit = _services.CreateAsyncScope();
  }

  #endregion

  #region Properties

  private ScopedSender Sender => _circuit.ServiceProvider.GetRequiredService<ScopedSender>();

  private static CancellationToken Token => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Send_ShouldUseFreshScopePerCall()
  {
    var circuitProbe = _circuit.ServiceProvider.GetRequiredService<ScopeProbe>();

    var first = await Sender.Send(new ProbeQuery(), Token);
    var second = await Sender.Send(new ProbeQuery(), Token);

    first.ShouldNotBeSameAs(second);
    first.ShouldNotBeSameAs(circuitProbe);
    second.ShouldNotBeSameAs(circuitProbe);
    first.IsDisposed.ShouldBeTrue("the scope of a request is disposed once the request is handled");
    second.IsDisposed.ShouldBeTrue("the scope of a request is disposed once the request is handled");
    circuitProbe.IsDisposed.ShouldBeFalse();
  }

  [Fact]
  public async Task Send_WithCircuitUser_ShouldRunAsThatUser()
  {
    _circuit.ServiceProvider.GetRequiredService<CurrentUserContext>().SetUser(42, "octocat", isAdmin: true);

    var probe = await Sender.Send(new ProbeQuery(), Token);

    probe.User.UserId.ShouldBe(42);
    probe.User.Login.ShouldBe("octocat");
    probe.User.IsAdmin.ShouldBeTrue();
    probe.User.IsSystem.ShouldBeFalse();
  }

  [Fact]
  public async Task Send_WithAnonymousCircuit_ShouldRunAnonymously()
  {
    var probe = await Sender.Send(new ProbeQuery(), Token);

    probe.User.IsAuthenticated.ShouldBeFalse();
    probe.User.IsSystem.ShouldBeFalse();
  }

  [Fact]
  public async Task CreateStream_ShouldKeepTheScopeUntilTheStreamEnds()
  {
    var seen = new List<(ScopeProbe Probe, bool DisposedWhileEnumerating)>();

    await foreach (var probe in Sender.CreateStream(new ProbeStreamQuery(), Token))
    {
      seen.Add((probe, probe.IsDisposed));
    }

    seen.Count.ShouldBe(2);
    seen.ShouldAllBe(s => !s.DisposedWhileEnumerating);
    seen[0].Probe.ShouldBeSameAs(seen[1].Probe);
    seen[0].Probe.IsDisposed.ShouldBeTrue();
  }

  [Fact]
  public async Task Send_WithNullRequest_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await Sender.Send((IQuery<ScopeProbe>)null!, Token));
  }

  private static ISender ProbeSender(ScopeProbe probe)
  {
    var sender = A.Fake<ISender>();
    A.CallTo(() => sender.Send(A<IQuery<ScopeProbe>>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<ScopeProbe>(probe));
    A.CallTo(() => sender.CreateStream(A<IStreamQuery<ScopeProbe>>._, A<CancellationToken>._))
     .ReturnsLazily(() => Twice(probe));
    return sender;
  }

  private static async IAsyncEnumerable<ScopeProbe> Twice(
    ScopeProbe probe,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    for (var i = 0; i < 2; i++)
    {
      await Task.Yield();
      cancellationToken.ThrowIfCancellationRequested();
      yield return probe;
    }
  }

  #endregion

  #region Implementation of IAsyncDisposable

  public async ValueTask DisposeAsync()
  {
    await _circuit.DisposeAsync();
    await _services.DisposeAsync();
  }

  #endregion
}
