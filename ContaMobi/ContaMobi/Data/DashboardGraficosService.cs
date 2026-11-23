using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContaMobi.Data
{
    public static class DashboardGraficosService
    {
        /// <summary>
        /// Agrupa lançamentos em conta corrente (Entradas = "C" ou Saídas = "D") por categoria no período.
        /// </summary>
        public static List<(string Categoria, decimal Valor)> ObterMovimentacaoPorCategoria(
            string tipo, DateTime dataInicio, DateTime dataFim)
        {
            TransferenciaService.GarantirEstruturaTransferencia();
            using var db = new AppDbContext();

            DateTime dtIni = dataInicio.Date;
            DateTime dtFim = dataFim.Date.AddDays(1).AddTicks(-1);

            var lancamentos = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Include(l => l.Categoria)
                .Where(l => l.DataLancamento >= dtIni &&
                            l.DataLancamento <= dtFim &&
                            l.Tipo == tipo &&
                            !string.IsNullOrEmpty(l.CategoriaUuid) &&
                            l.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid &&
                            string.IsNullOrEmpty(l.TransferenciaUuid))
                .ToList();

            return lancamentos
                .GroupBy(l => l.Categoria?.Descricao ?? "Sem Categoria")
                .Select(g => (Categoria: g.Key, Valor: g.Sum(x => Math.Abs(x.Valor))))
                .Where(x => x.Valor > 0m)
                .OrderByDescending(x => x.Valor)
                .ToList();
        }

        /// <summary>
        /// Soma o Orçamento Planejado do ano corrente filtrado por tipo ("C" = Receitas, "D" = Despesas).
        /// </summary>
        public static List<(string Categoria, decimal Valor)> ObterOrcamentoPlanejadoAnoCorrente(string tipo)
        {
            OrcamentoService.GarantirTabelaCriada();
            using var db = new AppDbContext();

            int anoAtual = DateTime.Today.Year;
            string tipoAlvo = OrcamentoService.NormalizarTipoCategoria(tipo);

            var categoriasMap = db.Categorias
                .AsNoTracking()
                .ToList()
                .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var orcamentosAno = db.Set<Orcamento>()
                .AsNoTracking()
                .Where(o => o.Ano == anoAtual &&
                            o.ValorPlanejado > 0m &&
                            o.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid)
                .ToList();

            return orcamentosAno
                .Where(o =>
                {
                    string catId = o.CategoriaUuid.Trim();
                    return categoriasMap.TryGetValue(catId, out var cat) &&
                           OrcamentoService.NormalizarTipoCategoria(cat.Tipo) == tipoAlvo;
                })
                .GroupBy(o => o.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => (
                    Categoria: categoriasMap.TryGetValue(g.Key, out var cat) ? cat.Descricao : "Sem Categoria",
                    Valor: g.Sum(x => Math.Abs(x.ValorPlanejado))))
                .Where(x => x.Valor > 0m)
                .OrderByDescending(x => x.Valor)
                .ToList();
        }

        /// <summary>
        /// Soma o Orçamento Realizado do ano corrente (lançamentos em conta corrente) filtrado por tipo ("C" = Receitas, "D" = Despesas).
        /// </summary>
        public static List<(string Categoria, decimal Valor)> ObterOrcamentoRealizadoAnoCorrente(string tipo)
        {
            TransferenciaService.GarantirEstruturaTransferencia();
            using var db = new AppDbContext();

            int anoAtual = DateTime.Today.Year;
            DateTime inicioAno = new DateTime(anoAtual, 1, 1);
            DateTime fimAno = new DateTime(anoAtual, 12, 31, 23, 59, 59);

            var lancamentosAno = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Include(l => l.Categoria)
                .Where(l => l.DataLancamento >= inicioAno &&
                            l.DataLancamento <= fimAno &&
                            l.Tipo == tipo &&
                            !string.IsNullOrEmpty(l.CategoriaUuid) &&
                            l.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid &&
                            string.IsNullOrEmpty(l.TransferenciaUuid))
                .ToList();

            return lancamentosAno
                .GroupBy(l => l.Categoria?.Descricao ?? "Sem Categoria")
                .Select(g => (Categoria: g.Key, Valor: g.Sum(x => Math.Abs(x.Valor))))
                .Where(x => x.Valor > 0m)
                .OrderByDescending(x => x.Valor)
                .ToList();
        }
    }
}