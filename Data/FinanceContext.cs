using Microsoft.EntityFrameworkCore;
using FinanceManager.Models;
using System.IO;
using System;

namespace FinanceManager.Data
{
    public class FinanceContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<SavingsTarget> SavingsTargets { get; set; }
        public DbSet<SavingsUpdateHistory> SavingsUpdateHistories { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Investment> Investments { get; set; }
        public DbSet<RecurringExpense> RecurringExpenses { get; set; }  // TABELA CORRIGIDA

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var financeManagerPath = Path.Combine(appDataPath, "FinanceManager");

            // Criar diretório se não existir
            Directory.CreateDirectory(financeManagerPath);

            var dbPath = Path.Combine(financeManagerPath, "finance.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            // Configurações adicionais para melhor performance
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableDetailedErrors(true);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurações para User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(100).IsRequired();
                entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            });

            // Configurações para Expense
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Description).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => e.Category);
            });

            // Configurações para Income
            modelBuilder.Entity<Income>(entity =>
            {
                entity.Property(i => i.Amount).HasPrecision(18, 2);
                entity.Property(i => i.Description).HasMaxLength(200).IsRequired();
                entity.Property(i => i.Category).HasMaxLength(100).IsRequired();
                entity.Property(i => i.Notes).HasMaxLength(500);

                entity.HasOne(i => i.User)
                      .WithMany()
                      .HasForeignKey(i => i.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => i.UserId);
                entity.HasIndex(i => i.Date);
                entity.HasIndex(i => i.Category);
            });

            // Configurações para SavingsTarget
            modelBuilder.Entity<SavingsTarget>(entity =>
            {
                entity.Property(s => s.TargetAmount).HasPrecision(18, 2);
                entity.Property(s => s.CurrentAmount).HasPrecision(18, 2);
                entity.Property(s => s.Name).HasMaxLength(200).IsRequired();
                entity.Property(s => s.Description).HasMaxLength(500);

                entity.HasOne(s => s.User)
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.UserId);
            });

            // Configurações para SavingsUpdateHistory
            modelBuilder.Entity<SavingsUpdateHistory>(entity =>
            {
                entity.Property(h => h.AmountAdded).HasPrecision(18, 2);
                entity.Property(h => h.PreviousAmount).HasPrecision(18, 2);
                entity.Property(h => h.NewAmount).HasPrecision(18, 2);
                entity.Property(h => h.Notes).HasMaxLength(500);

                entity.HasOne(h => h.SavingsTarget)
                      .WithMany(s => s.UpdateHistory)
                      .HasForeignKey(h => h.SavingsTargetId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(h => h.SavingsTargetId);
                entity.HasIndex(h => h.UpdateDate);
            });

            // Configurações para Note
            modelBuilder.Entity<Note>(entity =>
            {
                entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
                entity.Property(n => n.Content).HasMaxLength(2000);

                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.CreatedDate);
            });

            // Configurações para Investment
            modelBuilder.Entity<Investment>(entity =>
            {
                entity.Property(i => i.Quantity).HasPrecision(18, 8);
                entity.Property(i => i.PurchasePrice).HasPrecision(18, 2);
                entity.Property(i => i.CurrentPrice).HasPrecision(18, 2);
                entity.Property(i => i.Amount).HasPrecision(18, 2);
                entity.Property(i => i.Name).HasMaxLength(200).IsRequired();
                entity.Property(i => i.Type).HasMaxLength(100).IsRequired();
                entity.Property(i => i.Broker).HasMaxLength(100);
                entity.Property(i => i.Description).HasMaxLength(500);
                entity.Property(i => i.Currency).HasMaxLength(10).HasDefaultValue("EUR");

                entity.HasOne(i => i.User)
                      .WithMany()
                      .HasForeignKey(i => i.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => i.UserId);
                entity.HasIndex(i => i.Type);
                entity.HasIndex(i => i.IsActive);
                entity.HasIndex(i => i.PurchaseDate);
            });

            // ===== CONFIGURAÇÃO COMPLETA PARA RecurringExpense =====
            modelBuilder.Entity<RecurringExpense>(entity =>
            {
                // Propriedades básicas
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();

                entity.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
                entity.Property(r => r.Description).HasMaxLength(200).IsRequired();
                entity.Property(r => r.Category).HasMaxLength(100).IsRequired();
                entity.Property(r => r.Frequency).HasMaxLength(50).IsRequired().HasDefaultValue("Mensal");
                entity.Property(r => r.Notes).HasMaxLength(500);

                // Datas
                entity.Property(r => r.StartDate).IsRequired();
                entity.Property(r => r.EndDate);
                entity.Property(r => r.LastProcessedDate);
                entity.Property(r => r.CreatedDate).IsRequired().HasDefaultValueSql("datetime('now')");
                entity.Property(r => r.UpdatedDate);

                // Flags
                entity.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(r => r.UserId).IsRequired();

                // Relacionamento com User
                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índices para performance
                entity.HasIndex(r => r.UserId).HasDatabaseName("IX_RecurringExpenses_UserId");
                entity.HasIndex(r => r.IsActive).HasDatabaseName("IX_RecurringExpenses_IsActive");
                entity.HasIndex(r => r.Frequency).HasDatabaseName("IX_RecurringExpenses_Frequency");
                entity.HasIndex(r => r.StartDate).HasDatabaseName("IX_RecurringExpenses_StartDate");
                entity.HasIndex(r => r.EndDate).HasDatabaseName("IX_RecurringExpenses_EndDate");
                entity.HasIndex(r => r.LastProcessedDate).HasDatabaseName("IX_RecurringExpenses_LastProcessedDate");
                entity.HasIndex(r => r.Category).HasDatabaseName("IX_RecurringExpenses_Category");
                entity.HasIndex(r => r.CreatedDate).HasDatabaseName("IX_RecurringExpenses_CreatedDate");

                // Índices compostos para queries complexas
                entity.HasIndex(r => new { r.UserId, r.IsActive }).HasDatabaseName("IX_RecurringExpenses_UserId_IsActive");
                entity.HasIndex(r => new { r.IsActive, r.StartDate }).HasDatabaseName("IX_RecurringExpenses_IsActive_StartDate");
            });
        }
    }
}