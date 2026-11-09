// ------------------------------------------------------------------------------------------------
// <copyright file="RequestBodyLimitMiddleware.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;

namespace CodeCoverage.Api.Configuration.Uploads;

/// <summary>Gives the upload endpoint its body limit, read at each request from <c>Uploads:MaxRequestMegabytes</c>.</summary>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="options">The limits of the uploads.</param>
internal sealed class RequestBodyLimitMiddleware(RequestDelegate next, IOptionsMonitor<UploadOptions> options)
{
  #region Fields

  private const long BytesPerMegabyte = 1024 * 1024;

  private static readonly PathString UploadsPath = CodeCoverageApi.Prefix + "/uploads";

  #endregion

  #region Methods

  /// <summary>Tells whether a path is the one of the upload endpoint, which the routing also reaches with a trailing slash.</summary>
  /// <param name="path">The path of the request.</param>
  /// <returns><see langword="true" /> for <c>/api/v1/uploads</c> and <c>/api/v1/uploads/</c>, whatever the case.</returns>
  private static bool IsUploadPath(PathString path) =>
    path.StartsWithSegments(UploadsPath, StringComparison.OrdinalIgnoreCase, out var rest) && rest.Value is null or "" or "/";

  /// <summary>Applies the limit, then runs the rest of the pipeline.</summary>
  /// <param name="context">The context of the request.</param>
  /// <returns>A task that completes when the pipeline ends.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
  public async Task InvokeAsync(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (IsUploadPath(context.Request.Path)
        && HttpMethods.IsPost(context.Request.Method)
        && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
    {
      limit.MaxRequestBodySize = options.CurrentValue.MaxRequestMegabytes * BytesPerMegabyte;
    }

    await next(context).ConfigureAwait(false);
  }

  #endregion
}
