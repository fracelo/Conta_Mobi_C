using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace ContaMobi.Views
{
    public sealed partial class CategoriaEditWindow : Window
    {
        private Categoria _categoria;
        private bool _ehNovo;
        private bool _carregamentoInicialConcluido = false;
        private bool _atualizandoCombo = false;

        private ResultadoVinculoCategoria _vinculoCategoria = new();
        private string _tipoOriginal = "D";
        private short _nivelOriginal = 1;
        private string? _paiUuidOriginal = null;
        private bool _aceitaLancamentoOriginal = false;

        public CategoriaEditWindow(Categoria categoria)
        {
            this.InitializeComponent();
            UIThemeHelper.AplicarBarraTituloRetro(this);
            EnterNavegacaoHelper.HabilitarEnterComoTab(this);
            this.AppWindow.Resize(new SizeInt32(620, 560));

            _categoria = categoria;

            using (var db = new AppDbContext())
            {
                _ehNovo = string.IsNullOrEmpty(_categoria.CategoriaUuid) || db.Categorias.Find(_categoria.CategoriaUuid) == null;
            }

            if (!_ehNovo)
            {
                _tipoOriginal = _categoria.Tipo;
                _nivelOriginal = _categoria.Nivel;
                _paiUuidOriginal = _categoria.PaiUuid;
                _aceitaLancamentoOriginal = _categoria.AceitaLancamento;
                _vinculoCategoria = CategoriaValidacaoService.VerificarVinculos(_categoria.CategoriaUuid);
            }

            TxtTitulo.Text = _ehNovo ? "[ NOVA CATEGORIA // PLANO DE CONTAS ]" : "[ EDITANDO CATEGORIA ]";
            BtnExcluir.Visibility = _ehNovo ? Visibility.Collapsed : Visibility.Visible;

            PreencherCamposIniciais();

            if (this.Content is FrameworkElement root)
            {
                root.Loaded += (s, e) =>
                {
                    if (!_carregamentoInicialConcluido)
                    {
                        _carregamentoInicialConcluido = true;
                        PreencherCamposIniciais();
                        TxtDescricao.Focus(FocusState.Programmatic);
                    }
                };
            }
        }

        private void PreencherCamposIniciais()
        {
            TxtDescricao.Text = _categoria.Descricao ?? string.Empty;

            if (_categoria.Tipo == "C")
                RbReceita.IsChecked = true;
            else
                RbDespesa.IsChecked = true;

            // Por padrão, na inclusão o combo inicia vazio (null). Na edição de Nível 2, carrega o Pai atual.
            string? paiInicial = (!_ehNovo && _categoria.Nivel == 2) ? _categoria.PaiUuid : null;

            CarregarCategoriasPai(paiInicial);
            AtualizarAvisoNivel();
            AplicarRestricoesDeVinculo();
        }

        private void AplicarRestricoesDeVinculo()
        {
            if (_ehNovo || !_vinculoCategoria.PossuiMovimentacao)
            {
                RbDespesa.IsEnabled = true;
                RbReceita.IsEnabled = true;
                CboCategoriaPai.IsEnabled = true;
                BtnLimparPai.IsEnabled = true;
                return;
            }

            // Possui lançamentos ou Contas P&R: libera somente a alteração da descrição
            RbDespesa.IsEnabled = false;
            RbReceita.IsEnabled = false;
            CboCategoriaPai.IsEnabled = false;
            BtnLimparPai.IsEnabled = false;

            ExibirMensagem($"> [RESTRIÇÃO] {_vinculoCategoria.MensagemBloqueioEdicao}", true);
        }

        private void CarregarCategoriasPai(string? paiUuidParaSelecionar = null)
        {
            if (CboCategoriaPai == null) return;

            _atualizandoCombo = true;

            string tipoAtual = RbReceita?.IsChecked == true ? "C" : "D";

            using var db = new AppDbContext();
            var paisDoBanco = db.Categorias
                .AsNoTracking()
                .Where(c => c.Nivel == 1 && c.Tipo == tipoAtual && c.CategoriaUuid != _categoria.CategoriaUuid)
                .OrderBy(c => c.Descricao)
                .ToList();

            var listaCombo = new List<Categoria>
            {
                new Categoria
                {
                    CategoriaUuid = string.Empty,
                    Descricao = "[ DEIXAR VAZIO - CRIAR CATEGORIA DE 1º NIVEL (PAI) ]"
                }
            };
            listaCombo.AddRange(paisDoBanco);

            CboCategoriaPai.ItemsSource = listaCombo;
            CboCategoriaPai.DisplayMemberPath = "Descricao";
            CboCategoriaPai.SelectedValuePath = "CategoriaUuid";

            if (!string.IsNullOrWhiteSpace(paiUuidParaSelecionar))
            {
                var encontrado = listaCombo.FirstOrDefault(p => p.CategoriaUuid == paiUuidParaSelecionar);
                CboCategoriaPai.SelectedItem = encontrado;
            }
            else
            {
                CboCategoriaPai.SelectedItem = null;
            }

            _atualizandoCombo = false;
        }

        private Categoria? ObterCategoriaPaiSelecionada()
        {
            if (CboCategoriaPai?.SelectedItem is Categoria selecionada &&
                !string.IsNullOrWhiteSpace(selecionada.CategoriaUuid))
            {
                return selecionada;
            }
            return null;
        }

        private void RbTipo_Changed(object sender, RoutedEventArgs e)
        {
            if (!_carregamentoInicialConcluido || _vinculoCategoria.PossuiMovimentacao) return;
            CarregarCategoriasPai(null);
            AtualizarAvisoNivel();
        }

        private void CboCategoriaPai_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizandoCombo || _vinculoCategoria.PossuiMovimentacao) return;

            if (CboCategoriaPai.SelectedItem is Categoria item && string.IsNullOrEmpty(item.CategoriaUuid))
            {
                _atualizandoCombo = true;
                CboCategoriaPai.SelectedItem = null;
                _atualizandoCombo = false;
            }

            AtualizarAvisoNivel();
        }

        private void BtnLimparPai_Click(object sender, RoutedEventArgs e)
        {
            if (_vinculoCategoria.PossuiMovimentacao) return;

            _atualizandoCombo = true;
            CboCategoriaPai.SelectedItem = null;
            _atualizandoCombo = false;
            AtualizarAvisoNivel();
            TxtDescricao.Focus(FocusState.Programmatic);
        }

        private void AtualizarAvisoNivel()
        {
            if (LblInfoNivel == null) return;

            if (!_ehNovo && _vinculoCategoria.PossuiMovimentacao)
            {
                LblInfoNivel.Text = "> CATEGORIA COM MOVIMENTAÇÃO: TIPO (D/C) E NÍVEL BLOQUEADOS — APENAS A DESCRIÇÃO PODE SER EDITADA";
                LblInfoNivel.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 170, 0));
                return;
            }

            var pai = ObterCategoriaPaiSelecionada();
            if (pai == null)
            {
                LblInfoNivel.Text = "> COMBO VAZIO: A CATEGORIA SERA NIVEL 1 (GRUPO PAI - NAO ACEITA LANCAMENTO)";
                LblInfoNivel.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 170, 0));
            }
            else
            {
                LblInfoNivel.Text = $"> COMBO SELECIONADO [{pai.Descricao}]: A CATEGORIA SERA NIVEL 2 (ACEITA LANCAMENTO)";
                LblInfoNivel.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102));
            }
        }

        private void TxtDescricao_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                BtnSalvar_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (!_vinculoCategoria.PossuiMovimentacao)
                TxtMensagem.Visibility = Visibility.Collapsed;

            string descricao = TxtDescricao.Text.Trim();
            if (string.IsNullOrEmpty(descricao))
            {
                ExibirMensagem("> [ERRO] Digite a descricao da categoria.", true);
                TxtDescricao.Focus(FocusState.Programmatic);
                return;
            }

            var xamlRoot = this.Content?.XamlRoot;
            if (xamlRoot != null)
            {
                var confirmar = new ContentDialog
                {
                    Title = "CONFIRMAR OPERAÇÃO",
                    Content = _ehNovo
                        ? $"Deseja incluir a categoria \"{descricao}\"?"
                        : $"Deseja salvar as alterações da categoria \"{descricao}\"?",
                    PrimaryButtonText = "SALVAR",
                    CloseButtonText = "CANCELAR",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                if (await confirmar.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            string tipo = RbReceita.IsChecked == true ? "C" : "D";
            var categoriaPai = ObterCategoriaPaiSelecionada();

            short nivel = categoriaPai == null ? (short)1 : (short)2;
            string? paiUuid = categoriaPai?.CategoriaUuid;
            bool aceitaLancamento = (nivel == 2);

            string usuarioUuid = App.SupabaseClient?.Auth.CurrentUser?.Id ?? string.Empty;

            try
            {
                using var db = new AppDbContext();

                if (_ehNovo)
                {
                    string novoId = Guid.NewGuid().ToString();
                    var nova = new Categoria
                    {
                        CategoriaUuid = novoId,
                        UsuarioUuid = usuarioUuid,
                        Descricao = descricao,
                        Tipo = tipo,
                        Nivel = nivel,
                        PaiUuid = paiUuid,
                        AceitaLancamento = aceitaLancamento,
                        CriadoEm = DateTime.UtcNow,
                        AtualizadoEm = DateTime.UtcNow
                    };

                    db.Categorias.Add(nova);
                    db.SaveChanges();

                    _categoria = new Categoria
                    {
                        CategoriaUuid = Guid.NewGuid().ToString(),
                        Tipo = tipo
                    };
                    TxtDescricao.Text = string.Empty;

                    if (nivel == 1)
                    {
                        CarregarCategoriasPai(novoId);
                        AtualizarAvisoNivel();
                        ExibirMensagem($"> [OK] Categoria Nivel 1 '{descricao}' criada e selecionada no combo! Digite agora a subcategoria (Nivel 2) ou clique em FECHAR.", false);
                    }
                    else
                    {
                        CarregarCategoriasPai(paiUuid);
                        AtualizarAvisoNivel();
                        ExibirMensagem($"> [OK] Subcategoria Nivel 2 '{descricao}' gravada! Digite outra ou clique em FECHAR.", false);
                    }

                    TxtDescricao.Focus(FocusState.Programmatic);
                }
                else
                {
                    // Revalida vínculos no banco antes de gravar
                    _vinculoCategoria = CategoriaValidacaoService.VerificarVinculos(_categoria.CategoriaUuid);

                    if (!_vinculoCategoria.PossuiMovimentacao &&
                        nivel == 2 &&
                        db.Categorias.Any(c => c.PaiUuid == _categoria.CategoriaUuid))
                    {
                        ExibirMensagem("> [ERRO] Esta categoria possui subcategorias vinculadas e deve permanecer como 1º Nivel (deixe o combo vazio).", true);
                        return;
                    }

                    var existente = db.Categorias.Find(_categoria.CategoriaUuid);
                    if (existente != null)
                    {
                        existente.Descricao = descricao;

                        if (_vinculoCategoria.PossuiMovimentacao)
                        {
                            // Garante que Tipo, Nível e Pai originais não sejam alterados
                            existente.Tipo = _tipoOriginal;
                            existente.Nivel = _nivelOriginal;
                            existente.PaiUuid = _paiUuidOriginal;
                            existente.AceitaLancamento = _aceitaLancamentoOriginal;
                        }
                        else
                        {
                            existente.Tipo = tipo;
                            existente.Nivel = nivel;
                            existente.PaiUuid = paiUuid;
                            existente.AceitaLancamento = aceitaLancamento;
                        }

                        existente.AtualizadoEm = DateTime.UtcNow;
                        db.SaveChanges();
                    }

                    ExibirMensagem($"> [OK] Categoria '{descricao}' atualizada com sucesso! Clique em FECHAR para sair.", false);
                }
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO SALVAR]: {ex.Message}", true);
            }
        }

        private async void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            var xamlRoot = this.Content?.XamlRoot;

            try
            {
                if (string.Equals(_categoria.CategoriaUuid?.Trim(), TransferenciaService.CategoriaTransferenciaUuid, StringComparison.OrdinalIgnoreCase))
                {
                    await MostrarAvisoAsync(xamlRoot, "EXCLUSÃO NÃO PERMITIDA", "Esta é uma categoria interna do sistema e não pode ser excluída.");
                    return;
                }

                // 1. Verifica lançamentos em conta corrente e Contas a Pagar/Receber
                _vinculoCategoria = CategoriaValidacaoService.VerificarVinculos(_categoria.CategoriaUuid);
                if (_vinculoCategoria.PossuiMovimentacao)
                {
                    await MostrarAvisoAsync(xamlRoot, "EXCLUSÃO NÃO PERMITIDA", _vinculoCategoria.MensagemBloqueioExclusao);
                    return;
                }

                using var db = new AppDbContext();

                // 2. Verifica se é Grupo Pai com subcategorias filhas
                bool temFilhos = db.Categorias.Any(c => c.PaiUuid == _categoria.CategoriaUuid);
                if (temFilhos)
                {
                    await MostrarAvisoAsync(
                        xamlRoot,
                        "EXCLUSÃO NÃO PERMITIDA",
                        "Não é possível excluir uma categoria de 1º Nível que possui subcategorias vinculadas. Exclua ou mova as subcategorias primeiro.");
                    return;
                }

                // 3. Confirma exclusão
                if (xamlRoot != null)
                {
                    var confirmar = new ContentDialog
                    {
                        Title = "CONFIRMAR EXCLUSÃO",
                        Content = $"Deseja realmente excluir a categoria \"{_categoria.Descricao}\"?",
                        PrimaryButtonText = "EXCLUIR",
                        CloseButtonText = "CANCELAR",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = xamlRoot
                    };

                    if (await confirmar.ShowAsync() != ContentDialogResult.Primary)
                        return;
                }

                var existente = db.Categorias.Find(_categoria.CategoriaUuid);
                if (existente != null)
                {
                    db.Categorias.Remove(existente);
                    db.SaveChanges();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                ExibirMensagem($"> [ERRO AO EXCLUIR]: {ex.Message}", true);
            }
        }

        private async System.Threading.Tasks.Task MostrarAvisoAsync(XamlRoot? xamlRoot, string titulo, string mensagem)
        {
            if (xamlRoot != null)
            {
                var aviso = new ContentDialog
                {
                    Title = titulo,
                    Content = mensagem,
                    CloseButtonText = "ENTENDI",
                    XamlRoot = xamlRoot
                };
                await aviso.ShowAsync();
            }
            else
            {
                ExibirMensagem($"> [ERRO] {mensagem}", true);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => this.Close();

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