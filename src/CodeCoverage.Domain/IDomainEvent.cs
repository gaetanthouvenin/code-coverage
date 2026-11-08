// ------------------------------------------------------------------------------------------------
// <copyright file="IDomainEvent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain;

/// <summary>Marks something that happened in the Domain and that other parts may react to.</summary>
/// <remarks>
///   There is no domain event in the MVP: follow-up work goes through the job queue, enqueued in the
///   transaction of the change. This marker is kept for later features.
/// </remarks>
public interface IDomainEvent;
