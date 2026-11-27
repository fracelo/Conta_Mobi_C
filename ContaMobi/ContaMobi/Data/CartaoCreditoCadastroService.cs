using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContaMobi.Data
{
    public class ResumoCartoesCredito
    {
        public decimal TotalLimiteGeral { get; set; }
        public decimal TotalLimiteUtilizado { get; set; }
        public decimal TotalLimiteDisponivel { get; set; }
        public List<CartaoCredito> Registros { get; set; } = new();
    }

    public static class CartaoCreditoCadastroService
    {
        private static bool _estruturaVerificada = false;

        public static void GarantirEstruturaCartoes()
        {
            if (_estruturaVerificada) return;

            using var db = new AppDbContext();

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS bandeiras (
                    bandeirasuuid TEXT NOT NULL CONSTRAINT PK_bandeiras PRIMARY KEY,
                    usuariouuid TEXT NULL,
                    descricao_bandeira TEXT NOT NULL,
                    icone_bandeira TEXT NULL,
                    ativo INTEGER NOT NULL DEFAULT 1,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_bandeiras_usuario ON bandeiras (usuariouuid);

                CREATE TABLE IF NOT EXISTS cartoes_credito (
                    cartaouuid TEXT NOT NULL CONSTRAINT PK_cartoes_credito PRIMARY KEY,
                    usuariouuid TEXT NOT NULL,
                    bandeirauuid TEXT NOT NULL,
                    contauuid TEXT NULL,
                    descricao TEXT NOT NULL,
                    ultimos_digitos TEXT NULL,
                    dia_fechamento INTEGER NOT NULL DEFAULT 1,
                    dia_vencimento INTEGER NOT NULL DEFAULT 10,
                    limite_total TEXT NOT NULL DEFAULT '0.0',
                    limite_atual TEXT NOT NULL DEFAULT '0.0',
                    cor_hex TEXT NOT NULL DEFAULT '#008552',
                    ativo INTEGER NOT NULL DEFAULT 1,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_cartoes_credito_bandeiras FOREIGN KEY (bandeirauuid) REFERENCES bandeiras (bandeirasuuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_cartoes_credito_ContasCorrentes FOREIGN KEY (contauuid) REFERENCES ContasCorrentes (contauuid) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_cartoes_credito_usuario ON cartoes_credito (usuariouuid);

                CREATE TABLE IF NOT EXISTS faturas_cartao (
                    faturauuid TEXT NOT NULL CONSTRAINT PK_faturas_cartao PRIMARY KEY,
                    usuariouuid TEXT NOT NULL,
                    cartaouuid TEXT NOT NULL,
                    mesano_fatura TEXT NOT NULL,
                    data_fechamento TEXT NOT NULL,
                    vencimento_fatura TEXT NOT NULL,
                    total_fatura TEXT NOT NULL DEFAULT '0.0',
                    total_pago TEXT NOT NULL DEFAULT '0.0',
                    saldo_fatura TEXT NOT NULL DEFAULT '0.0',
                    status_fatura TEXT NOT NULL DEFAULT 'ABERTA',
                    data_pagamento TEXT NULL,
                    lancamento_pagamento_uuid TEXT NULL,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_faturas_cartao_cartoes_credito FOREIGN KEY (cartaouuid) REFERENCES cartoes_credito (cartaouuid) ON DELETE RESTRICT
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_faturas_cartao_mesano ON faturas_cartao (cartaouuid, mesano_fatura);
                CREATE INDEX IF NOT EXISTS idx_faturas_cartao_vencimento ON faturas_cartao (usuariouuid, vencimento_fatura);

                CREATE TABLE IF NOT EXISTS lancamentos_cartao (
                    lancamentocartaouuid TEXT NOT NULL CONSTRAINT PK_lancamentos_cartao PRIMARY KEY,
                    usuariouuid TEXT NOT NULL,
                    cartaouuid TEXT NOT NULL,
                    faturauuid TEXT NOT NULL,
                    categoriauuid TEXT NOT NULL,
                    entidadeuuid TEXT NULL,
                    grupo_parcelamento_uuid TEXT NOT NULL,
                    data_lancamento TEXT NOT NULL,
                    descricao TEXT NOT NULL,
                    valor_parcela TEXT NOT NULL DEFAULT '0.0',
                    nr_parcelas INTEGER NOT NULL DEFAULT 1,
                    parcela_atual INTEGER NOT NULL DEFAULT 1,
                    observacao TEXT NULL,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_lancamentos_cartao_cartoes_credito FOREIGN KEY (cartaouuid) REFERENCES cartoes_credito (cartaouuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_lancamentos_cartao_faturas_cartao FOREIGN KEY (faturauuid) REFERENCES faturas_cartao (faturauuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_lancamentos_cartao_Categorias FOREIGN KEY (categoriauuid) REFERENCES Categorias (categoriauuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_lancamentos_cartao_entidades FOREIGN KEY (entidadeuuid) REFERENCES entidades (entidadeuuid) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_lancamentos_cartao_data ON lancamentos_cartao (cartaouuid, data_lancamento);
                CREATE INDEX IF NOT EXISTS idx_lancamentos_cartao_fatura ON lancamentos_cartao (faturauuid);
                CREATE INDEX IF NOT EXISTS idx_lancamentos_cartao_grupo ON lancamentos_cartao (grupo_parcelamento_uuid);
            ");

            // Garante carga inicial das bandeiras padrão caso a tabela ainda esteja vazia
            if (!db.Bandeiras.AsNoTracking().Any())
            {
                var agora = DateTime.UtcNow;
                db.Bandeiras.AddRange(
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111101", DescricaoBandeira = "Visa", IconeBandeira = "ms-appx:///Assets/Bandeiras/visa.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111102", DescricaoBandeira = "Mastercard", IconeBandeira = "ms-appx:///Assets/Bandeiras/mastercard.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111103", DescricaoBandeira = "Elo", IconeBandeira = "ms-appx:///Assets/Bandeiras/elo.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111104", DescricaoBandeira = "American Express", IconeBandeira = "ms-appx:///Assets/Bandeiras/amex.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111105", DescricaoBandeira = "Hipercard", IconeBandeira = "ms-appx:///Assets/Bandeiras/hipercard.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora }
                );
                db.SaveChanges();
            }

            _estruturaVerificada = true;
        }

        public static List<Bandeira> ListarBandeirasDisponiveis(string? incluirBandeiraUuid = null)
        {
            GarantirEstruturaCartoes();

            using var db = new AppDbContext();
            return db.Bandeiras
                .AsNoTracking()
                .Where(b => b.Ativo || (!string.IsNullOrEmpty(incluirBandeiraUuid) && b.BandeiraUuid == incluirBandeiraUuid))
                .OrderBy(b => b.DescricaoBandeira)
                .ToList();
        }

        public static List<ContaCorrente> ListarContasCorrentes()
        {
            using var db = new AppDbContext();
            var bancosMap = db.Bancos
                .AsNoTracking()
                .ToList()
                .GroupBy(b => b.CodigoBanco)
                .ToDictionary(g => g.Key, g => g.First());

            var contas = db.ContasCorrentes
                .AsNoTracking()
                .OrderBy(c => c.Descricao)
                .ToList();

            foreach (var c in contas)
            {
                if (bancosMap.TryGetValue(c.CodigoBanco, out var banco))
                    c.Banco = banco;
            }

            return contas;
        }

        public static ResumoCartoesCredito ListarCartoes(string statusFiltro = "TODOS", string? termoBusca = null)
        {
            GarantirEstruturaCartoes();

            using var db = new AppDbContext();
            var lista = db.CartoesCredito
                .AsNoTracking()
                .Include(c => c.Bandeira)
                .Include(c => c.ContaCorrente)
                .OrderByDescending(c => c.Ativo)
                .ThenBy(c => c.Descricao)
                .ToList();

            if (statusFiltro == "ATIVOS")
            {
                lista = lista.Where(c => c.Ativo).ToList();
            }
            else if (statusFiltro == "INATIVOS")
            {
                lista = lista.Where(c => !c.Ativo).ToList();
            }

            if (!string.IsNullOrWhiteSpace(termoBusca))
            {
                string termo = termoBusca.Trim().ToLowerInvariant();
                lista = lista.Where(c =>
                    c.Descricao.ToLowerInvariant().Contains(termo) ||
                    c.NomeBandeira.ToLowerInvariant().Contains(termo) ||
                    (!string.IsNullOrWhiteSpace(c.UltimosDigitos) && c.UltimosDigitos.Contains(termo)) ||
                    (c.ContaCorrente != null && c.ContaCorrente.Descricao.ToLowerInvariant().Contains(termo)))
                    .ToList();
            }

            var ativos = lista.Where(c => c.Ativo).ToList();

            return new ResumoCartoesCredito
            {
                TotalLimiteGeral = ativos.Sum(c => c.LimiteTotal),
                TotalLimiteUtilizado = ativos.Sum(c => c.LimiteUtilizado),
                TotalLimiteDisponivel = ativos.Sum(c => c.LimiteAtual),
                Registros = lista
            };
        }

        public static CartaoCredito? ObterCartaoPorId(string cartaoUuid)
        {
            GarantirEstruturaCartoes();

            using var db = new AppDbContext();
            return db.CartoesCredito
                .AsNoTracking()
                .Include(c => c.Bandeira)
                .Include(c => c.ContaCorrente)
                .FirstOrDefault(c => c.CartaoUuid == cartaoUuid);
        }

        public static void SalvarCartaoCredito(CartaoCredito dados, bool ehNovo)
        {
            GarantirEstruturaCartoes();

            using var db = new AppDbContext();

            decimal limiteTotal = Math.Max(0m, dados.LimiteTotal);
            string? ultimosDigitos = string.IsNullOrWhiteSpace(dados.UltimosDigitos)
                ? null
                : dados.UltimosDigitos.Trim();

            string corHex = string.IsNullOrWhiteSpace(dados.CorHex)
                ? "#008552"
                : dados.CorHex.Trim().ToUpperInvariant();

            if (!corHex.StartsWith("#"))
                corHex = "#" + corHex;

            if (ehNovo)
            {
                var novo = new CartaoCredito
                {
                    CartaoUuid = string.IsNullOrWhiteSpace(dados.CartaoUuid) ? Guid.NewGuid().ToString() : dados.CartaoUuid,
                    UsuarioUuid = dados.UsuarioUuid,
                    BandeiraUuid = dados.BandeiraUuid,
                    ContaUuid = string.IsNullOrWhiteSpace(dados.ContaUuid) ? null : dados.ContaUuid,
                    Descricao = dados.Descricao.Trim(),
                    UltimosDigitos = ultimosDigitos,
                    DiaFechamento = Math.Clamp(dados.DiaFechamento, 1, 31),
                    DiaVencimento = Math.Clamp(dados.DiaVencimento, 1, 31),
                    LimiteTotal = limiteTotal,
                    LimiteAtual = limiteTotal,
                    CorHex = corHex,
                    Ativo = dados.Ativo,
                    CriadoEm = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.CartoesCredito.Add(novo);
            }
            else
            {
                var existente = db.CartoesCredito.FirstOrDefault(c => c.CartaoUuid == dados.CartaoUuid);
                if (existente == null)
                    throw new InvalidOperationException("Cartão de crédito não encontrado no banco de dados.");

                // Preserva o valor já comprometido em faturas abertas ao reajustar o limite total
                decimal utilizadoAtual = Math.Max(0m, existente.LimiteTotal - existente.LimiteAtual);
                decimal novoLimiteAtual = Math.Max(0m, limiteTotal - utilizadoAtual);

                existente.BandeiraUuid = dados.BandeiraUuid;
                existente.ContaUuid = string.IsNullOrWhiteSpace(dados.ContaUuid) ? null : dados.ContaUuid;
                existente.Descricao = dados.Descricao.Trim();
                existente.UltimosDigitos = ultimosDigitos;
                existente.DiaFechamento = Math.Clamp(dados.DiaFechamento, 1, 31);
                existente.DiaVencimento = Math.Clamp(dados.DiaVencimento, 1, 31);
                existente.LimiteTotal = limiteTotal;
                existente.LimiteAtual = novoLimiteAtual;
                existente.CorHex = corHex;
                existente.Ativo = dados.Ativo;
                existente.UpdatedAt = DateTime.UtcNow;
            }

            db.SaveChanges();
        }

        public static void ExcluirCartaoCredito(string cartaoUuid)
        {
            GarantirEstruturaCartoes();

            using var db = new AppDbContext();

            bool possuiLancamentos = db.LancamentosCartao.AsNoTracking().Any(l => l.CartaoUuid == cartaoUuid);
            bool possuiFaturas = db.FaturasCartao.AsNoTracking().Any(f => f.CartaoUuid == cartaoUuid);

            if (possuiLancamentos || possuiFaturas)
            {
                throw new InvalidOperationException(
                    "Este cartão já possui faturas ou lançamentos vinculados e não pode ser excluído. Edite o cartão e altere a situação para INATIVO.");
            }

            var existente = db.CartoesCredito.FirstOrDefault(c => c.CartaoUuid == cartaoUuid);
            if (existente != null)
            {
                db.CartoesCredito.Remove(existente);
                db.SaveChanges();
            }
        }
    }
}