using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DrawingImage = System.Drawing.Image;

namespace TeampptAddin
{
    internal class AssetCard : Border
    {
        public event Action<AssetCard> ClickInsertRequested;
        public event Action<AssetCard> DragStartRequested;

        public string PptxPath { get; }
        public string Title { get; }
        public DrawingImage DrawingThumbnail { get; }

        private bool _mousePressed;
        private Point _dragStart;

        public AssetCard(DrawingImage thumb, string title, string pptxPath)
        {
            PptxPath = pptxPath;
            Title = title;
            DrawingThumbnail = thumb;

            CornerRadius = ThemeResources.RadiusCard;
            Background = ThemeResources.BgCard;
            BorderBrush = ThemeResources.BorderCard;
            BorderThickness = new Thickness(1);
            Margin = new Thickness(10, 5, 10, 5);
            Cursor = Cursors.Hand;
            ClipToBounds = true;
            SnapsToDevicePixels = true;

            RenderTransform = new ScaleTransform(1, 1);
            RenderTransformOrigin = new Point(0.5, 0.5);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

            // Thumbnail area
            var thumbBorder = new Border { Background = ThemeResources.BgThumb, ClipToBounds = true };
            if (thumb != null)
            {
                thumbBorder.Child = new Image
                {
                    Source = ConvertToBitmapSource(thumb),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                thumbBorder.Child = new TextBlock
                {
                    Text = title,
                    Foreground = ThemeResources.TextSub,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            Grid.SetRow(thumbBorder, 0);
            grid.Children.Add(thumbBorder);

            // Separator
            var sep = new Border
            {
                Height = 1,
                Background = ThemeResources.BorderBase,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(sep, 0);
            grid.Children.Add(sep);

            // Label row
            var labelGrid = new Grid();
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            labelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = title,
                Foreground = ThemeResources.TextMain,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleText, 0);
            labelGrid.Children.Add(titleText);

            var badge = new Border
            {
                Background = ThemeResources.BgBadge,
                CornerRadius = ThemeResources.RadiusBadge,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = "DRAG",
                    Foreground = ThemeResources.TextAccent,
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    FontFamily = ThemeResources.FontBase
                }
            };
            Grid.SetColumn(badge, 1);
            labelGrid.Children.Add(badge);

            Grid.SetRow(labelGrid, 1);
            grid.Children.Add(labelGrid);

            Child = grid;

            MouseEnter          += OnMouseEnter;
            MouseLeave          += OnMouseLeave;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove           += OnMouseMove;
            MouseLeftButtonUp   += OnMouseUp;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            Background = ThemeResources.BgCardHover;
            BorderBrush = ThemeResources.BorderCardHover;
            AnimateScale(1.02);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            Background = ThemeResources.BgCard;
            BorderBrush = ThemeResources.BorderCard;
            AnimateScale(1.0);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _mousePressed = true;
            _dragStart = e.GetPosition(this);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_mousePressed || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _dragStart.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _mousePressed = false;
                DragStartRequested?.Invoke(this);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_mousePressed) return;
            _mousePressed = false;
            ClickInsertRequested?.Invoke(this);
        }

        private void AnimateScale(double target)
        {
            var scale = (ScaleTransform)RenderTransform;
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase()
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private static BitmapSource ConvertToBitmapSource(DrawingImage img)
        {
            using (var ms = new MemoryStream())
            {
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
    }
}
