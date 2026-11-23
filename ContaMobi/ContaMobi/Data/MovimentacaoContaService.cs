using Microsoft.EntityFrameworkCore;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContaMobi.Data
{
    public class ResultadoExtratoPeriodo
    {
        public decimal SaldoAnterior { get; set; }
        public decimal SaldoFinal { get; set; }
        public List<LancamentoConta> Lancamentos { get; set; } = new();
    }

    public static class MovimentacaoContaService
    {
        /// <summary>
        /// Executa DROP TABLE e recria as tabelas lancamentos_contas e saldo_contas_correntes no SQLite local.
        /// Chame uma vez no InicializarBancoLocal() em App.xaml.cs quando precisar resetar a estrutura.
        /// </summary>
        public static void RecriarTabelasMovimentacao()
        {
            using var db = new AppDbContext();
            db.Database.ExecuteSqlRaw(@"
                DROP TABLE IF EXISTS lancamentos_conta;
                DROP TABLE IF EXISTS lancamentos_contas;
                DROP TABLE IF EXISTS saldo_contas_correntes;

                CREATE TABLE saldo_contas_correntes (
                    saldoid TEXT NOT NULL CONSTRAINT PK_saldo_contas_correntes PRIMARY KEY,
                    contauuid TEXT NOT NULL,
                    data TEXT NOT NULL,
                    saldo_inicial TEXT NOT NULL DEFAULT '0.0',
                    total_credito TEXT NOT NULL DEFAULT '0.0',
                    total_debito TEXT NOT NULL DEFAULT '0.0',
                    saldo_final TEXT NOT NULL DEFAULT '0.0',
                    updated_at TEXT NULL,
                    CONSTRAINT FK_saldo_contas_correntes_ContasCorrentes FOREIGN KEY (contauuid) REFERENCES ContasCorrentes (contauuid) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX idx_saldo_conta_data ON saldo_contas_correntes (contauuid, data);

                CREATE TABLE lancamentos_contas (
                    lancamentouuid TEXT NOT NULL CONSTRAINT PK_lancamentos_contas PRIMARY KEY,
                    usuariouuid TEXT NOT NULL,
                    contauuid TEXT NOT NULL,
                    categoriauuid TEXT NOT NULL,
                    entidadeuuid TEXT NULL,
                    data_lancamento TEXT NOT NULL,
                    descricao TEXT NOT NULL,
                    tipo TEXT NOT NULL,
                    valor TEXT NOT NULL,
                    observacao TEXT NULL,
                    conciliado INTEGER NOT NULL DEFAULT 1,
                    data_conciliacao TEXT NULL,
                    criado_em TEXT NULL,
                    updated_at TEXT NULL,
                    CONSTRAINT FK_lancamentos_contas_ContasCorrentes FOREIGN KEY (contauuid) REFERENCES ContasCorrentes (contauuid) ON DELETE CASCADE,
                    CONSTRAINT FK_lancamentos_contas_Categorias FOREIGN KEY (categoriauuid) REFERENCES Categorias (categoriauuid) ON DELETE RESTRICT,
                    CONSTRAINT FK_lancamentos_contas_entidades FOREIGN KEY (entidadeuuid) REFERENCES entidades (entidadeuuid) ON DELETE SET NULL
                );

                CREATE INDEX idx_lancamentos_conta_data ON lancamentos_contas (contauuid, data_lancamento);
            ");
        }

        /// <summary>
        /// Obtém o saldo acumulado imediatamente anterior à data informada (data menor que dataReferencia.Date).
        /// Se contaUuid for vazio/nulo, soma o saldo anterior de todas as contas cadastradas.
        /// </summary>
        public static decimal ObterSaldoAnterior(string? contaUuid, DateTime dataReferencia)
        {
            using var db = new AppDbContext();
            DateTime dataCorte = dataReferencia.Date;

            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                return ObterSaldoAnteriorContaUnica(db, contaUuid, dataCorte);
            }

            var todasContas = db.ContasCorrentes.AsNoTracking().Select(c => c.ContaUuid).ToList();
            decimal somaSaldos = 0m;
            foreach (var id in todasContas)
            {
                somaSaldos += ObterSaldoAnteriorContaUnica(db, id, dataCorte);
            }
            return somaSaldos;
        }

        private static decimal ObterSaldoAnteriorContaUnica(AppDbContext db, string contaUuid, DateTime dataCorte)
        {
            var ultimoSaldo = db.SaldosContasCorrentes
                .AsNoTracking()
                .Where(s => s.ContaUuid == contaUuid && s.Data < dataCorte)
                .OrderByDescending(s => s.Data)
                .FirstOrDefault();

            if (ultimoSaldo != null)
            {
                return ultimoSaldo.SaldoFinal;
            }

            var conta = db.ContasCorrentes.AsNoTracking().FirstOrDefault(c => c.ContaUuid == contaUuid);
            decimal saldoBase = conta?.SaldoInicial ?? 0m;

            var lancamentosAnteriores = db.LancamentosConta
                .AsNoTracking()
                .Where(l => l.ContaUuid == contaUuid && l.DataLancamento < dataCorte)
                .ToList();

            foreach (var l in lancamentosAnteriores)
            {
                decimal val = Math.Abs(l.Valor);
                saldoBase += l.Tipo == "C" ? val : -val;
            }

            return saldoBase;
        }

        /// <summary>
        /// Obtém o saldo para a tela de Inclusão: considera o saldo anterior à data + lançamentos já feitos na própria data,
        /// permitindo que a cada inclusão o Saldo Atual passe imediatamente a ser o novo Saldo Anterior.
        /// </summary>
        public static decimal ObterSaldoParaNovoLancamento(string contaUuid, DateTime dataLancamento)
        {
            using var db = new AppDbContext();
            DateTime dataAlvo = dataLancamento.Date;

            var saldoNoDia = db.SaldosContasCorrentes
                .AsNoTracking()
                .FirstOrDefault(s => s.ContaUuid == contaUuid && s.Data == dataAlvo);

            if (saldoNoDia != null)
            {
                return saldoNoDia.SaldoFinal;
            }

            return ObterSaldoAnteriorContaUnica(db, contaUuid, dataAlvo);
        }

        /// <summary>
        /// Lista os lançamentos do período, calcula o Saldo Anterior, o Saldo linha a linha e o Saldo Final.
        /// </summary>
        public static ResultadoExtratoPeriodo ListarLancamentosDoPeriodo(string? contaUuid, DateTime dataInicio, DateTime dataFim)
        {
            using var db = new AppDbContext();
            DateTime dtIni = dataInicio.Date;
            DateTime dtFim = dataFim.Date.AddDays(1).AddTicks(-1);

            decimal saldoAnterior = ObterSaldoAnterior(contaUuid, dtIni);

            var query = db.LancamentosConta
                .AsNoTracking()
                .Include(l => l.ContaCorrente)
                .Include(l => l.Categoria)
                .Include(l => l.Entidade)
                .Where(l => l.DataLancamento >= dtIni && l.DataLancamento <= dtFim);

            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                query = query.Where(l => l.ContaUuid == contaUuid);
            }

            var lista = query
                .OrderBy(l => l.DataLancamento)
                .ThenBy(l => l.CriadoEm)
                .ToList();

            decimal saldoCorrente = saldoAnterior;
            foreach (var item in lista)
            {
                decimal valorAbs = Math.Abs(item.Valor);
                saldoCorrente += item.Tipo == "C" ? valorAbs : -valorAbs;
                item.SaldoAposLancamento = saldoCorrente;
            }

            return new ResultadoExtratoPeriodo
            {
                SaldoAnterior = saldoAnterior,
                SaldoFinal = saldoCorrente,
                Lancamentos = lista
            };
        }

        /// <summary>
        /// Insere um lançamento em lancamentos_contas e recalcula a tabela saldo_contas_correntes da data do lançamento para frente.
        /// </summary>
        public static void IncluirLancamento(LancamentoConta novoLancamento)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            novoLancamento.LancamentoUuid = string.IsNullOrWhiteSpace(novoLancamento.LancamentoUuid)
                ? Guid.NewGuid().ToString()
                : novoLancamento.LancamentoUuid;

            novoLancamento.DataLancamento = novoLancamento.DataLancamento.Date;
            novoLancamento.Valor = Math.Abs(novoLancamento.Valor);
            novoLancamento.CriadoEm = DateTime.UtcNow;
            novoLancamento.UpdatedAt = DateTime.UtcNow;

            db.LancamentosConta.Add(novoLancamento);
            db.SaveChanges();

            RecalcularSaldosInterno(db, novoLancamento.ContaUuid, novoLancamento.DataLancamento);

            transaction.Commit();
        }

        /// <summary>
        /// Atualiza um lançamento existente e recalcula os saldos da conta antiga e da nova conta (caso alteradas).
        /// </summary>
        public static void AtualizarLancamento(LancamentoConta lancamentoAtualizado)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var original = db.LancamentosConta
                .AsNoTracking()
                .FirstOrDefault(l => l.LancamentoUuid == lancamentoAtualizado.LancamentoUuid);

            if (original == null)
            {
                throw new InvalidOperationException("Lancamento original nao encontrado para edicao.");
            }

            string contaAntiga = original.ContaUuid;
            DateTime dataAntiga = original.DataLancamento.Date;

            lancamentoAtualizado.DataLancamento = lancamentoAtualizado.DataLancamento.Date;
            lancamentoAtualizado.Valor = Math.Abs(lancamentoAtualizado.Valor);
            lancamentoAtualizado.CriadoEm = original.CriadoEm ?? DateTime.UtcNow;
            lancamentoAtualizado.UpdatedAt = DateTime.UtcNow;

            db.LancamentosConta.Update(lancamentoAtualizado);
            db.SaveChanges();

            if (string.Equals(contaAntiga, lancamentoAtualizado.ContaUuid, StringComparison.OrdinalIgnoreCase))
            {
                DateTime menorData = dataAntiga < lancamentoAtualizado.DataLancamento
                    ? dataAntiga
                    : lancamentoAtualizado.DataLancamento;

                var saldosLimpar = db.SaldosContasCorrentes
                    .Where(s => s.ContaUuid == lancamentoAtualizado.ContaUuid && s.Data >= menorData)
                    .ToList();

                if (saldosLimpar.Count > 0)
                {
                    db.SaldosContasCorrentes.RemoveRange(saldosLimpar);
                    db.SaveChanges();
                }

                RecalcularSaldosInterno(db, lancamentoAtualizado.ContaUuid, menorData);
            }
            else
            {
                RecalcularSaldosInterno(db, contaAntiga, dataAntiga);
                RecalcularSaldosInterno(db, lancamentoAtualizado.ContaUuid, lancamentoAtualizado.DataLancamento);
            }

            transaction.Commit();
        }

        /// <summary>
        /// Exclui um lançamento e recalcula a tabela saldo_contas_correntes da data do lançamento excluído para frente.
        /// </summary>
        public static void ExcluirLancamento(string lancamentoUuid)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var existente = db.LancamentosConta.Find(lancamentoUuid);
            if (existente != null)
            {
                string contaUuid = existente.ContaUuid;
                DateTime dataAfetada = existente.DataLancamento.Date;

                db.LancamentosConta.Remove(existente);
                db.SaveChanges();

                RecalcularSaldosInterno(db, contaUuid, dataAfetada);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Recalcula todos os dias e saldos na tabela saldo_contas_correntes a partir da data informada para frente.
        /// </summary>
        public static void RecalcularTabelaSaldos(string contaUuid, DateTime dataInicial)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            RecalcularSaldosInterno(db, contaUuid, dataInicial.Date);
            transaction.Commit();
        }

        private static void RecalcularSaldosInterno(AppDbContext db, string contaUuid, DateTime dataAfetada)
        {
            DateTime dataCorte = dataAfetada.Date;

            var saldoDiaAnterior = db.SaldosContasCorrentes
                .AsNoTracking()
                .Where(s => s.ContaUuid == contaUuid && s.Data < dataCorte)
                .OrderByDescending(s => s.Data)
                .FirstOrDefault();

            decimal saldoAcumulado;
            DateTime dataInicioRecalculo;

            if (saldoDiaAnterior != null)
            {
                saldoAcumulado = saldoDiaAnterior.SaldoFinal;
                dataInicioRecalculo = dataCorte;
            }
            else
            {
                var conta = db.ContasCorrentes.AsNoTracking().FirstOrDefault(c => c.ContaUuid == contaUuid);
                saldoAcumulado = conta?.SaldoInicial ?? 0m;

                var primeiroLancamento = db.LancamentosConta
                    .AsNoTracking()
                    .Where(l => l.ContaUuid == contaUuid)
                    .OrderBy(l => l.DataLancamento)
                    .FirstOrDefault();

                dataInicioRecalculo = primeiroLancamento != null && primeiroLancamento.DataLancamento.Date < dataCorte
                    ? primeiroLancamento.DataLancamento.Date
                    : dataCorte;
            }

            var saldosParaAtualizar = db.SaldosContasCorrentes
                .Where(s => s.ContaUuid == contaUuid && s.Data >= dataInicioRecalculo)
                .ToList();

            if (saldosParaAtualizar.Count > 0)
            {
                db.SaldosContasCorrentes.RemoveRange(saldosParaAtualizar);
                db.SaveChanges();
            }

            var lancamentosFuturos = db.LancamentosConta
                .AsNoTracking()
                .Where(l => l.ContaUuid == contaUuid && l.DataLancamento >= dataInicioRecalculo)
                .OrderBy(l => l.DataLancamento)
                .ToList();

            var gruposPorData = lancamentosFuturos
                .GroupBy(l => l.DataLancamento.Date)
                .OrderBy(g => g.Key);

            foreach (var grupo in gruposPorData)
            {
                decimal saldoInicialDia = saldoAcumulado;
                decimal totalCredito = grupo.Where(l => l.Tipo == "C").Sum(l => Math.Abs(l.Valor));
                decimal totalDebito = grupo.Where(l => l.Tipo == "D").Sum(l => Math.Abs(l.Valor));
                decimal saldoFinalDia = saldoInicialDia + totalCredito - totalDebito;

                db.SaldosContasCorrentes.Add(new SaldoContaCorrente
                {
                    SaldoId = Guid.NewGuid().ToString(),
                    ContaUuid = contaUuid,
                    Data = grupo.Key,
                    SaldoInicial = saldoInicialDia,
                    TotalCredito = totalCredito,
                    TotalDebito = totalDebito,
                    SaldoFinal = saldoFinalDia,
                    UpdatedAt = DateTime.UtcNow
                });

                saldoAcumulado = saldoFinalDia;
            }

            db.SaveChanges();
        }
    }
}