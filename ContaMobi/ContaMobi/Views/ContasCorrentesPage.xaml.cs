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
    public sealed partial class ContasCorrentesPage : Page
    {
        private List<ContaCorrente> _todasContas = new();

        public ContasCorrentesPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => CarregarContas();
        }

        public void CarregarContas()
        {
            try
            {
                using var db = new AppDbContext();

                // AsNoTracking() evita locks desnecessários de leitura no SQLite
                _todasContas = db.ContasCorrentes
                    .AsNoTracking()
                    .Include(c => c.Banco)
                    .OrderBy(c => c.Descricao)
                    .ToList();

                AplicarFiltro();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO AO CARREGAR CONTAS]: {ex.Message}");
                if (TxtTotalRegistros != null)
                {
                    TxtTotalRegistros.Text = $"ERRO: {ex.Message}";
                    TxtTotalRegistros.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 85, 85));
                }
            }
        }

        private void AplicarFiltro()
        {
            string busca = TxtBusca.Text?.Trim().ToLower() ?? string.Empty;

            List<ContaCorrente> resultado;

            if (string.IsNullOrEmpty(busca))
            {
                resultado = _todasContas;
            }
            else
            {
                resultado = _todasContas.Where(c =>
                    (!string.IsNullOrEmpty(c.Descricao) && c.Descricao.ToLower().Contains(busca)) ||
                    (c.Banco != null && c.Banco.DescricaoBanco.ToLower().Contains(busca)) ||
                    c.CodigoBanco.ToString().Contains(busca)
                ).ToList();
            }

            ListaContas.ItemsSource = resultado;
            TxtTotalRegistros.Text = $"REGISTROS: {resultado.Count}";
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
            // Cria nova conta com ID vazio/novo e abre o formulário
            AbrirEditor(new ContaCorrente
            {
                ContaUuid = Guid.NewGuid().ToString(),
                DataCriacao = DateTime.Today,
                SaldoInicial = 0.00m
            });
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContaCorrente selecionada)
            {
                ListaContas.SelectedItem = selecionada;
                AbrirEditor(selecionada);
            }
        }

        private void ListaContas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaContas.SelectedItem is ContaCorrente selecionada)
            {
                AbrirEditor(selecionada);
            }
        }

        private void ListaContas_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ListaContas.SelectedItem is ContaCorrente selecionada)
            {
                AbrirEditor(selecionada);
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

        private void AbrirEditor(ContaCorrente conta)
        {
            // Instancia a janela modal de edição/inclusão
            var editor = new ContaCorrenteEditWindow(conta);

            // Recarrega a listagem e recalcula totais assim que fechar/salvar
            editor.Closed += (s, ev) => CarregarContas();
            editor.Activate();
        }
    }
}