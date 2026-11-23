using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ContaMobi.Data;
using ContaMobi.Models;
using ContaMobi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContaMobi.Views
{
    public sealed partial class BancosPage : Page
    {
        private List<Banco> _todosBancos = new List<Banco>();

        public BancosPage()
        {
            this.InitializeComponent();
            this.Loaded += async (s, e) =>
            {
                // 1. Sincroniza da nuvem (Supabase) caso a base SQLite local esteja vazia
                await BancoSyncService.SincronizarBancosSeVazioAsync();

                // 2. Carrega os registros na tela
                CarregarBancos();
            };
        }

        private void CarregarBancos()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Ordena pela coluna mapeada no banco (DescricaoBanco) para evitar falha no LINQ do EF Core
                    _todosBancos = db.Bancos
                        .OrderBy(b => b.DescricaoBanco)
                        .ToList();

                    AplicarFiltro();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar bancos: {ex.Message}");
            }
        }

        private void AplicarFiltro()
        {
            string busca = TxtBusca.Text?.Trim().ToLower() ?? string.Empty;

            if (string.IsNullOrEmpty(busca))
            {
                ListaBancos.ItemsSource = _todosBancos;
            }
            else
            {
                ListaBancos.ItemsSource = _todosBancos.Where(b =>
                    (!string.IsNullOrEmpty(b.DescricaoBanco) && b.DescricaoBanco.ToLower().Contains(busca)) ||
                    b.CodigoBanco.ToString().Contains(busca) ||
                    (!string.IsNullOrEmpty(b.CodigoIspb) && b.CodigoIspb.Contains(busca))
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
            // Abre janela de edição com novo registro (CodigoBanco = 0)
            AbrirEditor(new Banco { Ativo = true });
        }

        private void ListaBancos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListaBancos.SelectedItem is Banco bancoSelecionado)
            {
                AbrirEditor(bancoSelecionado);
            }
        }

        private void AbrirEditor(Banco banco)
        {
            var editWin = new BancoEditWindow(banco);
            editWin.Closed += (s, ev) =>
            {
                // Recarrega os registros assim que o usuário salva ou fecha a janela de edição
                CarregarBancos();
            };
            editWin.Activate();
        }
    }
}