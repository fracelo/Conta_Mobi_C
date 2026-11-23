using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;

namespace ContaMobi.Data
{
    public static class OrcamentoService
    {
        private static readonly CultureInfo _cultura = new("pt-BR");

        private static bool EhCategoriaTransferencia(string? categoriaUuid, string? tipoNormalizado = null)
        {
            if (!string.IsNullOrWhiteSpace(categoriaUuid) &&
                string.Equals(categoriaUuid.Trim(), TransferenciaService.CategoriaTransferenciaUuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(tipoNormalizado, "T", StringComparison.OrdinalIgnoreCase);
        }

        public static void GarantirTabelaCriada()
        {
            using var db = new AppDbContext();

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS orcamentos (
                    orcamentouuid TEXT NOT NULL PRIMARY KEY,
                    usuariouuid TEXT NOT NULL DEFAULT '',
                    categoriauuid TEXT NOT NULL,
                    ano INTEGER NOT NULL,
                    mes INTEGER NOT NULL CHECK (mes >= 1 AND mes <= 12),
                    valor_planejado NUMERIC(15, 2) DEFAULT 0,
                    valor_realizado NUMERIC(15, 2) NOT NULL DEFAULT 0.00,
                    updated_at TEXT NULL
                );
            ");

            var colunasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conn = db.Database.GetDbConnection();
            bool abriuAqui = conn.State != ConnectionState.Open;
            if (abriuAqui) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('orcamentos');";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? nomeColuna = reader["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(nomeColuna))
                        colunasExistentes.Add(nomeColuna.Trim());
                }
            }
            finally
            {
                if (abriuAqui) conn.Close();
            }

            if (!colunasExistentes.Contains("orcamentouuid") || !colunasExistentes.Contains("categoriauuid"))
            {
                db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS orcamentos;");
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE orcamentos (
                        orcamentouuid TEXT NOT NULL PRIMARY KEY,
                        usuariouuid TEXT NOT NULL DEFAULT '',
                        categoriauuid TEXT NOT NULL,
                        ano INTEGER NOT NULL,
                        mes INTEGER NOT NULL CHECK (mes >= 1 AND mes <= 12),
                        valor_planejado NUMERIC(15, 2) DEFAULT 0,
                        valor_realizado NUMERIC(15, 2) NOT NULL DEFAULT 0.00,
                        updated_at TEXT NULL
                    );
                ");
            }
            else
            {
                if (!colunasExistentes.Contains("usuariouuid"))
                    db.Database.ExecuteSqlRaw("ALTER TABLE orcamentos ADD COLUMN usuariouuid TEXT NOT NULL DEFAULT '';");

                if (!colunasExistentes.Contains("valor_planejado"))
                    db.Database.ExecuteSqlRaw("ALTER TABLE orcamentos ADD COLUMN valor_planejado NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");

                if (!colunasExistentes.Contains("valor_realizado"))
                    db.Database.ExecuteSqlRaw("ALTER TABLE orcamentos ADD COLUMN valor_realizado NUMERIC(15, 2) NOT NULL DEFAULT 0.00;");

                if (!colunasExistentes.Contains("updated_at"))
                    db.Database.ExecuteSqlRaw("ALTER TABLE orcamentos ADD COLUMN updated_at TEXT NULL;");
            }

            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_orcamentos_ano_mes_cat 
                    ON orcamentos (ano, mes, categoriauuid);
            ");
        }

