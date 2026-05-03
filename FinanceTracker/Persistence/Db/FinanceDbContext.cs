using Application.Abstractions;
using Domain.Accounts;
using Domain.Budgets;
using Domain.Categories;
using Domain.CreditObligations;
using Domain.Notifications;
using Domain.Receipts;
using Domain.RecurringPayments;
using Domain.Subscriptions;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Db;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options), IFinanceDbContext
{
    public DbSet<User> UsersDb => Set<User>();
    public DbSet<Subscription> SubscriptionsDb => Set<Subscription>();
    public DbSet<Account> AccountsDb => Set<Account>();
    public DbSet<Transaction> TransactionsDb => Set<Transaction>();
    public DbSet<Transfer> TransfersDb => Set<Transfer>();
    public DbSet<Category> CategoriesDb => Set<Category>();
    public DbSet<Budget> BudgetsDb => Set<Budget>();
    public DbSet<Notification> NotificationsDb => Set<Notification>();
    public DbSet<Receipt> ReceiptsDb => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItemsDb => Set<ReceiptItem>();
    public DbSet<RecurringPayment> RecurringPaymentsDb => Set<RecurringPayment>();
    public DbSet<CreditObligation> CreditObligationsDb => Set<CreditObligation>();

    public IQueryable<User> Users => UsersDb.AsQueryable();
    public IQueryable<Subscription> Subscriptions => SubscriptionsDb.AsQueryable();
    public IQueryable<Account> Accounts => AccountsDb.AsQueryable();
    public IQueryable<Transaction> Transactions => TransactionsDb.AsQueryable();
    public IQueryable<Transfer> Transfers => TransfersDb.AsQueryable();
    public IQueryable<Category> Categories => CategoriesDb.AsQueryable();
    public IQueryable<Budget> Budgets => BudgetsDb.AsQueryable();
    public IQueryable<Notification> Notifications => NotificationsDb.AsQueryable();
    public IQueryable<Receipt> Receipts => ReceiptsDb.AsQueryable();
    public IQueryable<ReceiptItem> ReceiptItems => ReceiptItemsDb.AsQueryable();
    public IQueryable<RecurringPayment> RecurringPayments => RecurringPaymentsDb.AsQueryable();
    public IQueryable<CreditObligation> CreditObligations => CreditObligationsDb.AsQueryable();

    public async Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await Set<T>().AddAsync(entity, ct);
    }

    public void Remove<T>(T entity) where T : class
    {
        Set<T>().Remove(entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.KeycloakUserId).IsUnique();
            entity.HasIndex(x => x.HasActivePremium);
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.KeycloakUserId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CurrentSubscriptionType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.UserId, x.EndDate });
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.IsArchived });
            entity.Property(x => x.CurrentBalance).HasPrecision(18, 2);
            entity.Property(x => x.FinancialGoalAmount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.CategoryId);
            entity.HasIndex(x => x.TransactionDate);
            entity.HasIndex(x => new { x.UserId, x.TransactionDate });
            entity.HasIndex(x => new { x.UserId, x.AccountId, x.TransactionDate });
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Source).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.ToTable("transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Name, x.Type }).IsUnique();
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.ToTable("budgets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CategoryId);
            entity.Property(x => x.LimitAmount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.PeriodType).HasConversion<int>();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.RelatedEntityType).HasMaxLength(100);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OcrStatus).HasConversion<int>();
            entity.Property(x => x.RecognizedTotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.RawOcrData).HasColumnType("jsonb");
            entity.Property(x => x.StorageContainer).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.RecognizedMerchant).HasMaxLength(255);
            entity.Property(x => x.ProcessingError).HasMaxLength(2000);
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.ToTable("receipt_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ReceiptId);
            entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.CategoryName).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<RecurringPayment>(entity =>
        {
            entity.ToTable("recurring_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EstimatedAmount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<CreditObligation>(entity =>
        {
            entity.ToTable("credit_obligations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RemainingAmount).HasPrecision(18, 2);
            entity.Property(x => x.MonthlyPayment).HasPrecision(18, 2);
            entity.Property(x => x.InterestRate).HasPrecision(5, 2);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
