using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VenueGo.Models.Entities;

namespace VenueGo.Data;

public partial class dbVenueContext : DbContext
{
    public dbVenueContext(DbContextOptions<dbVenueContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CheckInLog> CheckInLogs { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EntryTicket> EntryTickets { get; set; }

    public virtual DbSet<LoginLog> LoginLogs { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrdersDetail> OrdersDetails { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<ReservationSlot> ReservationSlots { get; set; }

    public virtual DbSet<ReviewMain> ReviewMains { get; set; }

    public virtual DbSet<ReviewPerBooking> ReviewPerBookings { get; set; }

    public virtual DbSet<ReviewPerVisit> ReviewPerVisits { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<SportType> SportTypes { get; set; }

    public virtual DbSet<SportTypePriceRule> SportTypePriceRules { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<Venue> Venues { get; set; }

    public virtual DbSet<VenueUnavailableSlot> VenueUnavailableSlots { get; set; }

    public virtual DbSet<WeekBusinessHour> WeekBusinessHours { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_AuditLogs_CreatedAt");
            entity.Property(e => e.EntityId)
                .HasMaxLength(50)
                .HasColumnName("EntityID");
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.Property(e => e.OldValue).HasMaxLength(500);
        });

        modelBuilder.Entity<CheckInLog>(entity =>
        {
            entity.HasKey(e => e.LogId);

            entity.ToTable("CheckInLog");

            entity.Property(e => e.ActionTime)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_CheckInLog_ActionTime");
            entity.Property(e => e.IsValid).HasDefaultValue(true, "DF_CheckInLog_IsValid");
            entity.Property(e => e.OperatorId).HasColumnName("OperatorID");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.EmployeeNo, "UQ_Employees_EmployeeNo").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ_Employees_UserId").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Employees_CreatedAt");
            entity.Property(e => e.EmployeeNo)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.JobTitle).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active", "DF_Employees_Status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Employees_UpdatedAt");
        });

        modelBuilder.Entity<EntryTicket>(entity =>
        {
            entity.HasKey(e => e.TicketId);

            entity.ToTable("EntryTicket");

            entity.HasIndex(e => e.Qrtoken, "UQ_EntryTicket_QRToken").IsUnique();

            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.Qrtoken)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("QRToken");
        });

        modelBuilder.Entity<LoginLog>(entity =>
        {
            entity.Property(e => e.FailureReason).HasMaxLength(100);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.LoginAccount)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LoginTime)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_LoginLogs_LoginTime");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Orders_UserId");

            entity.HasIndex(e => e.OrderNo, "UQ_Orders_OrderNo").IsUnique();

            entity.HasIndex(e => e.ReservationId, "UQ_Orders_ReservationId")
                .IsUnique()
                .HasFilter("([ReservationId] IS NOT NULL)");

