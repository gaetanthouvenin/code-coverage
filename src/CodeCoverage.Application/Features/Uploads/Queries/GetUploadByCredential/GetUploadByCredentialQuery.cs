// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadByCredentialQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Uploads.Queries.GetUploadByCredential;

/// <summary>Reads an upload with the credential that may send uploads to its repository.</summary>
/// <param name="Credential">The credential.</param>
/// <param name="Forge">The forge of the repository.</param>
/// <param name="Owner">The owner segment of the address.</param>
/// <param name="Name">The name of the repository.</param>
/// <param name="UploadId">The public identifier of the upload.</param>
public sealed record GetUploadByCredentialQuery(UploadCredential Credential, Forge Forge, string Owner, string Name, Guid UploadId)
  : IQuery<Upload>, IPublicOwnerAddressed;
