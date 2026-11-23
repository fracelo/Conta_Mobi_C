using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace ContaMobi.Helpers
{
    public static class EnterNavegacaoHelper
    {
        /// <summary>
        /// Ativa a navegação com a tecla ENTER (como TAB) em todos os campos de entrada da janela.
        /// </summary>
        public static void HabilitarEnterComoTab(Window janela)
        {
            if (janela.Content is not UIElement raiz)
                return;

            raiz.PreviewKeyDown -= Raiz_PreviewKeyDown;
            raiz.PreviewKeyDown += Raiz_PreviewKeyDown;
        }

        private static void Raiz_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
                return;

            if (sender is not UIElement raiz || raiz.XamlRoot == null)
                return;

            var focado = FocusManager.GetFocusedElement(raiz.XamlRoot) as DependencyObject;
            if (focado == null)
                return;

            // Se o foco está em um botão, deixa o Enter acionar o clique normalmente
            if (focado is Button || EncontrarAncestral<Button>(focado) != null)
                return;

            // Se está dentro de um AutoSuggestBox com a lista aberta, deixa o Enter escolher a sugestão
            var asb = EncontrarAncestral<AutoSuggestBox>(focado);
            if (asb != null && asb.IsSuggestionListOpen)
                return;

            // Se está em um ComboBox com o dropdown aberto, deixa o Enter confirmar o item
            var combo = focado as ComboBox ?? EncontrarAncestral<ComboBox>(focado);
            if (combo != null && combo.IsDropDownOpen)
                return;

            // Se for TextBox multilinha (ex.: Observações), mantém a quebra de linha
            if (focado is TextBox tb && tb.AcceptsReturn)
                return;

            // Só intercepta se for campo de entrada/seleção
            bool ehCampoEntrada =
                focado is TextBox ||
                focado is PasswordBox ||
                focado is ComboBox ||
                focado is CheckBox ||
                asb != null;

            if (!ehCampoEntrada)
                return;

            e.Handled = true;

            var opcoes = new FindNextElementOptions
            {
                SearchRoot = raiz.XamlRoot.Content
            };

            FocusManager.TryMoveFocus(FocusNavigationDirection.Next, opcoes);
        }

        private static T? EncontrarAncestral<T>(DependencyObject? elemento) where T : DependencyObject
        {
            var atual = elemento;
            while (atual != null)
            {
                if (atual is T encontrado)
                    return encontrado;

                atual = VisualTreeHelper.GetParent(atual);
            }
            return null;
        }
    }
}