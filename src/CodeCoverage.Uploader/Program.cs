// ------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader;
using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

var reporter = new ConsoleReporter(Console.Out);

using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

var runner = new UploadRunner(
  new UploadClient(
    client,
    new RetryPolicy(Task.Delay, reporter),
    new OidcTokenFetcher(client, Environment.GetEnvironmentVariable, reporter),
    reporter),
  new UploadStatusPoller(client, Task.Delay, TimeProvider.System, reporter),
  Environment.GetEnvironmentVariable,
  reporter);

return await UploadCommandFactory
             .RunAsync(args, Environment.GetEnvironmentVariable, reporter, runner.RunAsync, CancellationToken.None)
             .ConfigureAwait(false);
