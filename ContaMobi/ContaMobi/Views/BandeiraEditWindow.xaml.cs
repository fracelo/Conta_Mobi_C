using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using ContaMobi.Data;
using ContaMobi.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace ContaMobi.Views
{
    public sealed partial class BandeiraEditWindow : Window
    {
        private readonly Bandeira _bandeira;
        private readonly bool _ehNovo;
        private string? _caminhoArquivoOrigemSelecionado;

        public BandeiraEditWindow(Bandeira bandeira, bool ehNovo)
        {
            this.InitializeComponent();
            ContaMobi.Helpers.UIThemeHelper.AplicarBarraTituloRetro(this);
            this.AppWindow.Resize(new SizeInt32(540, 490));

            _bandeira = bandeira;
            _ehNovo = ehNovo;

            TxtTitulo.Text = _ehNovo ? "[ NOVA BANDEIRA DE CARTAO ]" : "[ EDITANDO BANDEIRA ]";
            BtnExcluir.IsEnabled = !_ehNovo;

            PreencherCampos();
        }

        private void PreencherCampos()
        {
            _caminhoArquivoOrigemSelecionado = null;
            TxtDescricao.Text = _bandeira.DescricaoBandeira ?? string.Empty;
            ChkAtivo.IsChecked = _bandeira.Ativo;
            TxtMensagem.Visibility = Visibility.Collapsed;

            AtualizarPreviewImagem(_bandeira.IconeCaminhoResolvido);
        }

        private void AtualizarPreviewImagem(string caminhoOuUri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(caminhoOuUri))
                {
                    ImgPreview.Source = null;
                    return;
                }

                if (caminhoOuUri.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                    caminhoOuUri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var bmp = new BitmapImage();
                    bmp.UriSource = new Uri(caminhoOuUri);
                    ImgPreview.Source = bmp;
                    return;
                }

                if (File.Exists(caminhoOuUri))
                {
                    var bmp = new BitmapImage();
                    bmp.UriSource = new Uri(caminhoOuUri, UriKind.Absolute);
                    ImgPreview.Source = bmp;
                    return;
                }

                ImgPreview.Source = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO PREVIEW BANDEIRA]: {ex.Message}");
                ImgPreview.Source = null;
            }
        }

        private void BtnSelecionarImagem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                string? caminho = SelecionarArquivoImagemWin32(hwnd);

                if (!string.IsNullOrWhiteSpace(caminho) && File.Exists(caminho))
                {
                    _caminhoArquivoOrigemSelecionado = caminho;
                    AtualizarPreviewImagem(caminho);
                }
            }
            catch (Exception ex)
            {
                TxtMensagem.Text = $"> [ERRO AO SELECIONAR IMAGEM]: {ex.Message}";
                TxtMensagem.Visibility = Visibility.Visible;
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            string descricao = TxtDescricao.Text.Trim();
            if (string.IsNullOrEmpty(descricao))
            {
                TxtMensagem.Text = "> [ERRO] A descricao da bandeira e obrigatoria.";
                TxtMensagem.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                _bandeira.DescricaoBandeira = descricao;
                _bandeira.Ativo = ChkAtivo.IsChecked ?? true;

                CartaoCreditoService.SalvarBandeira(_bandeira, _caminhoArquivoOrigemSelecionado);
                this.Close();
            }
            catch (Exception ex)
            {
                TxtMensagem.Text = $"> [ERRO AO SALVAR]: {ex.Message}";
                TxtMensagem.Visibility = Visibility.Visible;
            }
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            TxtMensagem.Visibility = Visibility.Collapsed;

            try
            {
                CartaoCreditoService.ExcluirBandeira(_bandeira.BandeiraUuid);
                this.Close();
            }
            catch (Exception ex)
            {
                TxtMensagem.Text = $"> [ERRO AO EXCLUIR]: {ex.Message}";
                TxtMensagem.Visibility = Visibility.Visible;
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            PreencherCampos();
        }

        private void BtnSair_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // =====================================================================
        // SELETOR DE ARQUIVO NATIVO WIN32
        // =====================================================================

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName([In, Out] OPENFILENAME ofn);

        private static string? SelecionarArquivoImagemWin32(IntPtr hwndOwner)
        {
            var ofn = new OPENFILENAME();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.hwndOwner = hwndOwner;
            ofn.lpstrFilter = "Imagens (*.png;*.jpg;*.jpeg;*.webp)\0*.png;*.jpg;*.jpeg;*.webp\0Todos os arquivos (*.*)\0*.*\0\0";
            ofn.lpstrFile = new string('\0', 1024);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrTitle = "Selecione o icone da bandeira";
            ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800;

            if (GetOpenFileName(ofn))
            {
                return ofn.lpstrFile.TrimEnd('\0');
            }

            return null;
        }
    }
}