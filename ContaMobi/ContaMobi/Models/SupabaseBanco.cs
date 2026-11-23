using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace ContaMobi.Models
{
    [Table("bancos")]
    public class SupabaseBanco : BaseModel
    {
        [PrimaryKey("codigo_banco", false)]
        public int CodigoBanco { get; set; }

        [Column("descricao_banco")]
        public string DescricaoBanco { get; set; } = string.Empty;

        [Column("telefone_sac")]
        public string? TelefoneSac { get; set; }

        [Column("codigo_ispb")]
        public string? CodigoIspb { get; set; }

        [Column("icone_url")]
        public string? IconeUrl { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("usuariouuid")]
        public string? UsuarioUuid { get; set; }
    }
}