using ContaMobi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;

namespace ContaMobi
{
    public partial class App : Application
    {
        public static Supabase.Client SupabaseClient { get; set; } = ContaMobi.Services.LicenseService.Client;

        private Window? _window;

        public App()
        {
            this.InitializeComponent();

            // Inicializa o banco de dados SQLite local na primeira execução
            InicializarBancoLocal();

            // Execute APENAS 1 VEZ para criar as novas tabelas de parcelas, depois comente novamente
            // ContaMobi.Data.ContasPRService.RecriarTabelaContasPR();
        }

        private void InicializarBancoLocal()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao inicializar o banco SQLite local: {ex.Message}");
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new LoginWindow();
            _window.Activate();
        }
    }
}