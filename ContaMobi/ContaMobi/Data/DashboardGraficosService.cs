using ContaMobi.Controls;
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
        /// Agrupa lançamentos em conta corrente (Entradas = "C" ou Saídas = "D") por Categoria Nível 1,
        /// anexando os valores de Nível 2 para o gráfico de detalhamento ao clicar.
        /// </summary>
        public static List<CategoriaNivel1Grafico> ObterMovimentacaoPorCategoria(
            string tipo, DateTime dataInicio, DateTime dataFim)
        {
            TransferenciaService.GarantirEstruturaTransferencia();
            using var db = new AppDbContext();

            DateTime dtIni = dataInicio.Date;
            DateTime dtFim = dataFim.Date.AddDays(1).AddTicks(-1);

            var categoriasMap = CarregarMapaCategorias(db);

            var itensBrutos = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Where(l => l.DataLancamento >= dtIni &&
                            l.DataLancamento <= dtFim &&
                            l.Tipo == tipo &&
                            !string.IsNullOrEmpty(l.CategoriaUuid) &&
                            l.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid &&
                            string.IsNullOrEmpty(l.TransferenciaUuid))
                .Select(l => new { l.CategoriaUuid, l.Valor })
                .ToList()
                .Select(l => (CategoriaUuid: l.CategoriaUuid, Valor: Math.Abs(l.Valor)));

            return AgruparPorNivel1(categoriasMap, itensBrutos);
        }

        /// <summary>
        /// Soma o Orçamento Planejado do ano corrente filtrado por tipo ("C" = Receitas, "D" = Despesas),
        /// agrupado por Categoria Nível 1 com subcategorias de Nível 2.
        /// </summary>
        public static List<CategoriaNivel1Grafico> ObterOrcamentoPlanejadoAnoCorrente(string tipo)
        {
            OrcamentoService.GarantirTabelaCriada();
            using var db = new AppDbContext();

            int anoAtual = DateTime.Today.Year;
            string tipoAlvo = OrcamentoService.NormalizarTipoCategoria(tipo);

            var categoriasMap = CarregarMapaCategorias(db);

            var orcamentosAno = db.Set<Orcamento>()
                .AsNoTracking()
                .Where(o => o.Ano == anoAtual &&
                            o.ValorPlanejado > 0m &&
                            o.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid)
                .ToList();

            var itensFiltrados = orcamentosAno
                .Where(o =>
                {
                    string catId = o.CategoriaUuid.Trim();
                    return categoriasMap.TryGetValue(catId, out var cat) &&
                           OrcamentoService.NormalizarTipoCategoria(cat.Tipo) == tipoAlvo;
                })
                .Select(o => (CategoriaUuid: o.CategoriaUuid, Valor: Math.Abs(o.ValorPlanejado)));

            return AgruparPorNivel1(categoriasMap, itensFiltrados);
        }

        /// <summary>
        /// Soma o Orçamento Realizado do ano corrente (lançamentos em conta corrente) filtrado por tipo ("C" = Receitas, "D" = Despesas),
        /// agrupado por Categoria Nível 1 com subcategorias de Nível 2.
        /// </summary>
        public static List<CategoriaNivel1Grafico> ObterOrcamentoRealizadoAnoCorrente(string tipo)
        {
            TransferenciaService.GarantirEstruturaTransferencia();
            using var db = new AppDbContext();

            int anoAtual = DateTime.Today.Year;
            DateTime inicioAno = new DateTime(anoAtual, 1, 1);
            DateTime fimAno = new DateTime(anoAtual, 12, 31, 23, 59, 59);

            var categoriasMap = CarregarMapaCategorias(db);

            var itensBrutos = db.Set<LancamentoConta>()
                .AsNoTracking()
                .Where(l => l.DataLancamento >= inicioAno &&
                            l.DataLancamento <= fimAno &&
                            l.Tipo == tipo &&
                            !string.IsNullOrEmpty(l.CategoriaUuid) &&
                            l.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid &&
                            string.IsNullOrEmpty(l.TransferenciaUuid))
                .Select(l => new { l.CategoriaUuid, l.Valor })
                .ToList()
                .Select(l => (CategoriaUuid: l.CategoriaUuid, Valor: Math.Abs(l.Valor)));

            return AgruparPorNivel1(categoriasMap, itensBrutos);
        }

        private static Dictionary<string, Categoria> CarregarMapaCategorias(AppDbContext db)
        {
            return db.Categorias
                .AsNoTracking()
                .ToList()
                .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static List<CategoriaNivel1Grafico> AgruparPorNivel1(
            Dictionary<string, Categoria> categoriasMap,
            IEnumerable<(string CategoriaUuid, decimal Valor)> itensBrutos)
        {
            // 1. Consolida valores por categoria lançada (Nível 2)
            var totaisPorCategoria = itensBrutos
                .Where(x => !string.IsNullOrWhiteSpace(x.CategoriaUuid) && x.Valor > 0m)
                .GroupBy(x => x.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => (CategoriaUuid: g.Key, Valor: g.Sum(i => i.Valor)))
                .Where(x => x.Valor > 0m)
                .ToList();

            var gruposNivel1 = new Dictionary<string, CategoriaNivel1Grafico>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in totaisPorCategoria)
            {
                string paiId;
                string nomePai;
                string nomeSub;

                if (categoriasMap.TryGetValue(item.CategoriaUuid, out var cat))
                {
                    nomeSub = cat.Descricao;

                    if (cat.Nivel >= 2 &&
                        !string.IsNullOrWhiteSpace(cat.PaiUuid) &&
                        categoriasMap.TryGetValue(cat.PaiUuid.Trim(), out var catPai))
                    {
                        paiId = catPai.CategoriaUuid.Trim();
                        nomePai = catPai.Descricao;
                    }
                    else
                    {
                        // Caso o lançamento esteja direto em Nível 1 ou sem pai vinculado
                        paiId = cat.CategoriaUuid.Trim();
                        nomePai = cat.Descricao;
                    }
                }
                else
                {
                    paiId = "SEM_CATEGORIA";
                    nomePai = "Sem Categoria";
                    nomeSub = "Sem Categoria";
                }

                if (!gruposNivel1.TryGetValue(paiId, out var grupo))
                {
                    grupo = new CategoriaNivel1Grafico
                    {
                        CategoriaPaiUuid = paiId,
                        Nome = nomePai
                    };
                    gruposNivel1[paiId] = grupo;
                }

                grupo.Subcategorias.Add(new SubcategoriaNivel2Grafico
                {
                    CategoriaUuid = item.CategoriaUuid,
                    Nome = nomeSub,
                    Valor = item.Valor
                });
            }

            foreach (var grupo in gruposNivel1.Values)
            {
                grupo.Subcategorias = grupo.Subcategorias
                    .OrderByDescending(s => s.Valor)
                    .ToList();

                grupo.ValorTotal = grupo.Subcategorias.Sum(s => s.Valor);
            }

            return gruposNivel1.Values
                .Where(g => g.ValorTotal > 0m)
                .OrderByDescending(g => g.ValorTotal)
                .ToList();
        }
    }
}