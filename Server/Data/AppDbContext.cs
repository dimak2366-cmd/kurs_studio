using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Smeta> Smetas => Set<Smeta>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<ActualExpense> ActualExpenses => Set<ActualExpense>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<ExpenseRequest> ExpenseRequests => Set<ExpenseRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<AnalyticReport> AnalyticReports => Set<AnalyticReport>();
    public DbSet<ManagementDecision> ManagementDecisions => Set<ManagementDecision>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Login).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.PasswordSalt).IsRequired().HasMaxLength(64);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany(d => d.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RoleId);
            e.HasIndex(x => x.DepartmentId);
        });

        mb.Entity<Department>(e =>
        {
            e.ToTable("departments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.Name).IsUnique();
        });

        mb.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.HasOne(x => x.Department).WithMany(d => d.Projects).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.DepartmentId);
        });

        mb.Entity<Smeta>(e =>
        {
            e.ToTable("smetas");
            e.HasKey(x => x.Id);
            e.Property(x => x.FormCode).IsRequired().HasMaxLength(32);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.RejectReason).HasMaxLength(512);

            e.HasOne(x => x.Project).WithMany(p => p.Smetas)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ParentSmeta).WithMany(s => s.Changes)
                .HasForeignKey(x => x.ParentSmetaId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ParentSmetaId);
        });

        mb.Entity<ExpenseItem>(e =>
        {
            e.ToTable("expense_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.Property(x => x.KosguCode).IsRequired().HasMaxLength(16);
            e.Property(x => x.PlanAmount).HasPrecision(18, 2);
            e.Property(x => x.LimitAmount).HasPrecision(18, 2);
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Smeta).WithMany(s => s.ExpenseItems).HasForeignKey(x => x.SmetaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SmetaId);
        });

        mb.Entity<ActualExpense>(e =>
        {
            e.ToTable("actual_expenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.DocumentType).IsRequired().HasMaxLength(32);
            e.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(64);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.HasOne(x => x.ExpenseItem).WithMany(i => i.ActualExpenses).HasForeignKey(x => x.ExpenseItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Period).WithMany(p => p.ActualExpenses).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ExpenseItemId);
            e.HasIndex(x => x.PeriodId);
        });

        mb.Entity<Period>(e =>
        {
            e.ToTable("periods");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Year, x.Month }).IsUnique();
        });

        mb.Entity<ExpenseRequest>(e =>
        {
            e.ToTable("expense_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.Comment).HasMaxLength(512);
            e.HasOne(x => x.Project).WithMany(p => p.ExpenseRequests).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ExpenseItem).WithMany(i => i.ExpenseRequests).HasForeignKey(x => x.ExpenseItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.ExpenseRequests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ExpenseItemId);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(64);
            e.Property(x => x.Entity).IsRequired().HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.Details).HasMaxLength(2048);
            e.HasOne(x => x.User).WithMany(u => u.AuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Timestamp);
        });

        mb.Entity<ErrorLog>(e =>
        {
            e.ToTable("error_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Level).IsRequired().HasMaxLength(16);
            e.Property(x => x.Message).IsRequired().HasMaxLength(2048);
            e.Property(x => x.StackTrace).HasMaxLength(8192);
            e.HasIndex(x => x.Timestamp);
        });

        mb.Entity<AnalyticReport>(e =>
        {
            e.ToTable("analytic_reports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.Content).IsRequired();
            e.HasOne(x => x.Smeta).WithMany(s => s.AnalyticReports).HasForeignKey(x => x.SmetaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany(u => u.CreatedReports).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SmetaId);
            e.HasIndex(x => x.CreatedBy);
        });

        mb.Entity<ManagementDecision>(e =>
        {
            e.ToTable("management_decisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Decision).IsRequired().HasMaxLength(2048);
            e.HasOne(x => x.ExpenseItem).WithMany(i => i.ManagementDecisions).HasForeignKey(x => x.ExpenseItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Period).WithMany(p => p.ManagementDecisions).HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ExpenseItemId);
            e.HasIndex(x => x.PeriodId);
        });
    }
}
