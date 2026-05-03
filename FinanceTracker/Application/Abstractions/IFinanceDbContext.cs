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

namespace Application.Abstractions;

public interface IFinanceDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Subscription> Subscriptions { get; }
    IQueryable<Account> Accounts { get; }
    IQueryable<Transaction> Transactions { get; }
    IQueryable<Transfer> Transfers { get; }
    IQueryable<Category> Categories { get; }
    IQueryable<Budget> Budgets { get; }
    IQueryable<Notification> Notifications { get; }
    IQueryable<Receipt> Receipts { get; }
    IQueryable<ReceiptItem> ReceiptItems { get; }
    IQueryable<RecurringPayment> RecurringPayments { get; }
    IQueryable<CreditObligation> CreditObligations { get; }

    Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
