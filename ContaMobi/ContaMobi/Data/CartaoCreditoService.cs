using Microsoft.EntityFrameworkCore;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ContaMobi.Data
{
    public class PrevisaoParcelaCartao
    {
        public int NumeroParcela { get; set; }
        public int TotalParcelas { get; set; }
        public string MesAnoFatura { get; set; } = string.Empty;
        public DateTime DataVencimento { get; set; }
        public decimal ValorParcela { get; set; }
    }

    public static class CartaoCreditoService
    {
        /// <summary>
        /// Garante que as tabelas do módulo de Cartões de Crédito existam no SQLite local
        /// e insere as 5 bandeiras padrão caso ainda não existam.
        /// Pode ser chamado na inicialização do aplicativo (App.xaml.cs).
        /// </summary>
        public static void GarantirTabelasCartao()
        {
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

            if (!db.Bandeiras.Any())
            {
                var agora = DateTime.UtcNow;
                db.Bandeiras.AddRange(
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111101", UsuarioUuid = null, DescricaoBandeira = "Visa", IconeBandeira = "ms-appx:///Assets/Bandeiras/visa.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111102", UsuarioUuid = null, DescricaoBandeira = "Mastercard", IconeBandeira = "ms-appx:///Assets/Bandeiras/mastercard.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111103", UsuarioUuid = null, DescricaoBandeira = "Elo", IconeBandeira = "ms-appx:///Assets/Bandeiras/elo.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111104", UsuarioUuid = null, DescricaoBandeira = "American Express", IconeBandeira = "ms-appx:///Assets/Bandeiras/amex.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora },
                    new Bandeira { BandeiraUuid = "11111111-1111-1111-1111-111111111105", UsuarioUuid = null, DescricaoBandeira = "Hipercard", IconeBandeira = "ms-appx:///Assets/Bandeiras/hipercard.png", Ativo = true, CriadoEm = agora, UpdatedAt = agora }
                );
                db.SaveChanges();
            }
        }

        // =====================================================================
        // 1. BANDEIRAS
        // =====================================================================

        public static List<Bandeira> ListarBandeiras(string? usuarioUuid, bool apenasAtivas = true)
        {
            using var db = new AppDbContext();
            var query = db.Bandeiras.AsNoTracking()
                .Where(b => b.UsuarioUuid == null || b.UsuarioUuid == usuarioUuid);

            if (apenasAtivas)
                query = query.Where(b => b.Ativo);

            return query.OrderBy(b => b.DescricaoBandeira).ToList();
        }

        public static void SalvarBandeira(Bandeira bandeira, string? caminhoArquivoImagemOrigem = null)
        {
            if (string.IsNullOrWhiteSpace(bandeira.DescricaoBandeira))
                throw new InvalidOperationException("Informe a descrição da bandeira.");

            using var db = new AppDbContext();

            if (!string.IsNullOrWhiteSpace(caminhoArquivoImagemOrigem) && File.Exists(caminhoArquivoImagemOrigem))
            {
                string pastaDestino = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ContaMobi", "Bandeiras");

                if (!Directory.Exists(pastaDestino))
                    Directory.CreateDirectory(pastaDestino);

                string ext = Path.GetExtension(caminhoArquivoImagemOrigem);
                string nomeArquivo = $"{Guid.NewGuid()}{ext}";
                string caminhoFinal = Path.Combine(pastaDestino, nomeArquivo);

                File.Copy(caminhoArquivoImagemOrigem, caminhoFinal, overwrite: true);
                bandeira.IconeBandeira = nomeArquivo;
            }

            bandeira.UpdatedAt = DateTime.UtcNow;

            var existente = db.Bandeiras.AsNoTracking()
                .FirstOrDefault(b => b.BandeiraUuid == bandeira.BandeiraUuid);

            if (existente == null)
            {
                bandeira.BandeiraUuid = string.IsNullOrWhiteSpace(bandeira.BandeiraUuid)
                    ? Guid.NewGuid().ToString()
                    : bandeira.BandeiraUuid;
                bandeira.CriadoEm = DateTime.UtcNow;
                db.Bandeiras.Add(bandeira);
            }
            else
            {
                bandeira.CriadoEm = existente.CriadoEm ?? DateTime.UtcNow;
                db.Bandeiras.Update(bandeira);
            }

            db.SaveChanges();
        }

        public static void ExcluirBandeira(string bandeiraUuid)
        {
            using var db = new AppDbContext();
            bool emUso = db.CartoesCredito.Any(c => c.BandeiraUuid == bandeiraUuid);
            if (emUso)
                throw new InvalidOperationException("Não é possível excluir esta bandeira pois existem cartões vinculados a ela.");

            var bandeira = db.Bandeiras.Find(bandeiraUuid);
            if (bandeira != null)
            {
                db.Bandeiras.Remove(bandeira);
                db.SaveChanges();
            }
        }

        // =====================================================================
        // 2. CARTÕES DE CRÉDITO
        // =====================================================================

        public static List<CartaoCredito> ListarCartoes(string usuarioUuid, bool apenasAtivos = true)
        {
            using var db = new AppDbContext();
            var query = db.CartoesCredito
                .AsNoTracking()
                .Include(c => c.Bandeira)
                .Include(c => c.ContaCorrente)
                .Where(c => c.UsuarioUuid == usuarioUuid);

            if (apenasAtivos)
                query = query.Where(c => c.Ativo);

            return query.OrderBy(c => c.Descricao).ToList();
        }

        public static void SalvarCartao(CartaoCredito cartao)
        {
            if (string.IsNullOrWhiteSpace(cartao.Descricao))
                throw new InvalidOperationException("Informe a descrição/nome do cartão.");

            if (string.IsNullOrWhiteSpace(cartao.BandeiraUuid))
                throw new InvalidOperationException("Selecione a bandeira do cartão.");

            if (cartao.DiaFechamento < 1 || cartao.DiaFechamento > 31)
                throw new InvalidOperationException("O dia de fechamento deve estar entre 1 e 31.");

            if (cartao.DiaVencimento < 1 || cartao.DiaVencimento > 31)
                throw new InvalidOperationException("O dia de vencimento deve estar entre 1 e 31.");

            if (cartao.LimiteTotal < 0m)
                throw new InvalidOperationException("O limite total não pode ser negativo.");

            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            cartao.UpdatedAt = DateTime.UtcNow;

            var existente = db.CartoesCredito.AsNoTracking()
                .FirstOrDefault(c => c.CartaoUuid == cartao.CartaoUuid);

            if (existente == null)
            {
                cartao.CartaoUuid = string.IsNullOrWhiteSpace(cartao.CartaoUuid)
                    ? Guid.NewGuid().ToString()
                    : cartao.CartaoUuid;
                cartao.LimiteAtual = cartao.LimiteTotal;
                cartao.CriadoEm = DateTime.UtcNow;

                db.CartoesCredito.Add(cartao);
                db.SaveChanges();
            }
            else
            {
                cartao.CriadoEm = existente.CriadoEm ?? DateTime.UtcNow;
                db.CartoesCredito.Update(cartao);
                db.SaveChanges();

                RecalcularLimiteInterno(db, cartao.CartaoUuid);
            }

            transaction.Commit();
        }

        public static void ExcluirCartao(string cartaoUuid)
        {
            using var db = new AppDbContext();
            bool possuiLancamentos = db.LancamentosCartao.Any(l => l.CartaoUuid == cartaoUuid);
            if (possuiLancamentos)
                throw new InvalidOperationException("Este cartão possui lançamentos registrados. Inative-o em vez de excluir para preservar o histórico.");

            using var transaction = db.Database.BeginTransaction();

            var faturasVazias = db.FaturasCartao.Where(f => f.CartaoUuid == cartaoUuid).ToList();
            if (faturasVazias.Count > 0)
                db.FaturasCartao.RemoveRange(faturasVazias);

            var cartao = db.CartoesCredito.Find(cartaoUuid);
            if (cartao != null)
                db.CartoesCredito.Remove(cartao);

            db.SaveChanges();
            transaction.Commit();
        }

        public static void RecalcularLimiteCartao(string cartaoUuid)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            RecalcularLimiteInterno(db, cartaoUuid);
            transaction.Commit();
        }

        private static void RecalcularLimiteInterno(AppDbContext db, string cartaoUuid)
        {
            var cartao = db.CartoesCredito.FirstOrDefault(c => c.CartaoUuid == cartaoUuid);
            if (cartao == null) return;

            // Comprometido = soma do saldo devedor de todas as faturas não quitadas
            decimal valorComprometido = db.FaturasCartao
                .AsNoTracking()
                .Where(f => f.CartaoUuid == cartaoUuid && f.StatusFatura != "PAGA")
                .ToList()
                .Sum(f => Math.Max(0m, f.SaldoFatura));

            cartao.LimiteAtual = cartao.LimiteTotal - valorComprometido;
            cartao.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        // =====================================================================
        // 3. CÁLCULO DE CICLO, PREVISÃO E LANÇAMENTOS NO CARTÃO
        // =====================================================================

        /// <summary>
        /// Determina o ano/mês da fatura inicial conforme a data da compra e o dia de fechamento do cartão.
        /// Compras feitas no dia do fechamento ou após entram na fatura do mês seguinte.
        /// </summary>
        public static (int Ano, int Mes, string MesAnoFatura) CalcularCompetenciaInicial(DateTime dataCompra, int diaFechamento)
        {
            DateTime baseRef = dataCompra.Day >= diaFechamento
                ? new DateTime(dataCompra.Year, dataCompra.Month, 1).AddMonths(1)
                : new DateTime(dataCompra.Year, dataCompra.Month, 1);

            return (baseRef.Year, baseRef.Month, $"{baseRef.Year:D4}-{baseRef.Month:D2}");
        }

        /// <summary>
        /// Simula as parcelas antes de gravar para exibição em grade na tela de lançamento.
        /// </summary>
        public static List<PrevisaoParcelaCartao> SimularParcelamento(
            CartaoCredito cartao,
            DateTime dataCompra,
            decimal valorInformado,
            int nrParcelas,
            bool valorInformadoEhTotal = true)
        {
            nrParcelas = Math.Max(1, nrParcelas);
            decimal valorAbs = Math.Abs(valorInformado);
            decimal totalCompra = valorInformadoEhTotal ? valorAbs : Math.Round(valorAbs * nrParcelas, 2);

            decimal valorBaseParcela = Math.Floor((totalCompra / nrParcelas) * 100m) / 100m;
            decimal diferencaCentavos = totalCompra - (valorBaseParcela * nrParcelas);

            var (anoIni, mesIni, _) = CalcularCompetenciaInicial(dataCompra.Date, cartao.DiaFechamento);
            var mesBase = new DateTime(anoIni, mesIni, 1);

            var lista = new List<PrevisaoParcelaCartao>();
            for (int i = 0; i < nrParcelas; i++)
            {
                DateTime comp = mesBase.AddMonths(i);
                DateTime vencimento = CalcularDataVencimento(comp.Year, comp.Month, cartao.DiaFechamento, cartao.DiaVencimento);
                decimal valorParcela = i == 0 ? valorBaseParcela + diferencaCentavos : valorBaseParcela;

                lista.Add(new PrevisaoParcelaCartao
                {
                    NumeroParcela = i + 1,
                    TotalParcelas = nrParcelas,
                    MesAnoFatura = $"{comp.Year:D4}-{comp.Month:D2}",
                    DataVencimento = vencimento,
                    ValorParcela = valorParcela
                });
            }

            return lista;
        }

        /// <summary>
        /// Grava uma compra à vista ou parcelada no cartão, criando automaticamente as faturas futuras
        /// e atualizando o total das faturas e o limite disponível do cartão.
        /// </summary>
        public static void IncluirLancamentoCartao(
            string usuarioUuid,
            string cartaoUuid,
            string categoriaUuid,
            string? entidadeUuid,
            DateTime dataLancamento,
            string descricao,
            decimal valorInformado,
            int nrParcelas = 1,
            string? observacao = null,
            bool valorInformadoEhTotal = true)
        {
            if (string.IsNullOrWhiteSpace(descricao))
                throw new InvalidOperationException("Informe a descrição da compra.");

            if (string.IsNullOrWhiteSpace(categoriaUuid))
                throw new InvalidOperationException("Selecione uma categoria para o lançamento.");

            if (valorInformado <= 0m)
                throw new InvalidOperationException("O valor da compra deve ser maior que zero.");

            nrParcelas = Math.Max(1, nrParcelas);

            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var cartao = db.CartoesCredito.FirstOrDefault(c => c.CartaoUuid == cartaoUuid)
                ?? throw new InvalidOperationException("Cartão de crédito não encontrado.");

            var parcelas = SimularParcelamento(cartao, dataLancamento.Date, valorInformado, nrParcelas, valorInformadoEhTotal);
            string grupoUuid = Guid.NewGuid().ToString();
            var faturasAfetadas = new HashSet<string>();

            foreach (var p in parcelas)
            {
                var partes = p.MesAnoFatura.Split('-');
                int ano = int.Parse(partes[0]);
                int mes = int.Parse(partes[1]);

                var fatura = ObterOuCriarFaturaInterno(db, cartao, ano, mes);
                faturasAfetadas.Add(fatura.FaturaUuid);

                string descParcela = nrParcelas > 1
                    ? $"{descricao.Trim()} ({p.NumeroParcela:D2}/{p.TotalParcelas:D2})"
                    : descricao.Trim();

                db.LancamentosCartao.Add(new LancamentoCartao
                {
                    LancamentoCartaoUuid = Guid.NewGuid().ToString(),
                    UsuarioUuid = usuarioUuid,
                    CartaoUuid = cartao.CartaoUuid,
                    FaturaUuid = fatura.FaturaUuid,
                    CategoriaUuid = categoriaUuid,
                    EntidadeUuid = string.IsNullOrWhiteSpace(entidadeUuid) ? null : entidadeUuid,
                    GrupoParcelamentoUuid = grupoUuid,
                    DataLancamento = dataLancamento.Date,
                    Descricao = descParcela,
                    ValorParcela = p.ValorParcela,
                    NrParcelas = p.TotalParcelas,
                    ParcelaAtual = p.NumeroParcela,
                    Observacao = observacao,
                    CriadoEm = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            db.SaveChanges();

            foreach (string faturaUuid in faturasAfetadas)
            {
                RecalcularFaturaInterno(db, faturaUuid);
            }

            RecalcularLimiteInterno(db, cartao.CartaoUuid);
            transaction.Commit();
        }

        /// <summary>
        /// Exclui um lançamento único ou todas as parcelas do mesmo grupo de parcelamento.
        /// </summary>
        public static void ExcluirLancamentoCartao(string lancamentoCartaoUuid, bool excluirTodoParcelamento = false)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            var alvo = db.LancamentosCartao.FirstOrDefault(l => l.LancamentoCartaoUuid == lancamentoCartaoUuid);
            if (alvo == null) return;

            var itensParaExcluir = excluirTodoParcelamento
                ? db.LancamentosCartao.Where(l => l.GrupoParcelamentoUuid == alvo.GrupoParcelamentoUuid).ToList()
                : new List<LancamentoCartao> { alvo };

            var faturasIds = itensParaExcluir.Select(i => i.FaturaUuid).Distinct().ToList();
            var faturasEnvolvidas = db.FaturasCartao.Where(f => faturasIds.Contains(f.FaturaUuid)).ToList();

            if (faturasEnvolvidas.Any(f => f.StatusFatura == "PAGA" || f.TotalPago > 0m))
            {
                throw new InvalidOperationException("Não é possível excluir lançamentos vinculados a uma fatura já paga ou com pagamento parcial.");
            }

            string cartaoUuid = alvo.CartaoUuid;

            db.LancamentosCartao.RemoveRange(itensParaExcluir);
            db.SaveChanges();

            foreach (string faturaUuid in faturasIds)
            {
                RecalcularFaturaInterno(db, faturaUuid);
            }

            RecalcularLimiteInterno(db, cartaoUuid);
            transaction.Commit();
        }

        // =====================================================================
        // 4. FATURAS E PAGAMENTO (INTEGRAÇÃO COM CONTA CORRENTE)
        // =====================================================================

        public static List<FaturaCartao> ListarFaturasDoCartao(string cartaoUuid)
        {
            using var db = new AppDbContext();
            return db.FaturasCartao
                .AsNoTracking()
                .Where(f => f.CartaoUuid == cartaoUuid)
                .OrderByDescending(f => f.MesAnoFatura)
                .ToList();
        }

        public static FaturaCartao? ObterFaturaComLancamentos(string cartaoUuid, string mesAnoFatura)
        {
            using var db = new AppDbContext();
            var fatura = db.FaturasCartao
                .AsNoTracking()
                .Include(f => f.CartaoCredito)
                    .ThenInclude(c => c!.Bandeira)
                .FirstOrDefault(f => f.CartaoUuid == cartaoUuid && f.MesAnoFatura == mesAnoFatura);

            if (fatura != null)
            {
                fatura.Lancamentos = db.LancamentosCartao
                    .AsNoTracking()
                    .Include(l => l.Categoria)
                    .Include(l => l.Entidade)
                    .Where(l => l.FaturaUuid == fatura.FaturaUuid)
                    .OrderBy(l => l.DataLancamento)
                    .ThenBy(l => l.CriadoEm)
                    .ToList();
            }

            return fatura;
        }

        /// <summary>
        /// Realiza o pagamento total ou parcial da fatura, gerando automaticamente o débito na Conta Corrente
        /// e liberando o limite correspondente no Cartão de Crédito.
        /// </summary>
        public static void PagarFatura(
            string faturaUuid,
            string contaCorrenteUuid,
            string categoriaPagamentoUuid,
            DateTime dataPagamento,
            decimal valorPago)
        {
            if (valorPago <= 0m)
                throw new InvalidOperationException("O valor do pagamento deve ser maior que zero.");

            if (string.IsNullOrWhiteSpace(contaCorrenteUuid))
                throw new InvalidOperationException("Selecione a conta corrente para debitar o pagamento.");

            if (string.IsNullOrWhiteSpace(categoriaPagamentoUuid))
                throw new InvalidOperationException("Selecione a categoria para o lançamento bancário.");

            using var db = new AppDbContext();
            var fatura = db.FaturasCartao
                .Include(f => f.CartaoCredito)
                .FirstOrDefault(f => f.FaturaUuid == faturaUuid)
                ?? throw new InvalidOperationException("Fatura não encontrada.");

            if (fatura.EstaPaga)
                throw new InvalidOperationException("Esta fatura já está totalmente quitada.");

            decimal valorAbs = Math.Abs(valorPago);
            string nomeCartao = fatura.CartaoCredito?.Descricao ?? "Cartão de Crédito";

            // 1. Lança o débito na Conta Corrente e atualiza saldo_contas_correntes
            var lancamentoConta = new LancamentoConta
            {
                LancamentoUuid = Guid.NewGuid().ToString(),
                UsuarioUuid = fatura.UsuarioUuid,
                ContaUuid = contaCorrenteUuid,
                CategoriaUuid = categoriaPagamentoUuid,
                DataLancamento = dataPagamento.Date,
                Descricao = $"Pagamento Fatura {nomeCartao} ({fatura.MesAnoFatura})",
                Tipo = "D",
                Valor = valorAbs,
                Conciliado = true,
                DataConciliacao = dataPagamento.Date
            };

            MovimentacaoContaService.IncluirLancamento(lancamentoConta);

            // 2. Atualiza a fatura e libera o limite do cartão
            using var transaction = db.Database.BeginTransaction();

            fatura.TotalPago += valorAbs;
            fatura.SaldoFatura = Math.Max(0m, fatura.TotalFatura - fatura.TotalPago);
            fatura.StatusFatura = fatura.SaldoFatura <= 0m ? "PAGA" : "PARCIAL";
            fatura.DataPagamento = dataPagamento.Date;
            fatura.LancamentoPagamentoUuid = lancamentoConta.LancamentoUuid;
            fatura.UpdatedAt = DateTime.UtcNow;

            db.SaveChanges();

            RecalcularLimiteInterno(db, fatura.CartaoUuid);
            transaction.Commit();
        }

        /// <summary>
        /// Estorna o pagamento da fatura, removendo o débito da Conta Corrente e recompondo o saldo da fatura.
        /// </summary>
        public static void EstornarPagamentoFatura(string faturaUuid)
        {
            using var db = new AppDbContext();
            var fatura = db.FaturasCartao.FirstOrDefault(f => f.FaturaUuid == faturaUuid);
            if (fatura == null || fatura.TotalPago <= 0m) return;

            string? lancamentoContaUuid = fatura.LancamentoPagamentoUuid;

            using (var transaction = db.Database.BeginTransaction())
            {
                fatura.TotalPago = 0m;
                fatura.SaldoFatura = fatura.TotalFatura;
                fatura.StatusFatura = DateTime.Today >= fatura.DataFechamento.Date ? "FECHADA" : "ABERTA";
                fatura.DataPagamento = null;
                fatura.LancamentoPagamentoUuid = null;
                fatura.UpdatedAt = DateTime.UtcNow;

                db.SaveChanges();
                RecalcularLimiteInterno(db, fatura.CartaoUuid);
                transaction.Commit();
            }

            if (!string.IsNullOrWhiteSpace(lancamentoContaUuid))
            {
                MovimentacaoContaService.ExcluirLancamento(lancamentoContaUuid);
            }
        }

        // =====================================================================
        // MÉTODOS AUXILIARES INTERNOS
        // =====================================================================

        private static FaturaCartao ObterOuCriarFaturaInterno(AppDbContext db, CartaoCredito cartao, int ano, int mes)
        {
            string mesAno = $"{ano:D4}-{mes:D2}";
            var fatura = db.FaturasCartao.FirstOrDefault(f => f.CartaoUuid == cartao.CartaoUuid && f.MesAnoFatura == mesAno);
            if (fatura != null)
                return fatura;

            int diaFech = Math.Min(cartao.DiaFechamento, DateTime.DaysInMonth(ano, mes));
            DateTime dataFechamento = new DateTime(ano, mes, diaFech);
            DateTime dataVencimento = CalcularDataVencimento(ano, mes, cartao.DiaFechamento, cartao.DiaVencimento);

            fatura = new FaturaCartao
            {
                FaturaUuid = Guid.NewGuid().ToString(),
                UsuarioUuid = cartao.UsuarioUuid,
                CartaoUuid = cartao.CartaoUuid,
                MesAnoFatura = mesAno,
                DataFechamento = dataFechamento,
                VencimentoFatura = dataVencimento,
                TotalFatura = 0m,
                TotalPago = 0m,
                SaldoFatura = 0m,
                StatusFatura = DateTime.Today >= dataFechamento ? "FECHADA" : "ABERTA",
                CriadoEm = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.FaturasCartao.Add(fatura);
            db.SaveChanges();
            return fatura;
        }

        private static DateTime CalcularDataVencimento(int anoFechamento, int mesFechamento, int diaFechamento, int diaVencimento)
        {
            // Se o dia de vencimento for menor que o dia de fechamento (ex.: fecha dia 25, vence dia 05),
            // o vencimento ocorre no mês seguinte ao fechamento.
            DateTime refVenc = diaVencimento < diaFechamento
                ? new DateTime(anoFechamento, mesFechamento, 1).AddMonths(1)
                : new DateTime(anoFechamento, mesFechamento, 1);

            int diaValido = Math.Min(diaVencimento, DateTime.DaysInMonth(refVenc.Year, refVenc.Month));
            return new DateTime(refVenc.Year, refVenc.Month, diaValido);
        }

        private static void RecalcularFaturaInterno(AppDbContext db, string faturaUuid)
        {
            var fatura = db.FaturasCartao.FirstOrDefault(f => f.FaturaUuid == faturaUuid);
            if (fatura == null) return;

            decimal somaParcelas = db.LancamentosCartao
                .AsNoTracking()
                .Where(l => l.FaturaUuid == faturaUuid)
                .ToList()
                .Sum(l => Math.Abs(l.ValorParcela));

            fatura.TotalFatura = somaParcelas;
            fatura.SaldoFatura = Math.Max(0m, fatura.TotalFatura - fatura.TotalPago);

            if (fatura.TotalFatura > 0m && fatura.SaldoFatura <= 0m)
                fatura.StatusFatura = "PAGA";
            else if (fatura.TotalPago > 0m)
                fatura.StatusFatura = "PARCIAL";
            else
                fatura.StatusFatura = DateTime.Today >= fatura.DataFechamento.Date ? "FECHADA" : "ABERTA";

            fatura.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }
    }
}