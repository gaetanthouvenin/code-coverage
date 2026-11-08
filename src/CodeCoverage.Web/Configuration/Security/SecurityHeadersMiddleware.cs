// ------------------------------------------------------------------------------------------------
// <copyright file="SecurityHeadersMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Configuration.Security;

/// <summary>Adds the security headers every response carries.</summary>
/// <remarks>
///   <para>
///     The interface is never framed: <c>Content-Security-Policy: frame-ancestors 'none'</c>
///     forbids it on every response, pages, static assets and probes alike. The CSP Blazor emits on
///     its own pages is turned off in <c>Program</c>, so this middleware is the only source of the
///     header.
///   </para>
///   <para>
///     <c>X-Frame-Options</c> is deliberately not sent, the CSP directive supersedes it: the
///     antiforgery header is suppressed in <c>Program</c>
///     (<c>SuppressXFrameOptionsHeader</c>).
///   </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
  #region Fields

  /// <summary>The value of the <c>Content-Security-Policy</c> header.</summary>
  public const string ContentSecurityPolicy = "frame-ancestors 'none'";

  #endregion

  #region Methods

  /// <summary>Sets the headers when the response starts, then runs the rest of the pipeline.</summary>
  /// <param name="context">The request.</param>
  /// <returns>A task that completes with the request.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The headers are written when the response starts, not before the rest of the pipeline: the
  ///   exception handler clears the headers of a failed response before re-executing the error
  ///   page, and that page must carry them too.
  /// </remarks>
  public Task InvokeAsync(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    context.Response.OnStarting(SetHeaders, context.Response);

    return next(context);
  }

  private static Task SetHeaders(object state)
  {
    var headers = ((HttpResponse)state).Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers.ContentSecurityPolicy = ContentSecurityPolicy;

    return Task.CompletedTask;
  }

  #endregion
}
