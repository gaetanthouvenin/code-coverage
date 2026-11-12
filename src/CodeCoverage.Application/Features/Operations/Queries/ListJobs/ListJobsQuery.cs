// ------------------------------------------------------------------------------------------------
// <copyright file="ListJobsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Operations;

namespace CodeCoverage.Application.Features.Operations.Queries.ListJobs;

/// <summary>Lists one page of the background jobs, the most recent first: metadata only.</summary>
/// <param name="State">The state of the jobs, or <see langword="null" /> for every state.</param>
/// <param name="Page">The number of the page, from 1.</param>
/// <param name="PageSize">The number of jobs of a page, 1 to <see cref="MaxPageSize" />.</param>
public sealed record ListJobsQuery(JobState? State, int Page = 1, int PageSize = ListJobsQuery.DefaultPageSize) : IQuery<JobPage>, IAdminOnly
{
  #region Fields

  /// <summary>The number of jobs of a page when none is asked.</summary>
  public const int DefaultPageSize = 25;

  /// <summary>The largest page that can be asked.</summary>
  public const int MaxPageSize = 100;

  /// <summary>
  ///   The largest page that can be asked: its offset, <c>(page - 1) * MaxPageSize</c>, stays within an
  ///   <see cref="int" />.
  /// </summary>
  public const int MaxPage = int.MaxValue / MaxPageSize;

  #endregion
}
