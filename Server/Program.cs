using Microsoft.EntityFrameworkCore;
using Server.Commands;
using Server.Data;
using Server.Infrastructure;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

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
