using ContaMobi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace ContaMobi
{
    public partial class App : Application
    {
        private static Supabase.Client? _supabaseClient;
        public static Supabase.Client SupabaseClient
        {
            get => _supabaseClient ??= ContaMobi.Services.LicenseService.Client;
            set => _supabaseClient = value;
        }

        private Window? _window;

        public App()
        {
            this.InitializeComponent();

            this.UnhandledException += (sender, e) =>
            {
                string detalhes = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {e.Message}\n{e.Exception}";
                System.Diagnostics.Debug.WriteLine($"[ERRO FATAL WINUI]: {detalhes}");

                try
                {
                    string pasta = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ContaMobi");
                    Directory.CreateDirectory(pasta);
                    File.WriteAllText(Path.Combine(pasta, "erro_startup.txt"), detalhes);
                }
                catch { }
            };

            // Inicializa o banco de dados SQLite local na primeira execução
            InicializarBancoLocal();
        }

        private void InicializarBancoLocal()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();
                }

                // Cria as tabelas do módulo de Cartão de Crédito e cadastra as bandeiras padrão se ainda não existirem
                CartaoCreditoService.GarantirTabelasCartao();
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