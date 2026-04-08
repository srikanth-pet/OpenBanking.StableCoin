using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenBanking.StableCoin.API.Middleware;
using OpenBanking.StableCoin.Application.DependencyInjection;
using OpenBanking.StableCoin.Infrastructure.DependencyInjection;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/stablecoin-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7));

    // ── Services ─────────────────────────────────────────────────────────────
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Banking JWT authentication
    // In Development: use symmetric signing key so test tokens can be generated without a real IdP.
    // In Production:  set Banking:Jwt:Authority to your OIDC provider.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            var signingKey = builder.Configuration["Banking:Jwt:SigningKey"];
            var isDev = builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(signingKey);

            if (isDev)
            {
                // Development: symmetric HMAC-SHA256 key, no external OIDC call required
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Banking:Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Banking:Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey!)),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            }
            else
            {
                // Production: validate against real OIDC authority
                opts.Authority = builder.Configuration["Banking:Jwt:Authority"];
                opts.Audience = builder.Configuration["Banking:Jwt:Audience"];
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            }
        });

    builder.Services.AddAuthorization();

    // Per-customer sliding window rate limiter (30 requests / 60 seconds)
    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddSlidingWindowLimiter("per-customer", limiterOpts =>
        {
            limiterOpts.PermitLimit = builder.Configuration.GetValue("RateLimiting:PerCustomerPermitLimit", 30);
            limiterOpts.Window = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("RateLimiting:PerCustomerWindowSeconds", 60));
            limiterOpts.SegmentsPerWindow = 6;
            limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOpts.QueueLimit = 5;
        });

        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "OpenBanking Stablecoin API",
            Version = "v1",
            Description = "REST API for buying, selling, and transferring USDC stablecoin"
        });
        var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your banking JWT token. In Development: use GET /api/dev/token to obtain one.",
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Id = "Bearer",
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme
            }
        };
        opts.AddSecurityDefinition("Bearer", securityScheme);
        opts.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            { securityScheme, Array.Empty<string>() }
        });
    });

    builder.Services.AddHealthChecks();

    // ── Pipeline ─────────────────────────────────────────────────────────────
    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
        {
            diagCtx.Set("CustomerId",
                httpCtx.User.FindFirst("customer_id")?.Value ?? "anonymous");
            diagCtx.Set("RequestHost", httpCtx.Request.Host.Value);
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers().RequireRateLimiting("per-customer");
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
