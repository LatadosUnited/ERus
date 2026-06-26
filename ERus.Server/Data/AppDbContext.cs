using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ERus.Server.Data;

public class AppDbContext : DbContext
{
    public DbSet<UserAccount> Accounts { get; set; } = null!;
    public DbSet<RemoteProjectData> Projects { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_data.db");
        // We append Password to use SQLCipher encryption
        optionsBuilder.UseSqlite($"Data Source={dbPath};Password=ERusMasterKey2026!;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>()
            .HasKey(u => u.Id);

        modelBuilder.Entity<UserAccount>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<RemoteProjectData>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<RemoteProjectData>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
