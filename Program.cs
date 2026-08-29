
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Services;
using NundoTv_WebAPI.Services.ChannelResolvers;

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

            // EPG Background Service
            builder.Services.AddHttpClient<EpgSyncService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("User-Agent", "NundoTv-WebAPI/1.0 (EPG Sync)");
            });
            // builder.Services.AddHostedService<EpgSyncService>(); // PAUSED — re-enable when needed

            // Suppress noisy Entity Framework SQL command logging (INSERT floods)
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

            // Live Sports Aggregator & Scraper Background Service
            builder.Services.AddHttpClient("StreamScraper", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            });

            builder.Services.AddHttpClient("HealthCheck", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            });

            builder.Services.AddSingleton<IStreamScraperService, StreamScraperService>();
            builder.Services.AddHostedService<StreamScraperBackgroundWorker>();

            // Channel Resolver Strategies
            builder.Services.AddSingleton<DaddyLiveResolver>();
            builder.Services.AddSingleton<IProviderChannelResolver, DaddyLiveResolver>();
            builder.Services.AddSingleton<IProviderChannelResolver, StreamedSuResolver>();
            builder.Services.AddSingleton<IProviderChannelResolver, Score808Resolver>();
            builder.Services.AddSingleton<Score808Resolver>();
            builder.Services.AddSingleton<IChannelResolverService, ChannelResolverService>();
            builder.Services.AddSingleton<IPdfExportService, PdfExportService>();

            builder.Services.AddHttpClient<SportsScraperService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            });
            builder.Services.AddHostedService<SportsScraperBackgroundWorker>();

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

            // Auto-apply EF Core database migrations on startup
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    dbContext.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while applying EF Core database migrations.");
                }
            }

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
