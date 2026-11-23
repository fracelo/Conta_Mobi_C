using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ContaMobi.Data;
using ContaMobi.Models;

namespace ContaMobi.Services
{
    public class BancoSyncService
    {
        public static async Task SincronizarBancosSeVazioAsync()
        {
            try
            {
                using var db = new AppDbContext();

                // Evita chamadas desnecessárias se os bancos já existirem localmente
                if (await db.Bancos.AnyAsync())
                    return;

                if (App.SupabaseClient == null)
                    return;

                var resposta = await App.SupabaseClient
                    .From<SupabaseBanco>()
                    .Get();

                if (resposta.Models == null || !resposta.Models.Any())
                    return;

                foreach (var item in resposta.Models)
                {
                    db.Bancos.Add(new Banco
                    {
                        CodigoBanco = item.CodigoBanco,
                        DescricaoBanco = item.DescricaoBanco,
                        TelefoneSac = item.TelefoneSac,
                        CodigoIspb = item.CodigoIspb,
                        IconeUrl = item.IconeUrl,
                        UpdatedAt = item.UpdatedAt,
                        UsuarioUuid = item.UsuarioUuid,
                        Ativo = true
                    });
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC BANCOS]: {ex.Message}");
            }
        }
    }
}