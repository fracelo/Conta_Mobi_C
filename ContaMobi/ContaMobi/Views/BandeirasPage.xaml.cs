using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ContaMobi.Data;
using ContaMobi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContaMobi.Views
{
    public sealed partial class BandeirasPage : Page
    {
        private List<Bandeira> _todasBandeiras = new List<Bandeira>();

        public BandeirasPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => CarregarBandeiras();
        }

        private string? ObterUsuarioUuidAtual()
        {
            return App.SupabaseClient?.Auth?.CurrentUser?.Id;
        }

        private void CarregarBandeiras()
        {
            try
            {
                string? usuarioUuid = ObterUsuarioUuidAtual();
                _todasBandeiras = CartaoCreditoService.ListarBandeiras(usuarioUuid, apenasAtivas: false);
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar bandeiras: {ex.Message}");
            }
        }

        private void AplicarFiltro()
        {
            string busca = TxtBusca.Text?.Trim().ToLower() ?? string.Empty;

            if (string.IsNullOrEmpty(busca))
            {
                ListaBandeiras.ItemsSource = _todasBandeiras;
            }
            else
            {
                ListaBandeiras.ItemsSource = _todasBandeiras.Where(b =>
                    !string.IsNullOrEmpty(b.DescricaoBandeira) &&
                    b.DescricaoBandeira.ToLower().Contains(busca)
                ).ToList();
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            AplicarFiltro();
        }

        private void BtnInserir_Click(object sender, RoutedEventArgs e)
        {
            var nova = new Bandeira
            {
                BandeiraUuid = Guid.NewGuid().ToString(),
                UsuarioUuid = ObterUsuarioUuidAtual(),
                Ativo = true
            };
            AbrirEditor(nova, ehNovo: true);
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Bandeira bandeiraSelecionada)
            {
                ListaBandeiras.SelectedItem = bandeiraSelecionada;
                AbrirEditor(bandeiraSelecionada, ehNovo: false);
            }
        }

        private void ListaBandeiras_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaBandeiras.SelectedItem is Bandeira bandeiraSelecionada)
            {
                AbrirEditor(bandeiraSelecionada, ehNovo: false);
            }
        }

        private void AbrirEditor(Bandeira bandeira, bool ehNovo)
        {
            var editWin = new BandeiraEditWindow(bandeira, ehNovo);
            editWin.Closed += (s, ev) => CarregarBandeiras();
            editWin.Activate();
        }
    }
}