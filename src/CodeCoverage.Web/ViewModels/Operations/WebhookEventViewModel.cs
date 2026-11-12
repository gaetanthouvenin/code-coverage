// ------------------------------------------------------------------------------------------------
// <copyright file="WebhookEventViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Operations;

/// <summary>The latest delivery of a GitHub event.</summary>
/// <param name="Event">The event, e.g. <c>pull_request</c>.</param>
/// <param name="ReceivedAt">When it was received.</param>
/// <param name="IsProcessed">Whether it was handled.</param>
/// <param name="HasError">Whether its handling failed.</param>
public sealed record WebhookEventViewModel(string Event, DateTimeOffset ReceivedAt, bool IsProcessed, bool HasError);
