using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ContaMobi.Data;
using ContaMobi.Models;
using ContaMobi.Views;
using System;
using System.Globalization;
using System.Linq;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi
{
    public sealed partial class MainWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private bool _telaInicializada = false;
        private bool _formatandoDataGrafico = false;

        public MainWindow()
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(1180, 740));

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            this.Closed += (s, e) => Environment.Exit(0);

            // Captura erros de navegação caso o XAML da página falhe
            MainContentFrame.NavigationFailed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO NAVEGACAO FRAME]: {e.Exception?.Message}");
                if (TxtStatusBanco != null)
                {
                    TxtStatusBanco.Text = $"> [ERRO NAVEGACAO]: {e.Exception?.Message}";
                }
                e.Handled = true;
            };

            TxtDataSistema.Text = $"DATA: {DateTime.Now:dd/MM/yyyy}";

            AtualizarDatasFiltroGraficos();
            _telaInicializada = true;

            CarregarResumoInicial();

            // Sincroniza a tabela de bancos do Supabase em segundo plano se o SQLite estiver vazio
            _ = ContaMobi.Services.BancoSyncService.SincronizarBancosSeVazioAsync();
        }

        private void CarregarResumoInicial()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    int totalBancos = db.Bancos.Count();
                    int totalCategorias = db.Categorias.Count();
                    var contas = db.ContasCorrentes.AsNoTracking().ToList();
                    int totalContas = contas.Count;

                    TxtStatusBanco.Text = $"> [OK] SQLite conectado! Bancos: {totalBancos} | Categorias: {totalCategorias} | Contas: {totalContas}";

                    // 1. Calcula o Saldo Consolidado em Contas (Saldo Inicial + Créditos - Débitos até hoje)
                    decimal saldoInicialTotal = contas.Sum(c => c.SaldoInicial);
                    DateTime hojeFim = DateTime.Today.AddDays(1).AddTicks(-1);

                    var lancamentosAteHoje = db.Set<LancamentoConta>()
                        .AsNoTracking()
                        .Where(l => l.DataLancamento <= hojeFim)
                        .Select(l => new { l.Tipo, l.Valor })
                        .ToList();

                    decimal totalCreditos = lancamentosAteHoje
                        .Where(l => l.Tipo == "C")
                        .Sum(l => Math.Abs(l.Valor));

                    decimal totalDebitos = lancamentosAteHoje
                        .Where(l => l.Tipo == "D")
                        .Sum(l => Math.Abs(l.Valor));

                    decimal saldoConsolidado = saldoInicialTotal + totalCreditos - totalDebitos;

                    if (TxtSaldoTotal != null)
                    {
                        TxtSaldoTotal.Text = saldoConsolidado.ToString("C2", _cultura);
                        TxtSaldoTotal.Foreground = saldoConsolidado >= 0m
                            ? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
                            : new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
                    }

                    // 2. Carrega o resumo do orçamento do mês atual (Planejado x Realizado)
                    int anoAtual = DateTime.Today.Year;
                    int mesAtual = DateTime.Today.Month;

                    var (creditos, debitos) = OrcamentoService.ObterItensEdicaoMes(anoAtual, mesAtual);

                    decimal totalPlanCred = creditos.Sum(c => c.ValorPlanejado);
                    decimal totalRealCred = creditos.Sum(c => c.ValorRealizado);
                    decimal totalPlanDeb = debitos.Sum(d => d.ValorPlanejado);
                    decimal totalRealDeb = debitos.Sum(d => d.ValorRealizado);

                    decimal saldoPlanMes = totalPlanCred - totalPlanDeb;
                    decimal saldoRealMes = totalRealCred - totalRealDeb;

                    if (TxtOrcamentoMesResumo != null)
                    {
                        TxtOrcamentoMesResumo.Text =
                            $"Plan: {saldoPlanMes.ToString("C2", _cultura)}\n" +
                            $"Real: {saldoRealMes.ToString("C2", _cultura)}";
                    }
                }

                // 3. Atualiza os totais de Receitas/Despesas do período e todos os gráficos do Dashboard
                CarregarGraficosMovimentacao();
                CarregarGraficosOrcamentoAno();
                CarregarGraficoSaldoContas();
            }
            catch (Exception ex)
            {
                TxtStatusBanco.Text = $"> [ALERTA] Erro ao ler banco local: {ex.Message}";
                if (TxtSaldoTotal != null) TxtSaldoTotal.Text = "R$ 0,00";
                if (TxtReceitasMes != null) TxtReceitasMes.Text = "R$ 0,00";
                if (TxtDespesasMes != null) TxtDespesasMes.Text = "R$ 0,00";
                if (TxtOrcamentoMesResumo != null)
                {
                    TxtOrcamentoMesResumo.Text = "Plan: R$ 0,00\nReal: R$ 0,00";
                }
            }
        }

        #region Gráficos Pizza do Dashboard

        private void AtualizarDatasFiltroGraficos()
        {
            if (CboPeriodoGraficos == null || TxtDataIniGrafico == null || TxtDataFimGrafico == null)
                return;

            string tag = (CboPeriodoGraficos.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MES_ATUAL";
            DateTime hoje = DateTime.Today;

            _formatandoDataGrafico = true;

            if (tag == "MES_ATUAL")
            {
                DateTime ini = new DateTime(hoje.Year, hoje.Month, 1);
                DateTime fim = ini.AddMonths(1).AddDays(-1);
                TxtDataIniGrafico.IsEnabled = false;
                TxtDataFimGrafico.IsEnabled = false;
                TxtDataIniGrafico.Text = ini.ToString("dd/MM/yyyy");
                TxtDataFimGrafico.Text = fim.ToString("dd/MM/yyyy");
            }
            else if (tag == "MES_PASSADO")
            {
                DateTime mesAnterior = hoje.AddMonths(-1);
                DateTime ini = new DateTime(mesAnterior.Year, mesAnterior.Month, 1);
                DateTime fim = ini.AddMonths(1).AddDays(-1);
                TxtDataIniGrafico.IsEnabled = false;
                TxtDataFimGrafico.IsEnabled = false;
                TxtDataIniGrafico.Text = ini.ToString("dd/MM/yyyy");
                TxtDataFimGrafico.Text = fim.ToString("dd/MM/yyyy");
            }
            else
            {
                TxtDataIniGrafico.IsEnabled = true;
                TxtDataFimGrafico.IsEnabled = true;
                if (string.IsNullOrWhiteSpace(TxtDataIniGrafico.Text))
                    TxtDataIniGrafico.Text = new DateTime(hoje.Year, hoje.Month, 1).ToString("dd/MM/yyyy");
                if (string.IsNullOrWhiteSpace(TxtDataFimGrafico.Text))
                    TxtDataFimGrafico.Text = hoje.ToString("dd/MM/yyyy");
            }

            _formatandoDataGrafico = false;
        }

        private void CarregarGraficosMovimentacao()
        {
            if (GraficoEntradas == null || GraficoSaidas == null)
                return;

            if (!DateTime.TryParseExact(TxtDataIniGrafico.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtIni))
                dtIni = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            if (!DateTime.TryParseExact(TxtDataFimGrafico.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtFim))
                dtFim = dtIni.AddMonths(1).AddDays(-1);

            if (dtFim < dtIni)
                dtFim = dtIni;

            TxtPeriodoGraficoInfo.Text = $"Exibindo lançamentos de {dtIni:dd/MM/yyyy} até {dtFim:dd/MM/yyyy}";

            var entradas = DashboardGraficosService.ObterMovimentacaoPorCategoria("C", dtIni, dtFim);
            var saidas = DashboardGraficosService.ObterMovimentacaoPorCategoria("D", dtIni, dtFim);

            // Atualiza os cards de Receitas e Despesas do período selecionado
            decimal totalReceitasPeriodo = entradas.Sum(e => e.ValorTotal);
            decimal totalDespesasPeriodo = saidas.Sum(s => s.ValorTotal);

            if (TxtReceitasMes != null)
            {
                TxtReceitasMes.Text = totalReceitasPeriodo.ToString("C2", _cultura);
                TxtReceitasMes.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
            }

            if (TxtDespesasMes != null)
            {
                TxtDespesasMes.Text = totalDespesasPeriodo.ToString("C2", _cultura);
                TxtDespesasMes.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
            }

            GraficoEntradas.ConfigurarHierarquico(
                "ENTRADAS (RECEITAS)",
                Color.FromArgb(255, 51, 153, 255),
                entradas);

            GraficoSaidas.ConfigurarHierarquico(
                "SAÍDAS (DESPESAS)",
                Color.FromArgb(255, 255, 85, 85),
                saidas);
        }

        private void CarregarGraficosOrcamentoAno()
        {
            if (GraficoOrcamentoPlanejadoReceitas == null ||
                GraficoOrcamentoPlanejadoDespesas == null ||
                GraficoOrcamentoRealizadoReceitas == null ||
                GraficoOrcamentoRealizadoDespesas == null)
                return;

            int anoAtual = DateTime.Today.Year;
            TxtTituloOrcamentoPlanejadoAno.Text = $"[ ORÇAMENTO PLANEJADO // ANO {anoAtual} ]";
            TxtTituloOrcamentoRealizadoAno.Text = $"[ ORÇAMENTO REALIZADO // ANO {anoAtual} ]";

            var planReceitas = DashboardGraficosService.ObterOrcamentoPlanejadoAnoCorrente("C");
            var planDespesas = DashboardGraficosService.ObterOrcamentoPlanejadoAnoCorrente("D");
            var realReceitas = DashboardGraficosService.ObterOrcamentoRealizadoAnoCorrente("C");
            var realDespesas = DashboardGraficosService.ObterOrcamentoRealizadoAnoCorrente("D");

            GraficoOrcamentoPlanejadoReceitas.ConfigurarHierarquico(
                $"RECEITAS PLANEJADAS ({anoAtual})",
                Color.FromArgb(255, 51, 153, 255),
                planReceitas);

            GraficoOrcamentoPlanejadoDespesas.ConfigurarHierarquico(
                $"DESPESAS PLANEJADAS ({anoAtual})",
                Color.FromArgb(255, 255, 85, 85),
                planDespesas);

            GraficoOrcamentoRealizadoReceitas.ConfigurarHierarquico(
                $"RECEITAS REALIZADAS ({anoAtual})",
                Color.FromArgb(255, 0, 255, 102),
                realReceitas);

            GraficoOrcamentoRealizadoDespesas.ConfigurarHierarquico(
                $"DESPESAS REALIZADAS ({anoAtual})",
                Color.FromArgb(255, 255, 112, 67),
                realDespesas);
        }

        private void CarregarGraficoSaldoContas()
        {
            if (GraficoSaldoContas == null)
                return;

            var saldosContas = DashboardGraficosService.ObterSaldoPorContaCorrente();

            GraficoSaldoContas.ConfigurarHierarquico(
                "SALDO POR CONTA CORRENTE",
                Color.FromArgb(255, 0, 255, 102),
                saldosContas);
        }

        private void CboPeriodoGraficos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_telaInicializada) return;

            AtualizarDatasFiltroGraficos();

            string tag = (CboPeriodoGraficos.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MES_ATUAL";
            if (tag != "PERSONALIZADO")
            {
                CarregarGraficosMovimentacao();
            }
        }

        private void BtnFiltrarGraficos_Click(object sender, RoutedEventArgs e)
        {
            CarregarGraficosMovimentacao();
        }

        private void TxtDataGrafico_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatandoDataGrafico || sender is not TextBox tb) return;

            _formatandoDataGrafico = true;
            string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (digitos.Length > 8) digitos = digitos.Substring(0, 8);

            string formatada = digitos.Length switch
            {
                <= 2 => digitos,
                <= 4 => $"{digitos[..2]}/{digitos[2..]}",
                _ => $"{digitos[..2]}/{digitos[2..4]}/{digitos[4..]}"
            };

            if (tb.Text != formatada)
            {
                tb.Text = formatada;
                tb.SelectionStart = formatada.Length;
            }
            _formatandoDataGrafico = false;
        }

        #endregion

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string modulo)
            {
                modulo = modulo.Trim().ToUpperInvariant();
                TxtModuloAtual.Text = $"> MODULO: {modulo.Replace('_', ' ')}";

                if (modulo == "HOME" || modulo == "INICIO")
                {
                    MainContentFrame.Visibility = Visibility.Collapsed;
                    PainelDashboard.Visibility = Visibility.Visible;
                    CarregarResumoInicial();
                }
                else if (modulo == "CAD_BANCOS" || modulo == "BANCOS")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(BancosPage));
                }
                else if (modulo == "CAD_CONTAS" || modulo == "CONTAS" || modulo == "CONTAS_CORRENTES" || modulo == "CONTASCORRENTES")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(ContasCorrentesPage));
                }
                else if (modulo == "CAD_CATEGORIAS" || modulo == "CATEGORIAS")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(CategoriasPage));
                }
                else if (modulo == "CAD_ENTIDADES" || modulo == "ENTIDADES" || modulo == "FAVORECIDOS")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(EntidadesPage));
                }
                else if (modulo == "MOV_LANCAMENTOS" || modulo == "LANCAMENTOS")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(LancamentosPage));
                }
                else if (modulo == "MOV_PAGAR" || modulo == "PAGAR")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(ContasPRPage), "P");
                }
                else if (modulo == "MOV_RECEBER" || modulo == "RECEBER")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(ContasPRPage), "R");
                }
                else if (modulo == "ORCAMENTO" || modulo == "ORCAMENTOS")
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                    MainContentFrame.Navigate(typeof(OrcamentosPage));
                }
                else
                {
                    PainelDashboard.Visibility = Visibility.Collapsed;
                    MainContentFrame.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnSair_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}