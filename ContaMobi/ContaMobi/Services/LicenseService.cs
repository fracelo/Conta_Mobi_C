using Supabase;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ContaMobi.Services
{
    public class ResultadoLogin
    {
        public bool Sucesso { get; set; }
        public string MensagemErro { get; set; } = string.Empty;
    }

    public class LicenseService
    {
        private static Client? _instanciaClient;

        // Propriedade pública estática para acesso global em serviços como BancoSyncService
        public static Client Client
        {
            get
            {
                if (_instanciaClient == null)
                {
                    _instanciaClient = CriarClient();
                    App.SupabaseClient = _instanciaClient;
                }
                return _instanciaClient;
            }
        }

        private readonly Client _supabaseClient;

        public LicenseService()
        {
            _supabaseClient = Client;
            // Garante que App.SupabaseClient esteja sempre sincronizado
            App.SupabaseClient = _supabaseClient;
        }

        private static Client CriarClient()
        {
            string url = "https://btdzfgmejalaumsuzwpl.supabase.co";
            string key = "sb_publishable_tq4Wly3RyZM8jfGKxoszNQ_x0c-pjmw";

            return new Client(url, key, new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            });
        }

        public async Task<ResultadoLogin> ValidarAcessoAsync(string email, string senha)
        {
            await _supabaseClient.InitializeAsync();

            // 1. Autentica o usuário no Supabase Auth (Testa E-mail e Senha)
            Supabase.Gotrue.Session? session;
            try
            {
                session = await _supabaseClient.Auth.SignIn(email, senha);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Invalid login credentials") || ex.Message.Contains("400"))
                {
                    return new ResultadoLogin { Sucesso = false, MensagemErro = "E-mail ou senha incorretos no Supabase Auth." };
                }
                return new ResultadoLogin { Sucesso = false, MensagemErro = $"Erro no servidor Auth: {ex.Message}" };
            }

            if (session?.User == null || string.IsNullOrEmpty(session.User.Id))
            {
                return new ResultadoLogin { Sucesso = false, MensagemErro = "Sessao nao iniciada. Verifique suas credenciais." };
            }

            string userId = session.User.Id;

            // 2. Consulta a tabela 'usuarios' na nuvem para verificar o vencimento
            try
            {
                var response = await _supabaseClient.From<UsuarioNuvem>()
                    .Where(x => x.UsuarioUuid == userId)
                    .Get();

                var usuario = response.Models.FirstOrDefault();
                if (usuario == null)
                {
                    return new ResultadoLogin
                    {
                        Sucesso = false,
                        MensagemErro = $"Senha correta! Porem seu UUID ({userId.Substring(0, Math.Min(8, userId.Length))}...) nao esta na tabela 'usuarios'."
                    };
                }

                if (!usuario.DataVencimento.HasValue || usuario.DataVencimento.Value.Date < DateTime.Today)
                {
                    return new ResultadoLogin
                    {
                        Sucesso = false,
                        MensagemErro = $"Senha correta! Porem sua assinatura venceu em {usuario.DataVencimento:dd/MM/yyyy}."
                    };
                }

                return new ResultadoLogin { Sucesso = true };
            }
            catch (Exception ex)
            {
                return new ResultadoLogin { Sucesso = false, MensagemErro = $"Senha OK, mas erro ao ler tabela 'usuarios': {ex.Message}" };
            }
        }

        public async Task RecuperarSenhaAsync(string email)
        {
            await _supabaseClient.InitializeAsync();
            await _supabaseClient.Auth.ResetPasswordForEmail(email);
        }

        public async Task<bool> RedefinirSenhaComCodigoAsync(string email, string codigoOuLink, string novaSenha)
        {
            await _supabaseClient.InitializeAsync();

            string token = codigoOuLink.Trim();

            // 1. Valida o código de 6 dígitos enviado por e-mail
            var session = await _supabaseClient.Auth.VerifyOTP(
                email,
                token,
                Supabase.Gotrue.Constants.EmailOtpType.Recovery
            );

            if (session == null || string.IsNullOrEmpty(session.AccessToken))
                throw new Exception("Codigo invalido ou expirado.");

            // 2. Garante que a sessão autenticada pelo OTP está ativa no cliente antes do Update
            await _supabaseClient.Auth.SetSession(session.AccessToken, session.RefreshToken ?? string.Empty);

            // 3. Atualiza a senha no Supabase Auth
            var attrs = new Supabase.Gotrue.UserAttributes { Password = novaSenha };
            var userAtualizado = await _supabaseClient.Auth.Update(attrs);

            if (userAtualizado == null)
                throw new Exception("O Supabase nao confirmou a gravacao da nova senha.");

            // 4. Faz SignOut da sessão de recuperação para o usuário testar o login limpo
            await _supabaseClient.Auth.SignOut();

            return true;
        }
    }

    [Supabase.Postgrest.Attributes.Table("usuarios")]
    public class UsuarioNuvem : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("usuariouuid")]
        public string UsuarioUuid { get; set; } = string.Empty;

        [Supabase.Postgrest.Attributes.Column("datavencimento")]
        public DateTime? DataVencimento { get; set; }
    }
}