        public static bool MesAnoJaExiste(int ano, int mes)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();
            return db.Set<Orcamento>().AsNoTracking().Any(o => o.Ano == ano && o.Mes == mes);
        }

        /// <summary>
        /// Calcula o valor realizado no mês exclusivamente a partir dos lançamentos em contas correntes
        /// da mesma categoria e do mesmo mês/ano (excluindo transferências).
        /// </summary>
        public static Dictionary<string, decimal> CalcularRealizadoPorCategoriaNoMes(int ano, int mes)
        {
            TransferenciaService.GarantirEstruturaTransferencia();

            using var db = new AppDbContext();
            var resultado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            DateTime inicioMes = new DateTime(ano, mes, 1);
            DateTime fimMes = inicioMes.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            try
            {
                var lancamentos = db.Set<LancamentoConta>()
                    .AsNoTracking()
                    .Where(l => l.DataLancamento >= inicioMes &&
                                l.DataLancamento <= fimMes &&
                                !string.IsNullOrEmpty(l.CategoriaUuid) &&
                                l.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid &&
                                string.IsNullOrEmpty(l.TransferenciaUuid))
                    .ToList();

                foreach (var l in lancamentos)
                {
                    string catId = l.CategoriaUuid.Trim();
                    if (EhCategoriaTransferencia(catId))
                        continue;

                    if (!resultado.ContainsKey(catId))
                        resultado[catId] = 0m;

                    resultado[catId] += Math.Abs(l.Valor);
                }
            }
            catch { }

            return resultado;
        }

        public static List<OrcamentoMesResumo> ObterLista12Meses(int anoBase, int mesBase, string? busca = null)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();

            var categoriasMap = db.Categorias
                .AsNoTracking()
                .ToList()
                .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => NormalizarTipoCategoria(g.First().Tipo), StringComparer.OrdinalIgnoreCase);

            var todosOrcamentos = db.Set<Orcamento>()
                .AsNoTracking()
                .ToList();

            DateTime dataInicioJanela = new DateTime(anoBase, mesBase, 1);
            DateTime dataFimJanela = dataInicioJanela.AddMonths(11);

            DateTime dataPrimeiroCalculo = dataInicioJanela;
            if (todosOrcamentos.Count > 0)
            {
                var menor = todosOrcamentos.OrderBy(o => o.Ano).ThenBy(o => o.Mes).First();
                DateTime menorDataBanco = new DateTime(menor.Ano, menor.Mes, 1);
                if (menorDataBanco < dataPrimeiroCalculo)
                    dataPrimeiroCalculo = menorDataBanco;
            }

            decimal saldoAcumuladoPlanejado = 0m;
            decimal saldoAcumuladoRealizado = 0m;

            var listaCompleta = new List<OrcamentoMesResumo>();
            DateTime cursor = dataPrimeiroCalculo;

            while (cursor <= dataFimJanela)
            {
                int a = cursor.Year;
                int m = cursor.Month;

                var itensMes = todosOrcamentos.Where(o => o.Ano == a && o.Mes == m).ToList();
                var realizadoContas = CalcularRealizadoPorCategoriaNoMes(a, m);

                decimal creditosPlan = 0m;
                decimal debitosPlan = 0m;
                decimal creditosReal = 0m;
                decimal debitosReal = 0m;

                foreach (var item in itensMes)
                {
                    string catId = item.CategoriaUuid.Trim();
                    string tipoCat = categoriasMap.TryGetValue(catId, out string? t) ? t : "D";
                    if (EhCategoriaTransferencia(catId, tipoCat))
                        continue;

                    decimal planEfetivo = item.ValorPlanejado > 0m ? item.ValorPlanejado : 0m;
                    decimal realEfetivo = realizadoContas.TryGetValue(catId, out decimal rVal) ? rVal : 0m;

                    if (tipoCat == "C")
                    {
                        creditosPlan += planEfetivo;
                        creditosReal += realEfetivo;
                    }
                    else if (tipoCat == "D")
                    {
                        debitosPlan += planEfetivo;
                        debitosReal += realEfetivo;
                    }
                }

                // Inclui lançamentos realizados em contas correntes no mês mesmo que não tenham valor planejado informado
                var catsJaContabilizadas = new HashSet<string>(
                    itensMes.Select(o => o.CategoriaUuid.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in realizadoContas)
                {
                    if (catsJaContabilizadas.Contains(kvp.Key))
                        continue;

                    string tipoCat = categoriasMap.TryGetValue(kvp.Key, out string? t) ? t : "D";
                    if (EhCategoriaTransferencia(kvp.Key, tipoCat))
                        continue;

                    if (tipoCat == "C")
                        creditosReal += kvp.Value;
                    else if (tipoCat == "D")
                        debitosReal += kvp.Value;
                }

                bool possuiDados = itensMes.Count > 0 || realizadoContas.Count > 0;

                var resumo = new OrcamentoMesResumo
                {
                    Ano = a,
                    Mes = m,
                    PossuiLancamentosOrcados = possuiDados,
                    SaldoAnterior = saldoAcumuladoPlanejado,
                    TotalCreditos = creditosPlan,
                    TotalDebitos = debitosPlan,
                    SaldoAnteriorRealizado = saldoAcumuladoRealizado,
                    TotalCreditosRealizado = creditosReal,
                    TotalDebitosRealizado = debitosReal
                };

                saldoAcumuladoPlanejado = resumo.SaldoFinal;
                saldoAcumuladoRealizado = resumo.SaldoFinalRealizado;

                if (cursor >= dataInicioJanela)
                {
                    listaCompleta.Add(resumo);
                }

                cursor = cursor.AddMonths(1);
            }

            if (!string.IsNullOrWhiteSpace(busca))
            {
                string termo = busca.Trim().ToLowerInvariant();
                listaCompleta = listaCompleta
                    .Where(x => x.MesAnoFormatado.ToLowerInvariant().Contains(termo) ||
                                x.StatusTag.ToLowerInvariant().Contains(termo))
                    .ToList();
            }

            return listaCompleta;
        }

        public static (decimal SaldoAnteriorPlanejado, decimal SaldoAnteriorRealizado) ObterSaldosAnterioresDoMes(int ano, int mes)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();

            var todosOrcamentosAnteriores = db.Set<Orcamento>()
                .AsNoTracking()
                .Where(o => o.Ano < ano || (o.Ano == ano && o.Mes < mes))
                .ToList();

            var categoriasMap = db.Categorias
                .AsNoTracking()
                .ToList()
                .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => NormalizarTipoCategoria(g.First().Tipo), StringComparer.OrdinalIgnoreCase);

            var periodosOrc = todosOrcamentosAnteriores
                .Select(o => new { o.Ano, o.Mes })
                .Distinct()
                .OrderBy(p => p.Ano)
                .ThenBy(p => p.Mes)
                .ToList();

            decimal saldoPlan = 0m;
            decimal saldoReal = 0m;

            foreach (var p in periodosOrc)
            {
                var itensMes = todosOrcamentosAnteriores.Where(o => o.Ano == p.Ano && o.Mes == p.Mes).ToList();
                var realizadoContas = CalcularRealizadoPorCategoriaNoMes(p.Ano, p.Mes);

                decimal credP = 0m, debP = 0m, credR = 0m, debR = 0m;

                foreach (var item in itensMes)
                {
                    string catId = item.CategoriaUuid.Trim();
                    string tipoCat = categoriasMap.TryGetValue(catId, out string? t) ? t : "D";
                    if (EhCategoriaTransferencia(catId, tipoCat))
                        continue;

                    decimal planEfetivo = item.ValorPlanejado > 0m ? item.ValorPlanejado : 0m;
                    decimal realEfetivo = realizadoContas.TryGetValue(catId, out decimal rVal) ? rVal : 0m;

                    if (tipoCat == "C")
                    {
                        credP += planEfetivo;
                        credR += realEfetivo;
                    }
                    else if (tipoCat == "D")
                    {
                        debP += planEfetivo;
                        debR += realEfetivo;
                    }
                }

                var catsJaContabilizadas = new HashSet<string>(
                    itensMes.Select(o => o.CategoriaUuid.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in realizadoContas)
                {
                    if (catsJaContabilizadas.Contains(kvp.Key))
                        continue;

                    string tipoCat = categoriasMap.TryGetValue(kvp.Key, out string? t) ? t : "D";
                    if (EhCategoriaTransferencia(kvp.Key, tipoCat))
                        continue;

                    if (tipoCat == "C")
                        credR += kvp.Value;
                    else if (tipoCat == "D")
                        debR += kvp.Value;
                }

                saldoPlan = saldoPlan + credP - debP;
                saldoReal = saldoReal + credR - debR;
            }

            return (saldoPlan, saldoReal);
        }

        public static (List<OrcamentoCategoriaItem> Creditos, List<OrcamentoCategoriaItem> Debitos) ObterItensEdicaoMes(int ano, int mes)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();

            var todasCategorias = db.Categorias
                .AsNoTracking()
                .ToList()
                .Where(c => !EhCategoriaTransferencia(c.CategoriaUuid, NormalizarTipoCategoria(c.Tipo)))
                .ToList();

            var mapaTodas = todasCategorias
                .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var orcamentosMes = db.Set<Orcamento>()
                .AsNoTracking()
                .Where(o => o.Ano == ano && o.Mes == mes)
                .ToList()
                .GroupBy(o => o.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var realizadoEfetivo = CalcularRealizadoPorCategoriaNoMes(ano, mes);

            OrcamentoCategoriaItem CriarItem(Categoria cat, bool ehPai, string? paiUuidForcado = null)
            {
                string catId = cat.CategoriaUuid.Trim();
                string tipoNorm = NormalizarTipoCategoria(cat.Tipo);
                orcamentosMes.TryGetValue(catId, out Orcamento? orcSalvo);

                // Planejado vem exclusivamente do que foi digitado/replicado no orçamento do mês
                decimal valPlan = orcSalvo != null && orcSalvo.ValorPlanejado > 0m
                    ? orcSalvo.ValorPlanejado
                    : 0m;

                // Realizado vem exclusivamente dos lançamentos em contas correntes da categoria no mês
                decimal valReal = realizadoEfetivo.TryGetValue(catId, out decimal calc) ? calc : 0m;

                return new OrcamentoCategoriaItem
                {
                    OrcamentoUuid = orcSalvo?.OrcamentoUuid ?? Guid.NewGuid().ToString(),
                    CategoriaUuid = catId,
                    PaiUuid = paiUuidForcado ?? (string.IsNullOrWhiteSpace(cat.PaiUuid) ? null : cat.PaiUuid.Trim()),
                    CategoriaDescricao = cat.Descricao,
                    Tipo = tipoNorm,
                    EhCategoriaPai = ehPai,
                    EstaExpandida = true,
                    ValorPlanejado = valPlan,
                    ValorPlanejadoTexto = valPlan.ToString("N2", _cultura),
                    ValorRealizado = valReal,
                    ValorRealizadoTexto = valReal.ToString("N2", _cultura)
                };
            }

            List<OrcamentoCategoriaItem> MontarArvorePorTipo(string tipoAlvo)
            {
                var catsDoTipo = todasCategorias
                    .Where(c => NormalizarTipoCategoria(c.Tipo) == tipoAlvo)
                    .ToList();

                var filhosDoTipo = catsDoTipo
                    .Where(c => !string.IsNullOrWhiteSpace(c.PaiUuid) && c.Nivel != 1)
                    .ToList();

                var filhosPorPai = filhosDoTipo
                    .GroupBy(c => c.PaiUuid!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Descricao).ToList(), StringComparer.OrdinalIgnoreCase);

                var paisDict = new Dictionary<string, Categoria>(StringComparer.OrdinalIgnoreCase);

                foreach (var c in catsDoTipo.Where(c => c.Nivel == 1 || string.IsNullOrWhiteSpace(c.PaiUuid)))
                {
                    paisDict[c.CategoriaUuid.Trim()] = c;
                }

                foreach (var paiId in filhosPorPai.Keys)
                {
                    if (!paisDict.ContainsKey(paiId) && mapaTodas.TryGetValue(paiId, out var catPaiRef))
                    {
                        paisDict[paiId] = catPaiRef;
                    }
                }

                var paisOrdenados = paisDict.Values
                    .OrderBy(c => c.Descricao)
                    .ToList();

                var resultado = new List<OrcamentoCategoriaItem>();
                var idsAdicionados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pai in paisOrdenados)
                {
                    string paiId = pai.CategoriaUuid.Trim();
                    filhosPorPai.TryGetValue(paiId, out var filhas);
                    bool temFilhas = filhas != null && filhas.Count > 0;

                    var itemPai = CriarItem(pai, ehPai: true, paiUuidForcado: null);
                    itemPai.Tipo = tipoAlvo;
                    itemPai.TemFilhas = temFilhas;

                    if (temFilhas)
                    {
                        itemPai.ValorPlanejado = 0m;
                        itemPai.ValorPlanejadoTexto = "0,00";
                        itemPai.ValorRealizado = 0m;
                        itemPai.ValorRealizadoTexto = "0,00";
                    }

                    resultado.Add(itemPai);
                    idsAdicionados.Add(paiId);

                    if (filhas != null)
                    {
                        decimal somaPlan = 0m;
                        decimal somaReal = 0m;

                        foreach (var filha in filhas)
                        {
                            var itemFilha = CriarItem(filha, ehPai: false, paiUuidForcado: paiId);
                            itemFilha.Tipo = tipoAlvo;
                            somaPlan += itemFilha.ValorPlanejado;
                            somaReal += itemFilha.ValorRealizado;

                            resultado.Add(itemFilha);
                            idsAdicionados.Add(itemFilha.CategoriaUuid);
                        }

                        itemPai.SubtotalPlanejadoPai = somaPlan;
                        itemPai.SubtotalRealizadoPai = somaReal;
                    }
                }

                foreach (var restante in catsDoTipo.Where(c => !idsAdicionados.Contains(c.CategoriaUuid.Trim())).OrderBy(c => c.Descricao))
                {
                    var itemSolto = CriarItem(restante, ehPai: true, paiUuidForcado: null);
                    itemSolto.TemFilhas = false;
                    resultado.Add(itemSolto);
                }

                return resultado;
            }

            return (MontarArvorePorTipo("C"), MontarArvorePorTipo("D"));
        }

        public static void SalvarOrcamentoDoMes(
            int ano,
            int mes,
            string usuarioUuid,
            IEnumerable<OrcamentoCategoriaItem> itensCredito,
            IEnumerable<OrcamentoCategoriaItem> itensDebito)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();
            using var trans = db.Database.BeginTransaction();

            try
            {
                var existentes = db.Set<Orcamento>()
                    .Where(o => o.Ano == ano && o.Mes == mes)
                    .ToList()
                    .GroupBy(o => o.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var todosItens = itensCredito.Concat(itensDebito);

                foreach (var item in todosItens)
                {
                    string catId = item.CategoriaUuid.Trim();
                    if (EhCategoriaTransferencia(catId, item.Tipo))
                        continue;

                    if (existentes.TryGetValue(catId, out var registro))
                    {
                        registro.ValorPlanejado = item.ValorPlanejado;
                        registro.ValorRealizado = item.ValorRealizado;
                        registro.UpdatedAt = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(usuarioUuid))
                            registro.UsuarioUuid = usuarioUuid;
                    }
                    else
                    {
                        db.Set<Orcamento>().Add(new Orcamento
                        {
                            OrcamentoUuid = string.IsNullOrWhiteSpace(item.OrcamentoUuid) ? Guid.NewGuid().ToString() : item.OrcamentoUuid,
                            UsuarioUuid = usuarioUuid,
                            CategoriaUuid = catId,
                            Ano = ano,
                            Mes = mes,
                            ValorPlanejado = item.ValorPlanejado,
                            ValorRealizado = item.ValorRealizado,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
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

        public static void ReplicarOrcamentoParaProximosMeses(int anoOrigem, int mesOrigem, string usuarioUuid, int quantidadeMeses = 11)
        {
            GarantirTabelaCriada();
            using var db = new AppDbContext();
            using var trans = db.Database.BeginTransaction();

            try
            {
                var origemItens = db.Set<Orcamento>()
                    .AsNoTracking()
                    .Where(o => o.Ano == anoOrigem &&
                                o.Mes == mesOrigem &&
                                o.CategoriaUuid != TransferenciaService.CategoriaTransferenciaUuid)
                    .ToList();

                if (origemItens.Count == 0) return;

                DateTime cursor = new DateTime(anoOrigem, mesOrigem, 1).AddMonths(1);

                for (int i = 0; i < quantidadeMeses; i++)
                {
                    int anoDestino = cursor.Year;
                    int mesDestino = cursor.Month;

                    var existentesNoDestino = db.Set<Orcamento>()
                        .Where(o => o.Ano == anoDestino && o.Mes == mesDestino)
                        .ToList()
                        .GroupBy(o => o.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    foreach (var orig in origemItens)
                    {
                        string catId = orig.CategoriaUuid.Trim();
                        if (existentesNoDestino.TryGetValue(catId, out var existente))
                        {
                            existente.ValorPlanejado = orig.ValorPlanejado;
                            existente.UpdatedAt = DateTime.UtcNow;
                            if (!string.IsNullOrWhiteSpace(usuarioUuid))
                                existente.UsuarioUuid = usuarioUuid;
                        }
                        else
                        {
                            db.Set<Orcamento>().Add(new Orcamento
                            {
                                OrcamentoUuid = Guid.NewGuid().ToString(),
                                UsuarioUuid = usuarioUuid,
                                CategoriaUuid = catId,
                                Ano = anoDestino,
                                Mes = mesDestino,
                                ValorPlanejado = orig.ValorPlanejado,
                                ValorRealizado = 0m,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    cursor = cursor.AddMonths(1);
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

        public static string GerarE_SalvarCsv12Meses(int anoBase, int mesBase)
        {
            var lista = ObterLista12Meses(anoBase, mesBase);
            var sb = new StringBuilder();

            sb.AppendLine("Ano;Mes;Status;SaldoAnteriorPlanejado;TotalCreditosPlanejado;TotalDebitosPlanejado;SaldoFinalPlanejado;SaldoAnteriorRealizado;TotalCreditosRealizado;TotalDebitosRealizado;SaldoFinalRealizado");

            foreach (var item in lista)
            {
                sb.AppendLine($"{item.Ano};{item.Mes};{item.StatusTag};{item.SaldoAnterior};{item.TotalCreditos};{item.TotalDebitos};{item.SaldoFinal};{item.SaldoAnteriorRealizado};{item.TotalCreditosRealizado};{item.TotalDebitosRealizado};{item.SaldoFinalRealizado}".Replace('.', ','));
            }

            string pastaLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ContaMobi");
            if (!Directory.Exists(pastaLocal)) Directory.CreateDirectory(pastaLocal);

            string caminhoArquivo = Path.Combine(pastaLocal, $"Orcamento_12Meses_{anoBase}_{mesBase:D2}.csv");
            File.WriteAllText(caminhoArquivo, sb.ToString(), Encoding.UTF8);

            return caminhoArquivo;
        }

        public static string NormalizarTipoCategoria(string? tipo)
        {
            string t = (tipo ?? string.Empty).Trim().ToUpperInvariant();
            if (t == "T" || t.StartsWith("TRANSF"))
                return "T";
            if (t == "C" || t == "R" || t.StartsWith("REC") || t.StartsWith("CRED") || t == "E")
                return "C";
            return "D";
        }
    }
}