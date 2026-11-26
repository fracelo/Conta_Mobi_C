using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace ContaMobi.Views
{
    public sealed partial class ContasPRPage : Page
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private string _tipoModulo = "P"; // "P" = Pagar | "R" = Receber
        private bool _formatandoData = false;

        public ContasPRPage() : this("P")
        {
        }

        public ContasPRPage(string tipoModulo)
        {
            this.InitializeComponent();
            DefinirTipoModulo(tipoModulo);

            TxtDataInicio.TextChanged += TxtData_TextChanged;
            TxtDataFim.TextChanged += TxtData_TextChanged;

            this.Loaded += (s, e) =>
            {
                ContasPRBaixaService.GarantirColunasBaixa();
                AplicarTitulosModulo();
                CarregarComboContas();
                AtualizarDatasPorPeriodo();
                CarregarTitulos();
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string parametroTipo && !string.IsNullOrWhiteSpace(parametroTipo))
            {
                DefinirTipoModulo(parametroTipo);
                if (this.IsLoaded)
                {
                    AplicarTitulosModulo();
                    CarregarTitulos();
                }
            }
        }

        private void DefinirTipoModulo(string tipo)
        {
            string t = (tipo ?? "P").Trim().ToUpperInvariant();
            _tipoModulo = (t == "R" || t.Contains("RECEBER")) ? "R" : "P";
        }

        private void AplicarTitulosModulo()
        {
            if (TxtTituloPagina == null) return;

            if (_tipoModulo == "R")
            {
                TxtTituloPagina.Text = "[ CONTAS A RECEBER // GESTAO DE RECEBIVEIS ]";
                TxtSubtituloPagina.Text = "CONTROLE DE VENCIMENTOS, MULTAS, JUROS E BAIXAS DE RECEBIMENTOS";
                BtnIncluir.Content = "[+] INCLUIR A RECEBER (F2)";
            }
            else
            {
                TxtTituloPagina.Text = "[ CONTAS A PAGAR // GESTAO DE PAGAMENTOS ]";
                TxtSubtituloPagina.Text = "CONTROLE DE VENCIMENTOS, MULTAS, JUROS E BAIXAS DE CONTAS A PAGAR";
                BtnIncluir.Content = "[+] INCLUIR A PAGAR (F2)";
            }
        }

        private void CarregarComboContas()
        {
            using var db = new AppDbContext();
            var bancosMap = db.Bancos.AsNoTracking().ToDictionary(b => b.CodigoBanco, b => b);
            var contas = db.ContasCorrentes
                .AsNoTracking()
                .OrderBy(c => c.Descricao)
                .ToList();

            CboFiltroConta.Items.Clear();
            CboFiltroConta.Items.Add(new ComboBoxItem
            {
                Content = "TODAS AS CONTAS",
                Tag = string.Empty,
                IsSelected = true
            });

            foreach (var c in contas)
            {
                if (bancosMap.TryGetValue(c.CodigoBanco, out var b))
                    c.Banco = b;

                CboFiltroConta.Items.Add(new ComboBoxItem
                {
                    Content = $"{c.Descricao} ({c.BancoFormatado})",
                    Tag = c.ContaUuid
                });
            }
        }

        private void AtualizarDatasPorPeriodo()
        {
            if (TxtDataInicio == null || TxtDataFim == null || CboFiltroPeriodo == null)
                return;

            string tagPeriodo = (CboFiltroPeriodo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MES";
            DateTime hoje = DateTime.Today;

            if (tagPeriodo == "HOJE")
            {
                TxtDataInicio.IsEnabled = false;
                TxtDataFim.IsEnabled = false;
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = hoje.ToString("dd/MM/yyyy");
            }
            else if (tagPeriodo == "SEMANA")
            {
                int diff = (7 + (hoje.DayOfWeek - DayOfWeek.Sunday)) % 7;
                DateTime inicioSemana = hoje.AddDays(-1 * diff);
                DateTime fimSemana = inicioSemana.AddDays(6);

                TxtDataInicio.IsEnabled = false;
                TxtDataFim.IsEnabled = false;
                TxtDataInicio.Text = inicioSemana.ToString("dd/MM/yyyy");
                TxtDataFim.Text = fimSemana.ToString("dd/MM/yyyy");
            }
            else if (tagPeriodo == "MES")
            {
                DateTime inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
                DateTime fimMes = inicioMes.AddMonths(1).AddDays(-1);

                TxtDataInicio.IsEnabled = false;
                TxtDataFim.IsEnabled = false;
                TxtDataInicio.Text = inicioMes.ToString("dd/MM/yyyy");
                TxtDataFim.Text = fimMes.ToString("dd/MM/yyyy");
            }
            else
            {
                TxtDataInicio.IsEnabled = true;
                TxtDataFim.IsEnabled = true;
            }
        }

        public void CarregarTitulos()
        {
            if (!this.IsLoaded || ListaContasPR == null)
                return;

            if (!DateTime.TryParseExact(TxtDataInicio.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtIni))
                dtIni = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            if (!DateTime.TryParseExact(TxtDataFim.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtFim))
                dtFim = dtIni.AddMonths(1).AddDays(-1);

            string? contaUuid = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            string status = (CboFiltroStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODOS";
            string busca = TxtBusca.Text.Trim();

            try
            {
                var resumo = ContasPRService.ListarContas(_tipoModulo, dtIni, dtFim, status, contaUuid, busca);

                ListaContasPR.ItemsSource = resumo.Registros;
                TxtTotalRegistros.Text = $"REGISTROS: {resumo.Registros.Count}";

                string rotuloQuitado = _tipoModulo == "R" ? "Total Recebido" : "Total Pago";
                TxtTotalEmAberto.Text = $"Em Aberto..: {resumo.TotalEmAberto.ToString("C2", _cultura)}";
                TxtTotalQuitado.Text = $"{rotuloQuitado}..: {resumo.TotalQuitado.ToString("C2", _cultura)}";
            }
            catch (Exception ex)
            {
                TxtTotalRegistros.Text = $"ERRO: {ex.Message}";
            }
        }

        private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) CarregarTitulos();
        }

        private void CboFiltroPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                AtualizarDatasPorPeriodo();
                CarregarTitulos();
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded) CarregarTitulos();
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) => CarregarTitulos();

        private void BtnIncluir_Click(object sender, RoutedEventArgs e)
        {
            string? contaPreSelecionada = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            var novo = new ContaPR
            {
                ContasPRUuid = Guid.NewGuid().ToString(),
                Tipo = _tipoModulo,
                ContaUuid = string.IsNullOrWhiteSpace(contaPreSelecionada) ? null : contaPreSelecionada,
                DataLancamento = DateTime.Today,
                DataVencimento = DateTime.Today
            };

            AbrirEditor(novo);
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContaPR conta)
            {
                AbrirEditor(conta);
            }
        }

        private void BtnBaixarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContaPR conta)
            {
                var modalBaixa = new ContasPRBaixaWindow(conta.ContasPRUuid);
                modalBaixa.Closed += (s, ev) =>
                {
                    if (modalBaixa.BaixaConfirmada)
                    {
                        CarregarTitulos();
                    }
                };
                UIThemeHelper.AbrirModal(modalBaixa);
            }
        }

        private async void BtnExcluirLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContaPR conta)
            {
                await ConfirmarEExcluirContaPRAsync(conta);
            }
        }

        private void ListaContasPR_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaContasPR.SelectedItem is ContaPR selecionado)
            {
                AbrirEditor(selecionado);
            }
        }

        private async void ListaContasPR_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F2)
            {
                BtnIncluir_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter && ListaContasPR.SelectedItem is ContaPR selecionadoEditar)
            {
                AbrirEditor(selecionadoEditar);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Delete && ListaContasPR.SelectedItem is ContaPR selecionadoExcluir)
            {
                e.Handled = true;
                await ConfirmarEExcluirContaPRAsync(selecionadoExcluir);
            }
        }

        private async Task ConfirmarEExcluirContaPRAsync(ContaPR conta)
        {
            try
            {
                var futuras = ContasPRService.ObterParcelasFuturasRelacionadas(conta.ContasPRUuid);
                string avisoQuitado = conta.EstaQuitado
                    ? "\n\n[!] Esta parcela já está baixada: o lançamento correspondente na Conta Corrente também será estornado."
                    : string.Empty;

                if (futuras.Count > 0)
                {
                    int quitadasFuturas = futuras.Count(f => f.EstaQuitado);
                    string avisoQuitadasFuturas = quitadasFuturas > 0
                        ? $"\n[!] Atenção: {quitadasFuturas} parcela(s) futura(s) já está(ão) baixada(s) e também terão seus lançamentos estornados."
                        : string.Empty;

                    var dialog = new ContentDialog
                    {
                        Title = "Excluir parcela vinculada",
                        Content = $"A conta \"{conta.Descricao}\" ({conta.ParcelaFormatada} - Venc. {conta.DataVencimentoFormatada}) possui {futuras.Count} parcela(s) futura(s) relacionada(s).\n\n" +
                                  "O que deseja apagar?\n" +
                                  "• Somente esta: apaga apenas a parcela selecionada.\n" +
                                  $"• Esta e as futuras ({1 + futuras.Count} no total): apaga a parcela selecionada e todas as parcelas seguintes.{avisoQuitado}{avisoQuitadasFuturas}",
                        PrimaryButtonText = "Somente esta",
                        SecondaryButtonText = $"Esta e as {futuras.Count} futuras",
                        CloseButtonText = "Cancelar",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var resultado = await dialog.ShowAsync();
                    if (resultado == ContentDialogResult.None)
                        return;

                    bool excluirTodasFuturas = resultado == ContentDialogResult.Secondary;
                    ContasPRService.ExcluirContaPR(conta.ContasPRUuid, excluirTodasFuturas);
                    CarregarTitulos();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Confirmar exclusão",
                        Content = $"Deseja realmente apagar a conta \"{conta.Descricao}\" (Parc. {conta.ParcelaFormatada} - Venc. {conta.DataVencimentoFormatada} - {conta.ValorFormatado})?{avisoQuitado}",
                        PrimaryButtonText = "Sim, apagar",
                        CloseButtonText = "Cancelar",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var resultado = await dialog.ShowAsync();
                    if (resultado != ContentDialogResult.Primary)
                        return;

                    ContasPRService.ExcluirContaPR(conta.ContasPRUuid, excluirFuturas: false);
                    CarregarTitulos();
                }
            }
            catch (Exception ex)
            {
                TxtTotalRegistros.Text = $"ERRO AO EXCLUIR: {ex.Message}";
            }
        }

        private void AbrirEditor(ContaPR contaPR)
        {
            var janela = new ContaPREditWindow(contaPR);
            janela.Closed += (s, ev) => CarregarTitulos();
            UIThemeHelper.AbrirModal(janela);
        }

        private void TxtData_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatandoData || sender is not TextBox tb) return;

            _formatandoData = true;
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
            _formatandoData = false;
        }
    }
}