using Microsoft.EntityFrameworkCore;
using Serilog;
using Server.Commands;
using Server.Data;
using Server.Infrastructure;
using Server.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/server-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    AppConfig.Instance.Initialize(builder.Configuration);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(AppConfig.Instance.ConnectionString));

    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<SettingsService>();
    builder.Services.AddScoped<BackupService>();
    builder.Services.AddScoped<SmetaService>();
    builder.Services.AddScoped<LimitService>();
    builder.Services.AddScoped<ExpenseService>();
    builder.Services.AddScoped<PeriodService>();
    builder.Services.AddScoped<AnalysisService>();
    builder.Services.AddScoped<RequestService>();
    builder.Services.AddScoped<ReportService>();
    builder.Services.AddScoped<DecisionService>();
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddSingleton<CommandDispatcher>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    builder.WebHost.UseUrls($"http://{AppConfig.Instance.ServerAddress}:{AppConfig.Instance.ServerPort}");

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Сервер аварийно завершился");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
