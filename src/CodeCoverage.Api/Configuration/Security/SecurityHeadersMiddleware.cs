// ------------------------------------------------------------------------------------------------
// <copyright file="SecurityHeadersMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Configuration.Security;

/// <summary>Adds the security headers every response carries.</summary>
/// <remarks>
///   The REST API is never framed: <c>Content-Security-Policy: frame-ancestors 'none'</c> forbids
///   it. <c>X-Frame-Options</c> is deliberately not sent, the CSP directive supersedes it.
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
  #region Methods

  /// <summary>Sets the headers when the response starts, then runs the rest of the pipeline.</summary>
  /// <param name="context">The request.</param>
  /// <returns>A task that completes with the request.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The headers are written when the response starts, not before the rest of the pipeline: the
  ///   exception handler clears the headers of a failed response before writing its problem
  ///   details, and a refusal must carry them too.
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
    headers.ContentSecurityPolicy = "frame-ancestors 'none'";

    return Task.CompletedTask;
  }

  #endregion
}
