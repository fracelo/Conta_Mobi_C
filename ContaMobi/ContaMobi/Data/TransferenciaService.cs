using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;

namespace ContaMobi.Data
{
    public static class TransferenciaService
    {
        // UUID fixo da categoria interna de sistema (não editável e fora de relatórios/orçamento)
        public const string CategoriaTransferenciaUuid = "00000000-0000-0000-0000-000000000001";

        public static void GarantirEstruturaTransferencia(string? usuarioUuid = null)
        {
            using var db = new AppDbContext();
            var colunasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var conn = db.Database.GetDbConnection();
            bool abriuAqui = conn.State != ConnectionState.Open;
            if (abriuAqui) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('lancamentos_contas');";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? nome = reader["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(nome))
                        colunasExistentes.Add(nome.Trim());
                }
            }
            finally
            {
                if (abriuAqui) conn.Close();
            }

            if (colunasExistentes.Count > 0 && !colunasExistentes.Contains("transferencia_uuid"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE lancamentos_contas ADD COLUMN transferencia_uuid TEXT NULL;");
            }

            // Garante a categoria interna "TRANSFERÊNCIA" para satisfazer a FK de categoriauuid
            bool existeCat = db.Categorias
                .AsNoTracking()
                .Any(c => c.CategoriaUuid == CategoriaTransferenciaUuid);

            if (!existeCat)
            {
                db.Categorias.Add(new Categoria
                {
                    CategoriaUuid = CategoriaTransferenciaUuid,
                    UsuarioUuid = usuarioUuid ?? string.Empty,
                    PaiUuid = null,
                    Descricao = "TRANSFERÊNCIA",
                    Tipo = "T",
                    Nivel = 0,
                    AceitaLancamento = false,
                    CriadoEm = DateTime.UtcNow,
                    AtualizadoEm = DateTime.UtcNow
                });
                db.SaveChanges();
            }
        }

        public static List<ContaCorrente> ObterContasCorrentes()
        {
            using var db = new AppDbContext();
            return db.ContasCorrentes
                .AsNoTracking()
                .Include(c => c.Banco)
                .OrderBy(c => c.Descricao)
                .ToList();
        }

        public static void RealizarTransferencia(
            string contaOrigemUuid,
            string contaDestinoUuid,
            DateTime dataTransferencia,
            decimal valor,
            string? observacao,
            bool conciliado,
            string usuarioUuid)
        {
            if (string.IsNullOrWhiteSpace(contaOrigemUuid) || string.IsNullOrWhiteSpace(contaDestinoUuid))
                throw new InvalidOperationException("Selecione a conta de origem e a conta de destino.");

            if (string.Equals(contaOrigemUuid, contaDestinoUuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A conta de origem e a conta de destino não podem ser iguais.");

            if (valor <= 0m)
                throw new InvalidOperationException("O valor da transferência deve ser maior que zero.");

            GarantirEstruturaTransferencia(usuarioUuid);

            using var db = new AppDbContext();
            using var trans = db.Database.BeginTransaction();

            try
            {
                var contaOrigem = db.ContasCorrentes.AsNoTracking().FirstOrDefault(c => c.ContaUuid == contaOrigemUuid)
                    ?? throw new InvalidOperationException("Conta de origem não encontrada.");

                var contaDestino = db.ContasCorrentes.AsNoTracking().FirstOrDefault(c => c.ContaUuid == contaDestinoUuid)
                    ?? throw new InvalidOperationException("Conta de destino não encontrada.");

                string transferenciaUuid = Guid.NewGuid().ToString();
                string obsLimpa = (observacao ?? string.Empty).Trim();
                DateTime dataMov = dataTransferencia.Date;
                DateTime agoraUtc = DateTime.UtcNow;
                DateTime? dataConciliacao = conciliado ? dataMov : null;

                string descSaida = $"Transferência para {contaDestino.Descricao}";
                if (descSaida.Length > 150) descSaida = descSaida.Substring(0, 150);

                string descEntrada = $"Transferência de {contaOrigem.Descricao}";
                if (descEntrada.Length > 150) descEntrada = descEntrada.Substring(0, 150);

                // 1. Saída (Débito) na Conta de Origem
                var lancSaida = new LancamentoConta
                {
                    LancamentoUuid = Guid.NewGuid().ToString(),
                    UsuarioUuid = usuarioUuid,
                    ContaUuid = contaOrigemUuid,
                    CategoriaUuid = CategoriaTransferenciaUuid,
                    EntidadeUuid = null,
                    DataLancamento = dataMov,
                    Descricao = descSaida,
                    Tipo = "D",
                    Valor = Math.Abs(valor),
                    Observacao = string.IsNullOrEmpty(obsLimpa) ? "Transferência interna entre contas" : obsLimpa,
                    Conciliado = conciliado,
                    DataConciliacao = dataConciliacao,
                    TransferenciaUuid = transferenciaUuid,
                    CriadoEm = agoraUtc,
                    UpdatedAt = agoraUtc
                };

                // 2. Entrada (Crédito) na Conta de Destino
                var lancEntrada = new LancamentoConta
                {
                    LancamentoUuid = Guid.NewGuid().ToString(),
                    UsuarioUuid = usuarioUuid,
                    ContaUuid = contaDestinoUuid,
                    CategoriaUuid = CategoriaTransferenciaUuid,
                    EntidadeUuid = null,
                    DataLancamento = dataMov,
                    Descricao = descEntrada,
                    Tipo = "C",
                    Valor = Math.Abs(valor),
                    Observacao = string.IsNullOrEmpty(obsLimpa) ? "Transferência interna entre contas" : obsLimpa,
                    Conciliado = conciliado,
                    DataConciliacao = dataConciliacao,
                    TransferenciaUuid = transferenciaUuid,
                    CriadoEm = agoraUtc,
                    UpdatedAt = agoraUtc
                };

                db.Set<LancamentoConta>().Add(lancSaida);
                db.Set<LancamentoConta>().Add(lancEntrada);
                db.SaveChanges();

                // 3. Atualiza os saldos diários de ambas as contas a partir da data da transferência
                RecalcularSaldosDiariosConta(db, contaOrigemUuid, dataMov);
                RecalcularSaldosDiariosConta(db, contaDestinoUuid, dataMov);
                db.SaveChanges();

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Exclui os dois lançamentos (origem e destino) vinculados à mesma transferência e recalcula os saldos.
        /// </summary>
        public static void ExcluirTransferencia(string transferenciaUuid)
        {
            if (string.IsNullOrWhiteSpace(transferenciaUuid)) return;

            using var db = new AppDbContext();
            using var trans = db.Database.BeginTransaction();

            try
            {
                var pares = db.Set<LancamentoConta>()
                    .Where(l => l.TransferenciaUuid == transferenciaUuid)
                    .ToList();

                if (pares.Count == 0)
                    return;

                var contasAfetadas = pares
                    .GroupBy(l => l.ContaUuid, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { ContaUuid = g.Key, DataMinima = g.Min(x => x.DataLancamento.Date) })
                    .ToList();

                db.Set<LancamentoConta>().RemoveRange(pares);
                db.SaveChanges();

                foreach (var afetada in contasAfetadas)
                {
                    RecalcularSaldosDiariosConta(db, afetada.ContaUuid, afetada.DataMinima);
                }

                db.SaveChanges();
                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Recalcula a tabela saldo_contas_correntes para uma conta a partir de dataInicio em diante.
        /// </summary>
        public static void RecalcularSaldosDiariosConta(AppDbContext db, string contaUuid, DateTime dataInicio)
        {
            if (string.IsNullOrWhiteSpace(contaUuid))
                return;

            DateTime dtIni = dataInicio.Date;

            var conta = db.ContasCorrentes
                .AsNoTracking()
                .FirstOrDefault(c => c.ContaUuid == contaUuid);

            if (conta == null)
                return;

            // Determina o saldo de abertura imediatamente anterior a dtIni
            var saldoAnteriorRegistro = db.Set<SaldoContaCorrente>()
                .AsNoTracking()
                .Where(s => s.ContaUuid == contaUuid && s.Data < dtIni)
                .OrderByDescending(s => s.Data)
                .FirstOrDefault();

            decimal saldoAnterior;
            if (saldoAnteriorRegistro != null)
            {
                saldoAnterior = saldoAnteriorRegistro.SaldoFinal;
            }
            else
            {
                var lancamentosAnteriores = db.Set<LancamentoConta>()
                    .AsNoTracking()
                    .Where(l => l.ContaUuid == contaUuid && l.DataLancamento < dtIni)
                    .ToList();

                decimal credAnt = lancamentosAnteriores.Where(l => l.Tipo == "C").Sum(l => Math.Abs(l.Valor));
                decimal debAnt = lancamentosAnteriores.Where(l => l.Tipo == "D").Sum(l => Math.Abs(l.Valor));
                saldoAnterior = conta.SaldoInicial + credAnt - debAnt;
            }

            // Carrega movimentações e registros de saldo de dtIni em diante
            var lancamentosPeriodo = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Where(l => l.ContaUuid == contaUuid && l.DataLancamento >= dtIni)
                .ToList();

            var lancamentosPorDia = lancamentosPeriodo
                .GroupBy(l => l.DataLancamento.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var saldosExistentes = db.Set<SaldoContaCorrente>()
                .Where(s => s.ContaUuid == contaUuid && s.Data >= dtIni)
                .ToList();

            var saldosPorDia = saldosExistentes
                .GroupBy(s => s.Data.Date)
                .ToDictionary(g => g.Key, g => g.First());

            var todasDatas = lancamentosPorDia.Keys
                .Union(saldosPorDia.Keys)
                .OrderBy(d => d)
                .ToList();

            DateTime agoraUtc = DateTime.UtcNow;

            foreach (var dia in todasDatas)
            {
                bool temMovimento = lancamentosPorDia.TryGetValue(dia, out var movsDia) && movsDia.Count > 0;
                saldosPorDia.TryGetValue(dia, out var registroSaldo);

                if (!temMovimento)
                {
                    // Se o dia ficou sem nenhum lançamento após uma exclusão, remove o registro vazio
                    if (registroSaldo != null)
                    {
                        db.Set<SaldoContaCorrente>().Remove(registroSaldo);
                    }
                    continue;
                }

                decimal totalCredito = movsDia!.Where(l => l.Tipo == "C").Sum(l => Math.Abs(l.Valor));
                decimal totalDebito = movsDia!.Where(l => l.Tipo == "D").Sum(l => Math.Abs(l.Valor));
                decimal saldoFinal = saldoAnterior + totalCredito - totalDebito;

                if (registroSaldo != null)
                {
                    registroSaldo.SaldoInicial = saldoAnterior;
                    registroSaldo.TotalCredito = totalCredito;
                    registroSaldo.TotalDebito = totalDebito;
                    registroSaldo.SaldoFinal = saldoFinal;
                    registroSaldo.UpdatedAt = agoraUtc;
                }
                else
                {
                    db.Set<SaldoContaCorrente>().Add(new SaldoContaCorrente
                    {
                        SaldoId = Guid.NewGuid().ToString(),
                        ContaUuid = contaUuid,
                        Data = dia,
                        SaldoInicial = saldoAnterior,
                        TotalCredito = totalCredito,
                        TotalDebito = totalDebito,
                        SaldoFinal = saldoFinal,
                        UpdatedAt = agoraUtc
                    });
                }

                saldoAnterior = saldoFinal;
            }
        }
    }
}