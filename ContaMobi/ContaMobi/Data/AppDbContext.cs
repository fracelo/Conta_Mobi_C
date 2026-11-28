using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Models;
using System;
using System.IO;

namespace ContaMobi.Data
{
    public class AppDbContext : DbContext
    {
        private static readonly object _syncMigracao = new();
        private static bool _colunasVerificadas = false;

        public DbSet<Plano> Planos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
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
        public DbSet<Bandeira> Bandeiras { get; set; }
        public DbSet<CartaoCredito> CartoesCredito { get; set; }
        public DbSet<FaturaCartao> FaturasCartao { get; set; }
        public DbSet<LancamentoCartao> LancamentosCartao { get; set; }

        public AppDbContext()
        {
            GarantirColunasUmaVez();
        }

        public static string ObterCaminhoBancoSqlite()
        {
            string pastaApp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ContaMobi");

            if (!Directory.Exists(pastaApp))
            {
                Directory.CreateDirectory(pastaApp);
            }

            return Path.Combine(pastaApp, "contamobi.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string caminhoBanco = ObterCaminhoBancoSqlite();
            optionsBuilder.UseSqlite($"Data Source={caminhoBanco}");
        }

        private static void GarantirColunasUmaVez()
        {
            if (_colunasVerificadas)
                return;

            lock (_syncMigracao)
            {
                if (_colunasVerificadas)
                    return;

                string caminhoBanco = ObterCaminhoBancoSqlite();
                if (!File.Exists(caminhoBanco))
                    return;

                AplicarColunasNoSqlite(caminhoBanco);
                _colunasVerificadas = true;
            }
        }

        public void GarantirColunasAtualizadas()
        {
            lock (_syncMigracao)
            {
                string caminhoBanco = ObterCaminhoBancoSqlite();
                if (!File.Exists(caminhoBanco))
                    return;

                AplicarColunasNoSqlite(caminhoBanco);
                _colunasVerificadas = true;
            }
        }

        private static void AplicarColunasNoSqlite(string caminhoBanco)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={caminhoBanco}");
                conn.Open();

                GarantirColunaSqlite(conn, "lancamentos_contas", "transferencia_uuid", "TEXT NULL");

                GarantirColunaSqlite(conn, "contas_pr", "loteuuid", "TEXT NOT NULL DEFAULT ''");
                GarantirColunaSqlite(conn, "contas_pr", "valor_base", "NUMERIC(15, 2) NOT NULL DEFAULT 0.00");
                GarantirColunaSqlite(conn, "contas_pr", "modo_parcelamento", "TEXT NOT NULL DEFAULT 'R'");
                GarantirColunaSqlite(conn, "contas_pr", "numero_parcela", "INTEGER NOT NULL DEFAULT 1");
                GarantirColunaSqlite(conn, "contas_pr", "total_parcelas", "INTEGER NOT NULL DEFAULT 1");
                GarantirColunaSqlite(conn, "contas_pr", "intervalo_dias", "INTEGER NOT NULL DEFAULT 30");
                GarantirColunaSqlite(conn, "contas_pr", "lancamento_baixa_uuid", "TEXT NULL");

                GarantirColunaSqlite(conn, "cartoes_credito", "taxa_juros_rotativo", "NUMERIC(15, 4) NOT NULL DEFAULT 0.0000");
                GarantirColunaSqlite(conn, "cartoes_credito", "tipo_taxa_juros", "TEXT NOT NULL DEFAULT 'M'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO GARANTIR COLUNAS SQLITE]: {ex.Message}");
            }
        }

