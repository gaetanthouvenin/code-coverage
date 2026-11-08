// ------------------------------------------------------------------------------------------------
// <copyright file="MessagingConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Configuration.Identity;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Messaging;

namespace CodeCoverage.Web.Configuration;

/// <summary>Registers how the pages send their requests: <see cref="UiRequestRunner" /> over <see cref="ScopedSender" />.</summary>
public static class MessagingConfiguration
{
  #region Methods

  /// <summary>Registers the request runner of the pages, its scoped sender and the translation of the error codes.</summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The runner, the sender and the scope initializer are scoped to the circuit; each request then
  ///   gets a scope of its own. The toasts come from <c>AddCoverageUi()</c>, the mediator from
  ///   <c>AddApplication()</c> and the principal from <c>AddInfrastructure()</c>.
  /// </remarks>
  public static IServiceCollection AddUiMessaging(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddLocalization();
    services.AddSingleton<ErrorCodeLocalizer>();
    services.AddScoped<ISendScopeInitializer, CircuitUserScopeInitializer>();
    services.AddScoped<ScopedSender>();
    services.AddScoped<UiRequestRunner>();

    return services;
  }

  #endregion
}
