// ------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader;
using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Output;

var reporter = new ConsoleReporter(Console.Out);

return await UploadCommandFactory
             .RunAsync(
               args,
               Environment.GetEnvironmentVariable,
               reporter,
               UploadAsync,
               CancellationToken.None)
             .ConfigureAwait(false);

// Nothing is uploaded yet: the report search, the CI detection and the sending come next. Having
// uploaded nothing is the failure that --fail-on-error asks to be told about.
Task<bool> UploadAsync(UploaderSettings settings, CancellationToken cancellationToken)
{
  reporter.Warning($"Nothing was uploaded to {settings.Url}: the sending is not implemented yet.");
  return Task.FromResult(false);
}