        private static void GarantirColunaSqlite(SqliteConnection conn, string tabela, string coluna, string definicaoSql)
        {
            try
            {
                using (var cmdTabela = conn.CreateCommand())
                {
                    cmdTabela.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND lower(name) = lower(@tabela);";
                    cmdTabela.Parameters.AddWithValue("@tabela", tabela);
                    long existeTabela = Convert.ToInt64(cmdTabela.ExecuteScalar() ?? 0L);
                    if (existeTabela == 0)
                        return;
                }

                bool colunaExiste = false;
                using (var cmdCheck = conn.CreateCommand())
                {
                    cmdCheck.CommandText = $"PRAGMA table_info(\"{tabela}\");";
                    using var reader = cmdCheck.ExecuteReader();
                    while (reader.Read())
                    {
                        string nomeColuna = reader["name"]?.ToString() ?? string.Empty;
                        if (string.Equals(nomeColuna, coluna, StringComparison.OrdinalIgnoreCase))
                        {
                            colunaExiste = true;
                            break;
                        }
                    }
                }

                if (!colunaExiste)
                {
                    using var cmdAlter = conn.CreateCommand();
                    cmdAlter.CommandText = $"ALTER TABLE \"{tabela}\" ADD COLUMN \"{coluna}\" {definicaoSql};";
                    cmdAlter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO ALTER TABLE {tabela}.{coluna}]: {ex.Message}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plano>(entity =>
            {
                entity.HasKey(e => e.PlanoUuid);
                entity.Property(e => e.Valor).HasColumnType("decimal(15,2)");
                entity.Property(e => e.ValorAnualParcela).HasColumnType("decimal(15,2)");
                entity.Property(e => e.ValorAnualTotal).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => e.Codigo).IsUnique();
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasKey(e => e.UsuarioUuid);
                entity.HasOne(e => e.Plano)
                      .WithMany()
                      .HasForeignKey(e => e.PlanoUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Banco>(entity =>
            {
                entity.HasKey(e => e.CodigoBanco);
            });

            modelBuilder.Entity<Categoria>(entity =>
            {
                entity.HasKey(e => e.CategoriaUuid);
                entity.HasOne(e => e.CategoriaPai)
                      .WithMany(e => e.Subcategorias)
                      .HasForeignKey(e => e.PaiUuid)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ContaCorrente>(entity =>
            {
                entity.HasKey(e => e.ContaUuid);
                entity.Property(e => e.SaldoInicial).HasColumnType("decimal(15,2)");
                entity.HasOne(e => e.Banco)
                      .WithMany()
                      .HasForeignKey(e => e.CodigoBanco)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SaldoContaCorrente>(entity =>
            {
                entity.HasKey(e => e.SaldoId);
                entity.Property(e => e.SaldoInicial).HasColumnType("decimal(15,2)");
                entity.Property(e => e.TotalCredito).HasColumnType("decimal(15,2)");
                entity.Property(e => e.TotalDebito).HasColumnType("decimal(15,2)");
                entity.Property(e => e.SaldoFinal).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => new { e.ContaUuid, e.Data }).IsUnique();
                entity.HasOne(e => e.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(e => e.ContaUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Orcamento>(entity =>
            {
                entity.HasKey(e => e.OrcamentoUuid);
                entity.Property(e => e.ValorPlanejado).HasColumnType("decimal(15,2)");
                entity.Property(e => e.ValorRealizado).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => new { e.UsuarioUuid, e.CategoriaUuid, e.Ano, e.Mes }).IsUnique();
                entity.HasOne(e => e.Categoria)
                      .WithMany()
                      .HasForeignKey(e => e.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Entidade>(entity =>
            {
                entity.HasKey(e => e.EntidadeUuid);
            });

            modelBuilder.Entity<Contato>(entity =>
            {
                entity.HasKey(e => e.ContatoUuid);
                entity.HasOne(e => e.Entidade)
                      .WithMany(e => e.Contatos)
                      .HasForeignKey(e => e.EntidadeUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LancamentoConta>(entity =>
            {
                entity.HasKey(e => e.LancamentoUuid);
                entity.Property(e => e.Valor).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => new { e.ContaUuid, e.DataLancamento });
                entity.HasOne(e => e.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(e => e.ContaUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Categoria)
                      .WithMany()
                      .HasForeignKey(e => e.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Entidade)
                      .WithMany()
                      .HasForeignKey(e => e.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ContaPR>(entity =>
            {
                entity.HasKey(e => e.ContasPRUuid);
                entity.Property(e => e.ValorBase).HasColumnType("decimal(15,2)");
                entity.Property(e => e.Valor).HasColumnType("decimal(15,2)");
                entity.Property(e => e.Multa).HasColumnType("decimal(15,2)");
                entity.Property(e => e.Juro).HasColumnType("decimal(15,2)");
                entity.Property(e => e.Acrescimo).HasColumnType("decimal(15,2)");
                entity.Property(e => e.Desconto).HasColumnType("decimal(15,2)");
                entity.Property(e => e.ValorPago).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => new { e.Tipo, e.DataVencimento });
                entity.HasIndex(e => e.LoteUuid);
                entity.HasOne(e => e.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(e => e.ContaUuid)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Categoria)
                      .WithMany()
                      .HasForeignKey(e => e.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Entidade)
                      .WithMany()
                      .HasForeignKey(e => e.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ContaPRParcela>(entity =>
            {
                entity.HasKey(e => e.ParcelaUuid);
                entity.Property(e => e.ValorBase).HasColumnType("decimal(15,2)");
                entity.Property(e => e.ValorParcela).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => e.LoteUuid);
                entity.HasIndex(e => e.ContasPRUuid);
            });

            modelBuilder.Entity<Bandeira>(entity =>
            {
                entity.HasKey(e => e.BandeiraUuid);
            });

            modelBuilder.Entity<CartaoCredito>(entity =>
            {
                entity.HasKey(e => e.CartaoUuid);
                entity.Property(e => e.LimiteTotal).HasColumnType("decimal(15,2)");
                entity.Property(e => e.LimiteAtual).HasColumnType("decimal(15,2)");
                entity.Property(e => e.TaxaJurosRotativo).HasColumnType("decimal(15,4)");
                entity.HasOne(e => e.Bandeira)
                      .WithMany()
                      .HasForeignKey(e => e.BandeiraUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ContaCorrente)
                      .WithMany()
                      .HasForeignKey(e => e.ContaUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<FaturaCartao>(entity =>
            {
                entity.HasKey(e => e.FaturaUuid);
                entity.Property(e => e.TotalFatura).HasColumnType("decimal(15,2)");
                entity.Property(e => e.TotalPago).HasColumnType("decimal(15,2)");
                entity.Property(e => e.SaldoFatura).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => new { e.CartaoUuid, e.MesAnoFatura }).IsUnique();
                entity.HasOne(e => e.CartaoCredito)
                      .WithMany(c => c.Faturas)
                      .HasForeignKey(e => e.CartaoUuid)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LancamentoCartao>(entity =>
            {
                entity.HasKey(e => e.LancamentoCartaoUuid);
                entity.Property(e => e.ValorParcela).HasColumnType("decimal(15,2)");
                entity.HasIndex(e => e.FaturaUuid);
                entity.HasIndex(e => e.GrupoParcelamentoUuid);
                entity.HasOne(e => e.CartaoCredito)
                      .WithMany()
                      .HasForeignKey(e => e.CartaoUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.FaturaCartao)
                      .WithMany(f => f.Lancamentos)
                      .HasForeignKey(e => e.FaturaUuid)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Categoria)
                      .WithMany()
                      .HasForeignKey(e => e.CategoriaUuid)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Entidade)
                      .WithMany()
                      .HasForeignKey(e => e.EntidadeUuid)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}