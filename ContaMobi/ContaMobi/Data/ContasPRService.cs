using Microsoft.EntityFrameworkCore;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ContaMobi.Data
{
    public class ResumoContasPR
    {
        public decimal TotalValorOriginal { get; set; }
        public decimal TotalEncargos { get; set; }
        public decimal TotalQuitado { get; set; }
        public decimal TotalEmAberto { get; set; }
        public List<ContaPR> Registros { get; set; } = new();
    }

    public static class ContasPRService
    {
        private static bool _estruturaVerificada = false;

        /// <summary>
        /// Recria do zero as tabelas contas_pr e contas_pr_parcelas no SQLite local.
        /// </summary>
        public static void RecriarTabelaContasPR()
        {
            using var db = new AppDbContext();
            db.Database.ExecuteSqlRaw(@"
                DROP TABLE IF EXISTS contas_pr_parcelas;
                DROP TABLE IF EXISTS contas_pr;
            ");
            _estruturaVerificada = false;
            GarantirTabelasCriadas();
        }

        public static void GarantirTabelasCriadas()
        {
            if (_estruturaVerificada) return;

            using var db = new AppDbContext();

            // Verifica se a coluna loteuuid já existe na tabela contas_pr; se não existir, recria a estrutura nova
            try
            {
                _ = db.ContasPR.AsNoTracking().Select(c => c.LoteUuid).FirstOrDefault();
                _ = db.ContasPRParcelas.AsNoTracking().FirstOrDefault();
                _estruturaVerificada = true;
                return;
            }
            catch
            {
                // Cria ou atualiza as tabelas locais
            }

            db.Database.ExecuteSqlRaw(@"
                DROP TABLE IF EXISTS contas_pr_parcelas;
                DROP TABLE IF EXISTS contas_pr;

                CREATE TABLE IF NOT EXISTS contas_pr (
                    contaspruuid TEXT NOT NULL CONSTRAINT PK_contas_pr PRIMARY KEY,
                    loteuuid TEXT NOT NULL,
                    tipo TEXT NOT NULL,
                    usuariouuid TEXT NOT NULL,
                    contauuid TEXT NULL,
                    categoriauuid TEXT NOT NULL,
                    entidadeuuid TEXT NULL,
                    descricao TEXT NOT NULL,
                    data_lancamento TEXT NOT NULL,
                    data_vencimento TEXT NOT NULL,
                    valor_base TEXT NOT NULL DEFAULT '0.0',
                    modo_parcelamento TEXT NOT NULL DEFAULT 'R',
                    numero_parcela INTEGER NOT NULL DEFAULT 1,
                    total_parcelas INTEGER NOT NULL DEFAULT 1,
                    intervalo_dias INTEGER NOT NULL DEFAULT 30,
                    valor TEXT NOT NULL DEFAULT '0.0',
                    multa TEXT NOT NULL DEFAULT '0.0',
                    juro TEXT NOT NULL DEFAULT '0.0',
                    valor_pago TEXT NOT NULL DEFAULT '0.0',
                    data_pagamento TEXT NULL,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_contas_pr_ContasCorrentes FOREIGN KEY (contauuid) REFERENCES ContasCorrentes (contauuid) ON DELETE SET NULL,
                    CONSTRAINT FK_contas_pr_Categorias FOREIGN KEY (categoriauuid) REFERENCES Categorias (categoriauuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_contas_pr_entidades FOREIGN KEY (entidadeuuid) REFERENCES entidades (entidadeuuid) ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS idx_contas_pr_tipo_vencimento ON contas_pr (tipo, data_vencimento);
                CREATE INDEX IF NOT EXISTS idx_contas_pr_lote ON contas_pr (loteuuid);

                CREATE TABLE IF NOT EXISTS contas_pr_parcelas (
                    parcelauuid TEXT NOT NULL CONSTRAINT PK_contas_pr_parcelas PRIMARY KEY,
                    loteuuid TEXT NOT NULL,
                    contaspruuid TEXT NOT NULL,
                    numero_parcela INTEGER NOT NULL DEFAULT 1,
                    total_parcelas INTEGER NOT NULL DEFAULT 1,
                    modo_parcelamento TEXT NOT NULL DEFAULT 'R',
                    intervalo_dias INTEGER NOT NULL DEFAULT 30,
                    valor_base TEXT NOT NULL DEFAULT '0.0',
                    data_vencimento TEXT NOT NULL,
                    valor_parcela TEXT NOT NULL DEFAULT '0.0',
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_contas_pr_parcelas_contas_pr FOREIGN KEY (contaspruuid) REFERENCES contas_pr (contaspruuid) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_contas_pr_parcelas_lote ON contas_pr_parcelas (loteuuid, numero_parcela);
            ");

            _estruturaVerificada = true;
        }

        public static ResumoContasPR ListarContas(
            string tipo,
            DateTime dataInicio,
            DateTime dataFim,
            string statusFiltro = "TODOS",
            string? contaUuid = null,
            string? termoBusca = null)
        {
            GarantirTabelasCriadas();

            using var db = new AppDbContext();
            DateTime dtIni = dataInicio.Date;
            DateTime dtFim = dataFim.Date.AddDays(1).AddTicks(-1);

            var query = db.ContasPR
                .AsNoTracking()
                .Include(c => c.ContaCorrente)
                .Include(c => c.Categoria)
                .Include(c => c.Entidade)
                .Where(c => c.Tipo == tipo && c.DataVencimento >= dtIni && c.DataVencimento <= dtFim);

            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                query = query.Where(c => c.ContaUuid == contaUuid);
            }

            var lista = query
                .OrderBy(c => c.DataVencimento)
                .ThenBy(c => c.NumeroParcela)
                .ThenBy(c => c.Descricao)
                .ToList();

            if (!string.IsNullOrWhiteSpace(termoBusca))
            {
                string termo = termoBusca.Trim().ToLowerInvariant();
                lista = lista.Where(c =>
                    c.Descricao.ToLowerInvariant().Contains(termo) ||
                    c.NomeEntidade.ToLowerInvariant().Contains(termo) ||
                    c.NomeCategoria.ToLowerInvariant().Contains(termo)).ToList();
            }

            if (statusFiltro == "ABERTOS")
            {
                lista = lista.Where(c => !c.EstaQuitado).ToList();
            }
            else if (statusFiltro == "QUITADOS")
            {
                lista = lista.Where(c => c.EstaQuitado).ToList();
            }
            else if (statusFiltro == "VENCIDOS")
            {
                lista = lista.Where(c => !c.EstaQuitado && c.DataVencimento.Date < DateTime.Today).ToList();
            }

            decimal totalOriginal = lista.Sum(c => c.Valor);
            decimal totalEncargos = lista.Sum(c => c.Multa + c.Juro);
            decimal totalQuitado = lista.Where(c => c.EstaQuitado).Sum(c => c.ValorPago);
            decimal totalAberto = lista.Where(c => !c.EstaQuitado).Sum(c => c.Valor + c.Multa + c.Juro);

            return new ResumoContasPR
            {
                TotalValorOriginal = totalOriginal,
                TotalEncargos = totalEncargos,
                TotalQuitado = totalQuitado,
                TotalEmAberto = totalAberto,
                Registros = lista
            };
        }

        /// <summary>
        /// Recupera o Grid de parcelas salvo na tabela adicional contas_pr_parcelas ao editar uma conta.
        /// </summary>
        public static List<ContaPRParcela> ObterGradeParcelas(ContaPR conta)
        {
            GarantirTabelasCriadas();
            var cultura = new CultureInfo("pt-BR");

            using var db = new AppDbContext();
            var parcelasSalvas = new List<ContaPRParcela>();

            if (!string.IsNullOrWhiteSpace(conta.LoteUuid))
            {
                parcelasSalvas = db.ContasPRParcelas
                    .AsNoTracking()
                    .Where(p => p.LoteUuid == conta.LoteUuid)
                    .OrderBy(p => p.NumeroParcela)
                    .ToList();
            }

            if (parcelasSalvas.Count == 0)
            {
                parcelasSalvas.Add(new ContaPRParcela
                {
                    ParcelaUuid = Guid.NewGuid().ToString(),
                    LoteUuid = string.IsNullOrWhiteSpace(conta.LoteUuid) ? Guid.NewGuid().ToString() : conta.LoteUuid,
                    ContasPRUuid = conta.ContasPRUuid,
                    NumeroParcela = 1,
                    TotalParcelas = 1,
                    ModoParcelamento = string.IsNullOrWhiteSpace(conta.ModoParcelamento) ? "R" : conta.ModoParcelamento,
                    IntervaloDias = conta.IntervaloDias > 0 ? conta.IntervaloDias : 30,
                    ValorBase = conta.ValorBase > 0 ? conta.ValorBase : conta.Valor,
                    DataVencimento = conta.DataVencimento == default ? DateTime.Today : conta.DataVencimento,
                    ValorParcela = conta.Valor
                });
            }

            foreach (var p in parcelasSalvas)
            {
                p.DataVencimentoTexto = p.DataVencimento.ToString("dd/MM/yyyy");
                p.ValorParcelaTexto = p.ValorParcela.ToString("N2", cultura);
                p.DestaqueCor = p.ContasPRUuid == conta.ContasPRUuid ? "#3399FF" : "#00FF66";
            }

            return parcelasSalvas;
        }

        /// <summary>
        /// Salva o Grid na tabela adicional (contas_pr_parcelas) e insere/atualiza as contas correspondentes em contas_pr.
        /// </summary>
        public static void SalvarContaPRComGrade(
            ContaPR dadosBase,
            List<ContaPRParcela> gradeParcelas,
            bool ehNovo)
        {
            GarantirTabelasCriadas();

            using var db = new AppDbContext();
            using var transacao = db.Database.BeginTransaction();

            string loteUuid = string.IsNullOrWhiteSpace(dadosBase.LoteUuid)
                ? Guid.NewGuid().ToString()
                : dadosBase.LoteUuid;

            int totalParcelas = Math.Max(1, gradeParcelas.Count);

            // Remove sufixo antigo "(01/12)" da descrição base para não duplicar ao editar
            string descricaoLimpa = LimparSufixoParcela(dadosBase.Descricao);

            // Busca contas e parcelas já existentes deste lote (para o caso de edição)
            var contasExistentesLote = db.ContasPR
                .Where(c => c.LoteUuid == loteUuid)
                .ToList();

            var parcelasExistentesLote = db.ContasPRParcelas
                .Where(p => p.LoteUuid == loteUuid)
                .ToList();

            if (parcelasExistentesLote.Count > 0)
            {
                db.ContasPRParcelas.RemoveRange(parcelasExistentesLote);
            }

            var idsMantidos = new HashSet<string>();

            for (int i = 0; i < gradeParcelas.Count; i++)
            {
                var itemGrade = gradeParcelas[i];
                int numParcela = i + 1;

                string descricaoFinal = totalParcelas > 1
                    ? $"{descricaoLimpa} ({numParcela:D2}/{totalParcelas:D2})"
                    : descricaoLimpa;

                ContaPR? contaExistente = null;
                if (!ehNovo)
                {
                    contaExistente = contasExistentesLote.FirstOrDefault(c => c.ContasPRUuid == itemGrade.ContasPRUuid)
                                  ?? contasExistentesLote.FirstOrDefault(c => c.NumeroParcela == numParcela);
                }

                string contaPRUuid;

                if (contaExistente != null)
                {
                    contaPRUuid = contaExistente.ContasPRUuid;
                    idsMantidos.Add(contaPRUuid);

                    contaExistente.LoteUuid = loteUuid;
                    contaExistente.Tipo = dadosBase.Tipo;
                    contaExistente.ContaUuid = dadosBase.ContaUuid;
                    contaExistente.CategoriaUuid = dadosBase.CategoriaUuid;
                    contaExistente.EntidadeUuid = dadosBase.EntidadeUuid;
                    contaExistente.Descricao = descricaoFinal;
                    contaExistente.DataLancamento = dadosBase.DataLancamento.Date;
                    contaExistente.DataVencimento = itemGrade.DataVencimento.Date;
                    contaExistente.ValorBase = Math.Abs(dadosBase.ValorBase);
                    contaExistente.ModoParcelamento = dadosBase.ModoParcelamento;
                    contaExistente.NumeroParcela = numParcela;
                    contaExistente.TotalParcelas = totalParcelas;
                    contaExistente.IntervaloDias = dadosBase.IntervaloDias;
                    contaExistente.Valor = Math.Abs(itemGrade.ValorParcela);
                    contaExistente.UpdatedAt = DateTime.UtcNow;

                    // Se esta é a parcela aberta para edição, aplica também os dados de baixa (Multa, Juro, Pagto)
                    if (contaPRUuid == dadosBase.ContasPRUuid || totalParcelas == 1)
                    {
                        contaExistente.Multa = Math.Abs(dadosBase.Multa);
                        contaExistente.Juro = Math.Abs(dadosBase.Juro);
                        contaExistente.ValorPago = Math.Abs(dadosBase.ValorPago);
                        contaExistente.DataPagamento = dadosBase.DataPagamento?.Date;
                    }
                }
                else
                {
                    contaPRUuid = (i == 0 && ehNovo && !string.IsNullOrWhiteSpace(dadosBase.ContasPRUuid))
                        ? dadosBase.ContasPRUuid
                        : Guid.NewGuid().ToString();

                    idsMantidos.Add(contaPRUuid);

                    bool aplicarBaixaNesta = (i == 0 && totalParcelas == 1);

                    var novaConta = new ContaPR
                    {
                        ContasPRUuid = contaPRUuid,
                        LoteUuid = loteUuid,
                        Tipo = dadosBase.Tipo,
                        UsuarioUuid = dadosBase.UsuarioUuid,
                        ContaUuid = dadosBase.ContaUuid,
                        CategoriaUuid = dadosBase.CategoriaUuid,
                        EntidadeUuid = dadosBase.EntidadeUuid,
                        Descricao = descricaoFinal,
                        DataLancamento = dadosBase.DataLancamento.Date,
                        DataVencimento = itemGrade.DataVencimento.Date,
                        ValorBase = Math.Abs(dadosBase.ValorBase),
                        ModoParcelamento = dadosBase.ModoParcelamento,
                        NumeroParcela = numParcela,
                        TotalParcelas = totalParcelas,
                        IntervaloDias = dadosBase.IntervaloDias,
                        Valor = Math.Abs(itemGrade.ValorParcela),
                        Multa = aplicarBaixaNesta ? Math.Abs(dadosBase.Multa) : 0m,
                        Juro = aplicarBaixaNesta ? Math.Abs(dadosBase.Juro) : 0m,
                        ValorPago = aplicarBaixaNesta ? Math.Abs(dadosBase.ValorPago) : 0m,
                        DataPagamento = aplicarBaixaNesta ? dadosBase.DataPagamento?.Date : null,
                        CriadoEm = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    db.ContasPR.Add(novaConta);
                }

                // Insere a linha correspondente na tabela adicional contas_pr_parcelas
                var novaParcelaGrid = new ContaPRParcela
                {
                    ParcelaUuid = Guid.NewGuid().ToString(),
                    LoteUuid = loteUuid,
                    ContasPRUuid = contaPRUuid,
                    NumeroParcela = numParcela,
                    TotalParcelas = totalParcelas,
                    ModoParcelamento = dadosBase.ModoParcelamento,
                    IntervaloDias = dadosBase.IntervaloDias,
                    ValorBase = Math.Abs(dadosBase.ValorBase),
                    DataVencimento = itemGrade.DataVencimento.Date,
                    ValorParcela = Math.Abs(itemGrade.ValorParcela),
                    CriadoEm = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.ContasPRParcelas.Add(novaParcelaGrid);
            }

            // Se na edição o usuário diminuiu a quantidade de parcelas, remove as excedentes não quitadas
            if (!ehNovo && contasExistentesLote.Count > 0)
            {
                var excedentes = contasExistentesLote
                    .Where(c => !idsMantidos.Contains(c.ContasPRUuid) && !c.EstaQuitado)
                    .ToList();

                if (excedentes.Count > 0)
                {
                    db.ContasPR.RemoveRange(excedentes);
                }
            }

            db.SaveChanges();
            transacao.Commit();
        }

        private static string LimparSufixoParcela(string descricao)
        {
            if (string.IsNullOrWhiteSpace(descricao)) return string.Empty;
            string texto = descricao.Trim();

            int idxAbre = texto.LastIndexOf(" (");
            if (idxAbre > 0 && texto.EndsWith(")"))
            {
                string miolo = texto.Substring(idxAbre + 2, texto.Length - idxAbre - 3);
                if (miolo.Contains('/'))
                {
                    var partes = miolo.Split('/');
                    if (partes.Length == 2 && int.TryParse(partes[0], out _) && int.TryParse(partes[1], out _))
                    {
                        return texto.Substring(0, idxAbre).Trim();
                    }
                }
            }
            return texto;
        }

        public static void ExcluirContaPR(string contasPRUuid)
        {
            GarantirTabelasCriadas();

            using var db = new AppDbContext();
            var parcelasGrid = db.ContasPRParcelas.Where(p => p.ContasPRUuid == contasPRUuid).ToList();
            if (parcelasGrid.Count > 0)
            {
                db.ContasPRParcelas.RemoveRange(parcelasGrid);
            }

            var existente = db.ContasPR.Find(contasPRUuid);
            if (existente != null)
            {
                db.ContasPR.Remove(existente);
            }

            db.SaveChanges();
        }
    }
}