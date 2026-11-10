// ------------------------------------------------------------------------------------------------
// <copyright file="FakeHttpHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

namespace CodeCoverage.Uploader.Tests.Http;

// Answers from a queue and keeps what it received, the bodies read at once as a server would.
internal sealed class FakeHttpHandler : HttpMessageHandler
{
  #region Fields

  private readonly Queue<Func<HttpResponseMessage>> _responses = new();

  #endregion

  #region Properties

  public List<(HttpRequestMessage Request, byte[] Body)> Received { get; } = [];

  #endregion

  #region Methods

  public FakeHttpHandler Then(HttpStatusCode status, string body = "{}", Action<HttpResponseMessage>? configure = null)
  {
    _responses.Enqueue(() =>
    {
      var response = new HttpResponseMessage(status) { Content = new StringContent(body) };

      configure?.Invoke(response);

      return response;
    });

    return this;
  }

  public FakeHttpHandler ThenThrow(Exception exception)
  {
    _responses.Enqueue(() => throw exception);

    return this;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);

    Received.Add((request, body));

    return _responses.Dequeue().Invoke();
  }

  #endregion
}
