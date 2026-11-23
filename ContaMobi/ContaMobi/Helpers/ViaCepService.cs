using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ContaMobi.Helpers
{
    public class ViaCepEndereco
    {
        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Cidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("ibge")]
        public string? Ibge { get; set; }

        [JsonPropertyName("ddd")]
        public string? Ddd { get; set; }

        // O ViaCEP pode retornar "erro": true (bool) ou "erro": "true" (string)
        [JsonPropertyName("erro")]
        public JsonElement? Erro { get; set; }

        [JsonIgnore]
        public bool CepInvalido => Erro.HasValue &&
            (Erro.Value.ValueKind == JsonValueKind.True ||
            (Erro.Value.ValueKind == JsonValueKind.String && Erro.Value.GetString() == "true"));
    }

    public static class ViaCepService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        public static async Task<ViaCepEndereco?> BuscarEnderecoPorCepAsync(string cep)
        {
            // Extrai apenas os dígitos numéricos
            string cepLimpo = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());

            if (cepLimpo.Length != 8)
                throw new ArgumentException("O CEP deve conter exatamente 8 dígitos numéricos.");

            string url = $"https://viacep.com.br/ws/{cepLimpo}/json/";

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var dados = JsonSerializer.Deserialize<ViaCepEndereco>(json);

            if (dados == null || dados.CepInvalido)
                return null;

            return dados;
        }
    }
}