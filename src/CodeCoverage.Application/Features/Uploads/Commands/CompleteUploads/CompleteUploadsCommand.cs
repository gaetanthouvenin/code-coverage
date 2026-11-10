// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteUploadsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Uploads.Commands.CompleteUploads;

/// <summary>
///   Announces that every upload of a commit was sent: the commit is notified as soon as they are all
///   processed, whatever <c>after_n_builds</c> and <c>manual_trigger</c> say. The <c>upload-complete</c> of Codecov.
/// </summary>
/// <param name="Credential">The credential that sends the uploads of the repository.</param>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner of the repository.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="Sha">The SHA of the commit.</param>
public sealed record CompleteUploadsCommand(UploadCredential Credential, Forge Forge, string Owner, string Name, string Sha)
  : ICommand<UploadCompletion>, IPublicOwnerAddressed;