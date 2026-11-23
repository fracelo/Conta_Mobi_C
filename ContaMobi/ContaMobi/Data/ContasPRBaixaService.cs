using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;

namespace ContaMobi.Data
{
    public static class ContasPRBaixaService
    {
        public static void GarantirColunasBaixa()
        {
            using var db = new AppDbContext();
            var colunasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var conn = db.Database.GetDbConnection();
            bool abriuAqui = conn.State != ConnectionState.Open;
            if (abriuAqui) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('contas_pr');";
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

            if (colunasExistentes.Count == 0)
                return;

            if (!colunasExistentes.Contains("acrescimo"))
                db.Database.ExecuteSqlRaw("ALTER TABLE contas_pr ADD COLUMN acrescimo NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");

            if (!colunasExistentes.Contains("desconto"))
                db.Database.ExecuteSqlRaw("ALTER TABLE contas_pr ADD COLUMN desconto NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");

            if (!colunasExistentes.Contains("lancamento_baixa_uuid"))
                db.Database.ExecuteSqlRaw("ALTER TABLE contas_pr ADD COLUMN lancamento_baixa_uuid TEXT NULL;");
        }

        public static ContaPR? ObterContaCompleta(string contasPRUuid)
        {
            GarantirColunasBaixa();
            using var db = new AppDbContext();
            return db.Set<ContaPR>()
                .AsNoTracking()
                .Include(c => c.ContaCorrente)
                .Include(c => c.Categoria)
                .Include(c => c.Entidade)
                .FirstOrDefault(c => c.ContasPRUuid == contasPRUuid);
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

        public static void EfetuarBaixa(
            string contasPRUuid,
            string contaCorrenteUuid,
            DateTime dataBaixa,
            decimal juro,
            decimal multa,
            decimal acrescimo,
            decimal desconto,
            decimal valorPago,
            bool gerarContaSobra,
            decimal valorSobra,
            DateTime? dataVencimentoSobra,
            string usuarioUuid,
            string? categoriaUuid = null)
        {
            GarantirColunasBaixa();
            TransferenciaService.GarantirEstruturaTransferencia(usuarioUuid);

            using var db = new AppDbContext();
            using var trans = db.Database.BeginTransaction();

            try
            {
                var conta = db.Set<ContaPR>().FirstOrDefault(c => c.ContasPRUuid == contasPRUuid);
                if (conta == null)
                    throw new InvalidOperationException("Conta não encontrada no banco de dados.");

                string tipoLancamento = conta.Tipo == "R" ? "C" : "D";
                bool estavaQuitado = conta.EstaQuitado;
                string? contaAntigaUuid = null;
                DateTime? dataAntiga = null;

                // 1. Se a parcela já estava baixada, remove o lançamento antigo da conta corrente
                if (estavaQuitado)
                {
                    LancamentoConta? lancamentoAntigo = null;

                    if (!string.IsNullOrWhiteSpace(conta.LancamentoBaixaUuid))
                    {
                        lancamentoAntigo = db.Set<LancamentoConta>()
                            .FirstOrDefault(l => l.LancamentoUuid == conta.LancamentoBaixaUuid);
                    }

                    // Fallback para baixas efetuadas antes da coluna lancamento_baixa_uuid existir
                    if (lancamentoAntigo == null && conta.DataPagamento.HasValue && !string.IsNullOrWhiteSpace(conta.ContaUuid))
                    {
                        DateTime dtAntigaBusca = conta.DataPagamento.Value.Date;
                        decimal valAntigo = Math.Abs(conta.ValorPago);

                        lancamentoAntigo = db.Set<LancamentoConta>()
                            .Where(l => l.ContaUuid == conta.ContaUuid &&
                                        l.DataLancamento == dtAntigaBusca &&
                                        l.Tipo == tipoLancamento &&
                                        l.CategoriaUuid == conta.CategoriaUuid &&
                                        l.Valor == valAntigo)
                            .OrderByDescending(l => l.CriadoEm)
                            .FirstOrDefault();
                    }

                    if (lancamentoAntigo != null)
                    {
                        contaAntigaUuid = lancamentoAntigo.ContaUuid;
                        dataAntiga = lancamentoAntigo.DataLancamento.Date;
                        db.Set<LancamentoConta>().Remove(lancamentoAntigo);
                    }
                    else
                    {
                        contaAntigaUuid = conta.ContaUuid;
                        dataAntiga = conta.DataPagamento?.Date;
                    }
                }

                string userEfetivo = !string.IsNullOrWhiteSpace(usuarioUuid)
                    ? usuarioUuid
                    : conta.UsuarioUuid;

                string categoriaEfetiva = !string.IsNullOrWhiteSpace(categoriaUuid)
                    ? categoriaUuid
                    : conta.CategoriaUuid;

                DateTime dataMov = dataBaixa.Date;

                // 2. Gera o novo lançamento na Conta Corrente selecionada
                string descLancamento = $"{conta.Descricao} ({conta.NumeroParcela:D2}/{conta.TotalParcelas:D2})";
                if (descLancamento.Length > 150)
                    descLancamento = descLancamento.Substring(0, 150);

                var novoLancamento = new LancamentoConta
                {
                    LancamentoUuid = Guid.NewGuid().ToString(),
                    UsuarioUuid = userEfetivo,
                    ContaUuid = contaCorrenteUuid,
                    CategoriaUuid = categoriaEfetiva,
                    EntidadeUuid = conta.EntidadeUuid,
                    DataLancamento = dataMov,
                    Descricao = descLancamento,
                    Tipo = tipoLancamento,
                    Valor = Math.Abs(valorPago),
                    Observacao = $"Baixa automática Contas {(conta.Tipo == "P" ? "a Pagar" : "a Receber")} - Parc. {conta.NumeroParcela:D2}/{conta.TotalParcelas:D2}",
                    Conciliado = true,
                    DataConciliacao = dataMov,
                    CriadoEm = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Set<LancamentoConta>().Add(novoLancamento);

                // 3. Atualiza a parcela com os dados da baixa e vincula o UUID do novo lançamento
                conta.ContaUuid = contaCorrenteUuid;
                conta.CategoriaUuid = categoriaEfetiva;
                conta.Juro = juro;
                conta.Multa = multa;
                conta.Acrescimo = acrescimo;
                conta.Desconto = desconto;
                conta.ValorPago = valorPago;
                conta.DataPagamento = dataMov;
                conta.LancamentoBaixaUuid = novoLancamento.LancamentoUuid;
                conta.UpdatedAt = DateTime.UtcNow;

                // 4. Se for pagamento parcial, cria réplica da parcela com o valor da diferença
                if (gerarContaSobra && valorSobra > 0m)
                {
                    string descricaoSobra = $"{conta.Descricao} - SOBRA DA CONTA ORIGINAL (Parc. {conta.NumeroParcela:D2}/{conta.TotalParcelas:D2})";
                    if (descricaoSobra.Length > 150)
                        descricaoSobra = descricaoSobra.Substring(0, 150);

                    var novaContaSobra = new ContaPR
                    {
                        ContasPRUuid = Guid.NewGuid().ToString(),
                        LoteUuid = conta.LoteUuid,
                        Tipo = conta.Tipo,
                        UsuarioUuid = userEfetivo,
                        ContaUuid = contaCorrenteUuid,
                        CategoriaUuid = categoriaEfetiva,
                        EntidadeUuid = conta.EntidadeUuid,
                        Descricao = descricaoSobra,
                        DataLancamento = DateTime.Today,
                        DataVencimento = (dataVencimentoSobra ?? conta.DataVencimento).Date,
                        ValorBase = valorSobra,
                        ModoParcelamento = conta.ModoParcelamento,
                        NumeroParcela = conta.NumeroParcela,
                        TotalParcelas = conta.TotalParcelas,
                        IntervaloDias = conta.IntervaloDias,
                        Valor = valorSobra,
                        Multa = 0m,
                        Juro = 0m,
                        Acrescimo = 0m,
                        Desconto = 0m,
                        ValorPago = 0m,
                        DataPagamento = null,
                        LancamentoBaixaUuid = null,
                        CriadoEm = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    db.Set<ContaPR>().Add(novaContaSobra);
                }

                db.SaveChanges();

                // 5. Recalcula saldo_contas_correntes na conta atual (e na conta antiga, se houve troca na edição)
                if (estavaQuitado && !string.IsNullOrWhiteSpace(contaAntigaUuid) && dataAntiga.HasValue)
                {
                    if (string.Equals(contaAntigaUuid, contaCorrenteUuid, StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime dataMaisAntiga = dataAntiga.Value < dataMov ? dataAntiga.Value : dataMov;
                        TransferenciaService.RecalcularSaldosDiariosConta(db, contaCorrenteUuid, dataMaisAntiga);
                    }
                    else
                    {
                        TransferenciaService.RecalcularSaldosDiariosConta(db, contaAntigaUuid, dataAntiga.Value);
                        TransferenciaService.RecalcularSaldosDiariosConta(db, contaCorrenteUuid, dataMov);
                    }
                }
                else
                {
                    TransferenciaService.RecalcularSaldosDiariosConta(db, contaCorrenteUuid, dataMov);
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
    }
}