using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class ContaPREditWindow : Window
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private readonly ContaPR _contaPR;
        private readonly bool _ehNovo;

        // Se ContaPR.Tipo == "P" -> Categoria "D" (Despesa)
        // Se ContaPR.Tipo == "R" -> Categoria "C" (Receita)
        private string TipoCategoriaEquivalente => _contaPR.Tipo == "R" ? "C" : "D";

        private List<Categoria> _categoriasDisponiveis = new();
        private List<Entidade> _todasEntidades = new();
        private Entidade? _entidadeSelecionada = null;

        private readonly ObservableCollection<ContaPRParcela> _gradeParcelas = new();

        private decimal _valorBase = 0m;

        private bool _carregandoTela = true;
        private bool _formatandoTexto = false;
        private bool _editandoCelulaGrid = false;
        private bool _retornandoDeModal = false;

        public ContaPREditWindow(ContaPR contaPR)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(900, 800));

            _contaPR = contaPR;
            ContasPRService.GarantirTabelasCriadas();
            ContasPRBaixaService.GarantirColunasBaixa();

            using (var db = new AppDbContext())
            {
                _ehNovo = string.IsNullOrEmpty(_contaPR.ContasPRUuid) || db.ContasPR.Find(_contaPR.ContasPRUuid) == null;
            }

            ListaParcelas.ItemsSource = _gradeParcelas;

            ConfigurarRotulosPorTipo();
            CarregarListasDoBanco();
            PreencherCamposIniciais();

            _carregandoTela = false;
        }

        private void ConfigurarRotulosPorTipo()
        {
            bool ehReceber = _contaPR.Tipo == "R";
            string nomeModulo = ehReceber ? "CONTAS A RECEBER" : "CONTAS A PAGAR";

            TxtTituloModal.Text = _ehNovo
                ? $"[ INCLUIR TITULO // {nomeModulo} ]"
                : $"[ EDITANDO TITULO // {nomeModulo} ]";

            LblCategoria.Text = ehReceber
                ? "CATEGORIA DE RECEITA (*):"
                : "CATEGORIA DE DESPESA (*):";

            LblEntidade.Text = ehReceber
                ? "CLIENTE / PAGADOR (*):"
                : "FORNECEDOR / FAVORECIDO (*):";

            BtnExcluir.Visibility = _ehNovo ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CarregarListasDoBanco()
        {
            CarregarContasCorrentesCombo();
            RecarregarCategoriasCombo();
            RecarregarEntidadesDoBanco();
        }

        private void CarregarContasCorrentesCombo()
        {
            CboContaCorrente.Items.Clear();
            CboContaCorrente.Items.Add(new ComboBoxItem
            {
                Content = "[ SELECIONE UMA CONTA CORRENTE ]",
                Tag = string.Empty
            });

            try
            {
                using var db = new AppDbContext();

                var bancosMap = db.Bancos
                    .AsNoTracking()
                    .ToList()
                    .GroupBy(b => b.CodigoBanco)
                    .ToDictionary(g => g.Key, g => g.First());

                var contas = db.ContasCorrentes
                    .AsNoTracking()
                    .OrderBy(c => c.Descricao)
                    .ToList();

                foreach (var c in contas)
                {
                    if (bancosMap.TryGetValue(c.CodigoBanco, out var b))
                    {
                        c.Banco = b;
                    }

                    string textoConta = string.IsNullOrWhiteSpace(c.BancoFormatado)
                        ? c.Descricao
                        : $"{c.Descricao} ({c.BancoFormatado})";

                    CboContaCorrente.Items.Add(new ComboBoxItem
                    {
                        Content = textoConta,
                        Tag = c.ContaUuid
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar Contas Correntes: {ex.Message}");
            }
        }

        private void RecarregarCategoriasCombo(string? categoriaUuidParaSelecionar = null)
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
                bool ehReceber = _contaPR.Tipo == "R";

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
                _categoriasDisponiveis = todasCategorias
                    .Where(c => EhDoTipo(c) && c.Nivel >= 2 && c.AceitaLancamento)
                    .OrderBy(c => c.Descricao)
                    .ToList();

                var mapaTodas = todasCategorias
                    .GroupBy(c => c.CategoriaUuid.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var filhosPorPai = _categoriasDisponiveis
                    .Where(c => !string.IsNullOrWhiteSpace(c.PaiUuid))
                    .GroupBy(c => c.PaiUuid!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Descricao).ToList(), StringComparer.OrdinalIgnoreCase);

                var paisOrdenados = filhosPorPai.Keys
                    .Where(paiId => mapaTodas.ContainsKey(paiId))
                    .Select(paiId => mapaTodas[paiId])
                    .OrderBy(p => p.Descricao)
                    .ToList();

                string? alvo = categoriaUuidParaSelecionar ?? (!_ehNovo ? _contaPR.CategoriaUuid : null);
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
                foreach (var solta in _categoriasDisponiveis.Where(c => !adicionados.Contains(c.CategoriaUuid.Trim())))
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
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar Categorias: {ex.Message}");
            }
        }

        private void RecarregarEntidadesDoBanco()
        {
            try
            {
                using var db = new AppDbContext();
                _todasEntidades = db.Entidades
                    .AsNoTracking()
                    .Where(e => !string.IsNullOrWhiteSpace(e.NomeRazao))
                    .OrderBy(e => e.NomeRazao)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar Entidades: {ex.Message}");
            }
        }

        private void PreencherCamposIniciais()
        {
            _formatandoTexto = true;

            CboContaCorrente.SelectedIndex = 0;
            if (!_ehNovo && !string.IsNullOrWhiteSpace(_contaPR.ContaUuid))
            {
                foreach (ComboBoxItem item in CboContaCorrente.Items)
                {
                    if (item.Tag?.ToString() == _contaPR.ContaUuid)
                    {
                        CboContaCorrente.SelectedItem = item;
                        break;
                    }
                }
            }

            TxtDataLancamento.Text = (_contaPR.DataLancamento == default ? DateTime.Today : _contaPR.DataLancamento).ToString("dd/MM/yyyy");
            TxtDescricao.Text = _contaPR.Descricao ?? string.Empty;
            TxtNumeroDocumento.Text = _contaPR.NumeroDocumento ?? string.Empty;

            _entidadeSelecionada = null;
            AsbEntidade.Text = string.Empty;
            if (!_ehNovo && !string.IsNullOrWhiteSpace(_contaPR.EntidadeUuid))
            {
                _entidadeSelecionada = _todasEntidades.FirstOrDefault(e => e.EntidadeUuid == _contaPR.EntidadeUuid);
                if (_entidadeSelecionada != null)
                {
                    AsbEntidade.Text = _entidadeSelecionada.NomeRazao;
                }
            }

            if (!_ehNovo)
            {
                var parcelasSalvas = ContasPRService.ObterGradeParcelas(_contaPR);
                var primeira = parcelasSalvas.First();

                _valorBase = primeira.ValorBase > 0 ? primeira.ValorBase : _contaPR.Valor;
                TxtValor.Text = _valorBase > 0 ? _valorBase.ToString("N2", _cultura) : string.Empty;

                TxtNumeroParcelas.Text = Math.Max(1, primeira.TotalParcelas).ToString();
                TxtIntervaloDias.Text = (primeira.IntervaloDias >= 0 ? primeira.IntervaloDias : 30).ToString();

                SelecionarModoNoCombo(primeira.ModoParcelamento);

                _gradeParcelas.Clear();
                foreach (var p in parcelasSalvas)
                {
                    _gradeParcelas.Add(p);
                }
            }
            else
            {
                _valorBase = 0m;
                TxtValor.Text = string.Empty;
                TxtNumeroParcelas.Text = "1";
                TxtIntervaloDias.Text = "30";
                SelecionarModoNoCombo("R");

                _formatandoTexto = false;
                RecalcularGradeAutomaticamente();
                _formatandoTexto = true;
            }

            _formatandoTexto = false;
            AtualizarResumoTotalGrade();
        }

        private string ObterModoParcelamentoSelecionado()
        {
            return (CboModoParcelas?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "R";
        }

        private void SelecionarModoNoCombo(string modoTag)
        {
            string alvo = (modoTag ?? "R").ToUpperInvariant() == "D" ? "D" : "R";
            foreach (ComboBoxItem item in CboModoParcelas.Items)
            {
                if (item.Tag?.ToString() == alvo)
                {
                    CboModoParcelas.SelectedItem = item;
                    break;
                }
            }
        }

        #region Geração e Edição da Grade de Vencimentos / Parcelas

        private void ParametroParcelas_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoTela || _formatandoTexto) return;

            if (sender as TextBox == TxtValor)
            {
                FormatarCampoMoeda(TxtValor, out _valorBase);
            }
            else if (sender as TextBox == TxtNumeroParcelas)
            {
                FiltrarApenasDigitos(TxtNumeroParcelas);
            }
            else if (sender as TextBox == TxtIntervaloDias)
            {
                FiltrarApenasDigitos(TxtIntervaloDias);
            }

            RecalcularGradeAutomaticamente();
        }

        private void CboModoParcelas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregandoTela || _formatandoTexto) return;
            RecalcularGradeAutomaticamente();
        }

        private void TxtNumeroParcelas_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtNumeroParcelas.Text.Trim(), out int qtd) || qtd < 1)
            {
                _formatandoTexto = true;
                TxtNumeroParcelas.Text = "1";
                _formatandoTexto = false;
                RecalcularGradeAutomaticamente();
            }
        }

        private void TxtIntervaloDias_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtIntervaloDias.Text.Trim(), out int dias) || dias < 0)
            {
                _formatandoTexto = true;
                TxtIntervaloDias.Text = "30";
                _formatandoTexto = false;
                RecalcularGradeAutomaticamente();
            }
        }

        private void BtnRecalcularGrade_Click(object sender, RoutedEventArgs e)
        {
            TxtNumeroParcelas_LostFocus(sender, e);
            TxtIntervaloDias_LostFocus(sender, e);
            RecalcularGradeAutomaticamente();
        }

        private void RecalcularGradeAutomaticamente()
        {
            if (!int.TryParse(TxtNumeroParcelas?.Text?.Trim(), out int qtdParcelas) || qtdParcelas < 1)
                qtdParcelas = 1;

            if (qtdParcelas > 360)
                qtdParcelas = 360;

            if (!int.TryParse(TxtIntervaloDias?.Text?.Trim(), out int intervaloDias) || intervaloDias < 0)
                intervaloDias = 30;

            string modo = ObterModoParcelamentoSelecionado(); // "R" = Repetir | "D" = Dividir

            if (!DateTime.TryParseExact(TxtDataLancamento?.Text?.Trim() ?? "", "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dataBase))
            {
                dataBase = DateTime.Today;
            }

            if (_gradeParcelas.Count > 0 &&
                DateTime.TryParseExact(_gradeParcelas[0].DataVencimentoTexto?.Trim() ?? "", "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dataPrimeiraExistente))
            {
                dataBase = dataPrimeiraExistente;
            }

            var uuidsExistentes = _gradeParcelas.ToDictionary(p => p.NumeroParcela, p => p.ContasPRUuid);
            string loteAtual = string.IsNullOrWhiteSpace(_contaPR.LoteUuid) ? Guid.NewGuid().ToString() : _contaPR.LoteUuid;

            decimal valorParcelaPadrao = _valorBase;
            decimal valorUltimaParcela = _valorBase;

            if (modo == "D" && qtdParcelas > 1)
            {
                valorParcelaPadrao = Math.Round(_valorBase / qtdParcelas, 2, MidpointRounding.ToZero);
                valorUltimaParcela = _valorBase - (valorParcelaPadrao * (qtdParcelas - 1));
            }

            _editandoCelulaGrid = true;
            _gradeParcelas.Clear();

            for (int i = 1; i <= qtdParcelas; i++)
            {
                DateTime vencimento = dataBase.AddDays((i - 1) * intervaloDias);
                decimal valorDesta = (i == qtdParcelas) ? valorUltimaParcela : valorParcelaPadrao;

                string contaUuidParcela = uuidsExistentes.TryGetValue(i, out string? idAntigo) && !string.IsNullOrWhiteSpace(idAntigo)
                    ? idAntigo
                    : (i == 1 && !string.IsNullOrWhiteSpace(_contaPR.ContasPRUuid) ? _contaPR.ContasPRUuid : Guid.NewGuid().ToString());

                _gradeParcelas.Add(new ContaPRParcela
                {
                    ParcelaUuid = Guid.NewGuid().ToString(),
                    LoteUuid = loteAtual,
                    ContasPRUuid = contaUuidParcela,
                    NumeroParcela = i,
                    TotalParcelas = qtdParcelas,
                    ModoParcelamento = modo,
                    IntervaloDias = intervaloDias,
                    ValorBase = _valorBase,
                    DataVencimento = vencimento,
                    DataVencimentoTexto = vencimento.ToString("dd/MM/yyyy"),
                    ValorParcela = valorDesta,
                    ValorParcelaTexto = valorDesta.ToString("N2", _cultura),
                    DestaqueCor = contaUuidParcela == _contaPR.ContasPRUuid ? "#3399FF" : "#00FF66"
                });
            }

            _editandoCelulaGrid = false;
            AtualizarResumoTotalGrade();
        }

        private void TxtVencimentoGrid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_editandoCelulaGrid || sender is not TextBox tb) return;

            _editandoCelulaGrid = true;
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

            if (tb.Tag is ContaPRParcela parcela)
            {
                parcela.DataVencimentoTexto = formatada;
                if (DateTime.TryParseExact(formatada, "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtValida))
                {
                    parcela.DataVencimento = dtValida;
                }
            }
            _editandoCelulaGrid = false;
        }

        private void TxtValorGrid_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_editandoCelulaGrid || sender is not TextBox tb) return;

            _editandoCelulaGrid = true;
            FormatarCampoMoeda(tb, out decimal valorDigitado);

            if (tb.Tag is ContaPRParcela parcela)
            {
                parcela.ValorParcela = valorDigitado;
                parcela.ValorParcelaTexto = tb.Text;
            }
            _editandoCelulaGrid = false;

            AtualizarResumoTotalGrade();
        }

        private void AtualizarResumoTotalGrade()
        {
            if (TxtTotalCalculado == null || TxtResumoParcelas == null) return;

            decimal somaParcelas = _gradeParcelas.Sum(p => p.ValorParcela);

            string modoTexto = ObterModoParcelamentoSelecionado() == "D" ? "DIVIDIDO" : "REPETIDO";
            TxtResumoParcelas.Text = $"GRADE: {_gradeParcelas.Count} PARCELA(S) [{modoTexto}]";
            TxtTotalCalculado.Text = $"Total das Parcelas: {somaParcelas.ToString("C2", _cultura)}";
        }

        #endregion

        #region Botões [+] ADD (Categoria e Entidade)

        private void BtnAddCategoria_Click(object sender, RoutedEventArgs e)
        {
            string tipoCat = TipoCategoriaEquivalente;

            HashSet<string> idsAntes;
            Categoria? grupoPaiPadrao;
            using (var db = new AppDbContext())
            {
                idsAntes = db.Categorias.AsNoTracking().Select(c => c.CategoriaUuid).ToHashSet();
                grupoPaiPadrao = db.Categorias.AsNoTracking().FirstOrDefault(c => c.Tipo == tipoCat && c.Nivel == 1);
            }

            var novaCategoria = new Categoria
            {
                CategoriaUuid = Guid.NewGuid().ToString(),
                Tipo = tipoCat,
                Nivel = grupoPaiPadrao != null ? (short)2 : (short)1,
                PaiUuid = grupoPaiPadrao?.CategoriaUuid,
                AceitaLancamento = true
            };

            var editor = new CategoriaEditWindow(novaCategoria);
            UIThemeHelper.AbrirModal(editor, this);

            editor.Closed += (s, ev) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _retornandoDeModal = true;

                        using var db = new AppDbContext();
                        var ultimaCriada = db.Categorias
                            .AsNoTracking()
                            .Where(c => !idsAntes.Contains(c.CategoriaUuid))
                            .OrderByDescending(c => c.CriadoEm)
                            .FirstOrDefault();

                        RecarregarCategoriasCombo(ultimaCriada?.CategoriaUuid);
                        AsbEntidade.Focus(FocusState.Programmatic);
                    }
                    finally
                    {
                        _retornandoDeModal = false;
                    }
                });
            };
        }

        private void AsbEntidade_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_retornandoDeModal) return;

            FiltrarSugestoesEntidade(AsbEntidade.Text);
            AsbEntidade.IsSuggestionListOpen = true;
        }

        private void AsbEntidade_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _entidadeSelecionada = null;
                FiltrarSugestoesEntidade(sender.Text);
            }
        }

        private void FiltrarSugestoesEntidade(string? texto)
        {
            string termo = (texto ?? string.Empty).Trim().ToLowerInvariant();
            var filtradas = string.IsNullOrEmpty(termo)
                ? _todasEntidades
                : _todasEntidades.Where(e =>
                    (!string.IsNullOrEmpty(e.NomeRazao) && e.NomeRazao.ToLowerInvariant().Contains(termo)) ||
                    (!string.IsNullOrEmpty(e.CpfCnpj) && e.CpfCnpj.Contains(termo))).ToList();

            AsbEntidade.DisplayMemberPath = "NomeRazao";
            AsbEntidade.TextMemberPath = "NomeRazao";
            AsbEntidade.ItemsSource = filtradas;
        }

        private void AsbEntidade_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is Entidade ent)
            {
                _entidadeSelecionada = ent;
                sender.Text = ent.NomeRazao;
            }
            else if (args.SelectedItem is string nomeEscolhido)
            {
                _entidadeSelecionada = _todasEntidades.FirstOrDefault(e =>
                    string.Equals(e.NomeRazao, nomeEscolhido, StringComparison.OrdinalIgnoreCase));
                sender.Text = nomeEscolhido;
            }
        }

        private void BtnAddEntidade_Click(object sender, RoutedEventArgs e)
        {
            var idsAntes = _todasEntidades.Select(x => x.EntidadeUuid).ToHashSet();

            var novaEntidade = new Entidade
            {
                EntidadeUuid = Guid.NewGuid().ToString(),
                TipoEntidade = _contaPR.Tipo == "R" ? "Cliente" : "Fornecedor",
                TipoPessoa = "F",
                NomeRazao = AsbEntidade.Text?.Trim() ?? string.Empty
            };

            var editor = new EntidadeEditWindow(novaEntidade);
            UIThemeHelper.AbrirModal(editor, this);

            editor.Closed += (s, ev) =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        _retornandoDeModal = true;
                        RecarregarEntidadesDoBanco();
                        FiltrarSugestoesEntidade(string.Empty);

                        var recemCriada = _todasEntidades.FirstOrDefault(x => !idsAntes.Contains(x.EntidadeUuid));
                        if (recemCriada != null)
                        {
                            _entidadeSelecionada = recemCriada;
                            AsbEntidade.Text = recemCriada.NomeRazao;
                            TxtDescricao.Focus(FocusState.Programmatic);
                        }
                    }
                    finally
                    {
                        _retornandoDeModal = false;
                    }
                });
            };
        }

        #endregion

        #region Formatação de Datas e Valores

        private void TxtDataLancamento_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtData_TextChanged(sender, e);

            if (!_carregandoTela && _ehNovo && TxtDataLancamento.Text.Length == 10 &&
                DateTime.TryParseExact(TxtDataLancamento.Text, "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtLanc))
            {
                if (_gradeParcelas.Count > 0)
                {
                    _gradeParcelas[0].DataVencimento = dtLanc;
                    _gradeParcelas[0].DataVencimentoTexto = dtLanc.ToString("dd/MM/yyyy");
                    RecalcularGradeAutomaticamente();
                }
            }
        }

        private void FormatarCampoMoeda(TextBox tb, out decimal valorDecimal)
        {
            _formatandoTexto = true;
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
                tb.Text = string.Empty;
            }
            _formatandoTexto = false;
        }

        private void FiltrarApenasDigitos(TextBox tb)
        {
            _formatandoTexto = true;
            string digitos = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (tb.Text != digitos)
            {
                tb.Text = digitos;
                tb.SelectionStart = digitos.Length;
            }
            _formatandoTexto = false;
        }

        private void TxtData_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_formatandoTexto || sender is not TextBox tb) return;

            _formatandoTexto = true;
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
            _formatandoTexto = false;
        }

        #endregion

        #region Salvar e Excluir

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string? contaUuid = (CboContaCorrente.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(contaUuid))
            {
                ExibirMensagem("> [ERRO] Selecione uma Conta Corrente no combo.", true);
                CboContaCorrente.Focus(FocusState.Programmatic);
                return;
            }

            if (!DateTime.TryParseExact(TxtDataLancamento.Text.Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtLanc))
            {
                ExibirMensagem("> [ERRO] Informe uma Data de Lancamento valida (dd/mm/aaaa).", true);
                TxtDataLancamento.Focus(FocusState.Programmatic);
                return;
            }

            Categoria? categoriaSelecionada = (CboCategoria.SelectedItem as ComboBoxItem)?.Tag as Categoria
                                           ?? CboCategoria.SelectedItem as Categoria;

            if (categoriaSelecionada == null)
            {
                ExibirMensagem("> [ERRO] Selecione uma Categoria no combo.", true);
                CboCategoria.Focus(FocusState.Programmatic);
                return;
            }

            if (_entidadeSelecionada == null && !string.IsNullOrWhiteSpace(AsbEntidade.Text))
            {
                _entidadeSelecionada = _todasEntidades.FirstOrDefault(ent =>
                    ent.NomeRazao.Equals(AsbEntidade.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (_entidadeSelecionada == null)
            {
                ExibirMensagem("> [ERRO] Selecione uma Entidade (Cliente/Fornecedor) na lista.", true);
                AsbEntidade.Focus(FocusState.Programmatic);
                return;
            }

            string descricao = TxtDescricao.Text.Trim();
            if (string.IsNullOrEmpty(descricao))
            {
                ExibirMensagem("> [ERRO] A Descricao da conta e obrigatoria.", true);
                TxtDescricao.Focus(FocusState.Programmatic);
                return;
            }

            string? numeroDocumento = string.IsNullOrWhiteSpace(TxtNumeroDocumento.Text)
                ? null
                : TxtNumeroDocumento.Text.Trim();

            if (!int.TryParse(TxtNumeroParcelas.Text.Trim(), out int qtdParcelas) || qtdParcelas < 1)
            {
                ExibirMensagem("> [ERRO] O numero de parcelas nao pode ser menor que 1.", true);
                TxtNumeroParcelas.Focus(FocusState.Programmatic);
                return;
            }

            if (!int.TryParse(TxtIntervaloDias.Text.Trim(), out int intervaloDias) || intervaloDias < 0)
            {
                intervaloDias = 30;
            }

            if (_gradeParcelas.Count == 0)
            {
                ExibirMensagem("> [ERRO] A grade de vencimentos deve conter pelo menos 1 parcela.", true);
                return;
            }

            for (int i = 0; i < _gradeParcelas.Count; i++)
            {
                var p = _gradeParcelas[i];
                if (!DateTime.TryParseExact((p.DataVencimentoTexto ?? "").Trim(), "dd/MM/yyyy", _cultura, DateTimeStyles.None, out DateTime dtVencParcela))
                {
                    ExibirMensagem($"> [ERRO] Data de vencimento invalida na parcela {p.NumeroFormatado}. Use dd/mm/aaaa.", true);
                    return;
                }
                p.DataVencimento = dtVencParcela;

                if (p.ValorParcela <= 0m)
                {
                    ExibirMensagem($"> [ERRO] O valor da parcela {p.NumeroFormatado} deve ser maior que zero.", true);
                    return;
                }
            }

            string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

            try
            {
                _contaPR.UsuarioUuid = usuarioUuid;
                _contaPR.ContaUuid = contaUuid;
                _contaPR.CategoriaUuid = categoriaSelecionada.CategoriaUuid;
                _contaPR.EntidadeUuid = _entidadeSelecionada.EntidadeUuid;
                _contaPR.Descricao = descricao;
                _contaPR.NumeroDocumento = numeroDocumento;
                _contaPR.DataLancamento = dtLanc;
                _contaPR.ValorBase = _valorBase > 0 ? _valorBase : _gradeParcelas.Sum(p => p.ValorParcela);
                _contaPR.ModoParcelamento = ObterModoParcelamentoSelecionado();
                _contaPR.TotalParcelas = _gradeParcelas.Count;
                _contaPR.IntervaloDias = intervaloDias;

                ContasPRService.SalvarContaPRComGrade(_contaPR, _gradeParcelas.ToList(), _ehNovo);
                this.Close();
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO SALVAR]: {ex.Message}", true);
            }
        }

        private async void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var futuras = ContasPRService.ObterParcelasFuturasRelacionadas(_contaPR.ContasPRUuid);
                string avisoQuitado = _contaPR.EstaQuitado
                    ? "\n\n[!] Esta parcela já está baixada: o lançamento correspondente na Conta Corrente também será estornado."
                    : string.Empty;

                if (futuras.Count > 0)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Excluir parcela vinculada",
                        Content = $"Esta conta ({_contaPR.ParcelaFormatada}) possui {futuras.Count} parcela(s) futura(s) relacionada(s).\n\n" +
                                  "O que deseja apagar?\n" +
                                  "• Somente esta: apaga apenas a parcela atual.\n" +
                                  $"• Esta e as futuras ({1 + futuras.Count} no total): apaga esta e todas as parcelas seguintes.{avisoQuitado}",
                        PrimaryButtonText = "Somente esta",
                        SecondaryButtonText = $"Esta e as {futuras.Count} futuras",
                        CloseButtonText = "Cancelar",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var resultado = await dialog.ShowAsync();
                    if (resultado == ContentDialogResult.None)
                        return;

                    bool excluirTodasFuturas = resultado == ContentDialogResult.Secondary;
                    ContasPRService.ExcluirContaPR(_contaPR.ContasPRUuid, excluirTodasFuturas);
                    this.Close();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Confirmar exclusão",
                        Content = $"Deseja realmente apagar a conta \"{_contaPR.Descricao}\" ({_contaPR.ParcelaFormatada})?{avisoQuitado}",
                        PrimaryButtonText = "Sim, apagar",
                        CloseButtonText = "Cancelar",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var resultado = await dialog.ShowAsync();
                    if (resultado != ContentDialogResult.Primary)
                        return;

                    ContasPRService.ExcluirContaPR(_contaPR.ContasPRUuid, excluirFuturas: false);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO EXCLUIR]: {ex.Message}", true);
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

        #endregion
    }
}