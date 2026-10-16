using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Joms.Hubs;
using Joms.Endpoints;
using Joms.DTOs;
using Joms.Infrastructure;
using Joms.Middleware;
using Joms.Repositories;
using Joms.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();
builder.Services.AddScoped<IOrderWorkflowRepository, OrderWorkflowRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<NotificationOutboxDispatcher>();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<OrderWorkflowExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Browser SignalR clients send the access token in the query string for WebSockets.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    policy.WithOrigins(origins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "JOMS Notification API",
    status = "running",
    docs = "See README.md for setup and endpoints"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.MapAuthEndpoints();
app.MapNotificationEndpoints();
app.MapOrderWorkflowEndpoints();

// Development-only account provisioning for local testing. Production users must
// be provisioned through a trusted administrative process.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/users", async (
        CreateDevelopmentUserRequest request,
        IAuthService service,
        CancellationToken ct) =>
    {
        try
        {
            var created = await service.CreateDevelopmentUserAsync(request, ct);
            return created is null
                ? Results.Conflict(new { error = "That username is already in use." })
                : Results.Ok(created);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }).AllowAnonymous();
}

app.Run();