            entity.Property(e => e.CarrierNo).HasMaxLength(20);
            entity.Property(e => e.OrderCreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Orders_OrderCreatedAt");
            entity.Property(e => e.OrderNo).HasMaxLength(30);
        });

        modelBuilder.Entity<OrdersDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId);

            entity.HasIndex(e => e.OrderId, "IX_OrdersDetails_OrderId");

            entity.HasIndex(e => e.ReservationId, "UQ_OrdersDetails_ReservationId")
                .IsUnique()
                .HasFilter("([ReservationId] IS NOT NULL)");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.TokenId);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_PasswordResetTokens_CreatedAt");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.TokenHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UsedAt).HasPrecision(0);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.OrderId, "IX_Payments_OrderId");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Payments_CreatedAt");
            entity.Property(e => e.PaidAt).HasPrecision(0);
            entity.Property(e => e.PaymentDueAt).HasPrecision(0);
            entity.Property(e => e.TransactionNo).HasMaxLength(100);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.PermissionCode, "UQ_Permissions_PermissionCode").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Permissions_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.PermissionCode).HasMaxLength(50);
            entity.Property(e => e.PermissionName).HasMaxLength(50);
            entity.Property(e => e.Status).HasDefaultValue(true, "DF_Permissions_Status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Permissions_UpdatedAt");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.Property(e => e.CancelReason).HasMaxLength(200);
            entity.Property(e => e.RefundedAt).HasPrecision(0);
            entity.Property(e => e.RequestedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Refunds_RequestedAt");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasIndex(e => new { e.VenueId, e.BookingDate }, "IX_Reservations_Venue_Date");

            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.PaymentDueAt).HasPrecision(0);
            entity.Property(e => e.ReservedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Reservations_ReservedAt");
            entity.Property(e => e.StartTime).HasPrecision(0);
            entity.Property(e => e.TermsAcceptedAt).HasPrecision(0);
            entity.Property(e => e.TermsVersion).HasMaxLength(20);
        });

        modelBuilder.Entity<ReservationSlot>(entity =>
        {
            entity.HasIndex(e => e.ReservationId, "IX_ReservationSlots_ReservationId");

            entity.HasIndex(e => new { e.VenueId, e.BookingDate, e.SlotStartTime }, "UQ_ReservationSlots_Occupancy").IsUnique();

            entity.Property(e => e.SlotStartTime).HasPrecision(0);
        });

        modelBuilder.Entity<ReviewMain>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.ToTable("ReviewMain");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_ReviewMain_CreatedAt");
            entity.Property(e => e.IsAnonymous).HasDefaultValue(true, "DF_ReviewMain_IsAnonymous");
            entity.Property(e => e.ReadAt).HasPrecision(0);
            entity.Property(e => e.RepliedAt).HasPrecision(0);
            entity.Property(e => e.ReplyContent).HasMaxLength(1000);
            entity.Property(e => e.ReviewContent).HasMaxLength(1000);
        });

        modelBuilder.Entity<ReviewPerBooking>(entity =>
        {
            entity.ToTable("ReviewPerBooking");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_ReviewPerBooking_CreatedAt");
            entity.Property(e => e.ExpiredAt).HasPrecision(0);
        });

        modelBuilder.Entity<ReviewPerVisit>(entity =>
        {
            entity.ToTable("ReviewPerVisit");

            entity.HasIndex(e => e.Qrtoken, "UQ_ReviewPerVisit_QRToken").IsUnique();

            entity.Property(e => e.ActualEndTime).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_ReviewPerVisit_CreatedAt");
            entity.Property(e => e.ExpiredAt).HasPrecision(0);
            entity.Property(e => e.Qrtoken)
                .HasMaxLength(64)
                .HasColumnName("QRToken");
            entity.Property(e => e.RentEndTime).HasPrecision(0);
            entity.Property(e => e.RentStartTime).HasPrecision(0);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Roles_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.RoleName).HasMaxLength(50);
            entity.Property(e => e.Status).HasDefaultValue(true, "DF_Roles_Status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Roles_UpdatedAt");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId });

            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_RolePermissions_AssignedAt");
            entity.Property(e => e.Status).HasDefaultValue(true, "DF_RolePermissions_Status");
        });

        modelBuilder.Entity<SportType>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_SportTypes_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_SportTypes_IsActive");
            entity.Property(e => e.SportName).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<SportTypePriceRule>(entity =>
        {
            entity.Property(e => e.PeakStartTime).HasPrecision(0);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.Property(e => e.CarrierNo).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Users_CreatedAt");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LastLoginAt).HasPrecision(0);
            entity.Property(e => e.LockedUntil).HasPrecision(0);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active", "DF_Users_Status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Users_UpdatedAt");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_UserRoles_AssignedAt");
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_Venues_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Venues_IsActive");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.VenueName).HasMaxLength(40);
        });

        modelBuilder.Entity<VenueUnavailableSlot>(entity =>
        {
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_VenueUnavailableSlots_CreatedAt");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.UnavailableStartTime).HasPrecision(0);
        });

        modelBuilder.Entity<WeekBusinessHour>(entity =>
        {
            entity.HasKey(e => e.BusinessHoursId);

            entity.Property(e => e.BusinessHoursId).ValueGeneratedNever();
            entity.Property(e => e.CloseTime).HasPrecision(0);
            entity.Property(e => e.OpenTime).HasPrecision(0);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
