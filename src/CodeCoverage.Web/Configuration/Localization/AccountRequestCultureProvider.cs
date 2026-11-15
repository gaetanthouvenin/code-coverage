// ------------------------------------------------------------------------------------------------
// <copyright file="AccountRequestCultureProvider.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Localization;

using CodeCoverage.Web.Localization;

namespace CodeCoverage.Web.Configuration.Localization;

/// <summary>Reads the language the account of the signed-in user keeps (M4.02u), before the culture cookie.</summary>
/// <remarks>
///   The session check (<c>SessionCookieEvents</c>) reads the user and passes its language here through the items of
///   the request: the browser of another computer, or one whose cookies were cleared, speaks the language of the account
///   from its first page.
/// </remarks>
internal sealed class AccountRequestCultureProvider : RequestCultureProvider
{
  #region Fields

  /// <summary>The key of the language of the account in <see cref="HttpContext.Items" />.</summary>
  private static readonly object ItemKey = new();

  #endregion

  #region Methods

  /// <summary>Passes the language of the account of the request to the localization of the request.</summary>
  /// <param name="context">The HTTP context.</param>
  /// <param name="language">The language of the console the account keeps.</param>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static void SetLanguage(HttpContext context, ConsoleLanguage language)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(language);

    context.Items[ItemKey] = language.Code;
  }

  #endregion

  #region Overrides of RequestCultureProvider

  /// <inheritdoc />
  public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    return Task.FromResult(httpContext.Items.TryGetValue(ItemKey, out var code) && code is string language
                             ? new ProviderCultureResult(language)
                             : null);
  }

  #endregion
}
