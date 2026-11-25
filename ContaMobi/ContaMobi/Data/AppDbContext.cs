using Microsoft.EntityFrameworkCore;
using ContaMobi.Models;
using System;
using System.Data.Common;
using System.IO;

namespace ContaMobi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Banco> Bancos { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<ContaCorrente> ContasCorrentes { get; set; }
        public DbSet<SaldoContaCorrente> SaldoContasCorrentes { get; set; }
        public DbSet<SaldoContaCorrente> SaldosContasCorrentes => Set<SaldoContaCorrente>();
        public DbSet<Orcamento> Orcamentos { get; set; }
        public DbSet<Entidade> Entidades { get; set; }
        public DbSet<Contato> Contatos { get; set; }
        public DbSet<LancamentoConta> LancamentosConta { get; set; }
        public DbSet<ContaPR> ContasPR { get; set; }
        public DbSet<ContaPRParcela> ContasPRParcelas { get; set; }

        // Módulo de Cartões de Crédito
        public DbSet<Bandeira> Bandeiras { get; set; }
        public DbSet<CartaoCredito> CartoesCredito { get; set; }
        public DbSet<FaturaCartao> FaturasCartao { get; set; }
        public DbSet<LancamentoCartao> LancamentosCartao { get; set; }

        public string DbPath { get; }

        private static bool _schemaVerificado = false;

        public AppDbContext()
        {
            string pastaLocal = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ContaMobi");

            if (!Directory.Exists(pastaLocal))
            {
                Directory.CreateDirectory(pastaLocal);
            }

            DbPath = Path.Combine(pastaLocal, "contamobi.db");

            if (!_schemaVerificado)
            {
                _schemaVerificado = true;
                GarantirColunasAtualizadas();
            }
        }

