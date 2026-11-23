using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ContaMobi.Views
{
    public sealed partial class OrcamentosPage : Page
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly ObservableCollection<OrcamentoMesResumo> _lista12Meses = new();
        private bool _carregandoFiltros = true;

        public OrcamentosPage()
        {
            this.InitializeComponent();
            OrcamentoService.GarantirTabelaCriada();

            ListaOrcamentos.ItemsSource = _lista12Meses;

            DefinirFiltroMesAtual();
            _carregandoFiltros = false;

            RecarregarGrade12Meses();
        }

        private void DefinirFiltroMesAtual()
        {
            _carregandoFiltros = true;
            DateTime hoje = DateTime.Today;
            CboMesInicial.SelectedIndex = hoje.Month - 1;
            TxtAnoInicial.Text = hoje.Year.ToString();
            _carregandoFiltros = false;
        }

        private (int Ano, int Mes) ObterPeriodoInicialFiltro()
        {
            int mes = DateTime.Today.Month;
            if (CboMesInicial?.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int m) && m >= 1 && m <= 12)
            {
                mes = m;
            }

            if (!int.TryParse(TxtAnoInicial?.Text?.Trim(), out int ano) || ano < 2000 || ano > 2100)
            {
                ano = DateTime.Today.Year;
            }

            return (ano, mes);
        }

        public void RecarregarGrade12Meses()
        {
            if (_carregandoFiltros) return;

            var (ano, mes) = ObterPeriodoInicialFiltro();
            string? termoBusca = TxtBusca?.Text;

            var itens = OrcamentoService.ObterLista12Meses(ano, mes, termoBusca);

            _lista12Meses.Clear();
            foreach (var item in itens)
            {
                _lista12Meses.Add(item);
            }

            AtualizarRodapeAcumulado();
        }

        private void AtualizarRodapeAcumulado()
        {
            decimal totalCred = _lista12Meses.Sum(x => x.TotalCreditos);
            decimal totalDeb = _lista12Meses.Sum(x => x.TotalDebitos);
            decimal saldoUltimoMes = _lista12Meses.LastOrDefault()?.SaldoFinal ?? 0m;

            TxtSomaCreditos12M.Text = $"Créditos 12M: {totalCred.ToString("C2", _cultura)}";
            TxtSomaDebitos12M.Text = $"Débitos 12M: {totalDeb.ToString("C2", _cultura)}";
            TxtSaldoFinal12M.Text = $"Saldo Final 12º Mês: {saldoUltimoMes.ToString("C2", _cultura)}";
        }

        private void FiltroPeriodo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_carregandoFiltros) return;
            RecarregarGrade12Meses();
        }

        private void TxtAnoInicial_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoFiltros) return;
            string digitos = new string(TxtAnoInicial.Text.Where(char.IsDigit).ToArray());
            if (TxtAnoInicial.Text != digitos)
            {
                TxtAnoInicial.Text = digitos;
                TxtAnoInicial.SelectionStart = digitos.Length;
            }

            if (digitos.Length == 4)
            {
                RecarregarGrade12Meses();
            }
        }

        private void TxtAnoInicial_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtAnoInicial.Text.Trim(), out int ano) || ano < 2000 || ano > 2100)
            {
                _carregandoFiltros = true;
                TxtAnoInicial.Text = DateTime.Today.Year.ToString();
                _carregandoFiltros = false;
            }
            RecarregarGrade12Meses();
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecarregarGrade12Meses();
        }

        private void BtnPeriodoAtual_Click(object sender, RoutedEventArgs e)
        {
            DefinirFiltroMesAtual();
            RecarregarGrade12Meses();
        }

        private void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            RecarregarGrade12Meses();
        }

        private void BtnExportarCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (ano, mes) = ObterPeriodoInicialFiltro();
                string caminho = OrcamentoService.GerarE_SalvarCsv12Meses(ano, mes);

                // Abre o arquivo gerado automaticamente com o aplicativo padrão (Excel / Bloco de Notas)
                Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERRO EXPORTAR CSV]: {ex.Message}");
            }
        }

        private void BtnIncluirOrcamento_Click(object sender, RoutedEventArgs e)
        {
            var primeiroLivre = _lista12Meses.FirstOrDefault(x => !x.PossuiLancamentosOrcados);
            int anoSugerido = primeiroLivre?.Ano ?? DateTime.Today.Year;
            int mesSugerido = primeiroLivre?.Mes ?? DateTime.Today.Month;

            AbrirModalOrcamento(anoSugerido, mesSugerido, modoInclusao: true);
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrcamentoMesResumo resumo)
            {
                AbrirModalOrcamento(resumo.Ano, resumo.Mes, modoInclusao: false);
            }
        }

        private void ListaOrcamentos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaOrcamentos.SelectedItem is OrcamentoMesResumo resumo)
            {
                AbrirModalOrcamento(resumo.Ano, resumo.Mes, modoInclusao: false);
            }
        }

        private void AbrirModalOrcamento(int ano, int mes, bool modoInclusao)
        {
            var modal = new OrcamentoEditWindow(ano, mes, modoInclusao);
            modal.Activate();

            modal.Closed += (s, args) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    RecarregarGrade12Meses();
                });
            };
        }
    }
}