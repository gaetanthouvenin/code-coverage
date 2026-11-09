// ------------------------------------------------------------------------------------------------
// <copyright file="PipelineOrderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Application.Tests.Behaviors.Support;
using CodeCoverage.Domain.Ports;

using ValidationException = CodeCoverage.Application.Exceptions.ValidationException;

namespace CodeCoverage.Application.Tests.Behaviors;

// Integration test of the pipeline generated from AddApplication(): Logging, then Authorization,
// then Validation, then the handler (a fake one, registered as the innermost step).
public sealed class PipelineOrderTests
{
  #region Fields

  private readonly PipelineTrace _trace = new();

  #endregion

  #region Methods

  [Fact]
  public async Task Send_ShouldRunLoggingThenValidationThenHandler()
  {
    await using var provider = BuildProvider(fail: false);
    await using var scope = provider.CreateAsyncScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

    var response = await sender.Send(new PingQuery(), TestContext.Current.CancellationToken);

    response.ShouldBe(FakePingQueryHandler.Response);
    _trace.Steps.Count.ShouldBe(4);
    _trace.Steps[0].ShouldBe("Debug: Handling PingQuery");
    _trace.Steps[1].ShouldBe("Validation");
    _trace.Steps[2].ShouldBe("Handler");
    _trace.Steps[3].ShouldStartWith("Debug: Handled PingQuery in ");
  }

  [Fact]
  public async Task Send_WithFailingRule_ShouldLogTheRefusalWithoutReachingTheHandler()
  {
    await using var provider = BuildProvider(fail: true);
    await using var scope = provider.CreateAsyncScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

    var exception = await Should.ThrowAsync<ValidationException>(
                      async () => await sender.Send(new PingQuery(), TestContext.Current.CancellationToken));

    exception.Errors.Keys.ShouldBe(["Probe"]);
    _trace.Steps.Count.ShouldBe(3);
    _trace.Steps[0].ShouldBe("Debug: Handling PingQuery");
    _trace.Steps[1].ShouldBe("Validation");
    _trace.Steps[2].ShouldStartWith("Warning: Refused PingQuery after ");
    _trace.Steps[2].ShouldEndWith(": General.ValidationFailed");
  }

  [Fact]
  public void AddApplication_ShouldRegisterTheBehaviorsInPipelineOrder()
  {
    var services = new ServiceCollection().AddApplication();

    var behaviors = services.Where(d => d.ServiceType == typeof(IPipelineBehavior<PingQuery, string>))
                            .Select(d => d.ImplementationType!.Name.Split('`')[0])
                            .ToList();

    behaviors.ShouldBe(["LoggingBehavior", "AuthorizationBehavior", "ValidationBehavior"]);
  }

  private ServiceProvider BuildProvider(bool fail)
  {
    var services = new ServiceCollection();
    services.AddSingleton(_trace);
    services.AddSingleton(typeof(ILogger<>), typeof(RecordingLogger<>));
    services.AddScoped(_ => Principals.Anonymous());

    // The ports the handlers need, provided by the Infrastructure in the hosts: ValidateOnBuild
    // resolves every handler.
    services.AddScoped(_ => A.Fake<IAuditLog>());
    services.AddScoped(_ => A.Fake<ISecretProtector>());
    services.AddScoped(_ => A.Fake<ISettingsStore>());
    services.AddScoped(_ => A.Fake<ICoverageConfigParser>());
    services.AddScoped(_ => A.Fake<IUploadTokenRepository>());
    services.AddScoped(_ => A.Fake<IRepositoryRepository>());
    services.AddScoped(_ => A.Fake<ITokenGenerator>());
    services.AddScoped(_ => A.Fake<ITokenHasher>());
    services.AddScoped(_ => A.Fake<IClock>());
    services.AddScoped(_ => A.Fake<IWebhookSignatureValidator>());
    services.AddScoped(_ => A.Fake<IWebhookDeliveryRepository>());
    services.AddScoped(_ => A.Fake<IInstallationRepository>());
    services.AddScoped(_ => A.Fake<IGitCommitRepository>());
    services.AddScoped(_ => A.Fake<IRepositoryTreeRepository>());
    services.AddScoped(_ => A.Fake<IDiffCacheRepository>());
    services.AddScoped(_ => A.Fake<IForgeGatewayProvider>());
    services.AddScoped(_ => A.Fake<ICommitConfigRepository>());
    services.AddScoped(_ => A.Fake<IOrganizationSettingsProvider>());
    services.AddApplication();
    services.AddScoped<IValidator<PingQuery>>(sp => new TracingPingQueryValidator(
                                                sp.GetRequiredService<PipelineTrace>(),
                                                fail));
    services.AddScoped<IPipelineBehavior<PingQuery, string>, FakePingQueryHandler>();

    return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
  }

  #endregion
}
