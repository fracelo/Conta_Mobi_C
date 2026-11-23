using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class OrcamentoEditWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");

        private readonly List<OrcamentoCategoriaItem> _todosCreditos = new();
        private readonly List<OrcamentoCategoriaItem> _todosDebitos = new();

        private readonly ObservableCollection<OrcamentoCategoriaItem> _itensCreditos = new();
        private readonly ObservableCollection<OrcamentoCategoriaItem> _itensDebitos = new();

        private int _anoAtual;
        private int _mesAtual;
        private readonly bool _modoInclusao;

        private decimal _saldoAnteriorPlanejado = 0m;
        private decimal _saldoAnteriorRealizado = 0m;

        private bool _carregandoTela = true;
        private bool _editandoCelula = false;

        public OrcamentoEditWindow(int ano, int mes, bool modoInclusao = false)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(1060, 840));

            _anoAtual = ano;
            _mesAtual = Math.Clamp(mes, 1, 12);
            _modoInclusao = modoInclusao;

            ListaCreditos.ItemsSource = _itensCreditos;
            ListaDebitos.ItemsSource = _itensDebitos;

            CboMes.SelectedIndex = _mesAtual - 1;
            TxtAno.Text = _anoAtual.ToString();

            CboMes.IsEnabled = _modoInclusao;
            TxtAno.IsEnabled = _modoInclusao;

            CarregarDadosDoMesAno(_anoAtual, _mesAtual);

            _carregandoTela = false;
        }

        private void CarregarDadosDoMesAno(int ano, int mes)
        {
            _anoAtual = ano;
            _mesAtual = mes;

            string nomeMes = new DateTime(ano, mes, 1).ToString("MMMM", _cultura).ToUpperInvariant();
            TxtIdentificacaoMesAno.Text = $"COMPETÊNCIA: {mes:D2}/{ano:D4} ({nomeMes})";

            TxtTituloModal.Text = _modoInclusao
                ? $"[ INCLUIR ORÇAMENTO // {mes:D2}/{ano:D4} ]"
                : $"[ EDITANDO ORÇAMENTO // {mes:D2}/{ano:D4} ]";

            var saldosAnt = OrcamentoService.ObterSaldosAnterioresDoMes(ano, mes);
            _saldoAnteriorPlanejado = saldosAnt.SaldoAnteriorPlanejado;
            _saldoAnteriorRealizado = saldosAnt.SaldoAnteriorRealizado;

            var (creditos, debitos) = OrcamentoService.ObterItensEdicaoMes(ano, mes);

            _todosCreditos.Clear();
            _todosCreditos.AddRange(creditos);

            _todosDebitos.Clear();
            _todosDebitos.AddRange(debitos);

            AtualizarSubtotaisNaTela(_todosCreditos);
            AtualizarSubtotaisNaTela(_todosDebitos);
            AtualizarListasVisiveis();
            RecalcularSomatoriosETotais();
        }

        private static void AtualizarSubtotaisNaTela(List<OrcamentoCategoriaItem> listaTela)
        {
            var filhosPorPai = listaTela
                .Where(x => !x.EhCategoriaPai && !string.IsNullOrWhiteSpace(x.PaiUuid))
                .GroupBy(x => x.PaiUuid!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var pai in listaTela.Where(x => x.EhCategoriaPai))
            {
                if (filhosPorPai.TryGetValue(pai.CategoriaUuid.Trim(), out var filhas) && filhas.Count > 0)
                {
                    pai.TemFilhas = true;
                    pai.SubtotalPlanejadoPai = filhas.Sum(f => f.ValorPlanejado);
                    pai.SubtotalRealizadoPai = filhas.Sum(f => f.ValorRealizado);
                }
                else
                {
                    pai.TemFilhas = false;
                    pai.SubtotalPlanejadoPai = pai.ValorPlanejado;
                    pai.SubtotalRealizadoPai = pai.ValorRealizado;
                }
            }
        }

        private static IEnumerable<OrcamentoCategoriaItem> ItensQueEntramNoTotal(List<OrcamentoCategoriaItem> listaTela)
        {
            return listaTela.Where(x => !(x.EhCategoriaPai && x.TemFilhas));
        }

        private void AtualizarListasVisiveis()
        {
            _itensCreditos.Clear();
            foreach (var item in FiltrarVisiveis(_todosCreditos))
                _itensCreditos.Add(item);

            _itensDebitos.Clear();
            foreach (var item in FiltrarVisiveis(_todosDebitos))
                _itensDebitos.Add(item);
        }

        private static IEnumerable<OrcamentoCategoriaItem> FiltrarVisiveis(List<OrcamentoCategoriaItem> listaTela)
        {
            var paisRecolhidos = listaTela
                .Where(x => x.EhCategoriaPai && !x.EstaExpandida)
                .Select(x => x.CategoriaUuid.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in listaTela)
            {
                if (!item.EhCategoriaPai &&
                    !string.IsNullOrWhiteSpace(item.PaiUuid) &&
                    paisRecolhidos.Contains(item.PaiUuid.Trim()))
                {
                    continue;
                }

                yield return item;
            }
        }

        private void BtnAlternarGrupoCredito_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrcamentoCategoriaItem pai && pai.EhCategoriaPai && pai.TemFilhas)
            {
                pai.EstaExpandida = !pai.EstaExpandida;
                AtualizarListasVisiveis();
            }
        }

        private void BtnAlternarGrupoDebito_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrcamentoCategoriaItem pai && pai.EhCategoriaPai && pai.TemFilhas)
            {
                pai.EstaExpandida = !pai.EstaExpandida;
                AtualizarListasVisiveis();
            }
        }

        private void PeriodoChave_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_carregandoTela) return;
            AtualizarPeriodoDigitado();
        }

        private void TxtAno_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoTela) return;

            string digitos = new string(TxtAno.Text.Where(char.IsDigit).ToArray());
            if (TxtAno.Text != digitos)
            {
                TxtAno.Text = digitos;
                TxtAno.SelectionStart = digitos.Length;
            }

            if (digitos.Length == 4)
            {
                AtualizarPeriodoDigitado();
            }
        }

        private void AtualizarPeriodoDigitado()
        {
            int mes = (CboMes.SelectedIndex >= 0) ? CboMes.SelectedIndex + 1 : _mesAtual;
            if (!int.TryParse(TxtAno.Text.Trim(), out int ano) || ano < 2000 || ano > 2100)
                return;

            TxtMensagem.Visibility = Visibility.Collapsed;

            if (_modoInclusao && OrcamentoService.MesAnoJaExiste(ano, mes))
            {
                ExibirMensagem($"> [ATENÇÃO] O orçamento de {mes:D2}/{ano:D4} já existe! Escolha outro Mês/Ano ou edite-o na listagem.", true);
            }

            CarregarDadosDoMesAno(ano, mes);
        }

        private void TxtValorPlanejadoGrid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoTela || _editandoCelula || sender is not TextBox tb) return;
            if (tb.Tag is not OrcamentoCategoriaItem item) return;

            _editandoCelula = true;
            FormatarMoeda(tb, out decimal valor);

            item.ValorPlanejado = valor;
            item.ValorPlanejadoTexto = tb.Text;

            _editandoCelula = false;

            AtualizarSubtotaisNaTela(_todosCreditos);
            AtualizarSubtotaisNaTela(_todosDebitos);
            RecalcularSomatoriosETotais();
        }

        private void TxtValorRealizadoGrid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoTela || _editandoCelula || sender is not TextBox tb) return;
            if (tb.Tag is not OrcamentoCategoriaItem item) return;

            _editandoCelula = true;
            FormatarMoeda(tb, out decimal valor);

            item.ValorRealizado = valor;
            item.ValorRealizadoTexto = tb.Text;

            _editandoCelula = false;

            AtualizarSubtotaisNaTela(_todosCreditos);
            AtualizarSubtotaisNaTela(_todosDebitos);
            RecalcularSomatoriosETotais();
        }

        private void FormatarMoeda(TextBox tb, out decimal valorDecimal)
        {
            string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
            valorDecimal = 0m;

            if (!string.IsNullOrEmpty(digitos) && decimal.TryParse(digitos, out decimal centavos))
            {
                valorDecimal = centavos / 100m;
                tb.Text = valorDecimal.ToString("N2", _cultura);
                tb.SelectionStart = tb.Text.Length;
            }
            else
            {
                tb.Text = "0,00";
                tb.SelectionStart = tb.Text.Length;
            }
        }

        private void RecalcularSomatoriosETotais()
        {
            decimal somaCredPlan = ItensQueEntramNoTotal(_todosCreditos).Sum(x => x.ValorPlanejado);
            decimal somaCredReal = ItensQueEntramNoTotal(_todosCreditos).Sum(x => x.ValorRealizado);

            decimal somaDebPlan = ItensQueEntramNoTotal(_todosDebitos).Sum(x => x.ValorPlanejado);
            decimal somaDebReal = ItensQueEntramNoTotal(_todosDebitos).Sum(x => x.ValorRealizado);

            decimal percCredTotal = somaCredPlan > 0 ? (somaCredReal / somaCredPlan) * 100m : 0m;
            decimal percDebTotal = somaDebPlan > 0 ? (somaDebReal / somaDebPlan) * 100m : 0m;

            decimal saldoFinalPlan = _saldoAnteriorPlanejado + somaCredPlan - somaDebPlan;
            decimal saldoFinalReal = _saldoAnteriorRealizado + somaCredReal - somaDebReal;

            TxtTotalCreditosPlanejado.Text = somaCredPlan.ToString("C2", _cultura);
            TxtTotalCreditosRealizado.Text = somaCredReal.ToString("C2", _cultura);
            TxtPercentualCreditosTotal.Text = $"{percCredTotal:N1}%";

            TxtSaldoAnteriorPlanejado.Text = _saldoAnteriorPlanejado.ToString("C2", _cultura);
            TxtSaldoAnteriorRealizado.Text = _saldoAnteriorRealizado.ToString("C2", _cultura);
            TxtSaldoAnteriorPlanejado.Foreground = CorPorSinal(_saldoAnteriorPlanejado);
            TxtSaldoAnteriorRealizado.Foreground = CorPorSinal(_saldoAnteriorRealizado);

            TxtTotalDebitosPlanejado.Text = somaDebPlan.ToString("C2", _cultura);
            TxtTotalDebitosRealizado.Text = somaDebReal.ToString("C2", _cultura);
            TxtPercentualDebitosTotal.Text = $"{percDebTotal:N1}%";
            TxtPercentualDebitosTotal.Foreground = somaDebReal > somaDebPlan
                ? new SolidColorBrush(Color.FromArgb(255, 255, 85, 85))
                : new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));

            TxtSaldoFinalMesPlanejado.Text = $"Planejado: {saldoFinalPlan.ToString("C2", _cultura)}";
            TxtSaldoFinalMesRealizado.Text = $"Realizado: {saldoFinalReal.ToString("C2", _cultura)}";

            TxtSaldoFinalMesPlanejado.Foreground = CorPorSinal(saldoFinalPlan);
            TxtSaldoFinalMesRealizado.Foreground = CorPorSinal(saldoFinalReal);
        }

        private SolidColorBrush CorPorSinal(decimal valor)
        {
            return valor >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
                : new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
        }

        private void BtnCarregarPR_Click(object sender, RoutedEventArgs e)
        {
            ExibirMensagem("> [INFO] O planejamento mensal é informado diretamente no orçamento e replicado para os próximos meses (sem importar Contas a Pagar/Receber).", false);
        }

        private void BtnSincronizarRealizado_Click(object sender, RoutedEventArgs e)
        {
            var mapaRealizado = OrcamentoService.CalcularRealizadoPorCategoriaNoMes(_anoAtual, _mesAtual);

            foreach (var item in ItensQueEntramNoTotal(_todosCreditos).Concat(ItensQueEntramNoTotal(_todosDebitos)))
            {
                decimal val = mapaRealizado.TryGetValue(item.CategoriaUuid.Trim(), out decimal calc) ? calc : 0m;
                item.ValorRealizado = val;
                item.ValorRealizadoTexto = val.ToString("N2", _cultura);
            }

            AtualizarSubtotaisNaTela(_todosCreditos);
            AtualizarSubtotaisNaTela(_todosDebitos);
            AtualizarListasVisiveis();
            RecalcularSomatoriosETotais();
            ExibirMensagem("> [OK] Valores realizados atualizados com base nas movimentações do mês.", false);
        }

        private void BtnReplicar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int mes = (CboMes.SelectedIndex >= 0) ? CboMes.SelectedIndex + 1 : _mesAtual;
                if (!int.TryParse(TxtAno.Text.Trim(), out int ano) || ano < 2000 || ano > 2100)
                {
                    ExibirMensagem("> [ERRO] Informe um Ano válido antes de replicar.", true);
                    return;
                }

                string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

                OrcamentoService.SalvarOrcamentoDoMes(
                    ano,
                    mes,
                    usuarioUuid,
                    ItensQueEntramNoTotal(_todosCreditos),
                    ItensQueEntramNoTotal(_todosDebitos));

                OrcamentoService.ReplicarOrcamentoParaProximosMeses(ano, mes, usuarioUuid);

                ExibirMensagem("> [OK] Orçamento planejado replicado com sucesso para os próximos 11 meses!", false);
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO REPLICAR ORÇAMENTO]: {ex.Message}", true);
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            int mes = (CboMes.SelectedIndex >= 0) ? CboMes.SelectedIndex + 1 : _mesAtual;
            if (!int.TryParse(TxtAno.Text.Trim(), out int ano) || ano < 2000 || ano > 2100)
            {
                ExibirMensagem("> [ERRO] Informe um Ano válido (entre 2000 e 2100).", true);
                TxtAno.Focus(FocusState.Programmatic);
                return;
            }

            if (_modoInclusao && OrcamentoService.MesAnoJaExiste(ano, mes))
            {
                ExibirMensagem($"> [ERRO] Já existe um orçamento cadastrado para {mes:D2}/{ano:D4}. Não é permitido duplicar Mês/Ano.", true);
                return;
            }

            string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

            try
            {
                OrcamentoService.SalvarOrcamentoDoMes(
                    ano,
                    mes,
                    usuarioUuid,
                    ItensQueEntramNoTotal(_todosCreditos),
                    ItensQueEntramNoTotal(_todosDebitos));

                this.Close();
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO SALVAR ORÇAMENTO]: {ex.Message}", true);
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ExibirMensagem(string msg, bool ehErro)
        {
            TxtMensagem.Text = msg;
            TxtMensagem.Foreground = ehErro
                ? new SolidColorBrush(Color.FromArgb(255, 255, 85, 85))
                : new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
            TxtMensagem.Visibility = Visibility.Visible;
        }
    }
}