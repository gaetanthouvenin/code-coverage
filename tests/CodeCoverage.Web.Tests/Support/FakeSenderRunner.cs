// ------------------------------------------------------------------------------------------------
// <copyright file="FakeSenderRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Tests.Support;

// Registers a UiRequestRunner whose requests reach a fake ISender, each in its own scope as in a circuit.
public static class FakeSenderRunner
{
  #region Methods

  public static IServiceCollection AddFakeSenderRunner(this IServiceCollection services, ISender sender)
  {
    var inner = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();

    services.AddSingleton<ErrorCodeLocalizer>();
    services.AddScoped(_ => new ScopedSender(inner.GetRequiredService<IServiceScopeFactory>(), A.Fake<ISendScopeInitializer>()));
    services.AddScoped<UiRequestRunner>();

    return services;
  }

  #endregion
}
