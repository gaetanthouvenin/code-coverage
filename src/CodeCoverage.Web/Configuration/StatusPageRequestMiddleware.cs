// ------------------------------------------------------------------------------------------------
// <copyright file="StatusPageRequestMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace CodeCoverage.Web.Configuration;

/// <summary>Turns the request the status code pages re-execute into a plain <c>GET</c> of the status page.</summary>
/// <remarks>
///   <para>
///     The status code pages re-execute the request with its own method. A page of the console answers <c>GET</c> and
///     the <c>POST</c> of its own forms only: a body-less 400 of a <c>POST</c> (a sign-out whose antiforgery token
///     expired) or a 405 would re-execute into another empty refusal, and the browser would show a blank page.
///   </para>
///   <para>
///     The middleware runs between the status code pages and the routing of <c>Program</c>. On a re-executed request
///     of another method, it reads the status page with <c>GET</c> and forgets the endpoint the status code pages
///     matched for the original method, so that the routing that follows matches the page again. The method is given
///     back once the page is written.
///   </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class StatusPageRequestMiddleware(RequestDelegate next)
{
  #region Methods

  /// <summary>Runs the rest of the pipeline, as a <c>GET</c> for a re-executed status page.</summary>
  /// <param name="context">The request.</param>
  /// <returns>A task that completes with the request.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public async Task InvokeAsync(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    var method = context.Request.Method;

    if (context.Features.Get<IStatusCodeReExecuteFeature>() is null || HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
    {
      await next(context).ConfigureAwait(false);

      return;
    }

    context.Request.Method = HttpMethods.Get;
    context.SetEndpoint(endpoint: null);
    context.Features.Get<IRouteValuesFeature>()?.RouteValues.Clear();

    try
    {
      await next(context).ConfigureAwait(false);
    }
    finally
    {
      context.Request.Method = method;
    }
  }

  #endregion
}
