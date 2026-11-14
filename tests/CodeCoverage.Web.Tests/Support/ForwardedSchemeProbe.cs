// ------------------------------------------------------------------------------------------------
// <copyright file="ForwardedSchemeProbe.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CodeCoverage.Web.Tests.Support;

// Runs before the pipeline of Program: gives every request the remote address of a proxy, then
// returns the scheme the host finally saw (after UseForwardedHeaders) in the X-Seen-Scheme header.
public sealed class ForwardedSchemeProbe(IPAddress remoteAddress) : IStartupFilter
{
  #region Fields

  public const string SeenSchemeHeader = "X-Seen-Scheme";

  #endregion

  #region Methods

  public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
    app =>
    {
      app.Use(async (context, nextMiddleware) =>
              {
                context.Connection.RemoteIpAddress = remoteAddress;
                context.Response.OnStarting(() =>
                                            {
                                              context.Response.Headers[SeenSchemeHeader] = context.Request.Scheme;
                                              return Task.CompletedTask;
                                            });
                await nextMiddleware(context);
              });
      next(app);
    };

  #endregion
}
