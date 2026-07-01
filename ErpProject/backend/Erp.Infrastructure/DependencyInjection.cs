using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Auth.Commands;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Messaging;
using Erp.Infrastructure.Security;
using Erp.Infrastructure.Services;
using Erp.Infrastructure.Services.Sii;
using Erp.Infrastructure.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace Erp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Stripe: strongly-typed options with startup validation.
        // Production values come from env vars: Stripe__SecretKey, Stripe__WebhookSecret,
        // Stripe__PriceIds__Starter, Stripe__PriceIds__Professional, Stripe__PriceIds__Enterprise
        services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Email: SMTP transactional email.
        // Production env vars: Email__Host, Email__Port, Email__Username, Email__Password,
        //   Email__FromAddress, Email__FromName, Email__AppBaseUrl
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        // EmailAuthOptions shares the same "Email" section (only AppBaseUrl is used by auth handlers)
        services.AddOptions<EmailAuthOptions>()
            .BindConfiguration(EmailAuthOptions.SectionName);
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<Erp.Application.Common.Interfaces.IPortalUrlProvider, PortalUrlProvider>();

        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ErpDbContext>());
        services.AddScoped<Erp.Application.Common.Interfaces.ILicensingDbContext>(
            provider => provider.GetRequiredService<ErpDbContext>());
        // Module interfaces (IBillingDbContext, ICrmDbContext, IInventoryDbContext,
        // IAccountingDbContext, IExpensesDbContext) are now registered by each module's
        // own Infrastructure DI (AddBillingInfrastructure, AddCrmInfrastructure, etc.).
        services.AddScoped<IPlanLimitService, PlanLimitService>();
        services.AddScoped<OutboxProcessorJob>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<StripeService>();

        // ABAC: Permission service (Redis-cached, role+user resolution)
        services.AddScoped<IPermissionService, PermissionService>();

        // ABAC: Current user accessor (reads UserId from JWT via IHttpContextAccessor)
        services.AddScoped<IHttpContextCurrentUserAccessor, HttpContextCurrentUserAccessor>();

        // ABAC: MVC filter (scoped so it can inject IPermissionService)
        services.AddScoped<AbacAuthorizationFilter>();

        // SII: XML generation, XAdES-BES signing, AEAT SOAP submission
        services.AddScoped<SiiXmlGenerator>();
        services.AddScoped<SiiSigningService>();
        services.AddScoped<SiiSubmissionService>();

        // VERI*FACTU (RD 1007/2023): XML registro TIKE + envío (distinto de SII)
        services.AddScoped<VerifactuXmlGenerator>();
        services.AddScoped<VerifactuSubmissionService>();

        // SII: named HTTP client with mTLS + retry (x3 exponential) + circuit breaker
        // Handler lifetime managed by IHttpClientFactory (cert loaded once per rotation cycle).
        services.AddHttpClient("sii-aeat", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var handler = new HttpClientHandler();
                var certPath = cfg["Sii:CertPath"];
                var certPass = cfg["Sii:CertPass"];
                if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
                {
                    var cert = new X509Certificate2(certPath, certPass,
                        X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
                    handler.ClientCertificates.Add(cert);
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                }
                return handler;
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.FailureRatio = 0.5;
            });

        // Verifactu: hash chain + QR (RD 1007/2023)
        // VerifactuOptions validates NifSoftware at startup — app will refuse to start
        // if NifSoftware is blank or uses the dummy "B00000000" value.
        // Production env vars: Verifactu__NifSoftware, Verifactu__NombreSoftware,
        //   Verifactu__IdSistema, Verifactu__Version, Verifactu__NumeroInstalacion
        services.AddOptions<VerifactuOptions>()
            .BindConfiguration(VerifactuOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IVerifactuService, VerifactuService>();
        services.AddScoped<IVerifactuSubmissionService, VerifactuSubmissionService>();

        // RL-4: Almacenamiento de archivos sobre MinIO/S3 (cifrado en reposo)
        // Config: Storage:Endpoint, Storage:AccessKey, Storage:SecretKey, Storage:UseSSL
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // VIES: validación de NIF intracomunitarios (EU VAT Information Exchange System)
        // Endpoint oficial: https://ec.europa.eu/taxation_customs/vies/services/checkVatService
        services.AddHttpClient("vies", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "text/xml");
        });
        services.AddScoped<IViesService, ViesService>();

        // Calendario fiscal: generación de eventos y recordatorios
        services.AddScoped<IFiscalCalendarService, FiscalCalendarService>();
        services.AddScoped<FiscalReminderJob>();

        return services;
    }

    /// <summary>
    /// Registers RabbitMQ message bus, OutboxRelayService, and ErpEventConsumerService.
    /// Only call this when RabbitMQ:Enabled = true in configuration.
    /// When disabled, Hangfire OutboxProcessorJob remains the fallback transport.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services)
    {
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddScoped<IMessageBus, RabbitMqMessageBus>();
        services.AddHostedService<OutboxRelayService>();
        services.AddHostedService<ErpEventConsumerService>();
        return services;
    }
}
