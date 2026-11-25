using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class ContasPRBaixaWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly ContaPR _conta;
        private readonly List<ContaCorrente> _contasCorrentes;
        private readonly bool _ehEdicaoBaixa;
        private readonly decimal _valorPagoOriginal;

        private decimal _juros = 0m;
        private decimal _multa = 0m;
        private decimal _acrescimo = 0m;
        private decimal _desconto = 0m;
        private decimal _totalCalculado = 0m;
        private decimal _valorBaixar = 0m;

        private bool _carregando = true;
        private bool _formatando = false;

        public bool BaixaConfirmada { get; private set; } = false;

        public ContasPRBaixaWindow(string contasPRUuid)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(620, 730));
            CentralizarJanela();

            _conta = ContasPRBaixaService.ObterContaCompleta(contasPRUuid)
                ?? throw new InvalidOperationException("Parcela não encontrada para baixa.");

            _ehEdicaoBaixa = _conta.EstaQuitado;
            _valorPagoOriginal = _conta.ValorPago;
            _contasCorrentes = ContasPRBaixaService.ObterContasCorrentes();

            PreencherTela();
            _carregando = false;
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

        private void PreencherTela()
        {
            bool ehReceber = _conta.Tipo == "R";
            string acaoTitulo = _ehEdicaoBaixa ? "EDITAR BAIXA" : "BAIXA";

            TxtTituloModal.Text = ehReceber
                ? $"[ {acaoTitulo} // CONTA A RECEBER ]"
                : $"[ {acaoTitulo} // CONTA A PAGAR ]";

            TxtTituloModal.Foreground = ehReceber
                ? new SolidColorBrush(Color.FromArgb(255, 51, 153, 255))
                : new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));

            BtnBaixar.Content = _ehEdicaoBaixa ? "[ATUALIZAR BAIXA]" : "[BAIXAR]";

            TxtParcelaBadge.Text = $"PARCELA {_conta.NumeroParcela:D2}/{_conta.TotalParcelas:D2}";
            TxtDescricao.Text = _conta.Descricao;
            TxtNumeroDocumento.Text = _conta.NumeroDocumento ?? string.Empty;
            TxtVencimento.Text = _conta.DataVencimento.ToString("dd/MM/yyyy");
            TxtEntidade.Text = _conta.NomeEntidade;
            TxtValorOriginal.Text = _conta.Valor.ToString("C2", _cultura);

            CarregarCategoriasCombo();

            _juros = _conta.Juro;
            _multa = _conta.Multa;
            _acrescimo = _conta.Acrescimo;
            _desconto = _conta.Desconto;

            TxtJuros.Text = _juros.ToString("N2", _cultura);
            TxtMulta.Text = _multa.ToString("N2", _cultura);
            TxtAcrescimo.Text = _acrescimo.ToString("N2", _cultura);
            TxtDesconto.Text = _desconto.ToString("N2", _cultura);

            DateTime dataInicial = (_ehEdicaoBaixa && _conta.DataPagamento.HasValue)
                ? _conta.DataPagamento.Value
                : DateTime.Today;
            TxtDataBaixa.Text = dataInicial.ToString("dd/MM/yyyy");

            CboContaCorrente.ItemsSource = _contasCorrentes;
            if (!string.IsNullOrWhiteSpace(_conta.ContaUuid))
            {
                var contaPadrao = _contasCorrentes.FirstOrDefault(c =>
                    string.Equals(c.ContaUuid, _conta.ContaUuid, StringComparison.OrdinalIgnoreCase));
                if (contaPadrao != null)
                    CboContaCorrente.SelectedItem = contaPadrao;
            }
            else if (_contasCorrentes.Count > 0)
            {
                CboContaCorrente.SelectedIndex = 0;
            }

            _totalCalculado = Math.Max(0m, _conta.Valor + _juros + _multa + _acrescimo - _desconto);
            _valorBaixar = (_ehEdicaoBaixa && _conta.ValorPago > 0m) ? _conta.ValorPago : _totalCalculado;

            _formatando = true;
            TxtTotalBaixar.Text = _valorBaixar.ToString("N2", _cultura);
            _formatando = false;

            TxtResumoCalculado.Text = $"Saldo calculado: {_totalCalculado.ToString("C2", _cultura)}";
            AtualizarAvisoDiferenca();
        }

        private void CarregarCategoriasCombo()
        {
            CboCategoria.ItemsSource = null;
            CboCategoria.Items.Clear();
            CboCategoria.Items.Add(new ComboBoxItem
            {
                Content = "[ SELECIONE UMA CATEGORIA ]",
                Tag = null
            });

            try
            {
                using var db = new AppDbContext();
                bool ehReceber = _conta.Tipo == "R";

                var todasCategorias = db.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Descricao)
                    .ToList();

                bool EhDoTipo(Categoria c)
                {
                    string t = (c.Tipo ?? string.Empty).Trim().ToUpperInvariant();
                    if (ehReceber)
                        return t == "C" || t == "R" || t.StartsWith("REC") || t.StartsWith("CRED") || t == "E";
                    else
                        return t == "D" || t == "P" || t.StartsWith("DES") || t.StartsWith("DEB") || t == "S";
                }

                // Apenas categorias de nível 2 (ou superior) que aceitam lançamento
                var categoriasNivel2 = todasCategorias
                    .Where(c => EhDoTipo(c) && c.Nivel >= 2 && c.AceitaLancamento)
                    .OrderBy(c => c.Descricao)
                    .ToList();

                var mapaTodas = todasCategorias
                    .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var filhosPorPai = categoriasNivel2
                    .Where(c => !string.IsNullOrWhiteSpace(c.PaiUuid))
                    .GroupBy(c => c.PaiUuid!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Descricao).ToList(), StringComparer.OrdinalIgnoreCase);

                var paisOrdenados = filhosPorPai.Keys
                    .Where(paiId => mapaTodas.ContainsKey(paiId))
                    .Select(paiId => mapaTodas[paiId])
                    .OrderBy(p => p.Descricao)
                    .ToList();

                string? alvo = _conta.CategoriaUuid;
                ComboBoxItem? itemParaSelecionar = null;
                var adicionados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pai in paisOrdenados)
                {
                    string paiId = pai.CategoriaUuid.Trim();
                    if (!filhosPorPai.TryGetValue(paiId, out var filhas) || filhas.Count == 0)
                        continue;

                    // Nível 1 em cinza, negrito e não selecionável
                    CboCategoria.Items.Add(new ComboBoxItem
                    {
                        Content = pai.Descricao.ToUpperInvariant(),
                        Tag = null,
                        IsEnabled = false,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 130, 145, 160))
                    });

                    // Nível 2 indentado e selecionável
                    foreach (var filha in filhas)
                    {
                        var itemFilha = new ComboBoxItem
                        {
                            Content = $"   └─ {filha.Descricao}",
                            Tag = filha
                        };
                        CboCategoria.Items.Add(itemFilha);
                        adicionados.Add(filha.CategoriaUuid.Trim());

                        if (!string.IsNullOrWhiteSpace(alvo) &&
                            string.Equals(filha.CategoriaUuid.Trim(), alvo.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            itemParaSelecionar = itemFilha;
                        }
                    }
                }

                // Caso exista alguma categoria nível 2 que aceita lançamento sem pai vinculado
                foreach (var solta in categoriasNivel2.Where(c => !adicionados.Contains(c.CategoriaUuid.Trim())))
                {
                    var itemSolto = new ComboBoxItem
                    {
                        Content = $"   └─ {solta.Descricao}",
                        Tag = solta
                    };
                    CboCategoria.Items.Add(itemSolto);

                    if (!string.IsNullOrWhiteSpace(alvo) &&
                        string.Equals(solta.CategoriaUuid.Trim(), alvo.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        itemParaSelecionar = itemSolto;
                    }
                }

                if (itemParaSelecionar != null)
                {
                    CboCategoria.SelectedItem = itemParaSelecionar;
                }
                else
                {
                    CboCategoria.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar Categorias na baixa: {ex.Message}");
            }
        }

        private void TxtData_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando || _formatando || sender is not TextBox tb) return;

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

        private void TxtEncargo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando || _formatando || sender is not TextBox tb) return;

            _formatando = true;
            FormatarMoeda(tb, out decimal valor);

            if (tb == TxtJuros) _juros = valor;
            else if (tb == TxtMulta) _multa = valor;
            else if (tb == TxtAcrescimo) _acrescimo = valor;
            else if (tb == TxtDesconto) _desconto = valor;

            _formatando = false;

            RecalcularTotalPadrao();
        }

        private void RecalcularTotalPadrao()
        {
            _totalCalculado = Math.Max(0m, _conta.Valor + _juros + _multa + _acrescimo - _desconto);
            _valorBaixar = _totalCalculado;

            _formatando = true;
            TxtTotalBaixar.Text = _valorBaixar.ToString("N2", _cultura);
            _formatando = false;

            TxtResumoCalculado.Text = $"Saldo calculado: {_totalCalculado.ToString("C2", _cultura)}";
            AtualizarAvisoDiferenca();
        }

        private void TxtTotalBaixar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando || _formatando || sender is not TextBox tb) return;

            _formatando = true;
            FormatarMoeda(tb, out decimal valor);
            _valorBaixar = valor;
            _formatando = false;

            AtualizarAvisoDiferenca();
        }

        private void AtualizarAvisoDiferenca()
        {
            if (_valorBaixar < _totalCalculado)
            {
                decimal diff = _totalCalculado - _valorBaixar;
                TxtAvisoDiferenca.Text = $"[!] Valor menor que o saldo da parcela. Diferença: {diff.ToString("C2", _cultura)} (será perguntado ao baixar).";
                TxtAvisoDiferenca.Visibility = Visibility.Visible;
            }
            else
            {
                TxtAvisoDiferenca.Visibility = Visibility.Collapsed;
            }
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

        private async void BtnBaixar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            Categoria? categoriaSelecionada = (CboCategoria.SelectedItem as ComboBoxItem)?.Tag as Categoria;
            if (categoriaSelecionada == null)
            {
                ExibirErro("Selecione uma Categoria válida (nível 2) para registrar a baixa.");
                CboCategoria.Focus(FocusState.Programmatic);
                return;
            }

            if (CboContaCorrente.SelectedItem is not ContaCorrente contaSelecionada)
            {
                ExibirErro("Selecione a Conta Corrente para registrar o lançamento.");
                CboContaCorrente.Focus(FocusState.Programmatic);
                return;
            }

            if (!DateTime.TryParseExact(TxtDataBaixa.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dataBaixa))
            {
                ExibirErro("Informe uma Data de Baixa válida no formato dd/mm/aaaa.");
                TxtDataBaixa.Focus(FocusState.Programmatic);
                return;
            }

            if (_valorBaixar <= 0m)
            {
                ExibirErro("O valor a ser baixado deve ser maior que zero.");
                TxtTotalBaixar.Focus(FocusState.Programmatic);
                return;
            }

            string? numeroDocumento = string.IsNullOrWhiteSpace(TxtNumeroDocumento.Text)
                ? null
                : TxtNumeroDocumento.Text.Trim();

            decimal descontoFinal = _desconto;
            decimal acrescimoFinal = _acrescimo;
            bool gerarSobra = false;
            decimal valorSobra = 0m;
            DateTime? dataVencimentoSobra = null;

            bool devePerguntarDiferenca = _valorBaixar < _totalCalculado &&
                                          (!_ehEdicaoBaixa || _valorBaixar != _valorPagoOriginal);

            if (devePerguntarDiferenca)
            {
                decimal diferenca = _totalCalculado - _valorBaixar;

                var txtNovoVenc = new TextBox
                {
                    Header = "Novo vencimento da conta de sobra (caso escolha SIM):",
                    Text = _conta.DataVencimento.AddMonths(1).ToString("dd/MM/yyyy"),
                    MaxLength = 10,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                txtNovoVenc.TextChanged += TxtData_TextChanged;

                var painelDialog = new StackPanel { Spacing = 6 };
                painelDialog.Children.Add(new TextBlock
                {
                    Text = $"Saldo da parcela: {_totalCalculado.ToString("C2", _cultura)}\n" +
                           $"Valor informado: {_valorBaixar.ToString("C2", _cultura)}\n" +
                           $"Diferença: {diferenca.ToString("C2", _cultura)}\n\n" +
                           "Deseja lançar uma NOVA CONTA com o valor da diferença (pagamento parcial)?\n" +
                           "• SIM: cria réplica desta parcela com o valor restante.\n" +
                           "• NÃO: soma a diferença no campo de Desconto.",
                    TextWrapping = TextWrapping.Wrap
                });
                painelDialog.Children.Add(txtNovoVenc);

                var dialog = new ContentDialog
                {
                    Title = "Valor menor que o saldo da parcela",
                    Content = painelDialog,
                    PrimaryButtonText = "SIM (Lançar Sobra)",
                    SecondaryButtonText = "NÃO (Lançar Desconto)",
                    CloseButtonText = "Cancelar",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };

                var resultado = await dialog.ShowAsync();

                if (resultado == ContentDialogResult.None)
                    return;

                if (resultado == ContentDialogResult.Primary)
                {
                    if (!DateTime.TryParseExact(txtNovoVenc.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtVencSobra))
                    {
                        ExibirErro("Data de vencimento da conta de sobra inválida (use dd/mm/aaaa). Tente novamente.");
                        return;
                    }

                    gerarSobra = true;
                    valorSobra = diferenca;
                    dataVencimentoSobra = dtVencSobra;
                }
                else if (resultado == ContentDialogResult.Secondary)
                {
                    descontoFinal = _desconto + diferenca;
                    _desconto = descontoFinal;

                    _formatando = true;
                    TxtDesconto.Text = _desconto.ToString("N2", _cultura);
                    _formatando = false;
                }
            }
            else if (_valorBaixar > _totalCalculado)
            {
                acrescimoFinal += (_valorBaixar - _totalCalculado);
            }

            try
            {
                string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? _conta.UsuarioUuid;

                ContasPRBaixaService.EfetuarBaixa(
                    contasPRUuid: _conta.ContasPRUuid,
                    contaCorrenteUuid: contaSelecionada.ContaUuid,
                    dataBaixa: dataBaixa,
                    juro: _juros,
                    multa: _multa,
                    acrescimo: acrescimoFinal,
                    desconto: descontoFinal,
                    valorPago: _valorBaixar,
                    gerarContaSobra: gerarSobra,
                    valorSobra: valorSobra,
                    dataVencimentoSobra: dataVencimentoSobra,
                    usuarioUuid: usuarioUuid,
                    categoriaUuid: categoriaSelecionada.CategoriaUuid,
                    numeroDocumento: numeroDocumento);

                BaixaConfirmada = true;
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirErro($"Erro ao efetuar baixa: {ex.Message}");
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