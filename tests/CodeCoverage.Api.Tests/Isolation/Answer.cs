// ------------------------------------------------------------------------------------------------
// <copyright file="Answer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

namespace CodeCoverage.Api.Tests.Isolation;

// What the Api answered a probe with, reduced to what the suite compares: the status, the error code of the
// payload and the payload itself.
internal sealed record Answer(HttpStatusCode Status, string? ErrorCode, string Body);
