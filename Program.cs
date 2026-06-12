
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            // Add this line to handle Render's dynamic port assignment!
            //var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            //builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(int.Parse(port)));


            string connectionString = builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection String with name 'Default' does not exist");

            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

            // Register HttpClient for IptvSyncService with a generous timeout for large JSON files
            builder.Services.AddHttpClient<IptvSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NundoTv-WebAPI/1.0");
            });

            // Register HttpClient for ImerlSyncService
            builder.Services.AddHttpClient<ImerlSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("User-Agent", "NundoTv-WebAPI/1.0");
            });

            // --- New LiveChannel Aggregator Services ---
            builder.Services.AddHttpClient<LiveChannelSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NundoTv-WebAPI/1.0 (Channel Aggregator)");
            });

            builder.Services.AddHttpClient<LivePremiumChannelSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NundoTv-WebAPI/1.0 (Premium Aggregator)");
            });

            builder.Services.AddScoped<LiveChannelSyncService>();
            builder.Services.AddScoped<LivePremiumChannelSyncService>();
            builder.Services.AddHostedService<LiveChannelBackgroundWorker>();
            builder.Services.AddHostedService<LivePremiumChannelBackgroundWorker>();

            builder.Services.AddControllers();

            // JWT Authentication
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var key = System.Text.Encoding.ASCII.GetBytes(jwtSettings["Secret"] ?? "YourSuperSecretKeyWithAtLeast32Characters!");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Swagger / OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "NundoTv API",
                    Version = "v1",
                    Description = "IPTV channel management API — syncs channels from iptv-org and filters blocked content"
                });
            });

            // CORS — allow frontend access
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowAll");

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
