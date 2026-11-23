using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class TransferenciaWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly List<ContaCorrente> _contas;

        private decimal _valor = 0m;
        private bool _formatando = false;

        public bool TransferenciaConfirmada { get; private set; } = false;

        public TransferenciaWindow(string? contaOrigemPadraoUuid = null)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(560, 540));
            CentralizarJanela();

            _contas = TransferenciaService.ObterContasCorrentes();

            CboContaOrigem.ItemsSource = _contas;
            CboContaDestino.ItemsSource = _contas;

            if (!string.IsNullOrWhiteSpace(contaOrigemPadraoUuid))
            {
                var origem = _contas.FirstOrDefault(c =>
                    string.Equals(c.ContaUuid, contaOrigemPadraoUuid, StringComparison.OrdinalIgnoreCase));
                if (origem != null)
                    CboContaOrigem.SelectedItem = origem;
            }
            else if (_contas.Count > 0)
            {
                CboContaOrigem.SelectedIndex = 0;
            }

            TxtData.Text = DateTime.Today.ToString("dd/MM/yyyy");
        }

        private void CentralizarJanela()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                int x = workArea.X + (workArea.Width - this.AppWindow.Size.Width) / 2;
                int y = workArea.Y + (workArea.Height - this.AppWindow.Size.Height) / 2;
                this.AppWindow.Move(new PointInt32(x, y));
            }
        }

        private void TxtData_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatando || sender is not TextBox tb) return;

            _formatando = true;
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
            _formatando = false;
        }

        private void TxtValor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatando || sender is not TextBox tb) return;

            _formatando = true;
            string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
            _valor = 0m;

            if (!string.IsNullOrEmpty(digitos) && decimal.TryParse(digitos, out decimal centavos))
            {
                _valor = centavos / 100m;
                tb.Text = _valor.ToString("N2", _cultura);
                tb.SelectionStart = tb.Text.Length;
            }
            else
            {
                tb.Text = "0,00";
                tb.SelectionStart = tb.Text.Length;
            }
            _formatando = false;
        }

        private void BtnTransferir_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            if (CboContaOrigem.SelectedItem is not ContaCorrente contaOrigem)
            {
                ExibirErro("Selecione a Conta de Origem.");
                CboContaOrigem.Focus(FocusState.Programmatic);
                return;
            }

            if (CboContaDestino.SelectedItem is not ContaCorrente contaDestino)
            {
                ExibirErro("Selecione a Conta de Destino.");
                CboContaDestino.Focus(FocusState.Programmatic);
                return;
            }

            if (string.Equals(contaOrigem.ContaUuid, contaDestino.ContaUuid, StringComparison.OrdinalIgnoreCase))
            {
                ExibirErro("A conta de origem e a conta de destino devem ser diferentes.");
                CboContaDestino.Focus(FocusState.Programmatic);
                return;
            }

            if (!DateTime.TryParseExact(TxtData.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dataTransf))
            {
                ExibirErro("Informe uma data válida no formato dd/mm/aaaa.");
                TxtData.Focus(FocusState.Programmatic);
                return;
            }

            if (_valor <= 0m)
            {
                ExibirErro("Informe um valor maior que zero para a transferência.");
                TxtValor.Focus(FocusState.Programmatic);
                return;
            }

            try
            {
                string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

                TransferenciaService.RealizarTransferencia(
                    contaOrigemUuid: contaOrigem.ContaUuid,
                    contaDestinoUuid: contaDestino.ContaUuid,
                    dataTransferencia: dataTransf,
                    valor: _valor,
                    observacao: TxtObservacao.Text,
                    conciliado: ChkConciliado.IsChecked == true,
                    usuarioUuid: usuarioUuid);

                TransferenciaConfirmada = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirErro($"Erro ao realizar transferência: {ex.Message}");
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ExibirErro(string msg)
        {
            TxtMensagem.Text = $"> [ERRO]: {msg}";
            TxtMensagem.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
            TxtMensagem.Visibility = Visibility.Visible;
        }
    }
}