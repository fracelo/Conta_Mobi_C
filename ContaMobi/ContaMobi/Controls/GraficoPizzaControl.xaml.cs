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
    public class SubcategoriaNivel2Grafico
    {
        public string CategoriaUuid { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    public class CategoriaNivel1Grafico
    {
        public string CategoriaPaiUuid { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public List<SubcategoriaNivel2Grafico> Subcategorias { get; set; } = new();
    }

    public class SubcategoriaFatiaItem
    {
        public string Nome { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public double Percentual { get; set; }
        public Color Cor { get; set; }
    }

    public class FatiaPizzaItem
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public double Percentual { get; set; }
        public Color Cor { get; set; }
        public List<SubcategoriaFatiaItem> Subcategorias { get; set; } = new();
    }

    public sealed partial class GraficoPizzaControl : UserControl
    {
        private readonly CultureInfo _cultura = new("pt-BR");
        private decimal _totalAtual = 0m;
        private readonly Dictionary<FatiaPizzaItem, List<Shape>> _formasPorFatia = new();

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

        /// <summary>
        /// Sobrecarga simples (mantida por compatibilidade).
        /// </summary>
        public void Configurar(string titulo, Color corTitulo, List<(string Categoria, decimal Valor)> dados)
        {
            var hierarquico = dados.Select(d => new CategoriaNivel1Grafico
            {
                Nome = d.Categoria,
                ValorTotal = d.Valor,
                Subcategorias = new List<SubcategoriaNivel2Grafico>()
            }).ToList();

            ConfigurarHierarquico(titulo, corTitulo, hierarquico);
        }

        /// <summary>
        /// Configura o gráfico principal 3D com totais de Nível 1 e carrega os itens de Nível 2 para o drill-down ao clicar.
        /// </summary>
        public void ConfigurarHierarquico(string titulo, Color corTitulo, List<CategoriaNivel1Grafico> dadosNivel1)
        {
            TxtTitulo.Text = titulo;
            TxtTitulo.Foreground = new SolidColorBrush(corTitulo);

            CanvasPizza.Children.Clear();
            PainelLegenda.Children.Clear();
            _formasPorFatia.Clear();

            var validos = dadosNivel1
                .Where(d => d.ValorTotal > 0m)
                .OrderByDescending(d => d.ValorTotal)
                .ToList();

            _totalAtual = validos.Sum(d => d.ValorTotal);
            TxtTotal.Text = $"Total: {_totalAtual.ToString("C2", _cultura)}";

            if (validos.Count == 0 || _totalAtual <= 0m)
            {
                TxtSemDados.Visibility = Visibility.Visible;
                DesenharCirculoVazio3D();
                return;
            }

            TxtSemDados.Visibility = Visibility.Collapsed;

            var fatias = new List<FatiaPizzaItem>();
            for (int i = 0; i < validos.Count; i++)
            {
                var catN1 = validos[i];
                double percN1 = (double)(catN1.ValorTotal / _totalAtual) * 100.0;

                var subsValidas = catN1.Subcategorias
                    .Where(s => s.Valor > 0m)
                    .OrderByDescending(s => s.Valor)
                    .ToList();

                decimal totalSub = subsValidas.Sum(s => s.Valor);
                if (totalSub <= 0m) totalSub = catN1.ValorTotal;

                var listaSubsFatia = new List<SubcategoriaFatiaItem>();
                for (int j = 0; j < subsValidas.Count; j++)
                {
                    var sub = subsValidas[j];
                    double percSub = totalSub > 0m ? (double)(sub.Valor / totalSub) * 100.0 : 0.0;

                    listaSubsFatia.Add(new SubcategoriaFatiaItem
                    {
                        Nome = sub.Nome,
                        Valor = sub.Valor,
                        Percentual = percSub,
                        Cor = PaletaCores[(j + 2) % PaletaCores.Length]
                    });
                }

                fatias.Add(new FatiaPizzaItem
                {
                    Categoria = catN1.Nome,
                    Valor = catN1.ValorTotal,
                    Percentual = percN1,
                    Cor = PaletaCores[i % PaletaCores.Length],
                    Subcategorias = listaSubsFatia
                });
            }

            DesenharPizza3D(fatias);
            MontarLegendaCompacta(fatias);
        }

        private void DesenharCirculoVazio3D()
        {
            const double cx = 180.0;
            const double cy = 158.0;
            const double rx = 164.0;
            const double ry = 104.0;
            const double altura = 26.0;

            DesenharSombraProjetada(CanvasPizza, cx, cy, rx, ry, altura);

            Color corBase = Color.FromArgb(255, 22, 34, 46);
            var parede = CriarParedeCurva3D(cx, cy, rx, ry, altura, 0.0, 180.0, corBase);
            CanvasPizza.Children.Add(parede);

            var topo = new Ellipse
            {
                Width = rx * 2,
                Height = ry * 2,
                Fill = CriarBrushTopo3D(corBase),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 38, 54, 70)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(topo, cx - rx);
            Canvas.SetTop(topo, cy - ry);
            CanvasPizza.Children.Add(topo);
        }

        private void DesenharPizza3D(List<FatiaPizzaItem> fatias)
        {
            const double cx = 180.0;
            const double cy = 158.0;
            const double rx = 166.0;
            const double ry = 106.0;
            const double altura = 28.0;

            DesenharSombraProjetada(CanvasPizza, cx, cy, rx, ry, altura);

            if (fatias.Count == 1)
            {
                var unica = fatias[0];
                string tooltip = $"{unica.Categoria} — {unica.Valor.ToString("C2", _cultura)} ({unica.Percentual:N1}%) | Clique para detalhar Nível 2";

                var parede = CriarParedeCurva3D(cx, cy, rx, ry, altura, 0.0, 180.0, unica.Cor);
                RegistrarInteracaoFatia(parede, unica, tooltip);
                CanvasPizza.Children.Add(parede);

                var topo = new Ellipse
                {
                    Width = rx * 2,
                    Height = ry * 2,
                    Fill = CriarBrushTopo3D(unica.Cor),
                    Stroke = new SolidColorBrush(ClarearCor(unica.Cor, 0.35)),
                    StrokeThickness = 1.2
                };
                Canvas.SetLeft(topo, cx - rx);
                Canvas.SetTop(topo, cy - ry);
                RegistrarInteracaoFatia(topo, unica, tooltip);
                CanvasPizza.Children.Add(topo);

                AdicionarEtiquetaPercentual(CanvasPizza, unica.Percentual, cx, cy, unica);
                return;
            }

            // Calcula os intervalos angulares em [-90°, 270°]
            var intervalos = new List<(FatiaPizzaItem Fatia, double Ini, double Fim)>();
            double anguloAtual = -90.0;

            foreach (var fatia in fatias)
            {
                double sweep = (fatia.Percentual / 100.0) * 360.0;
                if (sweep >= 359.99) sweep = 359.99;

                intervalos.Add((fatia, anguloAtual, anguloAtual + sweep));
                anguloAtual += sweep;
            }

            // 1. Paredes internas radiais (cortes) visíveis, ordenadas de trás para frente (Painter's Algorithm)
            var cortesVisiveis = new List<(double ProfundidadeY, XamlPath Forma, FatiaPizzaItem Fatia)>();

            foreach (var item in intervalos)
            {
                // Parede radial inicial: visível quando cos(Ini) < 0 -> Ini entre 90° e 270°
                if (item.Ini > 90.0 && item.Ini < 270.0)
                {
                    double rad = item.Ini * Math.PI / 180.0;
                    double xEdge = cx + rx * Math.Cos(rad);
                    double yEdge = cy + ry * Math.Sin(rad);
                    var paredeCorte = CriarParedeRadial3D(cx, cy, xEdge, yEdge, altura, item.Fatia.Cor);
                    cortesVisiveis.Add(((cy + yEdge) * 0.5, paredeCorte, item.Fatia));
                }

                // Parede radial final: visível quando cos(Fim) > 0 -> Fim entre -90° e 90°
                if (item.Fim > -90.0 && item.Fim < 90.0)
                {
                    double rad = item.Fim * Math.PI / 180.0;
                    double xEdge = cx + rx * Math.Cos(rad);
                    double yEdge = cy + ry * Math.Sin(rad);
                    var paredeCorte = CriarParedeRadial3D(cx, cy, xEdge, yEdge, altura, item.Fatia.Cor);
                    cortesVisiveis.Add(((cy + yEdge) * 0.5, paredeCorte, item.Fatia));
                }
            }

            foreach (var corte in cortesVisiveis.OrderBy(c => c.ProfundidadeY))
            {
                string tooltip = $"{corte.Fatia.Categoria} — {corte.Fatia.Valor.ToString("C2", _cultura)} ({corte.Fatia.Percentual:N1}%) | Clique para detalhar Nível 2";
                RegistrarInteracaoFatia(corte.Forma, corte.Fatia, tooltip);
                CanvasPizza.Children.Add(corte.Forma);
            }

            // 2. Paredes externas curvas (frontais: trecho que cruza [0°, 180°])
            foreach (var item in intervalos)
            {
                double frenteIni = Math.Max(item.Ini, 0.0);
                double frenteFim = Math.Min(item.Fim, 180.0);

                if (frenteFim - frenteIni > 0.1)
                {
                    string tooltip = $"{item.Fatia.Categoria} — {item.Fatia.Valor.ToString("C2", _cultura)} ({item.Fatia.Percentual:N1}%) | Clique para detalhar Nível 2";
                    var paredeCurva = CriarParedeCurva3D(cx, cy, rx, ry, altura, frenteIni, frenteFim, item.Fatia.Cor);
                    RegistrarInteracaoFatia(paredeCurva, item.Fatia, tooltip);
                    CanvasPizza.Children.Add(paredeCurva);
                }
            }

            // 3. Faces superiores elípticas com iluminação diagonal
            var etiquetas = new List<(FatiaPizzaItem Item, double X, double Y)>();

            foreach (var item in intervalos)
            {
                double sweep = item.Fim - item.Ini;
                string tooltip = $"{item.Fatia.Categoria} — {item.Fatia.Valor.ToString("C2", _cultura)} ({item.Fatia.Percentual:N1}%) | Clique para detalhar Nível 2";

                var topoFatia = CriarTopoFatia3D(cx, cy, rx, ry, item.Ini, item.Fim, item.Fatia.Cor);
                RegistrarInteracaoFatia(topoFatia, item.Fatia, tooltip);
                CanvasPizza.Children.Add(topoFatia);

                double radMeio = (item.Ini + sweep / 2.0) * Math.PI / 180.0;
                double fatorRaio = sweep < 16.0 ? 0.80 : 0.64;
                double lx = cx + (rx * fatorRaio) * Math.Cos(radMeio);
                double ly = cy + (ry * fatorRaio) * Math.Sin(radMeio);

                etiquetas.Add((item.Fatia, lx, ly));
            }

            // 4. Etiquetas de percentual por cima de todas as faces 3D
            foreach (var et in etiquetas)
            {
                AdicionarEtiquetaPercentual(CanvasPizza, et.Item.Percentual, et.X, et.Y, et.Item);
            }
        }

        private static void DesenharSombraProjetada(Canvas canvas, double cx, double cy, double rx, double ry, double altura)
        {
            double[] escalas = { 1.05, 1.00, 0.94 };
            byte[] alphas = { 45, 75, 110 };

            for (int i = 0; i < escalas.Length; i++)
            {
                double srx = rx * escalas[i];
                double sry = ry * escalas[i];
                var sombra = new Ellipse
                {
                    Width = srx * 2,
                    Height = sry * 2,
                    Fill = new SolidColorBrush(Color.FromArgb(alphas[i], 0, 0, 0)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(sombra, cx - srx);
                Canvas.SetTop(sombra, cy - sry + altura + 7.0);
                canvas.Children.Add(sombra);
            }
        }

        private static XamlPath CriarTopoFatia3D(double cx, double cy, double rx, double ry, double angIni, double angFim, Color corBase)
        {
            double sweep = angFim - angIni;
            double radIni = angIni * Math.PI / 180.0;
            double radFim = angFim * Math.PI / 180.0;

            double x1 = cx + rx * Math.Cos(radIni);
            double y1 = cy + ry * Math.Sin(radIni);
            double x2 = cx + rx * Math.Cos(radFim);
            double y2 = cy + ry * Math.Sin(radFim);

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
                Size = new Size(rx, ry),
                IsLargeArc = sweep > 180.0,
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new XamlPath
            {
                Data = geometry,
                Fill = CriarBrushTopo3D(corBase),
                Stroke = new SolidColorBrush(Color.FromArgb(190, 8, 14, 18)),
                StrokeThickness = 1.0,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        private static XamlPath CriarParedeCurva3D(double cx, double cy, double rx, double ry, double altura, double angIni, double angFim, Color corBase)
        {
            double sweep = angFim - angIni;
            double radIni = angIni * Math.PI / 180.0;
            double radFim = angFim * Math.PI / 180.0;

            double xTopIni = cx + rx * Math.Cos(radIni);
            double yTopIni = cy + ry * Math.Sin(radIni);
            double xTopFim = cx + rx * Math.Cos(radFim);
            double yTopFim = cy + ry * Math.Sin(radFim);

            double xBotFim = xTopFim;
            double yBotFim = yTopFim + altura;
            double xBotIni = xTopIni;
            double yBotIni = yTopIni + altura;

            var figure = new PathFigure
            {
                StartPoint = new Point(xTopIni, yTopIni),
                IsClosed = true,
                IsFilled = true
            };

            // Borda superior da parede (acompanha a elipse do topo)
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(xTopFim, yTopFim),
                Size = new Size(rx, ry),
                IsLargeArc = sweep > 180.0,
                SweepDirection = SweepDirection.Clockwise
            });

            // Lateral direita descendo
            figure.Segments.Add(new LineSegment { Point = new Point(xBotFim, yBotFim) });

            // Borda inferior da parede (acompanha a base deslocada em +altura)
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(xBotIni, yBotIni),
                Size = new Size(rx, ry),
                IsLargeArc = sweep > 180.0,
                SweepDirection = SweepDirection.Counterclockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new XamlPath
            {
                Data = geometry,
                Fill = CriarBrushParede3D(corBase),
                Stroke = new SolidColorBrush(EscurecerCor(corBase, 0.32)),
                StrokeThickness = 0.8,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        private static XamlPath CriarParedeRadial3D(double cx, double cy, double xEdge, double yEdge, double altura, Color corBase)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(cx, cy),
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new LineSegment { Point = new Point(xEdge, yEdge) });
            figure.Segments.Add(new LineSegment { Point = new Point(xEdge, yEdge + altura) });
            figure.Segments.Add(new LineSegment { Point = new Point(cx, cy + altura) });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new XamlPath
            {
                Data = geometry,
                Fill = CriarBrushCorte3D(corBase),
                Stroke = new SolidColorBrush(EscurecerCor(corBase, 0.30)),
                StrokeThickness = 0.8,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        private static LinearGradientBrush CriarBrushTopo3D(Color corBase)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.15, 0.05),
                EndPoint = new Point(0.85, 0.95)
            };
            brush.GradientStops.Add(new GradientStop { Color = ClarearCor(corBase, 0.24), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = corBase, Offset = 0.55 });
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.76), Offset = 1.0 });
            return brush;
        }

        private static LinearGradientBrush CriarBrushParede3D(Color corBase)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.0, 0.1),
                EndPoint = new Point(1.0, 0.9)
            };
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.42), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.72), Offset = 0.38 });
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.54), Offset = 0.72 });
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.34), Offset = 1.0 });
            return brush;
        }

        private static LinearGradientBrush CriarBrushCorte3D(Color corBase)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.0, 0.0),
                EndPoint = new Point(0.0, 1.0)
            };
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.62), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = EscurecerCor(corBase, 0.36), Offset = 1.0 });
            return brush;
        }

        private static Color EscurecerCor(Color c, double fator)
        {
            return Color.FromArgb(
                c.A,
                (byte)Math.Clamp(c.R * fator, 0, 255),
                (byte)Math.Clamp(c.G * fator, 0, 255),
                (byte)Math.Clamp(c.B * fator, 0, 255));
        }

        private static Color ClarearCor(Color c, double fator)
        {
            return Color.FromArgb(
                c.A,
                (byte)Math.Clamp(c.R + (255 - c.R) * fator, 0, 255),
                (byte)Math.Clamp(c.G + (255 - c.G) * fator, 0, 255),
                (byte)Math.Clamp(c.B + (255 - c.B) * fator, 0, 255));
        }

        private void RegistrarInteracaoFatia(Shape forma, FatiaPizzaItem fatia, string tooltip)
        {
            forma.Tag = fatia;
            ToolTipService.SetToolTip(forma, tooltip);
            forma.Tapped += Fatia_Tapped;
            forma.PointerEntered += Fatia_PointerEntered;
            forma.PointerExited += Fatia_PointerExited;

            if (!_formasPorFatia.TryGetValue(fatia, out var lista))
            {
                lista = new List<Shape>();
                _formasPorFatia[fatia] = lista;
            }
            lista.Add(forma);
        }

        private void Fatia_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is FatiaPizzaItem fatia &&
                _formasPorFatia.TryGetValue(fatia, out var formas))
            {
                foreach (var f in formas)
                {
                    f.Stroke = new SolidColorBrush(Colors.White);
                    f.StrokeThickness = 1.6;
                }
            }
        }

        private void Fatia_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is FatiaPizzaItem fatia &&
                _formasPorFatia.TryGetValue(fatia, out var formas))
            {
                foreach (var f in formas)
                {
                    f.Stroke = new SolidColorBrush(Color.FromArgb(190, 8, 14, 18));
                    f.StrokeThickness = 1.0;
                }
            }
        }

        private void AdicionarEtiquetaPercentual(Canvas canvasDestino, double percentual, double x, double y, FatiaPizzaItem? fatiaClicavel = null)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 4, 10, 14)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                BorderThickness = new Thickness(0.6),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Tag = fatiaClicavel,
                Child = new TextBlock
                {
                    Text = $"{percentual:N1}%",
                    FontSize = fatiaClicavel != null ? 11 : 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };

            if (fatiaClicavel != null)
            {
                badge.Tapped += Fatia_Tapped;
            }

            badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(badge, x - (badge.DesiredSize.Width / 2.0));
            Canvas.SetTop(badge, y - (badge.DesiredSize.Height / 2.0));

            canvasDestino.Children.Add(badge);
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
                    ToolTipService.SetToolTip(btnChip, $"{f.Categoria} — {f.Valor.ToString("C2", _cultura)} ({f.Percentual:N1}%) | Clique para detalhar Nível 2");
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
            Point pontoClique = e.GetPosition(this);
            AbrirBoxDetalhesNivel2(pontoClique, fatia);
        }

        private void AbrirBoxDetalhesNivel2(Point pontoClique, FatiaPizzaItem fatiaNivel1)
        {
            var painelPrincipal = new StackPanel
            {
                Spacing = 10,
                Width = 370
            };

            // 1. Cabeçalho com Nome da Categoria Nível 1 e Valor Total
            var gridTopo = new Grid();
            gridTopo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridTopo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var indicadorCor = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(fatiaNivel1.Cor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(indicadorCor, 0);
            gridTopo.Children.Add(indicadorCor);

            var stackTitulo = new StackPanel { Spacing = 1 };
            stackTitulo.Children.Add(new TextBlock
            {
                Text = fatiaNivel1.Categoria.ToUpperInvariant(),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 102)),
                TextWrapping = TextWrapping.Wrap
            });
            stackTitulo.Children.Add(new TextBlock
            {
                Text = $"Total da Categoria: {fatiaNivel1.Valor.ToString("C2", _cultura)} ({fatiaNivel1.Percentual:N1}% do gráfico)",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            Grid.SetColumn(stackTitulo, 1);
            gridTopo.Children.Add(stackTitulo);

            painelPrincipal.Children.Add(gridTopo);

            painelPrincipal.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 55, 40))
            });

            // 2. Gráfico de Pizza 3D das Subcategorias (Nível 2)
            if (fatiaNivel1.Subcategorias.Count > 0)
            {
                var canvasSub = DesenharPizzaNivel2_3D(fatiaNivel1.Subcategorias);
                canvasSub.HorizontalAlignment = HorizontalAlignment.Center;
                painelPrincipal.Children.Add(canvasSub);

                painelPrincipal.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(255, 30, 55, 40))
                });

                // 3. Lista detalhada com Valor e Percentual de cada item Nível 2
                var listaItens = new StackPanel { Spacing = 5 };

                foreach (var sub in fatiaNivel1.Subcategorias)
                {
                    var linhaSub = new Grid();
                    linhaSub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    linhaSub.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    linhaSub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    linhaSub.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });

                    var dot = new Border
                    {
                        Width = 9,
                        Height = 9,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(sub.Cor),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 7, 0)
                    };
                    Grid.SetColumn(dot, 0);
                    linhaSub.Children.Add(dot);

                    var txtNome = new TextBlock
                    {
                        Text = sub.Nome,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    ToolTipService.SetToolTip(txtNome, sub.Nome);
                    Grid.SetColumn(txtNome, 1);
                    linhaSub.Children.Add(txtNome);

                    var txtValor = new TextBlock
                    {
                        Text = sub.Valor.ToString("C2", _cultura),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(txtValor, 2);
                    linhaSub.Children.Add(txtValor);

                    var txtPerc = new TextBlock
                    {
                        Text = $"{sub.Percentual:N1}%",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(sub.Cor),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(txtPerc, 3);
                    linhaSub.Children.Add(txtPerc);

                    listaItens.Children.Add(linhaSub);
                }

                var scrollItens = new ScrollViewer
                {
                    MaxHeight = 190,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = listaItens
                };

                painelPrincipal.Children.Add(scrollItens);
            }
            else
            {
                painelPrincipal.Children.Add(new TextBlock
                {
                    Text = "Nenhuma subcategoria de Nível 2 vinculada a este grupo.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var presenterStyle = new Style(typeof(FlyoutPresenter));
            presenterStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
            presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            presenterStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            presenterStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 430.0));
            presenterStyle.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, 580.0));

            var flyout = new Flyout
            {
                FlyoutPresenterStyle = presenterStyle,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 6, 13, 8)),
                    BorderBrush = new SolidColorBrush(fatiaNivel1.Cor),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 12, 14, 12),
                    Child = painelPrincipal
                }
            };

            var opcoes = new FlyoutShowOptions
            {
                Position = new Point(pontoClique.X, pontoClique.Y - 6),
                Placement = FlyoutPlacementMode.Auto
            };

            flyout.ShowAt(this, opcoes);
        }

        private Canvas DesenharPizzaNivel2_3D(List<SubcategoriaFatiaItem> subcategorias)
        {
            const double largura = 240.0;
            const double alturaCanvas = 175.0;
            const double cx = 120.0;
            const double cy = 76.0;
            const double rx = 104.0;
            const double ry = 66.0;
            const double altura3D = 18.0;

            var canvas = new Canvas
            {
                Width = largura,
                Height = alturaCanvas
            };

            DesenharSombraProjetada(canvas, cx, cy, rx, ry, altura3D);

            if (subcategorias.Count == 1)
            {
                var unica = subcategorias[0];
                string tooltip = $"{unica.Nome} — {unica.Valor.ToString("C2", _cultura)} (100,0%)";

                var parede = CriarParedeCurva3D(cx, cy, rx, ry, altura3D, 0.0, 180.0, unica.Cor);
                ToolTipService.SetToolTip(parede, tooltip);
                canvas.Children.Add(parede);

                var topo = new Ellipse
                {
                    Width = rx * 2,
                    Height = ry * 2,
                    Fill = CriarBrushTopo3D(unica.Cor),
                    Stroke = new SolidColorBrush(ClarearCor(unica.Cor, 0.35)),
                    StrokeThickness = 1.0
                };
                ToolTipService.SetToolTip(topo, tooltip);
                Canvas.SetLeft(topo, cx - rx);
                Canvas.SetTop(topo, cy - ry);
                canvas.Children.Add(topo);

                AdicionarEtiquetaPercentual(canvas, 100.0, cx, cy);
                return canvas;
            }

            var intervalos = new List<(SubcategoriaFatiaItem Sub, double Ini, double Fim)>();
            double anguloAtual = -90.0;

            foreach (var sub in subcategorias)
            {
                double sweep = (sub.Percentual / 100.0) * 360.0;
                if (sweep >= 359.99) sweep = 359.99;

                intervalos.Add((sub, anguloAtual, anguloAtual + sweep));
                anguloAtual += sweep;
            }

            // 1. Paredes radiais internas ordenadas de trás para frente
            var cortesVisiveis = new List<(double ProfundidadeY, XamlPath Forma, SubcategoriaFatiaItem Sub)>();

            foreach (var item in intervalos)
            {
                if (item.Ini > 90.0 && item.Ini < 270.0)
                {
                    double rad = item.Ini * Math.PI / 180.0;
                    double xEdge = cx + rx * Math.Cos(rad);
                    double yEdge = cy + ry * Math.Sin(rad);
                    cortesVisiveis.Add(((cy + yEdge) * 0.5, CriarParedeRadial3D(cx, cy, xEdge, yEdge, altura3D, item.Sub.Cor), item.Sub));
                }

                if (item.Fim > -90.0 && item.Fim < 90.0)
                {
                    double rad = item.Fim * Math.PI / 180.0;
                    double xEdge = cx + rx * Math.Cos(rad);
                    double yEdge = cy + ry * Math.Sin(rad);
                    cortesVisiveis.Add(((cy + yEdge) * 0.5, CriarParedeRadial3D(cx, cy, xEdge, yEdge, altura3D, item.Sub.Cor), item.Sub));
                }
            }

            foreach (var corte in cortesVisiveis.OrderBy(c => c.ProfundidadeY))
            {
                ToolTipService.SetToolTip(corte.Forma, $"{corte.Sub.Nome} — {corte.Sub.Valor.ToString("C2", _cultura)} ({corte.Sub.Percentual:N2}%)");
                canvas.Children.Add(corte.Forma);
            }

            // 2. Paredes externas curvas (frontais)
            foreach (var item in intervalos)
            {
                double frenteIni = Math.Max(item.Ini, 0.0);
                double frenteFim = Math.Min(item.Fim, 180.0);

                if (frenteFim - frenteIni > 0.1)
                {
                    var paredeCurva = CriarParedeCurva3D(cx, cy, rx, ry, altura3D, frenteIni, frenteFim, item.Sub.Cor);
                    ToolTipService.SetToolTip(paredeCurva, $"{item.Sub.Nome} — {item.Sub.Valor.ToString("C2", _cultura)} ({item.Sub.Percentual:N2}%)");
                    canvas.Children.Add(paredeCurva);
                }
            }

            // 3. Faces superiores elípticas
            var etiquetas = new List<(double Percentual, double X, double Y)>();

            foreach (var item in intervalos)
            {
                double sweep = item.Fim - item.Ini;
                var topoSub = CriarTopoFatia3D(cx, cy, rx, ry, item.Ini, item.Fim, item.Sub.Cor);
                ToolTipService.SetToolTip(topoSub, $"{item.Sub.Nome} — {item.Sub.Valor.ToString("C2", _cultura)} ({item.Sub.Percentual:N2}%)");
                canvas.Children.Add(topoSub);

                if (item.Sub.Percentual >= 3.5)
                {
                    double radMeio = (item.Ini + sweep / 2.0) * Math.PI / 180.0;
                    double fatorRaio = sweep < 18.0 ? 0.78 : 0.62;
                    double lx = cx + (rx * fatorRaio) * Math.Cos(radMeio);
                    double ly = cy + (ry * fatorRaio) * Math.Sin(radMeio);
                    etiquetas.Add((item.Sub.Percentual, lx, ly));
                }
            }

            foreach (var et in etiquetas)
            {
                AdicionarEtiquetaPercentual(canvas, et.Percentual, et.X, et.Y);
            }

            return canvas;
        }
    }
}