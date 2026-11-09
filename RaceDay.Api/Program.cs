using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
using RaceDay.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);


if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayDbContext>(opt =>
        opt.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));
}


var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Missing Jwt configuration section.");
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });


builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("OrganiserOnly", p => p.RequireRole("Organiser"));
    o.AddPolicy("ParticipantOnly", p => p.RequireRole("Participant"));
});

// ---------- Controllers + Swagger ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "Event management API for South African running, walking and cycling events."
    });

    // Needs <GenerateDocumentationFile>true</GenerateDocumentationFile> in the .csproj
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);

    // "Authorize" button so endpoints can be tested in the browser with a JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login (no 'Bearer ' prefix)."
    });
    
    c.OperationFilter<AuthOperationFilter>();
    c.SupportNonNullableReferenceTypes();
});


builder.Services.AddHttpClient<IWeatherService, WeatherService>(c => c.Timeout = TimeSpan.FromSeconds(10));


builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

var app = builder.Build();


if (!app.Environment.IsEnvironment("Testing"))
{
    
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

            
            if (db.Database.GetMigrations().Any())
                db.Database.Migrate();
            else
                db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            app.Logger.LogWarning(ex, "Database not ready yet (attempt {Attempt} of 10). Retrying in 3 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "RaceDay API";
    c.EnablePersistAuthorization();   // keeps your token after a page refresh
    c.DisplayRequestDuration();
});

app.UseHttpsRedirection();
app.UseAuthentication();   
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the test project use WebApplicationFactory<Program>
public partial class Program { }
