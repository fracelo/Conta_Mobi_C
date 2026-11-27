using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ContaMobi.Data
{
    public enum TipoAgrupamentoFluxo
    {
        Diario,
        Semanal,
        Mensal
    }

    public class FluxoCaixaLinha
    {
        private static readonly CultureInfo _cultura = new("pt-BR");

        public string Chave { get; set; } = string.Empty;
        public string PeriodoLabel { get; set; } = string.Empty;
        public string SubtituloPeriodo { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public bool EhLinhaAtrasados { get; set; }

        public decimal SaldoAnterior { get; set; }
        public decimal TotalReceber { get; set; }
        public decimal TotalPagar { get; set; }
        public decimal ResultadoPeriodo => TotalReceber - TotalPagar;
        public decimal SaldoFinal { get; set; }

        public int QtdReceber { get; set; }
        public int QtdPagar { get; set; }
        public List<ContaPR> Titulos { get; set; } = new();

        public bool PossuiMovimento => QtdReceber > 0 || QtdPagar > 0;

        public string PeriodoCor => EhLinhaAtrasados
            ? "#FFAA00"
            : (PossuiMovimento ? "#00FF66" : "#669977");

        public string SaldoAnteriorFormatado => SaldoAnterior.ToString("C2", _cultura);
        public string SaldoAnteriorCor => SaldoAnterior >= 0m ? "#88CC99" : "#FF5555";

        public string TotalReceberFormatado => TotalReceber > 0m ? $"+ {TotalReceber.ToString("C2", _cultura)}" : "R$ 0,00";
        public string TotalReceberCor => TotalReceber > 0m ? "#3399FF" : "#446655";

        public string TotalPagarFormatado => TotalPagar > 0m ? $"- {TotalPagar.ToString("C2", _cultura)}" : "R$ 0,00";
        public string TotalPagarCor => TotalPagar > 0m ? "#FF5555" : "#446655";

        public string ResultadoPeriodoFormatado
        {
            get
            {
                if (ResultadoPeriodo > 0m) return $"+ {ResultadoPeriodo.ToString("C2", _cultura)}";
                if (ResultadoPeriodo < 0m) return $"- {Math.Abs(ResultadoPeriodo).ToString("C2", _cultura)}";
                return "R$ 0,00";
            }
        }

        public string ResultadoPeriodoCor
        {
            get
            {
                if (ResultadoPeriodo > 0m) return "#00FF66";
                if (ResultadoPeriodo < 0m) return "#FF5555";
                return "#668877";
            }
        }

        public string SaldoFinalFormatado => SaldoFinal.ToString("C2", _cultura);
        public string SaldoFinalCor => SaldoFinal >= 0m ? "#00FF66" : "#FF5555";

        public string QtdResumo => (QtdReceber + QtdPagar) > 0
            ? $"{QtdReceber} a Receber / {QtdPagar} a Pagar"
            : "-";
    }

    public class ResultadoFluxoCaixa
    {
        public DateTime DataInicioEfetiva { get; set; }
        public DateTime DataFimEfetiva { get; set; }
        public decimal SaldoInicialContas { get; set; }
        public decimal TotalReceberAtrasado { get; set; }
        public decimal TotalReceberPeriodo { get; set; }
        public decimal TotalReceberGeral => TotalReceberAtrasado + TotalReceberPeriodo;

        public decimal TotalPagarAtrasado { get; set; }
        public decimal TotalPagarPeriodo { get; set; }
        public decimal TotalPagarGeral => TotalPagarAtrasado + TotalPagarPeriodo;

        public decimal SaldoFinalProjetado { get; set; }
        public List<FluxoCaixaLinha> Linhas { get; set; } = new();
    }

    public static class FluxoCaixaService
    {
        private static readonly CultureInfo _cultura = new("pt-BR");

        public static ResultadoFluxoCaixa GerarFluxoCaixa(
            DateTime dataInicio,
            DateTime dataFim,
            TipoAgrupamentoFluxo agrupamento,
            string? contaUuid = null,
            bool incluirAtrasadosEmAberto = true,
            bool ocultarSemMovimento = false)
        {
            ContasPRService.GarantirTabelasCriadas();
            ContasPRBaixaService.GarantirColunasBaixa();

            DateTime hoje = DateTime.Today;

            // Fluxo de caixa projetado inicia sempre a partir de hoje (nunca no passado)
            DateTime dtIni = dataInicio.Date < hoje ? hoje : dataInicio.Date;
            DateTime dtFim = dataFim.Date < dtIni ? dtIni : dataFim.Date;

            using var db = new AppDbContext();

            // 1. Calcula o Saldo Realizado das Contas Correntes até o dia atual
            var contasQuery = db.ContasCorrentes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                contasQuery = contasQuery.Where(c => c.ContaUuid == contaUuid);
            }

            decimal saldoInicialBase = contasQuery.ToList().Sum(c => c.SaldoInicial);

            DateTime hojeFim = hoje.AddDays(1).AddTicks(-1);
            var lancQuery = db.LancamentosConta
                .AsNoTracking()
                .Where(l => l.DataLancamento <= hojeFim);

            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                lancQuery = lancQuery.Where(l => l.ContaUuid == contaUuid);
            }

            var lancamentosAteHoje = lancQuery
                .Select(l => new { l.Tipo, l.Valor })
                .ToList();

            decimal totalCreditosConta = lancamentosAteHoje
                .Where(l => l.Tipo == "C")
                .Sum(l => Math.Abs(l.Valor));

            decimal totalDebitosConta = lancamentosAteHoje
                .Where(l => l.Tipo == "D")
                .Sum(l => Math.Abs(l.Valor));

            decimal saldoContas = saldoInicialBase + totalCreditosConta - totalDebitosConta;

            // 2. Busca todos os títulos EM ABERTO (A Pagar e A Receber) até a data final da projeção
            DateTime dtFimLimite = dtFim.AddDays(1).AddTicks(-1);

            var titulosQuery = db.ContasPR
                .AsNoTracking()
                .Include(c => c.ContaCorrente)
                .Include(c => c.Categoria)
                .Include(c => c.Entidade)
                .Where(c => c.DataPagamento == null &&
                            c.ValorPago <= 0m &&
                            c.DataVencimento <= dtFimLimite);

            if (!string.IsNullOrWhiteSpace(contaUuid))
            {
                titulosQuery = titulosQuery.Where(c => c.ContaUuid == contaUuid);
            }

            var todosTitulosAbertos = titulosQuery
                .OrderBy(c => c.DataVencimento)
                .ThenBy(c => c.NumeroParcela)
                .ThenBy(c => c.Descricao)
                .ToList();

            var anterioresAoInicio = todosTitulosAbertos
                .Where(c => c.DataVencimento.Date < dtIni)
                .ToList();

            var doPeriodo = todosTitulosAbertos
                .Where(c => c.DataVencimento.Date >= dtIni && c.DataVencimento.Date <= dtFim)
                .ToList();

            decimal receberAtrasado = incluirAtrasadosEmAberto
                ? anterioresAoInicio.Where(c => c.Tipo == "R").Sum(c => c.ValorTotalComEncargos)
                : 0m;

            decimal pagarAtrasado = incluirAtrasadosEmAberto
                ? anterioresAoInicio.Where(c => c.Tipo == "P").Sum(c => c.ValorTotalComEncargos)
                : 0m;

            decimal receberPeriodo = doPeriodo
                .Where(c => c.Tipo == "R")
                .Sum(c => c.ValorTotalComEncargos);

            decimal pagarPeriodo = doPeriodo
                .Where(c => c.Tipo == "P")
                .Sum(c => c.ValorTotalComEncargos);

            var linhas = new List<FluxoCaixaLinha>();
            decimal saldoCorrente = saldoContas;

            // 3. Linha inicial de Atrasados / Pendentes anteriores ao início da projeção
            if (incluirAtrasadosEmAberto && anterioresAoInicio.Count > 0)
            {
                decimal saldoAposAtrasados = saldoCorrente + receberAtrasado - pagarAtrasado;
                bool ehAtrasadoReal = dtIni == hoje;

                linhas.Add(new FluxoCaixaLinha
                {
                    Chave = "ATRASADOS",
                    PeriodoLabel = ehAtrasadoReal
                        ? $"ATRASADOS EM ABERTO (antes de {hoje:dd/MM/yyyy})"
                        : $"PENDENTES ANTERIORES (ate {dtIni.AddDays(-1):dd/MM/yyyy})",
                    SubtituloPeriodo = ehAtrasadoReal
                        ? "Títulos vencidos até ontem e ainda não baixados"
                        : "Títulos em aberto entre hoje e a data inicial selecionada",
                    DataInicio = DateTime.MinValue,
                    DataFim = dtIni.AddDays(-1),
                    EhLinhaAtrasados = true,
                    SaldoAnterior = saldoCorrente,
                    TotalReceber = receberAtrasado,
                    TotalPagar = pagarAtrasado,
                    SaldoFinal = saldoAposAtrasados,
                    QtdReceber = anterioresAoInicio.Count(c => c.Tipo == "R"),
                    QtdPagar = anterioresAoInicio.Count(c => c.Tipo == "P"),
                    Titulos = anterioresAoInicio
                });

                saldoCorrente = saldoAposAtrasados;
            }

            // 4. Gera os intervalos futuros a partir de dtIni até dtFim (Diário, Semanal ou Mensal)
            var intervalos = GerarIntervalos(dtIni, dtFim, agrupamento, hoje);

            foreach (var intervalo in intervalos)
            {
                var titulosIntervalo = doPeriodo
                    .Where(c => c.DataVencimento.Date >= intervalo.Inicio && c.DataVencimento.Date <= intervalo.Fim)
                    .ToList();

                decimal rec = titulosIntervalo
                    .Where(c => c.Tipo == "R")
                    .Sum(c => c.ValorTotalComEncargos);

                decimal pag = titulosIntervalo
                    .Where(c => c.Tipo == "P")
                    .Sum(c => c.ValorTotalComEncargos);

                decimal saldoFinalLinha = saldoCorrente + rec - pag;

                var linha = new FluxoCaixaLinha
                {
                    Chave = intervalo.Chave,
                    PeriodoLabel = intervalo.Label,
                    SubtituloPeriodo = intervalo.Subtitulo,
                    DataInicio = intervalo.Inicio,
                    DataFim = intervalo.Fim,
                    EhLinhaAtrasados = false,
                    SaldoAnterior = saldoCorrente,
                    TotalReceber = rec,
                    TotalPagar = pag,
                    SaldoFinal = saldoFinalLinha,
                    QtdReceber = titulosIntervalo.Count(c => c.Tipo == "R"),
                    QtdPagar = titulosIntervalo.Count(c => c.Tipo == "P"),
                    Titulos = titulosIntervalo
                };

                saldoCorrente = saldoFinalLinha;

                if (!ocultarSemMovimento || linha.PossuiMovimento)
                {
                    linhas.Add(linha);
                }
            }

            return new ResultadoFluxoCaixa
            {
                DataInicioEfetiva = dtIni,
                DataFimEfetiva = dtFim,
                SaldoInicialContas = saldoContas,
                TotalReceberAtrasado = receberAtrasado,
                TotalReceberPeriodo = receberPeriodo,
                TotalPagarAtrasado = pagarAtrasado,
                TotalPagarPeriodo = pagarPeriodo,
                SaldoFinalProjetado = saldoCorrente,
                Linhas = linhas
            };
        }

        private record IntervaloFluxo(string Chave, string Label, string Subtitulo, DateTime Inicio, DateTime Fim);

        private static List<IntervaloFluxo> GerarIntervalos(
            DateTime dtIni,
            DateTime dtFim,
            TipoAgrupamentoFluxo agrupamento,
            DateTime hoje)
        {
            var lista = new List<IntervaloFluxo>();

            if (agrupamento == TipoAgrupamentoFluxo.Diario)
            {
                for (DateTime dia = dtIni; dia <= dtFim; dia = dia.AddDays(1))
                {
                    string diaSemana = dia.ToString("ddd", _cultura).ToUpperInvariant().TrimEnd('.');
                    string tagHoje = dia == hoje ? " [HOJE]" : string.Empty;

                    lista.Add(new IntervaloFluxo(
                        Chave: dia.ToString("yyyy-MM-dd"),
                        Label: $"{dia:dd/MM/yyyy} ({diaSemana}){tagHoje}",
                        Subtitulo: dia.ToString("dddd", _cultura),
                        Inicio: dia,
                        Fim: dia));
                }
            }
            else if (agrupamento == TipoAgrupamentoFluxo.Semanal)
            {
                DateTime cursor = dtIni;
                int numSemana = 1;

                while (cursor <= dtFim)
                {
                    int diasAteSabado = ((int)DayOfWeek.Saturday - (int)cursor.DayOfWeek + 7) % 7;
                    DateTime fimSemana = cursor.AddDays(diasAteSabado);
                    if (fimSemana > dtFim)
                        fimSemana = dtFim;

                    lista.Add(new IntervaloFluxo(
                        Chave: $"SEM-{numSemana:D2}-{cursor:yyyyMMdd}",
                        Label: $"SEM {numSemana:D2} ({cursor:dd/MM/yyyy} a {fimSemana:dd/MM/yyyy})",
                        Subtitulo: $"{(fimSemana - cursor).Days + 1} dia(s)",
                        Inicio: cursor,
                        Fim: fimSemana));

                    cursor = fimSemana.AddDays(1);
                    numSemana++;
                }
            }
            else // Mensal
            {
                DateTime mesCursor = new DateTime(dtIni.Year, dtIni.Month, 1);
                DateTime mesLimite = new DateTime(dtFim.Year, dtFim.Month, 1);

                while (mesCursor <= mesLimite)
                {
                    DateTime inicioEfetivo = mesCursor < dtIni ? dtIni : mesCursor;
                    DateTime ultimoDiaMes = mesCursor.AddMonths(1).AddDays(-1);
                    DateTime fimEfetivo = ultimoDiaMes > dtFim ? dtFim : ultimoDiaMes;

                    string nomeMes = mesCursor.ToString("MMMM", _cultura).ToUpperInvariant();
                    lista.Add(new IntervaloFluxo(
                        Chave: $"{mesCursor:yyyy-MM}",
                        Label: $"{mesCursor:MM/yyyy} - {nomeMes}",
                        Subtitulo: $"De {inicioEfetivo:dd/MM} a {fimEfetivo:dd/MM}",
                        Inicio: inicioEfetivo,
                        Fim: fimEfetivo));

                    mesCursor = mesCursor.AddMonths(1);
                }
            }

            return lista;
        }
    }
}