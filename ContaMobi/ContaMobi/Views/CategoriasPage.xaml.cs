using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace ContaMobi.Views
{
    public sealed partial class CategoriasPage : Page
    {
        private List<Categoria> _categoriasOrdenadas = new();

        public CategoriasPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => CarregarCategorias();
        }

        public void CarregarCategorias()
        {
            try
            {
                using var db = new AppDbContext();
                var todas = db.Categorias
                    .Include(c => c.CategoriaPai)
                    .ToList();

                var listaFinal = new List<Categoria>();

                // Ordena Grupos Pai (Nível 1) por Tipo (Receitas primeiro, depois Despesas) e Descrição
                var pais = todas
                    .Where(c => c.Nivel == 1)
                    .OrderBy(c => c.Tipo)
                    .ThenBy(c => c.Descricao)
                    .ToList();

                // Monta a estrutura linear em árvore adicionando os Filhos sob seus respectivos Pais
                foreach (var pai in pais)
                {
                    listaFinal.Add(pai);

                    var filhos = todas
                        .Where(c => c.PaiUuid == pai.CategoriaUuid)
                        .OrderBy(c => c.Descricao)
                        .ToList();

                    listaFinal.AddRange(filhos);
                }

                _categoriasOrdenadas = listaFinal;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO AO CARREGAR CATEGORIAS]: {ex.Message}");
            }
        }

        private void AplicarFiltro()
        {
            if (ListaCategorias == null || _categoriasOrdenadas == null)
                return;

            string busca = TxtBusca?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            string tipoFiltro = string.Empty;
            if (CboFiltroTipo?.SelectedItem is ComboBoxItem itemSelecionado && itemSelecionado.Tag != null)
            {
                tipoFiltro = itemSelecionado.Tag.ToString() ?? string.Empty;
            }

            var filtradas = _categoriasOrdenadas.AsEnumerable();

            if (!string.IsNullOrEmpty(tipoFiltro))
            {
                filtradas = filtradas.Where(c => c != null && c.Tipo == tipoFiltro);
            }

            if (!string.IsNullOrEmpty(busca))
            {
                filtradas = filtradas.Where(c => c != null && !string.IsNullOrEmpty(c.Descricao) && c.Descricao.ToLowerInvariant().Contains(busca));
            }

            var lista = filtradas.ToList();
            ListaCategorias.ItemsSource = lista;

            if (TxtTotalRegistros != null)
            {
                TxtTotalRegistros.Text = $"REGISTROS: {lista.Count}";
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private void CboFiltroTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                AplicarFiltro();
            }
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e) => AplicarFiltro();

        private void BtnInserir_Click(object sender, RoutedEventArgs e)
        {
            AbrirEditor(new Categoria
            {
                CategoriaUuid = Guid.NewGuid().ToString(),
                Nivel = 1,
                Tipo = "D",
                AceitaLancamento = false
            });
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Categoria categoria)
            {
                AbrirEditor(categoria);
            }
        }

        private async void BtnExcluirLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Categoria categoria)
                return;

            // Impede exclusão da categoria interna de transferência, se aplicável
            if (string.Equals(categoria.CategoriaUuid?.Trim(), TransferenciaService.CategoriaTransferenciaUuid, StringComparison.OrdinalIgnoreCase))
            {
                var avisoSistema = new ContentDialog
                {
                    Title = "EXCLUSÃO NÃO PERMITIDA",
                    Content = "Esta é uma categoria interna do sistema e não pode ser excluída.",
                    CloseButtonText = "ENTENDI",
                    XamlRoot = this.XamlRoot
                };
                await avisoSistema.ShowAsync();
                return;
            }

            // 1. Verifica se existem lançamentos em conta corrente ou Contas a Pagar/Receber
            var vinculo = CategoriaValidacaoService.VerificarVinculos(categoria.CategoriaUuid);
            if (vinculo.PossuiMovimentacao)
            {
                var aviso = new ContentDialog
                {
                    Title = "EXCLUSÃO NÃO PERMITIDA",
                    Content = vinculo.MensagemBloqueioExclusao,
                    CloseButtonText = "ENTENDI",
                    XamlRoot = this.XamlRoot
                };
                await aviso.ShowAsync();
                return;
            }

            // 2. Se for Grupo Pai (Nível 1), verifica se ainda existem subcategorias filhas
            using (var dbCheck = new AppDbContext())
            {
                bool possuiFilhas = dbCheck.Categorias
                    .AsNoTracking()
                    .Any(c => c.PaiUuid == categoria.CategoriaUuid);

                if (possuiFilhas)
                {
                    var avisoFilhas = new ContentDialog
                    {
                        Title = "EXCLUSÃO NÃO PERMITIDA",
                        Content = "Esta categoria pai possui subcategorias vinculadas. Exclua ou mova as subcategorias antes de excluir o grupo pai.",
                        CloseButtonText = "ENTENDI",
                        XamlRoot = this.XamlRoot
                    };
                    await avisoFilhas.ShowAsync();
                    return;
                }
            }

            // 3. Confirmação antes de excluir
            var confirmar = new ContentDialog
            {
                Title = "CONFIRMAR EXCLUSÃO",
                Content = $"Deseja realmente excluir a categoria \"{categoria.Descricao}\"?",
                PrimaryButtonText = "EXCLUIR",
                CloseButtonText = "CANCELAR",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirmar.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {
                using var db = new AppDbContext();
                var registro = db.Categorias.Find(categoria.CategoriaUuid);
                if (registro != null)
                {
                    db.Categorias.Remove(registro);
                    db.SaveChanges();
                }

                CarregarCategorias();
            }
            catch (Exception ex)
            {
                var erro = new ContentDialog
                {
                    Title = "ERRO AO EXCLUIR",
                    Content = $"Não foi possível excluir a categoria: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await erro.ShowAsync();
            }
        }

        private void ListaCategorias_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaCategorias.SelectedItem is Categoria selecionada)
            {
                AbrirEditor(selecionada);
            }
        }

        private void ListaCategorias_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ListaCategorias.SelectedItem is Categoria selecionada)
            {
                AbrirEditor(selecionada);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.F2)
            {
                BtnInserir_Click(sender, e);
                e.Handled = true;
            }
        }

        private void TxtBusca_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                AplicarFiltro();
                e.Handled = true;
            }
        }

        private void AbrirEditor(Categoria categoria)
        {
            var editor = new CategoriaEditWindow(categoria);
            editor.Closed += (s, ev) =>
            {
                this.DispatcherQueue.TryEnqueue(() => CarregarCategorias());
            };
            editor.Activate();
        }
    }
}