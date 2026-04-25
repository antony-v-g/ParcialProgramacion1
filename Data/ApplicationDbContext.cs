using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ParcialProgramacion1.Models;

namespace ParcialProgramacion1.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<SolicitudCredito> SolicitudesCredito { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>()
            .Property(c => c.IngresosMensuales)
            .HasPrecision(18, 2);

        builder.Entity<SolicitudCredito>()
            .Property(s => s.MontoSolicitado)
            .HasPrecision(18, 2);

        builder.Entity<Cliente>()
            .HasMany(c => c.SolicitudesCredito)
            .WithOne(s => s.Cliente)
            .HasForeignKey(s => s.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Cliente>()
            .ToTable(t => t.HasCheckConstraint("CK_Cliente_IngresosMensuales", "IngresosMensuales > 0"));

        builder.Entity<SolicitudCredito>()
            .ToTable(t => t.HasCheckConstraint("CK_SolicitudCredito_MontoSolicitado", "MontoSolicitado > 0"));

        builder.Entity<SolicitudCredito>()
            .HasIndex(s => new { s.ClienteId, s.Estado })
            .IsUnique()
            .HasFilter("Estado = 0");
    }
}