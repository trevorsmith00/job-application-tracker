using JobApplicationTracker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SqliteConnectionFactory>();
builder.Services.AddSingleton<MigrationService>();
builder.Services.AddScoped<JobApplicationRepository>();
builder.Services.AddScoped<JobPostingDraftRepository>();
builder.Services.AddScoped<GhostingService>();
builder.Services.AddHttpClient<JobPostingExtractionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<MigrationService>();
    await migrationService.ApplyMigrationsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
