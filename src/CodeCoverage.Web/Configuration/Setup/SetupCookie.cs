// ------------------------------------------------------------------------------------------------
// <copyright file="SetupCookie.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.DataProtection;

namespace CodeCoverage.Web.Configuration.Setup;

/// <summary>
///   The cookie that carries the setup token, and the <c>state</c> of the round trip through GitHub, from one request
///   of the wizard to the next.
/// </summary>
/// <remarks>
///   <para>
///     The value is protected by Data Protection, so a browser holds no readable token, and the cookie is HttpOnly,
///     <c>SameSite=Lax</c> and limited to the path of the wizard: the round trip of the manifest comes back as a
///     navigation of the browser, which Lax allows, while nothing else on the instance sends it.
///   </para>
///   <para>
///     The token never travels in the address bar beyond the first visit, and the <c>state</c> is never read from the
///     query: it is compared with the one this cookie holds, and dropped as soon as it is used, so a replayed return
///     is refused.
///   </para>
/// </remarks>
/// <param name="protection">The provider of the data protectors.</param>
internal sealed class SetupCookie(IDataProtectionProvider protection)
{
  #region Fields

  /// <summary>The name of the cookie.</summary>
  internal const string Name = "cc-setup";

  /// <summary>The path the cookie is sent to: the wizard only.</summary>
  internal const string Path = "/setup";

  /// <summary>The purpose that separates this protector from every other one of the instance.</summary>
  private const string Purpose = "CodeCoverage.Web.Setup.v1";

  /// <summary>What separates the token from the state in the protected payload, and never appears in either.</summary>
  private const char Separator = '\n';

  private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

  #endregion

  #region Methods

  /// <summary>Reads the token and the state the cookie of a request holds.</summary>
  /// <param name="context">The context of the request.</param>
  /// <returns>The token and the state, both <see langword="null" /> when the cookie is missing or unreadable.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public (string? Token, string? State) Read(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (!context.Request.Cookies.TryGetValue(Name, out var protectedValue) || string.IsNullOrEmpty(protectedValue))
    {
      return (null, null);
    }

    try
    {
      var parts = _protector.Unprotect(protectedValue).Split(Separator, 2);

      return (parts[0], parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
      // A cookie of another instance, or one whose keys are gone: the wizard asks for the token again.
      return (null, null);
    }
  }

  /// <summary>Writes the token and the state of the wizard in the cookie of the answer.</summary>
  /// <param name="context">The context of the request.</param>
  /// <param name="token">The setup token.</param>
  /// <param name="state">The state of a round trip through GitHub, or <see langword="null" />.</param>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> or <paramref name="token" /> is <see langword="null" />.</exception>
  public void Write(HttpContext context, string token, string? state)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentException.ThrowIfNullOrWhiteSpace(token);

    context.Response.Cookies.Append(
      Name,
      _protector.Protect($"{token}{Separator}{state}"),
      new CookieOptions
      {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        Path = Path,
      });
  }

  /// <summary>Drops the cookie: the setup is over, or the token it held is refused.</summary>
  /// <param name="context">The context of the request.</param>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public void Clear(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    context.Response.Cookies.Delete(Name, new CookieOptions { Path = Path, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax });
  }

  #endregion
}
