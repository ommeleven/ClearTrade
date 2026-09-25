using Brokerage.Core.Interfaces;
using Brokerage.Data;
using Brokerage.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Brokerage.Api.Infrastructure;

public static class ServiceRegistration
{
    public const string ConnectionStringName = "BrokerageDb";

    public static bool HasDatabase(this IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config.GetConnectionString(ConnectionStringName));

    /// <summary>Uses PostgreSQL when a connection string is configured, otherwise an in-memory store so the API runs with zero setup.</summary>
    public static IServiceCollection AddBrokeragePersistence(this IServiceCollection services, IConfiguration config)
    {
        var health = services.AddHealthChecks();

        if (config.HasDatabase())
        {
            services.AddDbContext<BrokerageDbContext>(o => o.UseNpgsql(
                config.GetConnectionString(ConnectionStringName),
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IUsageStore, EfUsageStore>();
            health.AddDbContextCheck<BrokerageDbContext>("database", tags: ["ready"]);
        }
        else
        {
            services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
            services.AddSingleton<IUsageStore, InMemoryUsageStore>();
        }

        services.AddSingleton<IPriceFeed, StubPriceFeed>();
        return services;
    }

    public static IServiceCollection AddBrokerageServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }

    public static IServiceCollection AddBrokerageAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Section))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key must be configured and at least 32 characters long.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.SigningKey,
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddBrokerageSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ClearTrade Brokerage API",
                Version = "v1",
                Description = "Accounts, deposits and withdrawals with JWT auth, validation and rate limiting. " +
                              "Try it: POST /api/auth/login with username `demo` / password `Demo@12345`, then click Authorize.",
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken returned by /api/auth/login.",
            });
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });
            var xml = Path.Combine(AppContext.BaseDirectory, "Brokerage.Api.xml");
            if (File.Exists(xml)) c.IncludeXmlComments(xml);
        });
        return services;
    }
}
