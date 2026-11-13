// ------------------------------------------------------------------------------------------------
// <copyright file="Answer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

namespace CodeCoverage.Web.Tests.Isolation;

// What the console answered a probe with, reduced to what the suite compares: the status and the markup of the
// page, with what belongs to the request erased.
internal sealed record Answer(HttpStatusCode Status, string Markup);
