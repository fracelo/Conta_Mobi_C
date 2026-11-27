using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using System.Linq;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class CartaoCreditoEditWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly CartaoCredito _cartao;
        private readonly bool _ehNovo;
        private readonly decimal _limiteUtilizadoOriginal;

        private decimal _limiteTotalDigitado = 0m;
        private bool _carregandoTela = true;
        private bool _formatandoTexto = false;

        public CartaoCreditoEditWindow(CartaoCredito cartao, bool ehNovo)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(780, 620));

            _cartao = cartao;
            _ehNovo = ehNovo;
            _limiteUtilizadoOriginal = ehNovo ? 0m : Math.Max(0m, cartao.LimiteTotal - cartao.LimiteAtual);

            TxtTituloModal.Text = ehNovo
                ? "[ INCLUIR CARTAO DE CREDITO ]"
                : $"[ EDITANDO CARTAO // {cartao.Descricao.ToUpperInvariant()} ]";

            BtnSalvar.Content = ehNovo ? "[F10] SALVAR CARTAO DE CREDITO" : "[F10] SALVAR ALTERACOES";

            CarregarCombos();
            PreencherCampos();

            _carregandoTela = false;
            AtualizarPreviewLimite();
        }

        private void CarregarCombos()
        {
            var bandeiras = CartaoCreditoCadastroService.ListarBandeirasDisponiveis(_ehNovo ? null : _cartao.BandeiraUuid);
            CboBandeira.Items.Clear();

            ComboBoxItem? bandeiraSelecionar = null;
            foreach (var b in bandeiras)
            {
                var item = new ComboBoxItem
                {
                    Content = b.DescricaoBandeira,
                    Tag = b.BandeiraUuid
                };
                CboBandeira.Items.Add(item);

                if (!_ehNovo && string.Equals(b.BandeiraUuid, _cartao.BandeiraUuid, StringComparison.OrdinalIgnoreCase))
                {
                    bandeiraSelecionar = item;
                }
            }

            if (bandeiraSelecionar != null)
                CboBandeira.SelectedItem = bandeiraSelecionar;
            else if (CboBandeira.Items.Count > 0)
                CboBandeira.SelectedIndex = 0;

            var contas = CartaoCreditoCadastroService.ListarContasCorrentes();
            CboContaCorrente.Items.Clear();
            CboContaCorrente.Items.Add(new ComboBoxItem
            {
                Content = "[ NENHUMA / DEFINIR NO PAGAMENTO ]",
                Tag = string.Empty,
                IsSelected = true
            });

            foreach (var c in contas)
            {
                string texto = string.IsNullOrWhiteSpace(c.BancoFormatado)
                    ? c.Descricao
                    : $"{c.Descricao} ({c.BancoFormatado})";

                var item = new ComboBoxItem
                {
                    Content = texto,
                    Tag = c.ContaUuid
                };
                CboContaCorrente.Items.Add(item);

                if (!_ehNovo && !string.IsNullOrWhiteSpace(_cartao.ContaUuid) &&
                    string.Equals(c.ContaUuid, _cartao.ContaUuid, StringComparison.OrdinalIgnoreCase))
                {
                    CboContaCorrente.SelectedItem = item;
                }
            }
        }

        private void PreencherCampos()
        {
            _formatandoTexto = true;

            TxtDescricao.Text = _cartao.Descricao ?? string.Empty;
            TxtUltimosDigitos.Text = _cartao.UltimosDigitos ?? string.Empty;
            TxtDiaFechamento.Text = (_cartao.DiaFechamento is >= 1 and <= 31 ? _cartao.DiaFechamento : 1).ToString();
            TxtDiaVencimento.Text = (_cartao.DiaVencimento is >= 1 and <= 31 ? _cartao.DiaVencimento : 10).ToString();

            _limiteTotalDigitado = Math.Max(0m, _cartao.LimiteTotal);
            TxtLimiteTotal.Text = _limiteTotalDigitado > 0m ? _limiteTotalDigitado.ToString("N2", _cultura) : string.Empty;

            string tagAtivo = _cartao.Ativo ? "S" : "N";
            foreach (ComboBoxItem item in CboAtivo.Items)
            {
                if (item.Tag?.ToString() == tagAtivo)
                {
                    CboAtivo.SelectedItem = item;
                    break;
                }
            }

            string cor = string.IsNullOrWhiteSpace(_cartao.CorHex) ? "#008552" : _cartao.CorHex;
            TxtCorHex.Text = cor;
            AtualizarPreviewCor(cor);

            _formatandoTexto = false;
        }

        private void TxtApenasDigitos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatandoTexto || sender is not TextBox tb) return;

            _formatandoTexto = true;
            string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (tb.Text != digitos)
            {
                tb.Text = digitos;
                tb.SelectionStart = digitos.Length;
            }
            _formatandoTexto = false;
        }

        private void TxtLimiteTotal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatandoTexto) return;

            _formatandoTexto = true;
            string digitos = new string(TxtLimiteTotal.Text.Where(char.IsDigit).ToArray());

            if (!string.IsNullOrEmpty(digitos) && decimal.TryParse(digitos, out decimal centavos))
            {
                _limiteTotalDigitado = centavos / 100m;
                TxtLimiteTotal.Text = _limiteTotalDigitado.ToString("N2", _cultura);
                TxtLimiteTotal.SelectionStart = TxtLimiteTotal.Text.Length;
            }
            else
            {
                _limiteTotalDigitado = 0m;
                TxtLimiteTotal.Text = string.Empty;
            }
            _formatandoTexto = false;

            AtualizarPreviewLimite();
        }

        private void AtualizarPreviewLimite()
        {
            if (TxtLimiteDisponivelPreview == null || TxtInfoLimiteUtilizado == null)
                return;

            decimal disponivel = Math.Max(0m, _limiteTotalDigitado - _limiteUtilizadoOriginal);

            TxtInfoLimiteUtilizado.Text = $"Limite ja comprometido em faturas: {_limiteUtilizadoOriginal.ToString("C2", _cultura)}";
            TxtLimiteDisponivelPreview.Text = $"Limite Disponivel: {disponivel.ToString("C2", _cultura)}";
            TxtLimiteDisponivelPreview.Foreground = disponivel > 0m
                ? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
                : new SolidColorBrush(Color.FromArgb(255, 255, 170, 0));
        }

        private void BtnCorPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _formatandoTexto = true;
                TxtCorHex.Text = hex;
                _formatandoTexto = false;
                AtualizarPreviewCor(hex);
            }
        }

        private void TxtCorHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoTela || _formatandoTexto) return;
            AtualizarPreviewCor(TxtCorHex.Text);
        }

        private void AtualizarPreviewCor(string? hexInput)
        {
            if (BorderPreviewCor == null) return;

            if (TentarConverterHex(hexInput, out Color cor))
            {
                BorderPreviewCor.Background = new SolidColorBrush(cor);
            }
        }

        private static bool TentarConverterHex(string? hexInput, out Color cor)
        {
            cor = Color.FromArgb(255, 0, 133, 82);
            if (string.IsNullOrWhiteSpace(hexInput)) return false;

            string limpo = hexInput.Trim().TrimStart('#');
            if (limpo.Length == 6 &&
                byte.TryParse(limpo[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(limpo[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(limpo[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                cor = Color.FromArgb(255, r, g, b);
                return true;
            }

            return false;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string descricao = TxtDescricao.Text.Trim();
            if (string.IsNullOrEmpty(descricao))
            {
                ExibirMensagem("> [ERRO] Informe a Descricao / Apelido do cartao.", true);
                TxtDescricao.Focus(FocusState.Programmatic);
                return;
            }

            string ultimosDigitos = TxtUltimosDigitos.Text.Trim();
            if (!string.IsNullOrEmpty(ultimosDigitos) && ultimosDigitos.Length != 4)
            {
                ExibirMensagem("> [ERRO] Se informar os ultimos digitos, preencha exatamente 4 numeros.", true);
                TxtUltimosDigitos.Focus(FocusState.Programmatic);
                return;
            }

            string? bandeiraUuid = (CboBandeira.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(bandeiraUuid))
            {
                ExibirMensagem("> [ERRO] Selecione uma Bandeira para o cartao.", true);
                CboBandeira.Focus(FocusState.Programmatic);
                return;
            }

            if (!int.TryParse(TxtDiaFechamento.Text.Trim(), out int diaFechamento) || diaFechamento < 1 || diaFechamento > 31)
            {
                ExibirMensagem("> [ERRO] Informe um Dia de Fechamento valido (entre 1 e 31).", true);
                TxtDiaFechamento.Focus(FocusState.Programmatic);
                return;
            }

            if (!int.TryParse(TxtDiaVencimento.Text.Trim(), out int diaVencimento) || diaVencimento < 1 || diaVencimento > 31)
            {
                ExibirMensagem("> [ERRO] Informe um Dia de Vencimento valido (entre 1 e 31).", true);
                TxtDiaVencimento.Focus(FocusState.Programmatic);
                return;
            }

            if (_limiteTotalDigitado <= 0m)
            {
                ExibirMensagem("> [ERRO] Informe um Limite Total maior que zero.", true);
                TxtLimiteTotal.Focus(FocusState.Programmatic);
                return;
            }

            string corHex = TxtCorHex.Text.Trim();
            if (!TentarConverterHex(corHex, out _))
            {
                ExibirMensagem("> [ERRO] Codigo de cor HEX invalido. Use o formato #RRGGBB (ex: #008552).", true);
                TxtCorHex.Focus(FocusState.Programmatic);
                return;
            }

            string? contaUuid = (CboContaCorrente.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool ativo = ((CboAtivo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "S") == "S";

            string usuarioUuid = !string.IsNullOrWhiteSpace(_cartao.UsuarioUuid)
                ? _cartao.UsuarioUuid
                : (App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty);

            try
            {
                _cartao.UsuarioUuid = usuarioUuid;
                _cartao.Descricao = descricao;
                _cartao.UltimosDigitos = string.IsNullOrEmpty(ultimosDigitos) ? null : ultimosDigitos;
                _cartao.BandeiraUuid = bandeiraUuid;
                _cartao.ContaUuid = string.IsNullOrWhiteSpace(contaUuid) ? null : contaUuid;
                _cartao.DiaFechamento = diaFechamento;
                _cartao.DiaVencimento = diaVencimento;
                _cartao.LimiteTotal = _limiteTotalDigitado;
                _cartao.CorHex = corHex.StartsWith("#") ? corHex.ToUpperInvariant() : $"#{corHex.ToUpperInvariant()}";
                _cartao.Ativo = ativo;

                CartaoCreditoCadastroService.SalvarCartaoCredito(_cartao, _ehNovo);
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO SALVAR CARTAO]: {ex.Message}", true);
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