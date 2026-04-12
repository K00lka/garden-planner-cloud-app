using CloudBackend.Data;
using Microsoft.EntityFrameworkCore;
using CloudBackend.Models;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets; // Potrzebne do DefaultAzureCredential

var builder = WebApplicationBuilder.CreateBuilder(args);

// Only add Key Vault in Production
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = new Uri("https://cloud-app-vault.vault.azure.net/");
    var credential = new DefaultAzureCredential();
    builder.Configuration.AddAzureKeyVault(keyVaultUrl, credential);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// --- SEKCJA USŁUG (Dependency Injection) ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Pobieramy Connection String. 
// Jeśli jesteśmy w Azure, nazwa "DbConnectionString" zostanie automatycznie 
// pobrana z Magazynu Kluczy dzięki powyższej konfiguracji.
var connectionString = builder.Configuration["DbConnectionString"] 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Rejestracja bazy danych z mechanizmem ponawiania prób (Retry Logic)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// --- AUTOMATYCZNE DANE STARTOWE ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (!context.Tasks.Any())
        {
            context.Tasks.AddRange(
                new CloudTask { Name = "Zrobić kawę", IsCompleted = true },
                new CloudTask { Name = "Zabezpieczyć aplikację w Azure", IsCompleted = true }
            );
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd bazy: {ex.Message}");
    }
}
// --- MIDDLEWARE ---
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cloud API V1");
    c.RoutePrefix = string.Empty; 
});
app.UseCors();
app.MapControllers();
app.Run();