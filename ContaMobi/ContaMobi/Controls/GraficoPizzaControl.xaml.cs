using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace ContaMobi.Controls
{
    public class FatiaPizzaItem
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public double Percentual { get; set; }
        public Color Cor { get; set; }
    }

    public sealed partial class GraficoPizzaControl : UserControl
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private decimal _totalAtual = 0m;

        private static readonly Color[] PaletaCores =
        {
            Color.FromArgb(255, 0, 230, 118),   // Verde neon
            Color.FromArgb(255, 41, 182, 246),  // Azul claro
            Color.FromArgb(255, 255, 202, 40),  // Amarelo
            Color.FromArgb(255, 255, 112, 67),  // Laranja
            Color.FromArgb(255, 171, 71, 188),  // Roxo
            Color.FromArgb(255, 236, 64, 122),  // Rosa
            Color.FromArgb(255, 38, 198, 218),  // Ciano
            Color.FromArgb(255, 156, 204, 101), // Verde lima
            Color.FromArgb(255, 92, 107, 192),  // Índigo
            Color.FromArgb(255, 239, 83, 80),   // Vermelho
            Color.FromArgb(255, 255, 171, 64),  // Âmbar
            Color.FromArgb(255, 38, 166, 154)   // Turquesa
        };

        public GraficoPizzaControl()
        {
            this.InitializeComponent();
        }

        public void Configurar(string titulo, Color corTitulo, List<(string Categoria, decimal Valor)> dados)
        {
            TxtTitulo.Text = titulo;
            TxtTitulo.Foreground = new SolidColorBrush(corTitulo);

            CanvasPizza.Children.Clear();
            PainelLegenda.Children.Clear();

            var validos = dados
                .Where(d => d.Valor > 0m)
                .OrderByDescending(d => d.Valor)
                .ToList();

            _totalAtual = validos.Sum(d => d.Valor);
            TxtTotal.Text = $"Total: {_totalAtual.ToString("C2", _cultura)}";

            if (validos.Count == 0 || _totalAtual <= 0m)
            {
                TxtSemDados.Visibility = Visibility.Visible;
                DesenharCirculoVazio();
                return;
            }

            TxtSemDados.Visibility = Visibility.Collapsed;

            var fatias = new List<FatiaPizzaItem>();
            for (int i = 0; i < validos.Count; i++)
            {
                double perc = (double)(validos[i].Valor / _totalAtual) * 100.0;
                fatias.Add(new FatiaPizzaItem
                {
                    Categoria = validos[i].Categoria,
                    Valor = validos[i].Valor,
                    Percentual = perc,
                    Cor = PaletaCores[i % PaletaCores.Length]
                });
            }

            DesenharPizza(fatias);
            MontarLegendaCompacta(fatias);
        }

        private void DesenharCirculoVazio()
        {
            var circulo = new Ellipse
            {
                Width = 320,
                Height = 320,
                Fill = new SolidColorBrush(Color.FromArgb(255, 15, 23, 32)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(circulo, 20);
            Canvas.SetTop(circulo, 20);
            CanvasPizza.Children.Add(circulo);
        }

        private void DesenharPizza(List<FatiaPizzaItem> fatias)
        {
            const double cx = 180.0;
            const double cy = 180.0;
            const double raio = 165.0;

            if (fatias.Count == 1)
            {
                var unica = fatias[0];
                var circulo = new Ellipse
                {
                    Width = raio * 2,
                    Height = raio * 2,
                    Fill = new SolidColorBrush(unica.Cor),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 10, 14, 20)),
                    StrokeThickness = 1.5,
                    Tag = unica
                };
                circulo.Tapped += Fatia_Tapped;
                Canvas.SetLeft(circulo, cx - raio);
                Canvas.SetTop(circulo, cy - raio);
                CanvasPizza.Children.Add(circulo);

                AdicionarEtiquetaPercentual(unica, cx, cy);
                return;
            }

            double anguloInicial = -90.0;
            var etiquetas = new List<(FatiaPizzaItem Item, double X, double Y)>();

            foreach (var fatia in fatias)
            {
                double sweep = (fatia.Percentual / 100.0) * 360.0;
                if (sweep >= 359.99) sweep = 359.99;

                double radIni = anguloInicial * Math.PI / 180.0;
                double radFim = (anguloInicial + sweep) * Math.PI / 180.0;

                double x1 = cx + raio * Math.Cos(radIni);
                double y1 = cy + raio * Math.Sin(radIni);
                double x2 = cx + raio * Math.Cos(radFim);
                double y2 = cy + raio * Math.Sin(radFim);

                var figure = new PathFigure
                {
                    StartPoint = new Point(cx, cy),
                    IsClosed = true,
                    IsFilled = true
                };

                figure.Segments.Add(new LineSegment { Point = new Point(x1, y1) });
                figure.Segments.Add(new ArcSegment
                {
                    Point = new Point(x2, y2),
                    Size = new Size(raio, raio),
                    IsLargeArc = sweep > 180.0,
                    SweepDirection = SweepDirection.Clockwise
                });

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                var path = new XamlPath
                {
                    Data = geometry,
                    Fill = new SolidColorBrush(fatia.Cor),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 10, 14, 20)),
                    StrokeThickness = 1.5,
                    Tag = fatia
                };
                path.Tapped += Fatia_Tapped;

                CanvasPizza.Children.Add(path);

                double radMeio = (anguloInicial + sweep / 2.0) * Math.PI / 180.0;
                double fatorRaio = sweep < 16.0 ? 0.80 : 0.64;
                double lx = cx + (raio * fatorRaio) * Math.Cos(radMeio);
                double ly = cy + (raio * fatorRaio) * Math.Sin(radMeio);

                etiquetas.Add((fatia, lx, ly));
                anguloInicial += sweep;
            }

            foreach (var et in etiquetas)
            {
                AdicionarEtiquetaPercentual(et.Item, et.X, et.Y);
            }
        }

        private void AdicionarEtiquetaPercentual(FatiaPizzaItem fatia, double x, double y)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(205, 5, 10, 15)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Tag = fatia,
                Child = new TextBlock
                {
                    Text = $"{fatia.Percentual:N1}%",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };
            badge.Tapped += Fatia_Tapped;

            badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(badge, x - (badge.DesiredSize.Width / 2.0));
            Canvas.SetTop(badge, y - (badge.DesiredSize.Height / 2.0));

            CanvasPizza.Children.Add(badge);
        }

        private void MontarLegendaCompacta(List<FatiaPizzaItem> fatias)
        {
            const int itensPorLinha = 3;
            var exibidos = fatias.Take(9).ToList();

            for (int i = 0; i < exibidos.Count; i += itensPorLinha)
            {
                var linha = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                for (int j = i; j < Math.Min(i + itensPorLinha, exibidos.Count); j++)
                {
                    var f = exibidos[j];

                    var conteudoChip = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    conteudoChip.Children.Add(new Border
                    {
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(f.Cor),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    conteudoChip.Children.Add(new TextBlock
                    {
                        Text = f.Categoria,
                        FontSize = 10,
                        MaxWidth = 92,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    conteudoChip.Children.Add(new TextBlock
                    {
                        Text = $"{f.Percentual:N1}%",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(f.Cor),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var btnChip = new Button
                    {
                        Content = conteudoChip,
                        Tag = f,
                        Background = new SolidColorBrush(Color.FromArgb(255, 10, 20, 14)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 30, 55, 40)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2, 6, 2),
                        MinHeight = 0,
                        MinWidth = 0
                    };
                    ToolTipService.SetToolTip(btnChip, $"{f.Categoria} — {f.Valor.ToString("C2", _cultura)} ({f.Percentual:N1}%)");
                    btnChip.Tapped += Fatia_Tapped;

                    linha.Children.Add(btnChip);
                }

                PainelLegenda.Children.Add(linha);
            }
        }

        private void Fatia_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement elemento || elemento.Tag is not FatiaPizzaItem fatia)
                return;

            e.Handled = true;

            // Pega a posição exata do clique relativa ao UserControl (fora da escala do Viewbox)
            Point pontoClique = e.GetPosition(this);
            AbrirBoxDetalhes(pontoClique, fatia);
        }

        private void AbrirBoxDetalhes(Point pontoClique, FatiaPizzaItem fatia)
        {
            var painel = new StackPanel
            {
                Spacing = 6,
                MinWidth = 220,
                Padding = new Thickness(4)
            };

            var cabecalho = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            cabecalho.Children.Add(new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(fatia.Cor),
                VerticalAlignment = VerticalAlignment.Center
            });
            cabecalho.Children.Add(new TextBlock
            {
                Text = fatia.Categoria,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102)),
                TextWrapping = TextWrapping.Wrap
            });

            painel.Children.Add(cabecalho);
            painel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) });

            painel.Children.Add(new TextBlock
            {
                Text = $"Valor: {fatia.Valor.ToString("C2", _cultura)}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White)
            });

            painel.Children.Add(new TextBlock
            {
                Text = $"Participação: {fatia.Percentual:N2}% do total",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fatia.Cor)
            });

            painel.Children.Add(new TextBlock
            {
                Text = $"Total do gráfico: {_totalAtual.ToString("C2", _cultura)}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184))
            });

            var flyout = new Flyout
            {
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 8, 15, 22)),
                    BorderBrush = new SolidColorBrush(fatia.Cor),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10, 12, 10),
                    Child = painel
                }
            };

            var opcoes = new FlyoutShowOptions
            {
                Position = new Point(pontoClique.X, pontoClique.Y - 6),
                Placement = FlyoutPlacementMode.Top
            };

            flyout.ShowAt(this, opcoes);
        }
    }
}