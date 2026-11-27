using ContaMobi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class FluxoCaixaPage : Page
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private TipoAgrupamentoFluxo _agrupamentoAtual = TipoAgrupamentoFluxo.Diario;
        private bool _formatandoData = false;

        public FluxoCaixaPage()
        {
            this.InitializeComponent();

            TxtDataInicio.TextChanged += TxtData_TextChanged;
            TxtDataFim.TextChanged += TxtData_TextChanged;

            this.Loaded += (s, e) =>
            {
                CarregarComboContas();
                AplicarDatasPorPeriodo();
                AtualizarVisualBotoesAgrupamento();
                CarregarFluxo();
            };
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
                Content = "TODAS AS CONTAS CORRENTES",
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

        private void AplicarDatasPorPeriodo()
        {
            if (TxtDataInicio == null || TxtDataFim == null || CboFiltroPeriodo == null)
                return;

            string tag = (CboFiltroPeriodo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MES";
            DateTime hoje = DateTime.Today;

            _formatandoData = true;

            if (tag == "MES")
            {
                DateTime fimMes = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = fimMes.ToString("dd/MM/yyyy");
            }
            else if (tag == "15_DIAS")
            {
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = hoje.AddDays(15).ToString("dd/MM/yyyy");
            }
            else if (tag == "30_DIAS")
            {
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = hoje.AddDays(30).ToString("dd/MM/yyyy");
            }
            else if (tag == "90_DIAS")
            {
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = hoje.AddDays(90).ToString("dd/MM/yyyy");
            }
            else if (tag == "ANO")
            {
                TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");
                TxtDataFim.Text = new DateTime(hoje.Year, 12, 31).ToString("dd/MM/yyyy");
            }
            else
            {
                if (!DateTime.TryParseExact(TxtDataInicio.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtAtualIni) || dtAtualIni < hoje)
                    TxtDataInicio.Text = hoje.ToString("dd/MM/yyyy");

                if (!DateTime.TryParseExact(TxtDataFim.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtAtualFim) || dtAtualFim < hoje)
                    TxtDataFim.Text = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1).ToString("dd/MM/yyyy");
            }

            _formatandoData = false;
        }

        private void AtualizarVisualBotoesAgrupamento()
        {
            if (BtnVisaoDiaria == null || BtnVisaoSemanal == null || BtnVisaoMensal == null)
                return;

            var ativoBg = new SolidColorBrush(Color.FromArgb(255, 0, 51, 25));
            var ativoFg = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
            var ativoBorder = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));

            var inativoBg = new SolidColorBrush(Color.FromArgb(255, 5, 21, 5));
            var inativoFg = new SolidColorBrush(Color.FromArgb(255, 0, 170, 68));
            var inativoBorder = new SolidColorBrush(Color.FromArgb(255, 0, 102, 34));

            void AplicarEstilo(Button btn, bool ativo)
            {
                btn.Background = ativo ? ativoBg : inativoBg;
                btn.Foreground = ativo ? ativoFg : inativoFg;
                btn.BorderBrush = ativo ? ativoBorder : inativoBorder;
            }

            AplicarEstilo(BtnVisaoDiaria, _agrupamentoAtual == TipoAgrupamentoFluxo.Diario);
            AplicarEstilo(BtnVisaoSemanal, _agrupamentoAtual == TipoAgrupamentoFluxo.Semanal);
            AplicarEstilo(BtnVisaoMensal, _agrupamentoAtual == TipoAgrupamentoFluxo.Mensal);

            string modoTexto = _agrupamentoAtual switch
            {
                TipoAgrupamentoFluxo.Semanal => "VISAO SEMANAL",
                TipoAgrupamentoFluxo.Mensal => "VISAO MENSAL",
                _ => "VISAO DIARIA"
            };

            TxtSubtituloFluxo.Text = $"{modoTexto}: SALDO EM CONTAS + A RECEBER (INCL. ATRASADOS) - A PAGAR (INCL. ATRASADOS)";
        }

        public void CarregarFluxo()
        {
            if (!this.IsLoaded || ListaFluxoCaixa == null)
                return;

            DateTime hoje = DateTime.Today;
            bool ajustadoParaHoje = false;

            if (!DateTime.TryParseExact(TxtDataInicio.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtIni))
            {
                dtIni = hoje;
            }

            if (dtIni < hoje)
            {
                dtIni = hoje;
                ajustadoParaHoje = true;
                _formatandoData = true;
                TxtDataInicio.Text = dtIni.ToString("dd/MM/yyyy");
                _formatandoData = false;
            }

            if (!DateTime.TryParseExact(TxtDataFim.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtFim))
            {
                dtFim = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);
            }

            if (dtFim < dtIni)
            {
                dtFim = dtIni;
                _formatandoData = true;
                TxtDataFim.Text = dtFim.ToString("dd/MM/yyyy");
                _formatandoData = false;
            }

            string? contaUuid = (CboFiltroConta.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool incluirAtrasados = ChkIncluirAtrasados.IsChecked ?? true;
            bool ocultarSemMov = ChkOcultarSemMovimento.IsChecked ?? false;

            try
            {
                var resultado = FluxoCaixaService.GerarFluxoCaixa(
                    dataInicio: dtIni,
                    dataFim: dtFim,
                    agrupamento: _agrupamentoAtual,
                    contaUuid: contaUuid,
                    incluirAtrasadosEmAberto: incluirAtrasados,
                    ocultarSemMovimento: ocultarSemMov);

                ListaFluxoCaixa.ItemsSource = resultado.Linhas;
                TxtResumoLinhas.Text = ajustadoParaHoje
                    ? $"LINHAS EXIBIDAS: {resultado.Linhas.Count} (Inicio ajustado para hoje: {hoje:dd/MM/yyyy})"
                    : $"LINHAS EXIBIDAS: {resultado.Linhas.Count}";

                TxtCardSaldoContas.Text = resultado.SaldoInicialContas.ToString("C2", _cultura);
                TxtCardSaldoContas.Foreground = resultado.SaldoInicialContas >= 0m
                    ? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
                    : new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));

                TxtCardTotalReceber.Text = resultado.TotalReceberGeral.ToString("C2", _cultura);
                TxtCardDetalheReceber.Text = $"Atrasados: {resultado.TotalReceberAtrasado.ToString("C2", _cultura)} | Periodo: {resultado.TotalReceberPeriodo.ToString("C2", _cultura)}";

                TxtCardTotalPagar.Text = resultado.TotalPagarGeral.ToString("C2", _cultura);
                TxtCardDetalhePagar.Text = $"Atrasados: {resultado.TotalPagarAtrasado.ToString("C2", _cultura)} | Periodo: {resultado.TotalPagarPeriodo.ToString("C2", _cultura)}";

                TxtCardSaldoFinal.Text = resultado.SaldoFinalProjetado.ToString("C2", _cultura);
                TxtCardSaldoFinal.Foreground = resultado.SaldoFinalProjetado >= 0m
                    ? new SolidColorBrush(Color.FromArgb(255, 0, 255, 102))
                    : new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
            }
            catch (Exception ex)
            {
                TxtResumoLinhas.Text = $"ERRO AO GERAR FLUXO: {ex.Message}";
            }
        }

        private void BtnAgrupamento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag)
                return;

            _agrupamentoAtual = tag switch
            {
                "SEMANAL" => TipoAgrupamentoFluxo.Semanal,
                "MENSAL" => TipoAgrupamentoFluxo.Mensal,
                _ => TipoAgrupamentoFluxo.Diario
            };

            // Se mudar para Mensal e o período atual cobrir apenas o mês corrente, expande de Hoje até Dezembro
            if (_agrupamentoAtual == TipoAgrupamentoFluxo.Mensal &&
                DateTime.TryParseExact(TxtDataInicio.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime d1) &&
                DateTime.TryParseExact(TxtDataFim.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime d2) &&
                d1.Year == d2.Year && d1.Month == d2.Month)
            {
                foreach (ComboBoxItem item in CboFiltroPeriodo.Items)
                {
                    if (item.Tag?.ToString() == "ANO")
                    {
                        CboFiltroPeriodo.SelectedItem = item;
                        break;
                    }
                }
            }

            AtualizarVisualBotoesAgrupamento();
            CarregarFluxo();
        }

        private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) CarregarFluxo();
        }

        private void CboFiltroPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                AplicarDatasPorPeriodo();
                CarregarFluxo();
            }
        }

        private void OpcaoCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded) CarregarFluxo();
        }

        private void BtnGerarFluxo_Click(object sender, RoutedEventArgs e) => CarregarFluxo();

        private async void BtnVerTitulos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FluxoCaixaLinha linha)
            {
                await ExibirDetalhesLinhaAsync(linha);
            }
        }

        private async void ListaFluxoCaixa_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaFluxoCaixa.SelectedItem is FluxoCaixaLinha linha)
            {
                await ExibirDetalhesLinhaAsync(linha);
            }
        }

        private async Task ExibirDetalhesLinhaAsync(FluxoCaixaLinha linha)
        {
            var corVerde = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
            var corAzul = new SolidColorBrush(Color.FromArgb(255, 51, 153, 255));
            var corVermelha = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
            var corTextoSecundario = new SolidColorBrush(Color.FromArgb(255, 136, 204, 153));
            var corDivisor = new SolidColorBrush(Color.FromArgb(255, 0, 136, 51));

            var painelPrincipal = new StackPanel
            {
                Spacing = 8,
                MinWidth = 480
            };

            // Resumo financeiro do intervalo
            painelPrincipal.Children.Add(new TextBlock
            {
                Text = $"Saldo Anterior: {linha.SaldoAnteriorFormatado}",
                FontSize = 12,
                Foreground = corTextoSecundario
            });

            painelPrincipal.Children.Add(new TextBlock
            {
                Text = $"Saldo Final Projetado: {linha.SaldoFinalFormatado}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = linha.SaldoFinal >= 0m ? corVerde : corVermelha
            });

            painelPrincipal.Children.Add(new Border
            {
                Height = 1,
                Background = corDivisor,
                Margin = new Thickness(0, 4, 0, 4)
            });

            if (linha.Titulos.Count == 0)
            {
                painelPrincipal.Children.Add(new TextBlock
                {
                    Text = "Nenhum título em aberto neste intervalo.",
                    FontSize = 12,
                    Foreground = corTextoSecundario
                });
            }
            else
            {
                var titulosReceber = linha.Titulos
                    .Where(t => t.Tipo == "R")
                    .OrderBy(t => t.DataVencimento)
                    .ThenBy(t => t.NumeroParcela)
                    .ToList();

                var titulosPagar = linha.Titulos
                    .Where(t => t.Tipo == "P")
                    .OrderBy(t => t.DataVencimento)
                    .ThenBy(t => t.NumeroParcela)
                    .ToList();

                // 1. GRUPO: A RECEBER
                painelPrincipal.Children.Add(new TextBlock
                {
                    Text = $"(+) A RECEBER ({titulosReceber.Count}) — Total: {linha.TotalReceberFormatado}",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = corAzul
                });

                if (titulosReceber.Count == 0)
                {
                    painelPrincipal.Children.Add(new TextBlock
                    {
                        Text = "   Nenhum título a receber neste intervalo.",
                        FontSize = 12,
                        Foreground = corTextoSecundario
                    });
                }
                else
                {
                    foreach (var t in titulosReceber)
                    {
                        string docInfo = string.IsNullOrWhiteSpace(t.NumeroDocumento) ? string.Empty : $" | Doc: {t.NumeroDocumento}";
                        var itemPanel = new StackPanel { Spacing = 1, Margin = new Thickness(8, 2, 0, 4) };

                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = $"Venc: {t.DataVencimentoFormatada}  •  {t.ValorTotalComEncargos.ToString("C2", _cultura)}",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = corAzul
                        });

                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = $"{t.Descricao} ({t.NomeEntidade}){docInfo}",
                            FontSize = 12,
                            Foreground = corTextoSecundario,
                            TextWrapping = TextWrapping.Wrap
                        });

                        painelPrincipal.Children.Add(itemPanel);
                    }
                }

                // LINHA SEPARANDO A RECEBER DE A PAGAR
                painelPrincipal.Children.Add(new Border
                {
                    Height = 1,
                    Background = corDivisor,
                    Margin = new Thickness(0, 6, 0, 6)
                });

                // 2. GRUPO: A PAGAR
                painelPrincipal.Children.Add(new TextBlock
                {
                    Text = $"(-) A PAGAR ({titulosPagar.Count}) — Total: {linha.TotalPagarFormatado}",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = corVermelha
                });

                if (titulosPagar.Count == 0)
                {
                    painelPrincipal.Children.Add(new TextBlock
                    {
                        Text = "   Nenhum título a pagar neste intervalo.",
                        FontSize = 12,
                        Foreground = corTextoSecundario
                    });
                }
                else
                {
                    foreach (var t in titulosPagar)
                    {
                        string docInfo = string.IsNullOrWhiteSpace(t.NumeroDocumento) ? string.Empty : $" | Doc: {t.NumeroDocumento}";
                        var itemPanel = new StackPanel { Spacing = 1, Margin = new Thickness(8, 2, 0, 4) };

                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = $"Venc: {t.DataVencimentoFormatada}  •  {t.ValorTotalComEncargos.ToString("C2", _cultura)}",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = corVermelha
                        });

                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = $"{t.Descricao} ({t.NomeEntidade}){docInfo}",
                            FontSize = 12,
                            Foreground = corTextoSecundario,
                            TextWrapping = TextWrapping.Wrap
                        });

                        painelPrincipal.Children.Add(itemPanel);
                    }
                }
            }

            var scroll = new ScrollViewer
            {
                MaxHeight = 440,
                Padding = new Thickness(0, 0, 12, 0),
                Content = painelPrincipal
            };

            var dialog = new ContentDialog
            {
                Title = $"Detalhamento — {linha.PeriodoLabel}",
                Content = scroll,
                CloseButtonText = "Fechar",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
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