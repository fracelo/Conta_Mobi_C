using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using ContaMobi.Services;
using System;
using Windows.Graphics;
using Windows.System;

namespace ContaMobi
{
    public sealed partial class LoginWindow : Window
    {
        private readonly LicenseService _licenseService;
        private bool _loginSucesso = false;

        public LoginWindow()
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(420, 580));
            CentralizarJanela();
            _licenseService = new LicenseService();

            this.Closed += (s, e) =>
            {
                if (!_loginSucesso)
                {
                    Environment.Exit(0);
                }
            };
        }

        private void CentralizarJanela()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                int x = workArea.X + (workArea.Width - this.AppWindow.Size.Width) / 2;
                int y = workArea.Y + (workArea.Height - this.AppWindow.Size.Height) / 2;
                this.AppWindow.Move(new PointInt32(x, y));
            }
        }

        private async void ExecutarLogin()
        {
            TxtErro.Visibility = Visibility.Collapsed;
            string email = TxtEmail.Text.Trim();
            string senha = TxtSenha.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(senha))
            {
                MostrarErro("Preencha o usuario (e-mail) e a senha.");
                return;
            }

            try
            {
                BtnLogin.IsEnabled = false;
                BtnLogin.Content = "AGUARDE...";
                TxtEmail.IsEnabled = false;
                TxtSenha.IsEnabled = false;

                var resultado = await _licenseService.ValidarAcessoAsync(email, senha);

                if (resultado.Sucesso)
                {
                    _loginSucesso = true;
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Activate();
                    this.Close();
                }
                else
                {
                    MostrarErro(resultado.MensagemErro);
                }
            }
            catch (Exception ex)
            {
                MostrarErro($"Falha: {ex.Message}");
            }
            finally
            {
                if (!_loginSucesso)
                {
                    BtnLogin.IsEnabled = true;
                    BtnLogin.Content = "[ENTER] LOGIN";
                    TxtEmail.IsEnabled = true;
                    TxtSenha.IsEnabled = true;
                    TxtSenha.Password = string.Empty;
                    TxtSenha.Focus(FocusState.Programmatic);
                }
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            ExecutarLogin();
        }

        private async void BtnEsqueciSenha_Click(object sender, RoutedEventArgs e)
        {
            TxtErro.Visibility = Visibility.Collapsed;
            string email = TxtEmail.Text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                MostrarErro("Informe seu e-mail no campo acima antes de recuperar a senha.");
                return;
            }

            BtnEsqueciSenha.IsEnabled = false;
            MostrarSucesso("Enviando solicitacao para o e-mail...");

            try
            {
                await _licenseService.RecuperarSenhaAsync(email);

                PainelLogin.Visibility = Visibility.Collapsed;
                PainelRecuperacao.Visibility = Visibility.Visible;
                TxtSubtitulo.Text = "> REDEFINICAO DE SENHA";

                MostrarSucesso("Codigo enviado! Verifique sua caixa de entrada.");
            }
            catch (Exception ex)
            {
                MostrarErro($"Falha ao recuperar: {ex.Message}");
            }
            finally
            {
                BtnEsqueciSenha.IsEnabled = true;
            }
        }

        private async void BtnSalvarNovaSenha_Click(object sender, RoutedEventArgs e)
        {
            TxtErro.Visibility = Visibility.Collapsed;
            string email = TxtEmail.Text.Trim();
            string codigo = TxtCodigoToken.Text.Trim();
            string novaSenha = TxtNovaSenha.Password;
            string confirmar = TxtConfirmarSenha.Password;

            if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(novaSenha))
            {
                MostrarErro("Informe o codigo recebido e a nova senha.");
                return;
            }

            if (novaSenha.Length < 6)
            {
                MostrarErro("A nova senha deve ter no minimo 6 caracteres.");
                return;
            }

            if (novaSenha != confirmar)
            {
                MostrarErro("As senhas digitadas nao conferem.");
                return;
            }

            BtnSalvarNovaSenha.IsEnabled = false;
            BtnSalvarNovaSenha.Content = "GRAVANDO...";

            try
            {
                bool sucesso = await _licenseService.RedefinirSenhaComCodigoAsync(email, codigo, novaSenha);

                if (sucesso)
                {
                    PainelRecuperacao.Visibility = Visibility.Collapsed;
                    PainelLogin.Visibility = Visibility.Visible;
                    TxtSubtitulo.Text = "> AUTENTICACAO DE USUARIO";
                    TxtSenha.Password = string.Empty;

                    MostrarSucesso("Senha alterada com sucesso! Faca seu login.");
                }
                else
                {
                    MostrarErro("Codigo invalido ou expirado.");
                }
            }
            catch (Exception ex)
            {
                MostrarErro($"Falha ao gravar: {ex.Message}");
            }
            finally
            {
                BtnSalvarNovaSenha.IsEnabled = true;
                BtnSalvarNovaSenha.Content = "[GRAVAR SENHA]";
            }
        }

        private void BtnVoltarLogin_Click(object sender, RoutedEventArgs e)
        {
            TxtErro.Visibility = Visibility.Collapsed;
            PainelRecuperacao.Visibility = Visibility.Collapsed;
            PainelLogin.Visibility = Visibility.Visible;
            TxtSubtitulo.Text = "> AUTENTICACAO DE USUARIO";
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void MostrarErro(string mensagem)
        {
            TxtErro.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 85, 85));
            TxtErro.Text = $"> [ERRO]: {mensagem}";
            TxtErro.Visibility = Visibility.Visible;
        }

        private void MostrarSucesso(string mensagem)
        {
            TxtErro.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 102));
            TxtErro.Text = $"> [OK]: {mensagem}";
            TxtErro.Visibility = Visibility.Visible;
        }
    }
}