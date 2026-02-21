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
    public DbSet<Organizacao> Organizacoes => Set<Organizacao>();
    public DbSet<UnidadeOrganizacional> UnidadesOrganizacionais => Set<UnidadeOrganizacional>();
    public DbSet<CentroCusto> CentrosCusto => Set<CentroCusto>();
    public DbSet<UnidadeCentroCusto> UnidadeCentroCustos => Set<UnidadeCentroCusto>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<LotacaoUsuario> LotacoesUsuario => Set<LotacaoUsuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tabelas já existem no banco: Usuario, Perfil, UsuarioPerfil
        modelBuilder.Entity<Usuario>().ToTable("Usuario").HasKey(x => x.IdUsuario);
        modelBuilder.Entity<Perfil>().ToTable("Perfil").HasKey(x => x.IdPerfil);

        modelBuilder.Entity<UsuarioPerfil>()
            .ToTable("UsuarioPerfil")
            .HasKey(x => new { x.IdUsuario, x.IdPerfil });

        modelBuilder.Entity<Organizacao>(entity =>
        {
            entity.ToTable("Organizacao").HasKey(x => x.IdOrganizacao);
            entity.Property(x => x.RazaoSocial).HasMaxLength(200).IsRequired();
            entity.Property(x => x.NomeFantasia).HasMaxLength(200);
            entity.Property(x => x.Cnpj).HasMaxLength(14).IsRequired();
            entity.HasIndex(x => x.Cnpj).IsUnique();
        });

        modelBuilder.Entity<UnidadeOrganizacional>(entity =>
        {
            entity.ToTable("UnidadeOrganizacional").HasKey(x => x.IdUnidadeOrganizacional);
            entity.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Nome).HasMaxLength(200).IsRequired();

            entity.HasIndex(x => new { x.IdOrganizacao, x.Codigo }).IsUnique();

            entity
                .HasOne<Organizacao>()
                .WithMany()
                .HasForeignKey(x => x.IdOrganizacao)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<UnidadeOrganizacional>()
                .WithMany()
                .HasForeignKey(x => x.IdUnidadePai)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CentroCusto>(entity =>
        {
            entity.ToTable("CentroCusto").HasKey(x => x.IdCentroCusto);
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.IdOrganizacao, x.Codigo }).IsUnique();

            entity
                .HasOne<Organizacao>()
                .WithMany()
                .HasForeignKey(x => x.IdOrganizacao)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnidadeCentroCusto>(entity =>
        {
            entity.ToTable("UnidadeCentroCusto").HasKey(x => new { x.IdUnidadeOrganizacional, x.IdCentroCusto });

            entity
                .HasOne<UnidadeOrganizacional>()
                .WithMany()
                .HasForeignKey(x => x.IdUnidadeOrganizacional)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<CentroCusto>()
                .WithMany()
                .HasForeignKey(x => x.IdCentroCusto)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cargo>(entity =>
        {
            entity.ToTable("Cargo").HasKey(x => x.IdCargo);
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.IdOrganizacao, x.Codigo }).IsUnique();

            entity
                .HasOne<Organizacao>()
                .WithMany()
                .HasForeignKey(x => x.IdOrganizacao)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LotacaoUsuario>(entity =>
        {
            entity.ToTable("LotacaoUsuario").HasKey(x => x.IdLotacaoUsuario);
            entity.HasIndex(x => new { x.IdUsuario, x.Principal, x.Ativa })
                .HasFilter("[Principal] = 1 AND [Ativa] = 1")
                .IsUnique();

            entity
                .HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<UnidadeOrganizacional>()
                .WithMany()
                .HasForeignKey(x => x.IdUnidadeOrganizacional)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<Cargo>()
                .WithMany()
                .HasForeignKey(x => x.IdCargo)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });
    }
}
