// ------------------------------------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Behaviors;

namespace CodeCoverage.Application;

/// <summary>Registers the use cases of CodeCoverage.</summary>
public static partial class DependencyInjection
{
  #region Methods

  /// <summary>
  ///   Registers the mediator, its handlers, its pipeline behaviors, the validators and the services
  ///   of each area.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The same collection, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   The mediator is generated at compile time by <c>Mediator.SourceGenerator</c>, referenced by this
  ///   project only (ADR-001). Its options are read by the generator, so they stay compile-time
  ///   constants. It is scoped, like the <c>DbContext</c> its handlers use.
  ///   The pipeline runs <see cref="LoggingBehavior{TMessage,TResponse}" /> first, then
  ///   <see cref="AuthorizationBehavior{TMessage,TResponse}" />, then
  ///   <see cref="ValidationBehavior{TMessage,TResponse}" />: every refusal is logged, and a message
  ///   the principal may not send is refused before its content is validated.
  ///   The behaviors need an <see cref="Domain.Ports.ICurrentUser" />, registered by the
  ///   Infrastructure.
  ///   The validators (<c>internal sealed</c> included) are discovered in this assembly.
  ///   Each area registers its services in its own <c>DependencyInjection.{Area}.cs</c> file, one by
  ///   one and never by scanning a namespace: the lifetime of a service is part of its behavior.
  /// </remarks>
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddMediator((MediatorOptions options) =>
    {
      options.Namespace = "CodeCoverage.Application.Messaging";
      options.ServiceLifetime = ServiceLifetime.Scoped;
      options.GenerateTypesAsInternal = true;
      options.Assemblies = [typeof(DependencyInjection)];
      options.PipelineBehaviors =
        [typeof(LoggingBehavior<,>), typeof(AuthorizationBehavior<,>), typeof(ValidationBehavior<,>)];
    });

    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

    return services
      .AddCommitServices()
      .AddUploadServices()
      .AddComparisonServices()
      .AddNotificationServices()
      .AddAccessServices();
  }

  #endregion
}
