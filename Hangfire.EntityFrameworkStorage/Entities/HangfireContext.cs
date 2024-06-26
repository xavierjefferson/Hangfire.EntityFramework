﻿using Hangfire.EntityFrameworkStorage.Interfaces;
using Hangfire.EntityFrameworkStorage.Maps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class HangfireContext : DbContext
{
    private readonly string _prefix = "Hangfire_";

    public HangfireContext(DbContextOptions<HangfireContext> options) : base(options)
    {
    }

    public DbSet<_AggregatedCounter> AggregatedCounters { get; set; }
    public DbSet<_Counter> Counters { get; set; }
    public DbSet<_DistributedLock> DistributedLocks { get; set; }
    public DbSet<_Dual> Duals { get; set; }
    public DbSet<_Hash> Hashes { get; set; }
    public DbSet<_Job> Jobs { get; set; }
    public DbSet<_JobParameter> JobParameters { get; set; }
    public DbSet<_JobQueue> JobQueues { get; set; }
    public DbSet<_JobState> JobStates { get; set; }
    public DbSet<_List> Lists { get; set; }
    public DbSet<_Server> Servers { get; set; }
    public DbSet<_Set> Sets { get; set; }

    private void MapExpireAt<T>(EntityTypeBuilder<T> entity) where T : EntityBase, IExpirable
    {
        entity.Property(i => i.ExpireAt).IsRequired(false);
        entity.HasIndex(i => i.ExpireAt);
    }

    private void MapCreatedAt<T>(EntityTypeBuilder<T> entity) where T : EntityBase, ICreatedAt
    {
        entity.Property(i => i.CreatedAt).IsRequired();
        entity.HasIndex(i => i.CreatedAt);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<_AggregatedCounter>(entity =>
        {
            entity.ToTable($"{_prefix}AggregatedCounter");
            MapCounterBase(entity, true);
        });
        modelBuilder.Entity<_Counter>(entity =>
        {
            entity.ToTable($"{_prefix}Counter");
            MapCounterBase(entity, false);
        });
        modelBuilder.Entity<_DistributedLock>(entity =>
        {
            entity.ToTable($"{_prefix}DistributedLock");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Resource).HasMaxLength(100).IsRequired();
            entity.HasIndex(i => i.Resource).IsUnique();
            entity.Property(i => i.ExpireAt).IsRequired();
            entity.Property(i => i.CreatedAt).IsRequired();
        });
        modelBuilder.Entity<_Dual>(entity =>
        {
            entity.ToTable($"{_prefix}Dual");
            entity.HasKey(i => i.Id);
        });
        modelBuilder.Entity<_Hash>(entity =>
        {
            entity.ToTable($"{_prefix}Hash");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Key).HasMaxLength(100);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(40);
            entity.Property(i => i.Value).IsRequired(false).HasMaxLength(Constants.VarcharMaxLength);
            MapExpireAt(entity);
            entity.HasIndex(i => new { i.Name, i.Key }).IsUnique();
        });
        modelBuilder.Entity<_Job>(entity =>
        {
            entity.ToTable($"{_prefix}Job");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.LastStateChangedAt).IsRequired(false);
            entity.Property(i => i.StateData).HasMaxLength(Constants.StateDataLength).IsRequired(false);
            entity.Property(i => i.InvocationData).HasMaxLength(Constants.VarcharMaxLength)
                .IsRequired();
            entity.Property(i => i.Arguments).HasMaxLength(Constants.VarcharMaxLength).IsRequired();
            entity.Property(i => i.StateName).HasMaxLength(Constants.StateNameLength).IsRequired(false);
            entity.Property(i => i.StateReason).HasMaxLength(Constants.StateReasonLength).IsRequired(false);
            entity.HasMany(i => i.Parameters);
            entity.HasMany(i => i.History);
            MapExpireAt(entity);
            MapCreatedAt(entity);
        });

        modelBuilder.Entity<_JobParameter>(entity =>
        {
            entity.ToTable($"{_prefix}JobParameter");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).HasMaxLength(40).IsRequired();
            entity.Property(i => i.Value).HasMaxLength(Constants.VarcharMaxLength).IsRequired(false);
            entity.HasOne(i => i.Job);
            //TODO this needs an index on name and job id somehow
        });
        modelBuilder.Entity<_JobQueue>(entity =>
        {
            entity.ToTable($"{_prefix}JobQueue");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.FetchedAt).IsRequired(false);
            entity.Property(i => i.Queue).HasMaxLength(50).IsRequired();
            entity.Property(i => i.FetchToken).HasMaxLength(36).IsRequired(false);
            entity.HasOne(i => i.Job);
        });

        modelBuilder.Entity<_JobState>(entity =>
        {
            entity.ToTable($"{_prefix}JobState");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).HasMaxLength(Constants.StateNameLength).IsRequired();
            entity.Property(i => i.Reason).HasMaxLength(Constants.StateReasonLength).IsRequired(false);
            entity.Property(i => i.Data).HasMaxLength(Constants.StateDataLength).IsRequired(false);
            entity.HasOne(i => i.Job);
            MapCreatedAt(entity);
        });
        modelBuilder.Entity<_List>(entity =>
        {
            entity.ToTable($"{_prefix}List");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasMaxLength(100);

            entity.Property(i => i.Value).IsRequired(false).HasMaxLength(Constants.VarcharMaxLength);
            MapExpireAt(entity);
        });
        modelBuilder.Entity<_Server>(entity =>
        {
            entity.ToTable($"{_prefix}Server");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasMaxLength(200);
            entity.Property(i => i.Data).HasMaxLength(Constants.VarcharMaxLength).IsRequired();
            entity.Property(i => i.LastHeartbeat).IsRequired(false);
        });


        modelBuilder.Entity<_Set>(entity =>
        {
            entity.ToTable($"{_prefix}Set");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Key).HasMaxLength(100);
            entity.Property(i => i.Score).IsRequired();
            entity.Property(i => i.Value).IsRequired(false).HasMaxLength(Constants.VarcharMaxLength);
            MapExpireAt(entity);
            entity.HasIndex(i => new { i.Value, i.Key }).IsUnique();
        });


        base.OnModelCreating(modelBuilder);
    }

    private void MapCounterBase<T>(EntityTypeBuilder<T> entity, bool keyIsUnique) where T : CounterBase
    {
        entity.HasKey(i => i.Id);
        entity.HasIndex(i => i.Key).IsUnique(keyIsUnique);
        entity.Property(i => i.Key).HasMaxLength(100);
        MapExpireAt(entity);
        entity.Property(i => i.Value).IsRequired();
    }
}