        private void GarantirColunasAtualizadas()
        {
            try
            {
                if (!File.Exists(DbPath))
                    return;

                using var conn = Database.GetDbConnection();
                conn.Open();

                GarantirColunaSqlite(conn, "lancamentos_contas", "numerodocumento", "TEXT NULL");
                GarantirColunaSqlite(conn, "contas_pr", "numerodocumento", "TEXT NULL");
                GarantirColunaSqlite(conn, "contas_pr_parcelas", "numerodocumento", "TEXT NULL");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MIGRACAO SQLITE]: {ex.Message}");
            }
        }

        private static void GarantirColunaSqlite(DbConnection conn, string tabela, string coluna, string definicao)
        {
            bool tabelaExiste = false;
            bool possuiColuna = false;

            using (var cmdCheck = conn.CreateCommand())
            {
                cmdCheck.CommandText = $"PRAGMA table_info('{tabela}');";
                using var reader = cmdCheck.ExecuteReader();
                while (reader.Read())
                {
                    tabelaExiste = true;
                    string nomeColuna = reader["name"]?.ToString() ?? string.Empty;
                    if (string.Equals(nomeColuna, coluna, StringComparison.OrdinalIgnoreCase))
                    {
                        possuiColuna = true;
                        break;
                    }
                }
            }

            if (tabelaExiste && !possuiColuna)
            {
                using var cmdAlter = conn.CreateCommand();
                cmdAlter.CommandText = $"ALTER TABLE {tabela} ADD COLUMN {coluna} {definicao};";
                cmdAlter.ExecuteNonQuery();
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={DbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Categorias (Auto-relacionamento Pai -> Filhos)
            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasKey(c => c.CategoriaUuid);

                entity.HasOne(c => c.CategoriaPai)
                      .WithMany(c => c.Subcategorias)
                      .HasForeignKey(c => c.PaiUuid)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Saldos de Contas Correntes
            modelBuilder.Entity<SaldoContaCorrente>(entity =>
            {
                entity.ToTable("saldo_contas_correntes");
                entity.HasKey(s => s.SaldoId);

                entity.HasIndex(s => new { s.ContaUuid, s.Data })
                      .IsUnique()
                      .HasDatabaseName("idx_saldo_conta_data");

                entity.HasOne(s => s.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(s => s.ContaUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Entidades
            modelBuilder.Entity<Entidade>(entity =>
            {
                entity.ToTable("entidades");
                entity.HasKey(e => e.EntidadeUuid);

                entity.HasMany(e => e.Contatos)
                      .WithOne(c => c.Entidade)
                      .HasForeignKey(c => c.EntidadeUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Contatos
            modelBuilder.Entity<Contato>(entity =>
            {
                entity.ToTable("contatos");
                entity.HasKey(c => c.ContatoUuid);
            });

            // Lançamentos de Contas Correntes
            modelBuilder.Entity<LancamentoConta>(entity =>
            {
                entity.ToTable("lancamentos_contas");
                entity.HasKey(l => l.LancamentoUuid);

                entity.HasIndex(l => new { l.ContaUuid, l.DataLancamento })
                      .HasDatabaseName("idx_lancamentos_conta_data");

                entity.HasOne(l => l.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(l => l.ContaUuid)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Categoria)
                      .WithMany()
                      .HasForeignKey(l => l.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Entidade)
                      .WithMany()
                      .HasForeignKey(l => l.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Contas a Pagar e a Receber (contas_pr)
            modelBuilder.Entity<ContaPR>(entity =>
            {
                entity.ToTable("contas_pr");
                entity.HasKey(c => c.ContasPRUuid);

                entity.HasIndex(c => new { c.Tipo, c.DataVencimento })
                      .HasDatabaseName("idx_contas_pr_tipo_vencimento");

                entity.HasIndex(c => c.LoteUuid)
                      .HasDatabaseName("idx_contas_pr_lote");

                entity.HasOne(c => c.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(c => c.ContaUuid)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.Categoria)
                      .WithMany()
                      .HasForeignKey(c => c.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Entidade)
                      .WithMany()
                      .HasForeignKey(c => c.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Parcelas / Grid salvo de Contas PR (contas_pr_parcelas)
            modelBuilder.Entity<ContaPRParcela>(entity =>
            {
                entity.ToTable("contas_pr_parcelas");
                entity.HasKey(p => p.ParcelaUuid);

                entity.HasIndex(p => new { p.LoteUuid, p.NumeroParcela })
                      .HasDatabaseName("idx_contas_pr_parcelas_lote");
            });

            // Orçamentos Mensais (orcamentos)
            modelBuilder.Entity<Orcamento>(entity =>
            {
                entity.ToTable("orcamentos");
                entity.HasKey(o => o.OrcamentoUuid);

                entity.HasIndex(o => new { o.Ano, o.Mes, o.CategoriaUuid })
                      .IsUnique()
                      .HasDatabaseName("ix_orcamentos_ano_mes_cat");

                entity.HasOne(o => o.Categoria)
                      .WithMany()
                      .HasForeignKey(o => o.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =================================================================
            // MÓDULO DE CARTÕES DE CRÉDITO
            // =================================================================

            // Bandeiras de Cartão (bandeiras)
            modelBuilder.Entity<Bandeira>(entity =>
            {
                entity.ToTable("bandeiras");
                entity.HasKey(b => b.BandeiraUuid);

                entity.HasIndex(b => b.UsuarioUuid)
                      .HasDatabaseName("idx_bandeiras_usuario");

                var dataSeed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                entity.HasData(
                    new Bandeira
                    {
                        BandeiraUuid = "11111111-1111-1111-1111-111111111101",
                        UsuarioUuid = null,
                        DescricaoBandeira = "Visa",
                        IconeBandeira = "ms-appx:///Assets/Bandeiras/visa.png",
                        Ativo = true,
                        CriadoEm = dataSeed,
                        UpdatedAt = dataSeed
                    },
                    new Bandeira
                    {
                        BandeiraUuid = "11111111-1111-1111-1111-111111111102",
                        UsuarioUuid = null,
                        DescricaoBandeira = "Mastercard",
                        IconeBandeira = "ms-appx:///Assets/Bandeiras/mastercard.png",
                        Ativo = true,
                        CriadoEm = dataSeed,
                        UpdatedAt = dataSeed
                    },
                    new Bandeira
                    {
                        BandeiraUuid = "11111111-1111-1111-1111-111111111103",
                        UsuarioUuid = null,
                        DescricaoBandeira = "Elo",
                        IconeBandeira = "ms-appx:///Assets/Bandeiras/elo.png",
                        Ativo = true,
                        CriadoEm = dataSeed,
                        UpdatedAt = dataSeed
                    },
                    new Bandeira
                    {
                        BandeiraUuid = "11111111-1111-1111-1111-111111111104",
                        UsuarioUuid = null,
                        DescricaoBandeira = "American Express",
                        IconeBandeira = "ms-appx:///Assets/Bandeiras/amex.png",
                        Ativo = true,
                        CriadoEm = dataSeed,
                        UpdatedAt = dataSeed
                    },
                    new Bandeira
                    {
                        BandeiraUuid = "11111111-1111-1111-1111-111111111105",
                        UsuarioUuid = null,
                        DescricaoBandeira = "Hipercard",
                        IconeBandeira = "ms-appx:///Assets/Bandeiras/hipercard.png",
                        Ativo = true,
                        CriadoEm = dataSeed,
                        UpdatedAt = dataSeed
                    }
                );
            });

            // Cartões de Crédito (cartoes_credito)
            modelBuilder.Entity<CartaoCredito>(entity =>
            {
                entity.ToTable("cartoes_credito");
                entity.HasKey(c => c.CartaoUuid);

                entity.HasIndex(c => c.UsuarioUuid)
                      .HasDatabaseName("idx_cartoes_credito_usuario");

                entity.HasOne(c => c.Bandeira)
                      .WithMany()
                      .HasForeignKey(c => c.BandeiraUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(c => c.ContaUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Faturas de Cartão (faturas_cartao)
            modelBuilder.Entity<FaturaCartao>(entity =>
            {
                entity.ToTable("faturas_cartao");
                entity.HasKey(f => f.FaturaUuid);

                entity.HasIndex(f => new { f.CartaoUuid, f.MesAnoFatura })
                      .IsUnique()
                      .HasDatabaseName("idx_faturas_cartao_mesano");

                entity.HasIndex(f => new { f.UsuarioUuid, f.VencimentoFatura })
                      .HasDatabaseName("idx_faturas_cartao_vencimento");

                entity.HasOne(f => f.CartaoCredito)
                      .WithMany(c => c.Faturas)
                      .HasForeignKey(f => f.CartaoUuid)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Lançamentos de Cartão (lancamentos_cartao)
            modelBuilder.Entity<LancamentoCartao>(entity =>
            {
                entity.ToTable("lancamentos_cartao");
                entity.HasKey(l => l.LancamentoCartaoUuid);

                entity.HasIndex(l => new { l.CartaoUuid, l.DataLancamento })
                      .HasDatabaseName("idx_lancamentos_cartao_data");

                entity.HasIndex(l => l.FaturaUuid)
                      .HasDatabaseName("idx_lancamentos_cartao_fatura");

                entity.HasIndex(l => l.GrupoParcelamentoUuid)
                      .HasDatabaseName("idx_lancamentos_cartao_grupo");

                entity.HasOne(l => l.CartaoCredito)
                      .WithMany()
                      .HasForeignKey(l => l.CartaoUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.FaturaCartao)
                      .WithMany(f => f.Lancamentos)
                      .HasForeignKey(l => l.FaturaUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Categoria)
                      .WithMany()
                      .HasForeignKey(l => l.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Entidade)
                      .WithMany()
                      .HasForeignKey(l => l.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}