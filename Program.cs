
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
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(int.Parse(port)));


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

            builder.Services.AddControllers();

            // Clerk JWT Bearer Authentication
            var clerkAuthority = builder.Configuration["Clerk:Authority"] ?? "";
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = clerkAuthority;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = true,
                        ValidIssuer = clerkAuthority,
                        NameClaimType = "sub"
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
