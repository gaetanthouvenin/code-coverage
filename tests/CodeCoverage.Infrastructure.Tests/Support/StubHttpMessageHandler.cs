// ------------------------------------------------------------------------------------------------
// <copyright file="StubHttpMessageHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Answers every request with what a test decides, and keeps what was sent.
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, string, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
  #region Fields

  private readonly List<string> _bodies = [];

  private int _calls;

  #endregion

  #region Properties

  public int Calls => Volatile.Read(ref _calls);

  public IReadOnlyList<string> Bodies
  {
    get
    {
      lock (_bodies)
      {
        return [.. _bodies];
      }
    }
  }

  #endregion

  #region Methods

  public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    Interlocked.Increment(ref _calls);

    var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

    lock (_bodies)
    {
      _bodies.Add(body);
    }

    return await respond(request, body);
  }

  #endregion
}
