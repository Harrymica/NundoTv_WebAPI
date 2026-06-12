using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Channel> Channels => Set<Channel>();
        public DbSet<BlockedChannel> BlockedChannels => Set<BlockedChannel>();
        public DbSet<ChannelLogo> ChannelLogos => Set<ChannelLogo>();
        public DbSet<BlockedChannelLogo> BlockedChannelLogos => Set<BlockedChannelLogo>();
        public DbSet<BlockedKeyword> BlockedKeywords => Set<BlockedKeyword>();
        public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
        public DbSet<IMerlChannel> ImerlChannels => Set<IMerlChannel>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Interest> Interests => Set<Interest>();
        public DbSet<UserInterest> UserInterests => Set<UserInterest>();
        public DbSet<LiveChannel> LiveChannels => Set<LiveChannel>();
        public DbSet<LivePremiumChannel> LivePremiumChannels => Set<LivePremiumChannel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- User ---
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Value converter: List<string> <-> JSON string for PostgreSQL jsonb columns
            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

            // Value comparer so EF Core can detect changes in List<string> properties
            var stringListComparer = new ValueComparer<List<string>>(
                (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!
            );

            // --- Channel ---
            modelBuilder.Entity<Channel>(entity =>
            {
                entity.HasIndex(c => c.IptvId).IsUnique();

                entity.Property(c => c.AltNames)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(c => c.Owners)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(c => c.Categories)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);
            });

            // --- BlockedChannel ---
            modelBuilder.Entity<BlockedChannel>(entity =>
            {
                entity.HasIndex(c => c.IptvId).IsUnique();

                entity.Property(c => c.AltNames)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(c => c.Owners)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(c => c.Categories)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);
            });

            // --- ChannelLogo ---
            modelBuilder.Entity<ChannelLogo>(entity =>
            {
                entity.HasOne(l => l.Channel)
                      .WithMany()
                      .HasForeignKey(l => l.ChannelId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(l => l.Tags)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);
            });

            // --- BlockedChannelLogo ---
            modelBuilder.Entity<BlockedChannelLogo>(entity =>
            {
                entity.HasOne(l => l.BlockedChannel)
                      .WithMany()
                      .HasForeignKey(l => l.BlockedChannelId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(l => l.Tags)
                      .HasColumnType("jsonb")
                      .HasConversion(stringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);
            });

            // Seed default blocked keywords
            modelBuilder.Entity<BlockedKeyword>().HasData(
                new BlockedKeyword { Id = 1, Keyword = "xxx" },
                new BlockedKeyword { Id = 2, Keyword = "porn" },
                new BlockedKeyword { Id = 3, Keyword = "adult" },
                new BlockedKeyword { Id = 4, Keyword = "sex" },
                new BlockedKeyword { Id = 5, Keyword = "18+" },
                new BlockedKeyword { Id = 6, Keyword = "nsfw" },
                new BlockedKeyword { Id = 7, Keyword = "onlyfans" },
                new BlockedKeyword { Id = 8, Keyword = "hentai" },
                new BlockedKeyword { Id = 9, Keyword = "casino" },
                new BlockedKeyword { Id = 10, Keyword = "gambling" }
            );

            modelBuilder.Entity<Interest>().HasData(
                new Interest { Id = new Guid("3bdd18f8-0af2-41fb-ac78-f22ea52bc3db"), Name = "Sports" },
                new Interest { Id = new Guid("61107eb1-86d6-4d6f-93e9-6888cc335a2a"), Name = "News" },
                new Interest { Id = new Guid("2fb507af-70f1-492a-a7e3-b0335c27ebe9"), Name = "Entertainment" },
                new Interest { Id = new Guid("8d27048a-773c-4530-a298-b1a7c43df201"), Name = "Cartoon" },
                new Interest { Id = new Guid("d0ba9a2f-bad5-495f-a7ad-fd33fbe9296f"), Name = "Kids" },
                new Interest { Id = new Guid("8fe0abf8-08a2-4ec0-b810-b26b5c41f1e1"), Name = "Movies" },
                new Interest { Id = new Guid("3f4e3aa7-6492-4e33-a4b5-0147e3c3acbb"), Name = "Lifestyle" },
                new Interest { Id = new Guid("c8fdbae6-3886-4f4f-b01e-ed2325591b84"), Name = "Reality Show" },
                new Interest { Id = new Guid("a466bfe4-954f-442c-8abd-40baaa067520"), Name = "Fashion" },
                new Interest { Id = new Guid("485cf338-8ee5-4038-a629-651311c4d5f4"), Name = "Discoveries" },
                new Interest { Id = new Guid("668d01f4-825c-4f63-9066-e36c779bae5b"), Name = "Anime" },
                new Interest { Id = new Guid("5e9776f1-1668-4de1-bbea-adc29b5795db"), Name = "Food" },
                new Interest { Id = new Guid("ac45bc0a-c4ea-435d-b61f-c50cb8901c1f"), Name = "Science" }
            );


            modelBuilder.Entity<UserInterest>()
              .HasKey(ui => new { ui.UserId, ui.InterestId });

            modelBuilder.Entity<UserInterest>()
                .HasOne(ui => ui.User)
                .WithMany(u => u.UserInterests)
                .HasForeignKey(ui => ui.UserId);

            modelBuilder.Entity<UserInterest>()
                .HasOne(ui => ui.Interest)
                .WithMany(i => i.UserInterests)
                .HasForeignKey(ui => ui.InterestId);

            // --- LiveChannel ---
            modelBuilder.Entity<LiveChannel>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.LanguagesRaw)
                      .HasColumnType("text");

                entity.Property(c => c.CategoriesRaw)
                      .HasColumnType("text");
            });

            // --- LivePremiumChannel ---
            modelBuilder.Entity<LivePremiumChannel>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.LanguagesRaw)
                      .HasColumnType("text");

                entity.Property(c => c.CategoriesRaw)
                      .HasColumnType("text");
            });
        }
    }
}
