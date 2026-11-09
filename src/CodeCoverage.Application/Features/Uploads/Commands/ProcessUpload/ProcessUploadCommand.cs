// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessUploadCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;

/// <summary>Reads the reports of an upload: parses them, fixes their paths and stores the coverage of the upload.</summary>
/// <param name="UploadId">The public identifier of the upload.</param>
/// <param name="Worker">The worker that runs the job.</param>
public sealed record ProcessUploadCommand(Guid UploadId, string Worker) : ICommand, ISystemOnly;
