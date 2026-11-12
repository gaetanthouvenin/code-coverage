// ------------------------------------------------------------------------------------------------
// <copyright file="JobRowViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>A failed or dead job, metadata only: never its payload, its keys nor its error text.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Type">Its type, e.g. <c>ProcessUpload</c>.</param>
/// <param name="Organization">The login of its organization, or <see langword="null" /> for the lane of the instance.</param>
/// <param name="IsDead">Whether it used up its attempts, rather than failed for good.</param>
/// <param name="Attempts">The attempts it made.</param>
/// <param name="MaxAttempts">The attempts it may make.</param>
/// <param name="At">When it ended, else when it was created.</param>
public sealed record JobRowViewModel(long Id, string Type, string? Organization, bool IsDead, int Attempts, int MaxAttempts, DateTimeOffset At);
