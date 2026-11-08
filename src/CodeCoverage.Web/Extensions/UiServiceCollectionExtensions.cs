// ------------------------------------------------------------------------------------------------
// <copyright file="UiServiceCollectionExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Services;

namespace CodeCoverage.Web.Extensions;

/// <summary>Registers the services of the Coverage UI components (<c>Web/Components/Ui</c>).</summary>
public static class UiServiceCollectionExtensions
{
  #region Methods

  /// <summary>
  ///   Registers the services the UI components need: the localization of their strings and the
  ///   toasts of the circuit (<see cref="IToastService" />).
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The strings come from <c>Resources/UiStrings.resx</c>, without a resources path. The toast
  ///   service is scoped: each circuit has its own toasts, shown by its <c>CcToast</c> stack.
  /// </remarks>
  public static IServiceCollection AddCoverageUi(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddLocalization();
    services.AddScoped<IToastService, ToastService>();

    return services;
  }

  #endregion
}
