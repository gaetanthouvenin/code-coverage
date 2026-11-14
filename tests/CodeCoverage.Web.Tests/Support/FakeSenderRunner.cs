// ------------------------------------------------------------------------------------------------
// <copyright file="FakeSenderRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Tests.Support;

// Registers a UiRequestRunner whose requests reach a fake ISender, each in its own scope as in a circuit. The circuit
// is signed in, unless a test registers an anonymous CurrentUserContext after it (AddAnonymousSession).
public static class FakeSenderRunner
{
  #region Methods

  public static IServiceCollection AddFakeSenderRunner(this IServiceCollection services, ISender sender)
  {
    var inner = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();

    services.AddSingleton<ErrorCodeLocalizer>();
    services.AddScoped(_ => new ScopedSender(inner.GetRequiredService<IServiceScopeFactory>(), A.Fake<ISendScopeInitializer>()));
    services.AddScoped<UiRequestRunner>();
    services.TryAddScoped(static _ => SignedIn());
    services.TryAddScoped<SessionState>();

    return services;
  }

  public static IServiceCollection AddAnonymousSession(this IServiceCollection services) =>
    services.AddScoped(static _ => new CurrentUserContext());

  private static CurrentUserContext SignedIn()
  {
    var user = new CurrentUserContext();
    user.SetUser(42, "octocat", Forge.GitHub, "583231");

    return user;
  }

  #endregion
}
