using ContaMobi.Data;
using ContaMobi.Helpers;
using ContaMobi.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.System;

namespace ContaMobi.Views
{
    public sealed partial class CartoesCreditoPage : Page
    {
        private readonly CultureInfo _cultura = new("pt-BR");

        public CartoesCreditoPage()
        {
            this.InitializeComponent();

            this.Loaded += (s, e) =>
            {
                CartaoCreditoCadastroService.GarantirEstruturaCartoes();
                CarregarCartoes();
            };
        }

        public void CarregarCartoes()
        {
            if (!this.IsLoaded || ListaCartoes == null)
                return;

            string status = (CboFiltroStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODOS";
            string busca = TxtBusca.Text.Trim();

            try
            {
                var resumo = CartaoCreditoCadastroService.ListarCartoes(status, busca);

                ListaCartoes.ItemsSource = resumo.Registros;
                TxtTotalRegistros.Text = $"CARTOES: {resumo.Registros.Count}";

                TxtLimiteTotalGeral.Text = $"Limite Total: {resumo.TotalLimiteGeral.ToString("C2", _cultura)}";
                TxtLimiteUtilizadoGeral.Text = $"Utilizado: {resumo.TotalLimiteUtilizado.ToString("C2", _cultura)}";
                TxtLimiteDisponivelGeral.Text = $"Disponivel: {resumo.TotalLimiteDisponivel.ToString("C2", _cultura)}";
            }
            catch (Exception ex)
            {
                TxtTotalRegistros.Text = $"ERRO: {ex.Message}";
            }
        }

        private void CboFiltroStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) CarregarCartoes();
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded) CarregarCartoes();
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) => CarregarCartoes();

        private void BtnIncluir_Click(object sender, RoutedEventArgs e)
        {
            var novo = new CartaoCredito
            {
                CartaoUuid = Guid.NewGuid().ToString(),
                DiaFechamento = 1,
                DiaVencimento = 10,
                CorHex = "#008552",
                Ativo = true
            };

            AbrirEditor(novo, ehNovo: true);
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartaoCredito cartao)
            {
                AbrirEditor(cartao, ehNovo: false);
            }
        }

        private async void BtnExcluirLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartaoCredito cartao)
            {
                await ConfirmarEExcluirCartaoAsync(cartao);
            }
        }

        private void ListaCartoes_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaCartoes.SelectedItem is CartaoCredito selecionado)
            {
                AbrirEditor(selecionado, ehNovo: false);
            }
        }

        private async void ListaCartoes_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F2)
            {
                BtnIncluir_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter && ListaCartoes.SelectedItem is CartaoCredito selecionadoEditar)
            {
                AbrirEditor(selecionadoEditar, ehNovo: false);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Delete && ListaCartoes.SelectedItem is CartaoCredito selecionadoExcluir)
            {
                e.Handled = true;
                await ConfirmarEExcluirCartaoAsync(selecionadoExcluir);
            }
        }

        private async Task ConfirmarEExcluirCartaoAsync(CartaoCredito cartao)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirmar exclusão do cartão",
                Content = $"Deseja realmente excluir o cartão \"{cartao.NomeExibicao}\" ({cartao.NomeBandeira})?\n\n" +
                          "Obs.: Cartões que já possuam faturas ou compras registradas não podem ser excluídos, devendo ser marcados como INATIVO.",
                PrimaryButtonText = "Sim, excluir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var resultado = await dialog.ShowAsync();
            if (resultado != ContentDialogResult.Primary)
                return;

            try
            {
                CartaoCreditoCadastroService.ExcluirCartaoCredito(cartao.CartaoUuid);
                CarregarCartoes();
            }
            catch (Exception ex)
            {
                var aviso = new ContentDialog
                {
                    Title = "Não foi possível excluir",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await aviso.ShowAsync();
            }
        }

        private void AbrirEditor(CartaoCredito cartao, bool ehNovo)
        {
            var janela = new CartaoCreditoEditWindow(cartao, ehNovo);
            janela.Closed += (s, ev) => CarregarCartoes();
            UIThemeHelper.AbrirModal(janela);
        }
    }
}