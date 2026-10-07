using Acxiom.Domain.Activities;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.Common;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Domain.Teams;
using Acxiom.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acxiom.Infrastructure.Persistence;

// Column names follow the table design in the assignment (CustomerCode, AssignedTo, CreatedDate ...)
// while the C# properties use shorter names.

internal static class OwnedColumns
{
    public static void Map<T>(EntityTypeBuilder<T> b, string idColumn) where T : OwnedEntity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName(idColumn);
        b.Property(e => e.OwnerId).HasColumnName("AssignedTo").IsRequired();
        b.Property(e => e.CreatedById).HasColumnName("CreatedBy").HasMaxLength(450);
        b.Property(e => e.CreatedAt).HasColumnName("CreatedDate");
        b.Property(e => e.UpdatedAt).HasColumnName("ModifiedDate");
        b.HasIndex(e => e.OwnerId);
        b.HasOne<AppUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customer");
        OwnedColumns.Map(b, "CustomerId");
        b.Property(c => c.Code).HasColumnName("CustomerCode").HasMaxLength(20);
        b.Property(c => c.Name).HasColumnName("CustomerName").HasMaxLength(Rules.NameMax);
        b.Property(c => c.Email).HasMaxLength(Rules.EmailMax);
        b.Property(c => c.Phone).HasMaxLength(10);
        b.Property(c => c.Company).HasColumnName("CompanyName").HasMaxLength(Rules.CompanyMax);
        b.Property(c => c.Address).HasMaxLength(250);
        b.Property(c => c.City).HasMaxLength(60);
        b.Property(c => c.State).HasMaxLength(60);
        b.Property(c => c.Notes).HasMaxLength(Rules.NotesMax);
        b.HasIndex(c => c.Code).IsUnique();
        b.HasIndex(c => c.Email).IsUnique().HasFilter("IsDeleted = 0");
        b.HasIndex(c => c.Phone).IsUnique().HasFilter("IsDeleted = 0");
    }
}

internal class LeadConfig : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("Lead");
        OwnedColumns.Map(b, "LeadId");
        b.Property(l => l.Code).HasColumnName("LeadCode").HasMaxLength(20);
        b.Property(l => l.Name).HasColumnName("LeadName").HasMaxLength(Rules.NameMax);
        b.Property(l => l.Email).HasMaxLength(Rules.EmailMax);
        b.Property(l => l.Phone).HasMaxLength(10);
        b.Property(l => l.Company).HasColumnName("CompanyName").HasMaxLength(Rules.CompanyMax);
        b.Property(l => l.Notes).HasMaxLength(Rules.NotesMax);
        b.HasIndex(l => l.Code).IsUnique();
        b.HasIndex(l => l.Status);
        b.HasOne<Customer>().WithMany().HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class OpportunityConfig : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> b)
    {
        b.ToTable("Opportunity");
        OwnedColumns.Map(b, "OpportunityId");
        b.Property(o => o.Name).HasColumnName("OpportunityName").HasMaxLength(150);
        b.Property(o => o.Notes).HasMaxLength(Rules.NotesMax);
        b.Property(o => o.ClosedAt).HasColumnName("ClosedDate");
        b.Ignore(o => o.Weighted);
        b.Ignore(o => o.IsOpen);
        b.HasIndex(o => o.Stage);
        b.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lead>().WithMany().HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class FollowUpConfig : IEntityTypeConfiguration<FollowUp>
{
    public void Configure(EntityTypeBuilder<FollowUp> b)
    {
        b.ToTable("FollowUp");
        OwnedColumns.Map(b, "FollowUpId");
        b.Property(f => f.DueAt).HasColumnName("FollowUpDate");
        b.Property(f => f.Type).HasColumnName("FollowUpType");
        b.Property(f => f.Subject).HasMaxLength(150);
        b.Property(f => f.Remarks).HasMaxLength(Rules.NotesMax);
        b.Property(f => f.CompletedAt).HasColumnName("CompletedDate");
        b.Ignore(f => f.Target);
        b.Ignore(f => f.TargetId);
        b.HasIndex(f => f.DueAt);
        b.HasOne<Customer>().WithMany().HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lead>().WithMany().HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Opportunity>().WithMany().HasForeignKey(f => f.OpportunityId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ActivityConfig : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("Activity");
        OwnedColumns.Map(b, "ActivityId");
        b.Property(a => a.Type).HasColumnName("ActivityType");
        b.Property(a => a.When).HasColumnName("ActivityDate");
        b.Property(a => a.Subject).HasMaxLength(150);
        b.Property(a => a.Description).HasMaxLength(Rules.NotesMax);
        b.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lead>().WithMany().HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class TeamConfig : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("Team");
        b.Property(t => t.Name).HasMaxLength(80);
        b.HasIndex(t => t.Name).IsUnique();
        b.HasOne<AppUser>().WithMany().HasForeignKey(t => t.ManagerId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal class AppUserConfig : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(100);
        b.HasOne<Team>().WithMany().HasForeignKey(u => u.TeamId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal class AuditEntryConfig : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditLog");
        b.Property(a => a.Id).HasColumnName("AuditLogId");
        b.Property(a => a.Action).HasMaxLength(30);
        b.Property(a => a.EntityName).HasMaxLength(30);
        b.Property(a => a.RecordId).HasMaxLength(64);
        b.Property(a => a.UserName).HasMaxLength(256);
        b.Property(a => a.IpAddress).HasMaxLength(45);
        b.Property(a => a.Note).HasMaxLength(500);
        b.HasIndex(a => a.CreatedDate);
        b.HasIndex(a => new { a.EntityName, a.RecordId });
    }
}
