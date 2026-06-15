using FeedBack.API.Config;
using FeedBack.API.Data;
using FeedBack.API.Middleware;
using FeedBack.API.Services;
using FeedBack.API.Validators;
// using FeedBack.API.Models;
using FluentValidation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Instrumentation.SqlClient;
using Prometheus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using AspNetCoreRateLimit;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Application Insights
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/feedback-api-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.ApplicationInsights(builder.Configuration["ApplicationInsights:ConnectionString"], TelemetryConverter.Traces)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Add OpenTelemetry with Prometheus
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("FeedbackAPI"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // .AddSqlClientInstrumentation(options => 
        // {
        //     options.SetDbStatementForText = true;
        //     options.RecordException = true;
        // })
        .AddPrometheusExporter());

// builder.Services.AddOpenTelemetry()
//     .WithTracing(tracing =>
//     {
//         tracing
//             .AddAspNetCoreInstrumentation()
//             .AddHttpClientInstrumentation();
//             // .AddSqlClientInstrumentation(options =>
//             // {
//             //     options.SetDbStatementForText = true;
//             //     options.RecordException = true;
//             // });
//     })
//     .WithMetrics(metrics =>
//     {
//         metrics
//             .SetResourceBuilder(
//                 ResourceBuilder.CreateDefault()
//                     .AddService("FeedbackAPI"))
//             .AddAspNetCoreInstrumentation()
//             .AddHttpClientInstrumentation()
//             .AddPrometheusExporter();
//     });

// Add Prometheus metrics
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddScoped<DatabaseMetricsInterceptor>();

// Configure DbContext with metrics interceptor
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<DatabaseMetricsInterceptor>();
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
});

// Add health checks for monitoring
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(new Uri("https://api.sendgrid.com/v3"), "SendGrid API")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "Redis");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Feedback API", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your token"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Ensure Hangfire database exists before Hangfire initializes
var hangfireConnStr = builder.Configuration.GetConnectionString("HangfireConnection")!;
var masterConnStr = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(hangfireConnStr)
{
    InitialCatalog = "master"
}.ConnectionString;
using (var conn = new Microsoft.Data.SqlClient.SqlConnection(masterConnStr))
{
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'FeedbackDB_Hangfire') CREATE DATABASE [FeedbackDB_Hangfire]";
    cmd.ExecuteNonQuery();
}

// Hangfire for background jobs
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(hangfireConnStr));
builder.Services.AddHangfireServer();

// JWT Authentication
var jwtKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
            ClockSkew = TimeSpan.Zero
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// Rate Limiting with Redis
builder.Services.AddRedisRateLimiting(builder.Configuration);

// Services
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IEmailQueueService, EmailQueueService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<FeedbackRequestValidator>();
builder.Services.AddHostedService<BackgroundEmailService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:8080/feedback",
                "http://localhost:8080",
                "http://localhost:8090",
                "https://xdsdataghana.cloud",
                "https://feed-back-frontend-six.vercel.app/")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

// Enable Prometheus metrics endpoint
app.UseHttpMetrics(); // prometheus-net
app.UseOpenTelemetryPrometheusScrapingEndpoint(); // OpenTelemetry

// Enable prometheus-net middleware
app.UseMetricServer();
app.UseHttpMetrics();

// Custom metrics middleware
app.UseMiddleware<MetricsMiddleware>();

// Enable health check endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    AllowCachingResponses = false
});

// Add custom metrics endpoint
app.MapGet("/metrics/custom", async (IMetricsService metricsService) =>
{
    return Results.Ok(new
    {
        message = "Custom metrics endpoint. Prometheus metrics available at /metrics and /scrape"
    });
});


app.UseCors("AllowFrontend");
app.UseIpRateLimiting();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Configure Hangfire dashboard (protected)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Recurring job to process email queue every minute
RecurringJob.AddOrUpdate<IEmailQueueService>(
    "process-email-queue",
    service => service.ProcessEmailQueueAsync(CancellationToken.None),
    "*/1 * * * *");

app.MapControllers();

// Ensure database is created and seed admin user
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    
    // Seed default admin user if none exists
    if (!await dbContext.Users.AnyAsync())
    {
        var adminPassword = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        dbContext.Users.Add(new FeedBack.API.Models.User
        {
            Email = "admin@xdsdata.com",
            PasswordHash = adminPassword,
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
    }
}

app.Run();

// Simple Hangfire authorization filter
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // In production, implement proper authorization
        return true;
    }
}
