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
    public sealed partial class EntidadesPage : Page
    {
        private List<Entidade> _todasEntidades = new();

        public EntidadesPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => CarregarEntidades();
        }

        public void CarregarEntidades()
        {
            try
            {
                using var db = new AppDbContext();
                _todasEntidades = db.Entidades
                    .AsNoTracking()
                    .OrderBy(e => e.NomeRazao)
                    .ToList();

                AplicarFiltro();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO AO CARREGAR ENTIDADES]: {ex.Message}");
                if (TxtTotalRegistros != null)
                {
                    TxtTotalRegistros.Text = $"ERRO: {ex.Message}";
                    TxtTotalRegistros.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 85, 85));
                }
            }
        }

        private void AplicarFiltro()
        {
            if (ListaEntidades == null || _todasEntidades == null)
                return;

            string busca = TxtBusca?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            string tipoEntidadeFiltro = string.Empty;
            if (CboFiltroTipoEntidade?.SelectedItem is ComboBoxItem itemEntidade && itemEntidade.Tag != null)
            {
                tipoEntidadeFiltro = itemEntidade.Tag.ToString() ?? string.Empty;
            }

            string tipoPessoaFiltro = string.Empty;
            if (CboFiltroPessoa?.SelectedItem is ComboBoxItem itemPessoa && itemPessoa.Tag != null)
            {
                tipoPessoaFiltro = itemPessoa.Tag.ToString() ?? string.Empty;
            }

            var filtradas = _todasEntidades.AsEnumerable();

            if (!string.IsNullOrEmpty(tipoEntidadeFiltro))
            {
                filtradas = filtradas.Where(e => e.TipoEntidade.Equals(tipoEntidadeFiltro, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(tipoPessoaFiltro))
            {
                filtradas = filtradas.Where(e => e.TipoPessoa == tipoPessoaFiltro);
            }

            if (!string.IsNullOrEmpty(busca))
            {
                filtradas = filtradas.Where(e =>
                    (!string.IsNullOrEmpty(e.NomeRazao) && e.NomeRazao.ToLowerInvariant().Contains(busca)) ||
                    (!string.IsNullOrEmpty(e.CpfCnpj) && e.CpfCnpj.Contains(busca)) ||
                    (!string.IsNullOrEmpty(e.Cidade) && e.Cidade.ToLowerInvariant().Contains(busca))
                );
            }

            var lista = filtradas.ToList();
            ListaEntidades.ItemsSource = lista;

            if (TxtTotalRegistros != null)
            {
                TxtTotalRegistros.Text = $"REGISTROS: {lista.Count}";
                TxtTotalRegistros.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 102));
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private void CboFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                AplicarFiltro();
            }
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e) => AplicarFiltro();

        private void BtnInserir_Click(object sender, RoutedEventArgs e)
        {
            AbrirEditor(new Entidade
            {
                EntidadeUuid = Guid.NewGuid().ToString(),
                TipoEntidade = "Cliente",
                TipoPessoa = "F"
            });
        }

        private void BtnEditarLinha_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Entidade selecionada)
            {
                ListaEntidades.SelectedItem = selecionada;
                AbrirEditor(selecionada);
            }
        }

        private void ListaEntidades_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaEntidades.SelectedItem is Entidade selecionada)
            {
                AbrirEditor(selecionada);
            }
        }

        private void ListaEntidades_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ListaEntidades.SelectedItem is Entidade selecionada)
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

        private void AbrirEditor(Entidade entidade)
        {
            var editor = new EntidadeEditWindow(entidade);
            editor.Closed += (s, ev) => CarregarEntidades();
            editor.Activate();
        }
    }
}