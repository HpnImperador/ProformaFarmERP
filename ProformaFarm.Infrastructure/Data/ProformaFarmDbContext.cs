using Microsoft.EntityFrameworkCore;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Infrastructure.Data;

public class ProformaFarmDbContext : DbContext
{
    public ProformaFarmDbContext(DbContextOptions<ProformaFarmDbContext> options) : base(options) { }

    // Por enquanto só Auth. Vamos adicionando módulos depois:
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<UsuarioPerfil> UsuarioPerfis => Set<UsuarioPerfil>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tabelas já existem no banco: Usuario, Perfil, UsuarioPerfil
        modelBuilder.Entity<Usuario>().ToTable("Usuario").HasKey(x => x.IdUsuario);
        modelBuilder.Entity<Perfil>().ToTable("Perfil").HasKey(x => x.IdPerfil);

        modelBuilder.Entity<UsuarioPerfil>()
            .ToTable("UsuarioPerfil")
            .HasKey(x => new { x.IdUsuario, x.IdPerfil });
    }
}
