using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.System;

namespace ContaMobi.Views
{
    public sealed partial class LancamentosPage : Page
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly HashSet<string> _lancamentosConferidos = new(StringComparer.OrdinalIgnoreCase);
        private bool _formatandoData = false;

        public LancamentosPage()
        {
            this.InitializeComponent();

            TxtDataInicio.TextChanged += TxtData_TextChanged;
            TxtDataFim.TextChanged += TxtData_TextChanged;

            this.Loaded += (s, e) =>
            {
                _lancamentosConferidos.Clear();
                TransferenciaService.GarantirEstruturaTransferencia();
                CarregarComboContas();
                AtualizarDatasPorPeriodo();
                CarregarExtrato(preservarConferidos: false);
            };
        }

        private void CarregarComboContas()
        {
            using var db = new AppDbContext();
            var contas = db.ContasCorrentes
                .AsNoTracking()
                .Include(c => c.Banco)
                .OrderBy(c => c.Descricao)
                .ToList();

            CboFiltroConta.Items.Clear();
            CboFiltroConta.Items.Add(new ComboBoxItem
            {
                Content = "TODAS AS CONTAS CORRENTES",
                Tag = string.Empty,
                IsSelected = true
            });

            foreach (var c in contas)
            {
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
                if (string.IsNullOrWhiteSpace(TxtDataInicio.Text))
                    TxtDataInicio.Text = new DateTime(hoje.Year, hoje.Month, 1).ToString("dd/MM/yyyy");
                if (string.IsNullOrWhiteSpace(TxtDataFim.Text))
                    TxtDataFim.Text = hoje.ToString("dd/MM/yyyy");
            }
        }

        public void CarregarExtrato(bool preservarConferidos = true)
        {
            if (!this.IsLoaded || ListaLancamentos == null)
                return;

            if (preservarConferidos)
            {
                if (ListaLancamentos.ItemsSource is IEnumerable<LancamentoConta> listaAtual)
                {
                    foreach (var item in listaAtual)
                    {
                        if (string.IsNullOrWhiteSpace(item.LancamentoUuid))
                            continue;

                        if (item.Conferido)
                            _lancamentosConferidos.Add(item.LancamentoUuid);
                        else
                            _lancamentosConferidos.Remove(item.LancamentoUuid);
                    }
                }
            }
            else
            {
                _lancamentosConferidos.Clear();
            }

            if (!DateTime.TryParseExact(TxtDataInicio.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtIni))
            {
                dtIni = DateTime.Today;
            }

            if (!DateTime.TryParseExact(TxtDataFim.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtFim))
            {
                dtFim = dtIni;
            }

            string? contaUuid = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            string filtroDoc = (TxtFiltroDocumento?.Text ?? string.Empty).Trim();

            try
            {
                var resultado = MovimentacaoContaService.ListarLancamentosDoPeriodo(contaUuid, dtIni, dtFim);
                var listaExibicao = resultado.Lancamentos;

                if (!string.IsNullOrWhiteSpace(filtroDoc))
                {
                    listaExibicao = listaExibicao
                        .Where(l => !string.IsNullOrWhiteSpace(l.NumeroDocumento) &&
                                    l.NumeroDocumento.Contains(filtroDoc, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (_lancamentosConferidos.Count > 0)
                {
                    foreach (var item in listaExibicao)
                    {
                        if (!string.IsNullOrWhiteSpace(item.LancamentoUuid) &&
                            _lancamentosConferidos.Contains(item.LancamentoUuid))
                        {
                            item.Conferido = true;
                        }
                    }
                }

                ListaLancamentos.ItemsSource = listaExibicao;
                TxtSaldoAnterior.Text = $"Saldo Anterior..: {resultado.SaldoAnterior.ToString("C2", _cultura)}";
                TxtSaldoFinal.Text = $"Saldo Final..: {resultado.SaldoFinal.ToString("C2", _cultura)}";

                TxtResumoPeriodo.Text = $"EXIBINDO MOVIMENTACAO DE {dtIni:dd/MM/yyyy} ATE {dtFim:dd/MM/yyyy}";
                TxtTotalRegistros.Text = $"REGISTROS: {listaExibicao.Count}";
            }
            catch (Exception ex)
            {
                TxtResumoPeriodo.Text = $"ERRO AO CARREGAR EXTRATO: {ex.Message}";
            }
        }

        private void CboFiltroConta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CarregarExtrato(preservarConferidos: false);
            }
        }

        private void CboFiltroPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                AtualizarDatasPorPeriodo();
                CarregarExtrato();
            }
        }

        private void TxtFiltroDocumento_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CarregarExtrato();
            }
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) => CarregarExtrato();

        private void BtnIncluir_Click(object sender, RoutedEventArgs e)
        {
            string? contaSelecionada = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var janela = new LancamentoEditWindow(contaSelecionada);
            janela.Closed += (s, ev) => CarregarExtrato();
            UIThemeHelper.AbrirModal(janela);
        }

        private async void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not LancamentoConta lancamento)
                return;

            if (lancamento.EhTransferencia || lancamento.CategoriaUuid == TransferenciaService.CategoriaTransferenciaUuid)
            {
                var aviso = new ContentDialog
                {
                    Title = "TRANSFERENCIA ENTRE CONTAS",
                    Content = "Lancamentos de transferencia nao podem ser editados individualmente. Exclua a transferencia (DEL) e lance novamente.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await aviso.ShowAsync();
                return;
            }

            var janela = new LancamentoEditWindow(lancamento);
            janela.Closed += (s, ev) => CarregarExtrato();
            UIThemeHelper.AbrirModal(janela);
        }

        private void BtnTransferencia_Click(object sender, RoutedEventArgs e)
        {
            string? contaSelecionada = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var modal = new TransferenciaWindow(string.IsNullOrWhiteSpace(contaSelecionada) ? null : contaSelecionada);
            modal.Closed += (s, ev) =>
            {
                if (modal.TransferenciaConfirmada)
                {
                    CarregarExtrato();
                }
            };
            UIThemeHelper.AbrirModal(modal);
        }

        private async void ListaLancamentos_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F2)
            {
                BtnIncluir_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.F4)
            {
                BtnTransferencia_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Delete && ListaLancamentos.SelectedItem is LancamentoConta selecionado)
            {
                e.Handled = true;

                if (selecionado.EhTransferencia && !string.IsNullOrWhiteSpace(selecionado.TransferenciaUuid))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Excluir transferência entre contas",
                        Content = "Este lançamento faz parte de uma transferência interna.\nAo excluir, tanto a saída na conta de origem quanto a entrada na conta de destino serão removidas.\n\nDeseja continuar?",
                        PrimaryButtonText = "Sim, excluir ambos",
                        CloseButtonText = "Cancelar",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var confirmacao = await dialog.ShowAsync();
                    if (confirmacao != ContentDialogResult.Primary)
                        return;

                    using var db = new AppDbContext();
                    var idsPar = db.Set<LancamentoConta>()
                        .AsNoTracking()
                        .Where(l => l.TransferenciaUuid == selecionado.TransferenciaUuid)
                        .Select(l => l.LancamentoUuid)
                        .ToList();

                    foreach (var id in idsPar)
                    {
                        MovimentacaoContaService.ExcluirLancamento(id);
                    }
                }
                else
                {
                    MovimentacaoContaService.ExcluirLancamento(selecionado.LancamentoUuid);
                }

                CarregarExtrato();
            }
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