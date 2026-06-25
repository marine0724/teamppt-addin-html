using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DrawingImage = System.Drawing.Image;

namespace TeampptAddin
{
    internal class AssetPanel : UserControl
    {
        public event Action<AssetCard> CardClickInsert;
        public event Action<AssetCard> CardDragStart;
        public event Action<StylePalette, StyleFont> StyleApplyRequested;

        private List<HeaderAsset> _allAssets = new List<HeaderAsset>();
        private List<AssetCard> _assetCards = new List<AssetCard>();
        private Dictionary<string, AssetCard> _cardByFile = new Dictionary<string, AssetCard>();
        private IAiService _aiService;
        private StyleConfig _styleConfig;
        private RemoteAssetCache _remoteCache;
        private HeaderAsset _anchorAsset;

        private Border[] _tabBorders;
        private FrameworkElement[] _tabPanels;
        private int _activeTab;

        private StackPanel _chatStack;
        private ScrollViewer _chatScroll;
        private TextBox _inputBox;
        private bool _hasPlaceholder;
        private StackPanel _emptyState;

        private StackPanel _assetStack;
        private Border[] _categoryBtns;
        private readonly string[] _categories = { "전체", "헤더", "섹션", "레이아웃", "마무리" };
        private string _activeCategory = "전체";

        private StylePalette _selectedPalette;
        private StyleFont _selectedFont;
        private StackPanel _styleStack;
        private Border[] _paletteBtns;
        private Border[] _fontBtns;

        private Border _sendBtn;
        private Border _shareBar;
        private Border _redesignBar;
        private RedesignService _redesign;
        private RecommendationService _recommend;
        private CombinationRecommendation _lastRecommendation;
        private RecommendationResult _lastRecoResult;
        private List<RedesignPreview> _lastPreviews;
        private bool _redesignRunning;
        private bool _redesignCommitted;

        private TextBlock _statusText;
        private Border _ingestButton;

        private Border _ingestProgressTrack;
        private Border _ingestProgressFill;
        private TextBlock _ingestProgressText;
        private Grid _ingestCurrentContainer;
        private TextBlock _ingestStageText;
        private StackPanel _ingestCompletedStack;
        private Border _completedCollapsedBox;
        private TextBlock _completedCountText;
        private TextBlock _completedChevron;
        private Image _completedLatestThumb;
        private TextBlock _completedLatestName;
        private bool _completedExpanded;
        private int _completedCount;
        private DispatcherTimer _ingestDotsTimer;
        private string _ingestStageBase;
        private Border _ingestScanLine;
        private TextBlock _ingestNameText;
        private int _ingestLastIndex;

        private DispatcherTimer _searchStageTimer;
        private DispatcherTimer _searchScrollTimer;
        private DispatcherTimer _searchShimmerTimer;
        private int _searchStage;

        private string _retryOutputDir;
        private string _retryBundleName;
        private int _retryStartFrom;

        public AssetPanel()
        {
            Background = ThemeResources.BgBase;
            FontFamily = ThemeResources.FontBase;

            var root = new DockPanel { LastChildFill = true };

            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            _updateBannerHost = new Border { Visibility = Visibility.Collapsed };
            DockPanel.SetDock(_updateBannerHost, Dock.Top);
            root.Children.Add(_updateBannerHost);

            var statusBar = BuildStatusBar();
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            var aiTab = BuildAiTab();
            var assetTab = BuildAssetTab();
            var styleTab = BuildStyleTab();
            _tabPanels = new FrameworkElement[] { aiTab, assetTab, styleTab };

            var content = new Grid();
            foreach (var p in _tabPanels) content.Children.Add(p);
            root.Children.Add(content);

            Content = root;
            SwitchTab(0);

            StartUpdateWatch();
        }

        // ── 자동 업데이트 배너 ─────────────────────────────────────────
        // UpdateService(백그라운드)가 새 버전을 staging에 받아두면 마커 파일을 남긴다.
        // 패널은 그 마커를 감시(폴링)하다 발견하면 상단 배너를 띄운다. (이벤트 배선 없이 디커플링)

        private Border _updateBannerHost;
        private DispatcherTimer _updateWatchTimer;
        private bool _updateBannerShown;

        private void StartUpdateWatch()
        {
            // 시작 직후 한 번, 이후 주기적으로 마커 확인(백그라운드 다운로드가 늦게 끝나는 경우 대비).
            TryShowUpdateBanner();
            if (_updateBannerShown) return;

            _updateWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _updateWatchTimer.Tick += (s, e) => TryShowUpdateBanner();
            _updateWatchTimer.Start();
        }

        private void TryShowUpdateBanner()
        {
            if (_updateBannerShown) return;
            try
            {
                if (!System.IO.File.Exists(UpdateService.MarkerPath)) return;
                var marker = Newtonsoft.Json.Linq.JObject.Parse(
                    System.IO.File.ReadAllText(UpdateService.MarkerPath));
                var version = (string)marker["version"] ?? "";
                var notes = (string)marker["notes"] ?? "";

                _updateBannerShown = true;
                if (_updateWatchTimer != null) { _updateWatchTimer.Stop(); _updateWatchTimer = null; }
                ShowUpdateBanner(version, notes);
            }
            catch (Exception ex) { Logger.Log($"[Update] 배너 표시 실패: {ex.Message}"); }
        }

        private void ShowUpdateBanner(string version, string notes)
        {
            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textCol.Children.Add(new TextBlock
            {
                Text = $"✨ 업데이트 v{version} 준비됨",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, FontFamily = ThemeResources.FontBase
            });
            if (!string.IsNullOrEmpty(notes))
                textCol.Children.Add(new TextBlock
                {
                    Text = notes,
                    FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontFamily = ThemeResources.FontBase,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1, 0, 0)
                });

            var applyBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 5, 12, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "지금 적용 ▶",
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White, FontFamily = ThemeResources.FontBase
                }
            };
            applyBtn.MouseEnter += (s, e) => applyBtn.Opacity = 0.8;
            applyBtn.MouseLeave += (s, e) => applyBtn.Opacity = 1;
            applyBtn.MouseLeftButtonUp += (s, e) => ApplyUpdate();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(textCol, 0);
            Grid.SetColumn(applyBtn, 1);
            grid.Children.Add(textCol);
            grid.Children.Add(applyBtn);

            _updateBannerHost.Background = ThemeResources.Accent;
            _updateBannerHost.Padding = new Thickness(14, 10, 12, 10);
            _updateBannerHost.Child = grid;
            _updateBannerHost.Visibility = Visibility.Visible;
        }

        private void ApplyUpdate()
        {
            var confirm = System.Windows.MessageBox.Show(
                "업데이트를 적용하려면 PowerPoint가 재시작됩니다.\n저장하지 않은 작업이 있으면 먼저 저장하세요.\n\n계속할까요?",
                "TEAMPPT 업데이트",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Information);
            if (confirm != System.Windows.MessageBoxResult.OK) return;

            if (!UpdateService.LaunchUpdater())
            {
                AddAiBubble("업데이트 적용을 시작하지 못했어요. 잠시 후 다시 시도하거나 수동으로 재설치해주세요.");
                return;
            }

            // updater.bat이 PowerPoint 종료를 기다렸다 파일을 교체하고 재실행한다.
            try { Globals.Application?.Quit(); }
            catch (Exception ex) { Logger.Log($"[Update] PowerPoint 종료 실패: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        //  HEADER + TAB BAR
        // ══════════════════════════════════════════════════════════════

        private StackPanel BuildHeader()
        {
            var header = new StackPanel { Background = ThemeResources.BgSurface };

            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            titleRow.Children.Add(new TextBlock
            {
                Text = "TEAMPPT",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.Accent,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(16, 12, 0, 8)
            });

            _ingestButton = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(234, 88, 12)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 10, 12, 6),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "인제스트",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    FontFamily = ThemeResources.FontBase
                }
            };
            _ingestButton.MouseLeftButtonUp += async (s, e) => await RunIngestAsync();
            Grid.SetColumn(_ingestButton, 1);
            titleRow.Children.Add(_ingestButton);
            header.Children.Add(titleRow);

            var tabGrid = new Grid { Height = 36 };
            for (int i = 0; i < 3; i++)
                tabGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var sep = new Border
            {
                Height = 1,
                Background = ThemeResources.BorderBase,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(sep, 3);
            tabGrid.Children.Add(sep);

            string[] labels = { "AI", "에셋", "스타일" };
            _tabBorders = new Border[3];

            for (int i = 0; i < 3; i++)
            {
                var idx = i;

                var indicator = new Border
                {
                    Height = 2,
                    CornerRadius = new CornerRadius(1),
                    Background = Brushes.Transparent,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(12, 0, 12, 0)
                };

                var lbl = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 13,
                    FontFamily = ThemeResources.FontBase,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = ThemeResources.TextSub,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var inner = new Grid();
                inner.Children.Add(lbl);
                inner.Children.Add(indicator);

                var tab = new Border
                {
                    Child = inner,
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                tab.Tag = new object[] { lbl, indicator };
                tab.MouseLeftButtonUp += (s, e) => SwitchTab(idx);

                _tabBorders[i] = tab;
                Grid.SetColumn(tab, i);
                tabGrid.Children.Add(tab);
            }

            header.Children.Add(tabGrid);
            return header;
        }

        private void SwitchTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < _tabBorders.Length; i++)
            {
                var parts = (object[])_tabBorders[i].Tag;
                var lbl = (TextBlock)parts[0];
                var ind = (Border)parts[1];
                bool active = i == index;
                lbl.Foreground = active ? ThemeResources.TextAccent : ThemeResources.TextSub;
                lbl.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                ind.Background = active ? ThemeResources.Accent : Brushes.Transparent;
            }
            for (int i = 0; i < _tabPanels.Length; i++)
                _tabPanels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════
        //  STATUS BAR
        // ══════════════════════════════════════════════════════════════

        private Border BuildStatusBar()
        {
            _statusText = new TextBlock
            {
                FontSize = 10,
                Foreground = ThemeResources.TextSub,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                Text = "로딩 중..."
            };
            return new Border
            {
                Height = 24,
                Background = ThemeResources.BgSurface,
                BorderBrush = ThemeResources.BorderBase,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = _statusText
            };
        }

        public void SetStatus(string text, Color color)
        {
            _statusText.Text = text;
            _statusText.Foreground = new SolidColorBrush(color);
        }

        public void ResetStatus()
        {
            int count = _assetCards.Count;
            _statusText.Text = count > 0
                ? $"{count}개 에셋 · 드래그 또는 클릭으로 삽입"
                : "Assets 폴더에 header_N.pptx 파일을 넣으세요";
            _statusText.Foreground = ThemeResources.TextSub;
        }

        // ══════════════════════════════════════════════════════════════
        //  AI TAB
        // ══════════════════════════════════════════════════════════════

        private Grid BuildAiTab()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _chatStack = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
            _emptyState = BuildEmptyState();
            _chatStack.Children.Add(_emptyState);

            _chatScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _chatStack,
                Background = ThemeResources.BgBase
            };
            Grid.SetRow(_chatScroll, 0);
            grid.Children.Add(_chatScroll);

            var bottom = new StackPanel();
            bottom.Children.Add(BuildShareBar());
            bottom.Children.Add(BuildRedesignBar());
            bottom.Children.Add(BuildInputBar());
            Grid.SetRow(bottom, 1);
            grid.Children.Add(bottom);

            return grid;
        }

        private StackPanel BuildEmptyState()
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 48, 20, 20)
            };

            var iconScale = new ScaleTransform(1, 1);
            var iconBorder = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = ThemeResources.BgChip,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12),
                RenderTransform = iconScale,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Child = new TextBlock
                {
                    Text = "✦",
                    FontSize = 22,
                    Foreground = ThemeResources.Accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            panel.Children.Add(iconBorder);

            try
            {
                var pulseAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.06,
                    Duration = TimeSpan.FromSeconds(1.4),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase()
                };
                iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnim);
                iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnim);
            }
            catch { }

            panel.Children.Add(new TextBlock
            {
                Text = "슬라이드 디자인을\n도와드릴게요",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "어떤 슬라이드가 필요한지 말씀해 주세요",
                FontSize = 12,
                Foreground = ThemeResources.TextSub,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            });

            var chips = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Orientation = Orientation.Horizontal
            };
            foreach (var ex in new[] { "회사 소개", "장점 3가지", "서비스 흐름", "팀 소개" })
                chips.Children.Add(BuildChip(ex));
            panel.Children.Add(chips);

            return panel;
        }

        private Border BuildChip(string text)
        {
            var lbl = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = ThemeResources.TextMain,
                VerticalAlignment = VerticalAlignment.Center
            };

            var chip = new Border
            {
                Child = lbl,
                Background = ThemeResources.BgBase,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.RadiusChip,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(4, 3, 4, 3),
                Cursor = Cursors.Hand
            };

            chip.MouseEnter += (s, e) =>
            {
                chip.Background = ThemeResources.BgCategoryActive;
                chip.BorderBrush = ThemeResources.AccentBorder;
                lbl.Foreground = ThemeResources.TextAccent;
            };
            chip.MouseLeave += (s, e) =>
            {
                chip.Background = ThemeResources.BgBase;
                chip.BorderBrush = ThemeResources.BorderCard;
                lbl.Foreground = ThemeResources.TextMain;
            };
            chip.MouseLeftButtonUp += async (s, e) => await SendAiMessage(text);

            return chip;
        }

        private Border BuildInputBar()
        {
            var bar = new Border
            {
                Background = ThemeResources.BgSurface,
                BorderBrush = ThemeResources.BorderBase,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(10, 10, 10, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var inputWrap = new Border
            {
                Background = ThemeResources.BgInput,
                BorderBrush = ThemeResources.AccentBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = ThemeResources.RadiusInput,
                Padding = new Thickness(12, 8, 12, 8)
            };

            _inputBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = ThemeResources.FontBase,
                Foreground = ThemeResources.TextDim,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 36,
                MaxHeight = 80,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "슬라이드에 뭘 넣고 싶어요?"
            };
            _hasPlaceholder = true;

            _inputBox.GotFocus += (s, e) =>
            {
                if (!_hasPlaceholder) return;
                _inputBox.Text = "";
                _inputBox.Foreground = ThemeResources.TextMain;
                _hasPlaceholder = false;
                inputWrap.BorderBrush = ThemeResources.Accent;
                UpdateSendButtonState();
            };
            _inputBox.LostFocus += (s, e) =>
            {
                inputWrap.BorderBrush = ThemeResources.AccentBorder;
                if (!string.IsNullOrEmpty(_inputBox.Text)) return;
                _inputBox.Text = "슬라이드에 뭘 넣고 싶어요?";
                _inputBox.Foreground = ThemeResources.TextDim;
                _hasPlaceholder = true;
                UpdateSendButtonState();
            };
            _inputBox.PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
                {
                    e.Handled = true;
                    await SendAiMessageFromInput();
                }
            };

            inputWrap.Child = _inputBox;
            Grid.SetColumn(inputWrap, 0);
            grid.Children.Add(inputWrap);

            _sendBtn = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(12),
                Width = 36,
                Height = 36,
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Arrow,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = new TextBlock
                {
                    Text = "↑",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeResources.TextDim,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _sendBtn.MouseLeftButtonUp += async (s, e) => await SendAiMessageFromInput();
            Grid.SetColumn(_sendBtn, 1);
            grid.Children.Add(_sendBtn);

            _inputBox.TextChanged += (s, e) => UpdateSendButtonState();

            bar.Child = grid;
            return bar;
        }

        private Border BuildShareBar()
        {
            _shareBar = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 8, 10, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand
            };
            SetShareBarIdle();
            return _shareBar;
        }

        // ── Route B: AI 리디자인 (현재 슬라이드 풀변환) ──

        private Border BuildRedesignBar()
        {
            _redesignBar = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 8, 10, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            row.Children.Add(new TextBlock
            {
                Text = "✨", FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "AI 리디자인", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            });
            _redesignBar.Child = row;
            _redesignBar.MouseLeftButtonUp += RedesignBarClick;
            return _redesignBar;
        }

        private async void RedesignBarClick(object sender, MouseButtonEventArgs e)
        {
            await RunRecommendationAsync();
        }

        private async Task RunRedesignAsync()
        {
            if (_redesignRunning) return;
            if (_redesign == null)
            {
                AddAiBubble("리디자인은 Supabase·Gemini 설정이 있어야 동작해요.");
                return;
            }

            _redesignRunning = true;
            _redesignCommitted = false;
            _redesignBar.IsEnabled = false;
            _redesignBar.Opacity = 0.6;
            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
                _emptyState.Visibility = Visibility.Collapsed;

            try
            {
                AddAiBubble("현재 슬라이드를 리디자인할게요.");
                // RedesignService는 COM(STA)을 호출하므로 이 UI 스레드에서 직접 await (ConfigureAwait(false) 미사용).
                _lastPreviews = await _redesign.RunAsync(msg => Dispatcher.Invoke(() => AddAiBubble(msg)));
                ShowRedesignCards(_lastPreviews);
            }
            catch (Exception ex)
            {
                AddAiBubble($"리디자인 중 오류: {ex.Message}");
                Logger.Log($"[Redesign] 실패: {ex}");
            }
            finally
            {
                _redesignRunning = false;
                _redesignBar.IsEnabled = true;
                _redesignBar.Opacity = 1;
                _chatScroll.ScrollToBottom();
            }
        }

        private async Task RunRecommendationAsync()
        {
            if (_redesignRunning) return;
            if (_recommend == null)
            {
                AddAiBubble("추천은 Supabase·Gemini 설정이 있어야 동작해요.");
                return;
            }

            _redesignRunning = true;
            _redesignBar.IsEnabled = false;
            _redesignBar.Opacity = 0.6;
            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
                _emptyState.Visibility = Visibility.Collapsed;

            try
            {
                AddAiBubble("현재 초안에 어울리는 에셋 조합을 찾아볼게요.");
                var result = await _recommend.RunAsync(msg => Dispatcher.Invoke(() => AddAiBubble(msg)));
                _lastRecoResult = result;
                ShowRecommendation(result.Recommendation);
            }
            catch (Exception ex)
            {
                AddAiBubble($"추천 중 오류: {ex.Message}");
                Logger.Log($"[Reco] 실패: {ex}");
            }
            finally
            {
                _redesignRunning = false;
                _redesignBar.IsEnabled = true;
                _redesignBar.Opacity = 1;
                _chatScroll.ScrollToBottom();
            }
        }

        private void ShowRecommendation(CombinationRecommendation rec)
        {
            if (rec == null) { AddAiBubble("추천 결과가 없어요."); return; }
            _lastRecommendation = rec;

            AddAiBubble(string.IsNullOrEmpty(rec.Purpose)
                ? "추천 조합이에요."
                : $"초안 분석: \"{rec.Purpose}\" ({rec.SlideKind})");

            _chatStack.Children.Add(new TextBlock
            {
                Text = "추천 조합",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Margin = new Thickness(12, 8, 0, 2)
            });

            if (rec.Slide != null)
                _chatStack.Children.Add(BuildRecoCard("표지", rec.Slide));
            if (rec.Header != null)
                _chatStack.Children.Add(BuildRecoCard("헤더", rec.Header));
            if (rec.Layout != null)
                _chatStack.Children.Add(BuildRecoCard("레이아웃", rec.Layout));
            for (int i = 0; i < rec.Components.Count; i++)
                _chatStack.Children.Add(BuildRecoCard($"컴포넌트 {i + 1}", rec.Components[i]));

            if (rec.Unmet != null && rec.Unmet.Count > 0)
                _chatStack.Children.Add(new TextBlock
                {
                    Text = $"미충족: {string.Join(", ", rec.Unmet)} (적합한 에셋 없음)",
                    FontSize = 10, Foreground = ThemeResources.TextSub,
                    Margin = new Thickness(14, 4, 12, 2)
                });

            _chatStack.Children.Add(BuildPlaceArrangeButton());
            _chatScroll.ScrollToBottom();
        }

        private Border BuildRecoCard(string slotLabel, RecommendedSlot slot)
        {
            var label = new TextBlock
            {
                Text = slotLabel,
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Width = 64, VerticalAlignment = VerticalAlignment.Center
            };
            var name = new TextBlock
            {
                Text = slot.Asset?.Name ?? slot.Asset?.File ?? "에셋",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase,
                TextWrapping = TextWrapping.Wrap
            };
            var note = new TextBlock
            {
                Text = $"{slot.FitNote}  ·  {(int)(slot.Confidence * 100)}%",
                FontSize = 10, Foreground = ThemeResources.TextSub,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textCol.Children.Add(name);
            textCol.Children.Add(note);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(label);
            row.Children.Add(textCol);

            return new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 4, 12, 4),
                Padding = new Thickness(10, 8, 10, 8),
                Child = row
            };
        }

        private Border BuildPlaceArrangeButton()
        {
            var border = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 8, 12, 8),
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "이 조합으로 배치 ▶",
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                    FontFamily = ThemeResources.FontBase,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            border.MouseLeftButtonUp += async (s, e) => await PlaceRecommendationAsync();
            return border;
        }

        private async Task PlaceRecommendationAsync()
        {
            var rec = _lastRecommendation;
            if (rec == null || _remoteCache == null) return;
            if (_redesignRunning) return;
            _redesignRunning = true;

            try
            {
                AddAiBubble("에셋 다운로드 중…");

                var slots = new List<RecommendedSlot>();
                if (rec.Slide != null) slots.Add(rec.Slide);
                if (rec.Header != null) slots.Add(rec.Header);
                if (rec.Layout != null) slots.Add(rec.Layout);
                slots.AddRange(rec.Components);

                if (slots.Count == 0)
                {
                    AddAiBubble("배치할 에셋이 없어요.");
                    return;
                }

                var localPaths = new List<string>();
                foreach (var slot in slots)
                {
                    var remotePath = slot.Asset.Extra != null && slot.Asset.Extra.ContainsKey("remote_file")
                        ? slot.Asset.Extra["remote_file"].ToString()
                        : slot.Asset.File;
                    var path = await _remoteCache.GetPptxAsync(remotePath);
                    localPaths.Add(path);
                    Logger.Log($"[Place] 다운로드 완료: {remotePath} → {path}");
                }

                AddAiBubble("새 슬라이드에 배치 중…");
                Dispatcher.Invoke(() => PlaceOnNewSlide(localPaths));
                AddAiBubble("배치 완료! 새 슬라이드를 확인해보세요.");
            }
            catch (Exception ex)
            {
                AddAiBubble($"배치 중 오류: {ex.Message}");
                Logger.Log($"[Place] 실패: {ex}");
            }
            finally
            {
                _redesignRunning = false;
                _chatScroll.ScrollToBottom();
            }
        }

        private void PlaceOnNewSlide(List<string> pptxPaths)
        {
            var app = Globals.Application;
            if (app == null) return;
            var pres = app.ActivePresentation;
            var currentSlide = (Microsoft.Office.Interop.PowerPoint.Slide)app.ActiveWindow.View.Slide;
            int insertAfter = currentSlide.SlideIndex;

            app.StartNewUndoEntry();

            var layout = currentSlide.CustomLayout;
            var newSlide = pres.Slides.AddSlide(insertAfter + 1, layout);

            while (newSlide.Shapes.Count > 0)
                newSlide.Shapes[1].Delete();

            foreach (var pptxPath in pptxPaths)
            {
                int tempIdx = pres.Slides.Count;
                pres.Slides.InsertFromFile(pptxPath, tempIdx, 1, 1);
                var tempSlide = pres.Slides[tempIdx + 1];

                int count = tempSlide.Shapes.Count;
                if (count > 0)
                {
                    var indices = new int[count];
                    for (int i = 0; i < count; i++)
                        indices[i] = i + 1;
                    tempSlide.Shapes.Range(indices).Copy();
                    newSlide.Shapes.Paste();
                }
                tempSlide.Delete();
            }

            app.ActiveWindow.View.GotoSlide(newSlide.SlideIndex);
            Logger.Log($"[Place] 새 슬라이드 {newSlide.SlideIndex} 생성, 에셋 {pptxPaths.Count}개 배치");
        }

        private void ShowRedesignCards(List<RedesignPreview> previews)
        {
            if (previews == null || previews.Count == 0)
            {
                AddAiBubble("만들 수 있는 시안이 없어요.");
                return;
            }

            AddAiBubble("시안을 골라줘. 선택한 디자인만 남기고 나머지는 정리할게.");
            _chatStack.Children.Add(new TextBlock
            {
                Text = "리디자인 시안",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Margin = new Thickness(12, 8, 0, 2)
            });

            foreach (var p in previews)
                _chatStack.Children.Add(BuildRedesignCard(p, previews));

            _chatScroll.ScrollToBottom();
        }

        private Border BuildRedesignCard(RedesignPreview preview, List<RedesignPreview> all)
        {
            var thumb = new Border
            {
                Width = 120, Height = 68,
                Background = ThemeResources.BgThumb,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 10, 0)
            };
            ThemeResources.ApplyRoundedClip(thumb, 8);
            var img = LoadBitmapFromFile(preview.ThumbPath);
            if (img != null)
                thumb.Child = new Image { Source = img, Stretch = Stretch.Uniform, Margin = new Thickness(2) };

            var nameText = new TextBlock
            {
                Text = preview.Asset?.Name ?? preview.Asset?.File ?? "에셋",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase,
                TextWrapping = TextWrapping.Wrap
            };
            var info = new TextBlock
            {
                Text = $"채움 {preview.Mapping?.Mappings.Count ?? 0} · 빈자리 {preview.Mapping?.Empty.Count ?? 0}",
                FontSize = 10, Foreground = ThemeResources.TextSub,
                Margin = new Thickness(0, 3, 0, 0)
            };
            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textCol.Children.Add(nameText);
            textCol.Children.Add(info);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(thumb);
            row.Children.Add(textCol);

            var card = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 4, 12, 4),
                Padding = new Thickness(8),
                Cursor = Cursors.Hand,
                Child = row
            };
            card.MouseLeftButtonUp += (s, e) => OnRedesignCardChosen(preview, all);
            return card;
        }

        private void OnRedesignCardChosen(RedesignPreview chosen, List<RedesignPreview> all)
        {
            if (_redesignCommitted) return;
            try
            {
                _redesign.Commit(chosen, all);
                _redesignCommitted = true;
                AddAiBubble($"'{chosen.Asset?.Name ?? chosen.Asset?.File}'(으)로 변환했어요. 원본은 그대로 두고 변환본을 옆에 만들었어요.");
            }
            catch (Exception ex)
            {
                AddAiBubble($"적용 중 오류: {ex.Message}");
                Logger.Log($"[Redesign] commit 실패: {ex}");
            }
            _chatScroll.ScrollToBottom();
        }

        private static System.Windows.Media.Imaging.BitmapImage LoadBitmapFromFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private void SetShareBarIdle()
        {
            _shareBar.RenderTransform = null;
            _shareBar.Opacity = 1;
            _shareBar.Background = ThemeResources.BgChip;
            _shareBar.Cursor = Cursors.Hand;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            row.Children.Add(new TextBlock
            {
                Text = "\U0001F5A5", FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "화면 공유 진단", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            });
            _shareBar.Child = row;

            _shareBar.MouseLeftButtonUp -= ShareBarClick;
            _shareBar.MouseLeftButtonUp += ShareBarClick;
        }

        private async void ShareBarClick(object sender, MouseButtonEventArgs e)
        {
            await RunDiagnosisAsync();
        }

        private void EnterSharingState(int slideNumber)
        {
            _shareBar.MouseLeftButtonUp -= ShareBarClick;
            _shareBar.Cursor = Cursors.Arrow;
            _shareBar.Background = ThemeResources.BgCategoryActive;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dotScale = new ScaleTransform(1, 1);
            var dot = new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(79, 92, 245)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                RenderTransform = dotScale,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            try
            {
                var pulse = new DoubleAnimation
                {
                    From = 1.0, To = 1.5,
                    Duration = TimeSpan.FromSeconds(0.8),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase()
                };
                dotScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                dotScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
            }
            catch { }
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            var lbl = new TextBlock
            {
                Text = $"슬라이드 {slideNumber} 공유 중",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 1);
            grid.Children.Add(lbl);

            var close = new TextBlock
            {
                Text = "✕", FontSize = 12,
                Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase,
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
            };
            close.MouseLeftButtonUp += (s, e) => { e.Handled = true; ExitSharingState(); };
            Grid.SetColumn(close, 2);
            grid.Children.Add(close);

            _shareBar.Child = grid;

            try
            {
                _shareBar.Opacity = 0;
                var tt = new TranslateTransform(0, 8);
                _shareBar.RenderTransform = tt;
                _shareBar.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
                tt.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(150))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            }
            catch { _shareBar.Opacity = 1; }
        }

        private void ExitSharingState()
        {
            SetShareBarIdle();
        }

        private FrameworkElement AddDiagnosisLoadingBubble()
        {
            var wrapper = new StackPanel { Margin = new Thickness(12, 4, 40, 4) };
            wrapper.Children.Add(new TextBlock
            {
                Text = "TEAMPPT AI", FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.Accent, Margin = new Thickness(4, 0, 0, 3)
            });

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = "화면을 읽는 중···", FontSize = 11,
                Foreground = ThemeResources.TextDim, FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var track = new Border
            {
                Height = 6, Width = 180, CornerRadius = new CornerRadius(3),
                Background = ThemeResources.BgSurface, ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var scan = new Border
            {
                Width = 50, CornerRadius = new CornerRadius(3),
                Background = ThemeResources.Accent,
                HorizontalAlignment = HorizontalAlignment.Left,
                RenderTransform = new TranslateTransform(-50, 0)
            };
            track.Child = scan;
            content.Children.Add(track);

            var border = new Border
            {
                Background = ThemeResources.BgAiResponse,
                CornerRadius = new CornerRadius(4, 13, 13, 13),
                Padding = new Thickness(14, 10, 14, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 180,
                Child = content
            };
            wrapper.Children.Add(border);
            _chatStack.Children.Add(wrapper);

            var scanTt = (TranslateTransform)scan.RenderTransform;
            double x = -50; bool fwd = true;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (s, e) =>
            {
                x += fwd ? 8 : -8;
                if (x >= 180) fwd = false;
                if (x <= -50) fwd = true;
                scanTt.X = x;
            };
            timer.Start();
            border.Tag = timer;

            return wrapper;
        }

        private async Task RunDiagnosisAsync()
        {
            if (_aiService == null)
            {
                AddAiBubble("AI 서비스를 초기화 중입니다.");
                return;
            }

            SlideCapture capture;
            try { capture = SlideCaptureService.CaptureCurrentSlide(); }
            catch (Exception ex)
            {
                AddAiBubble($"슬라이드를 읽지 못했어요: {ex.Message}");
                return;
            }
            if (capture == null)
            {
                AddAiBubble("공유할 슬라이드가 없어요. PowerPoint에서 슬라이드를 여세요.");
                return;
            }

            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
                _emptyState.Visibility = Visibility.Collapsed;

            EnterSharingState(capture.SlideNumber);
            var loading = AddDiagnosisLoadingBubble();
            _chatScroll.ScrollToBottom();

            try
            {
                var diag = await _aiService.DiagnoseSlideAsync(capture.PngPath);
                RemoveLoadingBubble(loading);
                AddDiagnosisResult(diag);
            }
            catch (Exception ex)
            {
                RemoveLoadingBubble(loading);
                AddAiBubble($"진단 중 오류: {ex.Message}");
            }

            _chatScroll.ScrollToBottom();
        }

        private void AddDiagnosisResult(SlideDiagnosis diag)
        {
            AddAiBubble(diag?.Message ?? "진단 결과를 확인해보세요.");

            var qs = diag?.SuggestedQuestions ?? new List<string>();
            if (qs.Count == 0) return;

            _chatStack.Children.Add(new TextBlock
            {
                Text = "이어서 물어보기", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub, Margin = new Thickness(12, 8, 0, 2)
            });

            var wrap = new WrapPanel { Margin = new Thickness(8, 0, 12, 4) };
            foreach (var q in qs.Take(3))
                wrap.Children.Add(BuildChip(q));
            _chatStack.Children.Add(wrap);
        }

        private void UpdateSendButtonState()
        {
            if (_sendBtn == null) return;
            bool active = !_hasPlaceholder && !string.IsNullOrWhiteSpace(_inputBox.Text);
            var lbl = (TextBlock)_sendBtn.Child;
            _sendBtn.Background = active ? ThemeResources.Accent : ThemeResources.BgChip;
            lbl.Foreground = active ? Brushes.White : ThemeResources.TextDim;
            _sendBtn.Cursor = active ? Cursors.Hand : Cursors.Arrow;
        }

        // ── AI messaging ─────────────────────────────────────────────

        private async Task SendAiMessageFromInput()
        {
            if (_hasPlaceholder || string.IsNullOrWhiteSpace(_inputBox.Text)) return;
            var intent = _inputBox.Text.Trim();
            _inputBox.Text = "";
            await SendAiMessage(intent);
        }

        private async Task SendAiMessage(string intent)
        {
            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
            {
                try
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(150)
                    };
                    fadeOut.Completed += (s, e) => _emptyState.Visibility = Visibility.Collapsed;
                    _emptyState.BeginAnimation(OpacityProperty, fadeOut);
                }
                catch { _emptyState.Visibility = Visibility.Collapsed; }
            }

            AddUserBubble(intent);

            if (_aiService == null)
            {
                AddAiBubble("AI 서비스를 초기화 중입니다.");
                return;
            }

            var loading = AddAiLoadingBubble();

            try
            {
                var rec = await _aiService.RecommendAsync(
                    intent,
                    _allAssets,
                    _styleConfig?.Palettes ?? new List<StylePalette>(),
                    _styleConfig?.Fonts ?? new List<StyleFont>());

                RemoveLoadingBubble(loading);
                ShowAiResponse(rec);
            }
            catch (Exception ex)
            {
                RemoveLoadingBubble(loading);
                AddAiBubble($"오류가 발생했습니다: {ex.Message}");
            }

            _chatScroll.ScrollToBottom();
        }

        private bool _isFirstMessage = true;

        private void AddUserBubble(string text)
        {
            var bubble = new Border
            {
                Background = ThemeResources.BgUserBubble,
                CornerRadius = new CornerRadius(14, 4, 14, 14),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(40, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 220,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = ThemeResources.FontBase
                }
            };

            if (_isFirstMessage)
            {
                _isFirstMessage = false;
                bubble.Opacity = 0;
                _chatStack.Children.Add(bubble);
                try
                {
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(200),
                        BeginTime = TimeSpan.FromMilliseconds(50)
                    };
                    bubble.BeginAnimation(OpacityProperty, fadeIn);
                }
                catch { bubble.Opacity = 1; }
            }
            else
            {
                _chatStack.Children.Add(bubble);
            }
        }

        private FrameworkElement AddAiLoadingBubble()
        {
            StopSearchAnimations();

            var wrapper = new StackPanel { Margin = new Thickness(12, 4, 40, 4) };

            wrapper.Children.Add(new TextBlock
            {
                Text = "TEAMPPT AI",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.Accent,
                Margin = new Thickness(4, 0, 0, 3)
            });

            var contentStack = new StackPanel();

            var stageTb = new TextBlock
            {
                Text = "질의를 벡터로 변환하는 중···",
                FontSize = 11,
                Foreground = ThemeResources.TextDim,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 6)
            };
            contentStack.Children.Add(stageTb);

            // Stage 1: pulse bar
            var pulseBar = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = ThemeResources.Accent,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
                Margin = new Thickness(0, 0, 0, 4)
            };
            contentStack.Children.Add(pulseBar);

            // ── Slot machine reel ──
            var assets = _allAssets ?? new List<HeaderAsset>();
            var rng = new Random();
            var names = assets.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (names.Count == 0) names.Add("에셋");
            var shuffled = names.OrderBy(_ => rng.Next()).ToList();
            int totalCount = Math.Max(assets.Count, 1);

            const double reelHeight = 88;
            const double itemHeight = 22;
            int visibleItems = (int)(reelHeight / itemHeight);

            var reelClip = new Border
            {
                Height = reelHeight,
                ClipToBounds = true,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 2, 0, 4),
                CornerRadius = new CornerRadius(8),
                Background = ThemeResources.BgSurface
            };

            // Top/bottom fade overlays via OpacityMask
            var fadeMask = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            fadeMask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
            fadeMask.GradientStops.Add(new GradientStop(Colors.Black, 0.25));
            fadeMask.GradientStops.Add(new GradientStop(Colors.Black, 0.75));
            fadeMask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0));
            reelClip.OpacityMask = fadeMask;

            var reelStack = new StackPanel
            {
                RenderTransform = new TranslateTransform(0, 0)
            };

            // Pre-fill reel with items
            for (int i = 0; i < visibleItems + 2; i++)
            {
                reelStack.Children.Add(BuildReelItem(shuffled[i % shuffled.Count], i == visibleItems / 2));
            }

            reelClip.Child = reelStack;
            contentStack.Children.Add(reelClip);

            var counterTb = new TextBlock
            {
                FontSize = 10,
                Foreground = ThemeResources.TextDim,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 2, 0, 0)
            };
            contentStack.Children.Add(counterTb);

            // Stage 3: shimmer cards
            var shimmerStack = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < 3; i++)
            {
                var shimmerCard = new Border
                {
                    Height = 22,
                    CornerRadius = new CornerRadius(6),
                    Background = ThemeResources.BorderBase,
                    Margin = new Thickness(0, 2, 0, 2),
                    Opacity = 0.3,
                    Child = new Border
                    {
                        Width = 60 + rng.Next(60),
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Background = ThemeResources.BgChip,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(8, 0, 0, 0)
                    }
                };
                shimmerStack.Children.Add(shimmerCard);
            }
            contentStack.Children.Add(shimmerStack);

            var border = new Border
            {
                Background = ThemeResources.BgAiResponse,
                CornerRadius = new CornerRadius(4, 13, 13, 13),
                Padding = new Thickness(14, 10, 14, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 180,
                Child = contentStack
            };
            wrapper.Children.Add(border);
            _chatStack.Children.Add(wrapper);

            // ── Animation logic ──
            _searchStage = 1;
            int dotCount = 1;
            bool pulseGrow = true;
            double pulseWidth = 0;
            int scrollIdx = visibleItems + 2;
            int scannedCount = 0;

            // Pulse bar (Stage 1)
            var pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            pulseTimer.Tick += (s, e) =>
            {
                if (_searchStage != 1) { pulseTimer.Stop(); return; }
                pulseWidth += pulseGrow ? 10 : -10;
                if (pulseWidth >= 180) pulseGrow = false;
                if (pulseWidth <= 0) pulseGrow = true;
                pulseBar.Width = Math.Max(0, Math.Min(180, pulseWidth));
            };
            pulseTimer.Start();
            border.Tag = pulseTimer;

            // Dots animation
            _searchStageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _searchStageTimer.Tick += (s, e) =>
            {
                dotCount = dotCount % 3 + 1;
                string dots = new string('·', dotCount);
                if (_searchStage == 1) stageTb.Text = "질의를 벡터로 변환하는 중" + dots;
                else if (_searchStage == 2) stageTb.Text = "에셋 스캐닝 중" + dots;
                else if (_searchStage == 3) stageTb.Text = "최적 에셋 선별 중" + dots;
            };
            _searchStageTimer.Start();

            // Stage 1 → 2 (500ms)
            var stage1to2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            stage1to2.Tick += (s, e) =>
            {
                stage1to2.Stop();
                _searchStage = 2;
                pulseBar.Visibility = Visibility.Collapsed;
                reelClip.Visibility = Visibility.Visible;
                stageTb.Text = "에셋 스캐닝 중···";

                // Slot reel scroll — fast (80ms per item)
                _searchScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                _searchScrollTimer.Tick += (s2, e2) =>
                {
                    if (_searchStage != 2) return;

                    // Add new item at bottom, remove top
                    reelStack.Children.Add(BuildReelItem(shuffled[scrollIdx % shuffled.Count], false));
                    if (reelStack.Children.Count > visibleItems + 2)
                        reelStack.Children.RemoveAt(0);

                    // Highlight center item
                    int centerIdx = reelStack.Children.Count / 2;
                    for (int i = 0; i < reelStack.Children.Count; i++)
                    {
                        var item = reelStack.Children[i] as Border;
                        if (item == null) continue;
                        bool isCenter = (i == centerIdx);
                        item.Background = isCenter
                            ? new SolidColorBrush(Color.FromArgb(20, 79, 92, 245))
                            : Brushes.Transparent;
                        var tb = item.Child as TextBlock;
                        if (tb != null)
                        {
                            tb.Foreground = isCenter ? ThemeResources.TextMain : ThemeResources.TextDim;
                            tb.FontWeight = isCenter ? FontWeights.SemiBold : FontWeights.Normal;
                            tb.FontSize = isCenter ? 12 : 11;
                        }
                    }

                    scrollIdx++;
                    scannedCount = Math.Min(scrollIdx - visibleItems, totalCount);
                    counterTb.Text = $"{scannedCount} / {totalCount}";
                };
                _searchScrollTimer.Start();
            };
            stage1to2.Start();

            // Stage 2 → 3 (2000ms): decelerate and stop
            var stage2to3 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            stage2to3.Tick += (s, e) =>
            {
                stage2to3.Stop();
                _searchStage = 3;
                stageTb.Text = "최적 에셋 선별 중···";
                counterTb.Text = $"{totalCount} / {totalCount}";

                if (_searchScrollTimer != null) { _searchScrollTimer.Stop(); _searchScrollTimer = null; }

                // Deceleration: slow scroll then stop
                int decelTicks = 0;
                var decelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                decelTimer.Tick += (s2, e2) =>
                {
                    decelTicks++;
                    if (decelTicks > 4)
                    {
                        decelTimer.Stop();
                        // Fade out reel, show shimmer
                        reelClip.Visibility = Visibility.Collapsed;
                        shimmerStack.Visibility = Visibility.Visible;

                        bool shimmerUp = true;
                        _searchShimmerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                        _searchShimmerTimer.Tick += (s3, e3) =>
                        {
                            foreach (Border card in shimmerStack.Children)
                            {
                                card.Opacity += shimmerUp ? 0.03 : -0.03;
                                if (card.Opacity >= 0.7) shimmerUp = false;
                                if (card.Opacity <= 0.3) shimmerUp = true;
                            }
                        };
                        _searchShimmerTimer.Start();
                        return;
                    }

                    decelTimer.Interval = TimeSpan.FromMilliseconds(200 + decelTicks * 100);

                    reelStack.Children.Add(BuildReelItem(shuffled[scrollIdx % shuffled.Count], false));
                    if (reelStack.Children.Count > visibleItems + 2)
                        reelStack.Children.RemoveAt(0);

                    int centerIdx = reelStack.Children.Count / 2;
                    for (int i = 0; i < reelStack.Children.Count; i++)
                    {
                        var item = reelStack.Children[i] as Border;
                        if (item == null) continue;
                        bool isCenter = (i == centerIdx);
                        item.Background = isCenter
                            ? new SolidColorBrush(Color.FromArgb(20, 79, 92, 245))
                            : Brushes.Transparent;
                        var tb = item.Child as TextBlock;
                        if (tb != null)
                        {
                            tb.Foreground = isCenter ? ThemeResources.TextMain : ThemeResources.TextDim;
                            tb.FontWeight = isCenter ? FontWeights.SemiBold : FontWeights.Normal;
                            tb.FontSize = isCenter ? 12 : 11;
                        }
                    }
                    scrollIdx++;
                };
                _searchScrollTimer = decelTimer;
                decelTimer.Start();
            };
            stage2to3.Start();

            return wrapper;
        }

        private static Border BuildReelItem(string name, bool isCenter)
        {
            return new Border
            {
                Height = 22,
                Padding = new Thickness(10, 2, 10, 2),
                CornerRadius = new CornerRadius(4),
                Background = isCenter
                    ? new SolidColorBrush(Color.FromArgb(20, 79, 92, 245))
                    : Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = name,
                    FontSize = isCenter ? 12 : 11,
                    FontWeight = isCenter ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isCenter ? ThemeResources.TextMain : ThemeResources.TextDim,
                    FontFamily = ThemeResources.FontBase,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void StopSearchAnimations()
        {
            if (_searchStageTimer != null) { _searchStageTimer.Stop(); _searchStageTimer = null; }
            if (_searchScrollTimer != null) { _searchScrollTimer.Stop(); _searchScrollTimer = null; }
            if (_searchShimmerTimer != null) { _searchShimmerTimer.Stop(); _searchShimmerTimer = null; }
        }

        private void RemoveLoadingBubble(FrameworkElement loading)
        {
            StopSearchAnimations();
            if (loading is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is Border b && b.Tag is DispatcherTimer timer)
                        timer.Stop();
                }
            }
            _chatStack.Children.Remove(loading);
        }

        private void AddAiBubble(string text)
        {
            var wrapper = new StackPanel
            {
                Margin = new Thickness(12, 4, 40, 4),
                Opacity = 0
            };

            wrapper.Children.Add(new TextBlock
            {
                Text = "TEAMPPT AI",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.Accent,
                Margin = new Thickness(4, 0, 0, 3)
            });

            var tb = new TextBlock
            {
                Text = "",
                Foreground = ThemeResources.TextMain,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = ThemeResources.FontBase
            };

            wrapper.Children.Add(new Border
            {
                Background = ThemeResources.BgAiResponse,
                CornerRadius = new CornerRadius(4, 13, 13, 13),
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = tb
            });

            _chatStack.Children.Add(wrapper);
            wrapper.Opacity = 1;

            int charIdx = 0;
            var typeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
            typeTimer.Tick += (s, e) =>
            {
                int charsPerTick = Math.Min(3, text.Length - charIdx);
                charIdx += charsPerTick;
                tb.Text = text.Substring(0, charIdx);
                _chatScroll.ScrollToBottom();
                if (charIdx >= text.Length)
                    typeTimer.Stop();
            };
            typeTimer.Start();
        }

        private void ShowAiResponse(AiRecommendation rec)
        {
            AddAiBubble(rec.Message ?? "추천 에셋을 확인해보세요.");

            if (rec.Assets == null || rec.Assets.Count == 0) return;

            _chatStack.Children.Add(new TextBlock
            {
                Text = "추천 에셋",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Margin = new Thickness(12, 8, 0, 2)
            });

            for (int i = 0; i < rec.Assets.Count; i++)
            {
                var card = BuildAiAssetCard(rec.Assets[i]);
                card.Opacity = 0;
                card.RenderTransform = new TranslateTransform(0, 12);
                _chatStack.Children.Add(card);

                try
                {
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                        BeginTime = TimeSpan.FromMilliseconds(i * 80)
                    };

                    var slideUp = new DoubleAnimation
                    {
                        From = 12,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                        BeginTime = TimeSpan.FromMilliseconds(i * 80)
                    };

                    var sb = new Storyboard();
                    Storyboard.SetTarget(fadeIn, card);
                    Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
                    Storyboard.SetTarget(slideUp, card);
                    Storyboard.SetTargetProperty(slideUp,
                        new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                    sb.Children.Add(fadeIn);
                    sb.Children.Add(slideUp);
                    sb.Begin();
                }
                catch { card.Opacity = 1; card.RenderTransform = null; }
            }
        }

        private Border BuildAiAssetCard(AssetSuggestion suggestion)
        {
            _cardByFile.TryGetValue(suggestion.Asset?.File ?? "", out var realCard);
            var hasRemoteFile = suggestion.Asset?.Extra != null
                && suggestion.Asset.Extra.ContainsKey("remote_file");
            var isRemote = realCard == null && hasRemoteFile;

            var thumbBorder = new Border
            {
                Width = 66,
                Height = 48,
                Background = ThemeResources.BgThumb,
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 10, 0)
            };
            ThemeResources.ApplyRoundedClip(thumbBorder, 8);
            if (realCard?.DrawingThumbnail != null)
            {
                thumbBorder.Child = new Image
                {
                    Source = ConvertToBitmapSource(realCard.DrawingThumbnail),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4)
                };
            }
            else if (realCard?.BitmapThumbnail != null)
            {
                thumbBorder.Child = new Image
                {
                    Source = realCard.BitmapThumbnail,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4)
                };
            }
            else if (hasRemoteFile && _remoteCache != null)
            {
                var remotePptx = suggestion.Asset.Extra["remote_file"].ToString();
                LoadRemoteThumbAsync(thumbBorder, remotePptx);
            }

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = suggestion.Asset?.Name ?? "에셋",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                FontFamily = ThemeResources.FontBase,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            info.Children.Add(new TextBlock
            {
                Text = suggestion.Reason ?? "",
                FontSize = 9.5,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var badge = new Border
            {
                Background = ThemeResources.BgBadge,
                CornerRadius = ThemeResources.RadiusBadge,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Visibility = (realCard != null || isRemote) ? Visibility.Visible : Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = isRemote ? "INSERT" : "DRAG",
                    Foreground = ThemeResources.TextAccent,
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    FontFamily = ThemeResources.FontBase
                }
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(thumbBorder, 0);
            Grid.SetColumn(info, 1);
            Grid.SetColumn(badge, 2);
            row.Children.Add(thumbBorder);
            row.Children.Add(info);
            row.Children.Add(badge);

            var card = new Border
            {
                Child = row,
                Background = ThemeResources.BgCard,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                ClipToBounds = true,
                Padding = new Thickness(8),
                Margin = new Thickness(12, 3, 12, 3),
                Cursor = (realCard != null || isRemote) ? Cursors.Hand : Cursors.Arrow
            };
            ThemeResources.ApplyRoundedClip(card, 11);

            bool useLocalCard = realCard != null && !hasRemoteFile;

            if (useLocalCard)
            {
                bool mouseDown = false;
                Point dragOrigin = default;

                card.MouseEnter += (s, e) =>
                {
                    card.Background = ThemeResources.BgCardHover;
                    card.BorderBrush = ThemeResources.BorderCardHover;
                };
                card.MouseLeave += (s, e) =>
                {
                    card.Background = ThemeResources.BgCard;
                    card.BorderBrush = ThemeResources.BorderCard;
                };
                card.MouseLeftButtonDown += (s, e) =>
                {
                    mouseDown = true;
                    dragOrigin = e.GetPosition(card);
                };
                card.MouseMove += (s, e) =>
                {
                    if (!mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
                    var pos = e.GetPosition(card);
                    if (Math.Abs(pos.X - dragOrigin.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(pos.Y - dragOrigin.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        mouseDown = false;
                        CardDragStart?.Invoke(realCard);
                    }
                };
                card.MouseLeftButtonUp += (s, e) =>
                {
                    if (!mouseDown) return;
                    mouseDown = false;
                    CardClickInsert?.Invoke(realCard);
                };
            }
            else if (hasRemoteFile && _remoteCache != null)
            {
                var remoteFile = suggestion.Asset.Extra["remote_file"].ToString();
                var assetName = suggestion.Asset?.Name ?? "에셋";
                var category = suggestion.Asset?.Category ?? "";
                var useWhen = suggestion.Reason ?? suggestion.Asset?.UseWhen ?? "";

                bool mouseDown = false;
                Point dragOrigin = default;
                Popup remotePopup = null;
                var popupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };

                popupTimer.Tick += (s2, e2) =>
                {
                    popupTimer.Stop();
                    if (remotePopup != null) return;
                    var popupContent = BuildRemotePopupContent(assetName, category, useWhen, thumbBorder);
                    popupContent.Opacity = 0;
                    popupContent.RenderTransform = new TranslateTransform(6, 0);
                    remotePopup = new Popup
                    {
                        Child = popupContent,
                        PlacementTarget = card,
                        Placement = PlacementMode.Left,
                        AllowsTransparency = true,
                        StaysOpen = true,
                        IsHitTestVisible = false,
                        IsOpen = true
                    };
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                    var slideIn = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(150));
                    popupContent.BeginAnimation(OpacityProperty, fadeIn);
                    ((TranslateTransform)popupContent.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);
                };

                closeTimer.Tick += (s2, e2) =>
                {
                    closeTimer.Stop();
                    if (remotePopup != null) { remotePopup.IsOpen = false; remotePopup = null; }
                };

                card.MouseEnter += (s, e) =>
                {
                    card.Background = ThemeResources.BgCardHover;
                    card.BorderBrush = ThemeResources.BorderCardHover;
                    closeTimer.Stop();
                    if (remotePopup == null) popupTimer.Start();
                };
                card.MouseLeave += (s, e) =>
                {
                    card.Background = ThemeResources.BgCard;
                    card.BorderBrush = ThemeResources.BorderCard;
                    popupTimer.Stop();
                    if (remotePopup != null) closeTimer.Start();
                };
                card.MouseLeftButtonDown += (s, e) =>
                {
                    mouseDown = true;
                    dragOrigin = e.GetPosition(card);
                    popupTimer.Stop();
                    if (remotePopup != null) { remotePopup.IsOpen = false; remotePopup = null; }
                };
                var capturedAsset = suggestion.Asset;
                card.MouseMove += (s, e) =>
                {
                    if (!mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
                    var pos = e.GetPosition(card);
                    if (Math.Abs(pos.X - dragOrigin.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(pos.Y - dragOrigin.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        mouseDown = false;
                        DragRemoteAssetAsync(remoteFile, assetName, capturedAsset);
                    }
                };
                card.MouseLeftButtonUp += (s, e) =>
                {
                    if (!mouseDown) return;
                    mouseDown = false;
                    InsertRemoteAssetAsync(remoteFile, assetName, capturedAsset);
                };
            }

            return card;
        }

        private async void LoadRemoteThumbAsync(Border thumbBorder, string remotePptxPath)
        {
            try
            {
                var localPptx = await _remoteCache.GetPptxAsync(remotePptxPath).ConfigureAwait(false);
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var thumbCachePath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "TeampptAddin", "cache", "thumb-shape",
                            System.IO.Path.GetFileNameWithoutExtension(localPptx) + ".png");
                        var thumbImg = ThumbnailService.LoadThumbnail(localPptx, thumbCachePath);
                        if (thumbImg != null)
                        {
                            var bmpSrc = ConvertToBitmapSource(thumbImg);
                            thumbBorder.Child = new Image
                            {
                                Source = bmpSrc,
                                Stretch = Stretch.Uniform,
                                Margin = new Thickness(4)
                            };
                        }
                    }
                    catch (Exception ex) { Logger.Log($"[RemoteThumb] Shape-only 생성 실패: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Logger.Log($"[RemoteThumb] pptx 다운로드 실패: {ex.Message}"); }
        }

        private Border BuildRemotePopupContent(string name, string category, string useWhen, Border thumbSource)
        {
            var outer = new Border
            {
                Width = 320,
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Background = ThemeResources.BgBase,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0x19, 0x1F, 0x28),
                    BlurRadius = 24, ShadowDepth = 4, Opacity = 0.10, Direction = 270
                }
            };

            var stack = new StackPanel();

            var thumbArea = new Border
            {
                Height = 180, ClipToBounds = true,
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Background = ThemeResources.BgThumb
            };
            ThemeResources.ApplyRoundedClip(thumbArea, new CornerRadius(16, 16, 0, 0));
            var srcImage = thumbSource.Child as Image;
            if (srcImage?.Source != null)
            {
                thumbArea.Child = new Image { Source = srcImage.Source, Stretch = Stretch.Uniform };
            }
            stack.Children.Add(thumbArea);

            stack.Children.Add(new Border { Height = 1, Background = ThemeResources.BorderBase });

            var metaGrid = new Grid();
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameText = new TextBlock
            {
                Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            metaGrid.Children.Add(nameText);
            if (!string.IsNullOrEmpty(category))
            {
                var catBadge = new Border
                {
                    Background = ThemeResources.BgBadge, CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = category, FontSize = 10, FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase
                    }
                };
                Grid.SetColumn(catBadge, 1);
                metaGrid.Children.Add(catBadge);
            }
            stack.Children.Add(new Border { Padding = new Thickness(12, 10, 12, 8), Child = metaGrid });

            if (!string.IsNullOrEmpty(useWhen))
            {
                stack.Children.Add(new Border
                {
                    Padding = new Thickness(12, 0, 12, 10),
                    Child = new TextBlock
                    {
                        Text = useWhen, FontSize = 11, Foreground = ThemeResources.TextSub,
                        FontFamily = ThemeResources.FontBase, TextWrapping = TextWrapping.Wrap, LineHeight = 16
                    }
                });
            }

            var hintPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hintPanel.Children.Add(new TextBlock { Text = "클릭 삽입", FontSize = 10, Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase });
            hintPanel.Children.Add(new TextBlock { Text = "  ·  ", FontSize = 10, Foreground = ThemeResources.TextDim, FontFamily = ThemeResources.FontBase });
            hintPanel.Children.Add(new TextBlock { Text = "드래그로 이동", FontSize = 10, Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase });
            stack.Children.Add(new Border
            {
                Background = ThemeResources.BgSurface,
                CornerRadius = new CornerRadius(0, 0, 16, 16),
                Padding = new Thickness(12, 8, 12, 8),
                BorderBrush = ThemeResources.BorderBase,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = hintPanel
            });

            outer.Child = stack;
            ThemeResources.ApplyRoundedClip(outer, 16);
            return outer;
        }

        private void EnsureAssetRegistered(HeaderAsset asset)
        {
            if (asset == null) return;
            if (_allAssets == null) _allAssets = new List<HeaderAsset>();
            var key = System.IO.Path.GetFileName(asset.File ?? "");
            var remoteKey = asset.Extra != null && asset.Extra.TryGetValue("remote_file", out var rf)
                ? System.IO.Path.GetFileName(rf.ToString()) : null;
            bool exists = _allAssets.Any(a =>
                (!string.IsNullOrEmpty(key) && string.Equals(System.IO.Path.GetFileName(a.File ?? ""), key, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(remoteKey) && a.Extra != null && a.Extra.TryGetValue("remote_file", out var arf)
                    && string.Equals(System.IO.Path.GetFileName(arf.ToString()), remoteKey, StringComparison.OrdinalIgnoreCase)));
            if (!exists)
            {
                _allAssets.Add(asset);
                Logger.Log($"[Style] EnsureAssetRegistered added '{asset.Name}', allAssets={_allAssets.Count}");
            }
        }

        private async void DragRemoteAssetAsync(string remotePptx, string assetName, HeaderAsset sourceAsset = null)
        {
            try
            {
                SetStatus($"⬇ {assetName} 다운로드 중...", ThemeResources.TextSub.Color);
                var localPptx = await _remoteCache.GetPptxAsync(remotePptx).ConfigureAwait(false);
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        EnsureAssetRegistered(sourceAsset);
                        var thumbCachePath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "TeampptAddin", "cache", "thumb-shape",
                            System.IO.Path.GetFileNameWithoutExtension(localPptx) + ".png");
                        var thumbImg = ThumbnailService.LoadThumbnail(localPptx, thumbCachePath);
                        var tempCard = new AssetCard(thumbImg, assetName, localPptx);
                        CardDragStart?.Invoke(tempCard);
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"드래그 실패: {ex.Message}", ThemeResources.StatusError.Color);
                        Logger.Log($"[RemoteDrag] 실패: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    SetStatus($"다운로드 실패: {ex.Message}", ThemeResources.StatusError.Color));
                Logger.Log($"[RemoteDrag] 다운로드 실패: {ex}");
            }
        }

        private async void InsertRemoteAssetAsync(string remotePath, string assetName, HeaderAsset sourceAsset = null)
        {
            try
            {
                SetStatus($"⬇ {assetName} 다운로드 중...", ThemeResources.TextSub.Color);
                var localPath = await _remoteCache.GetPptxAsync(remotePath).ConfigureAwait(false);
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        EnsureAssetRegistered(sourceAsset);
                        ShapeInserter.InsertToActiveSlide(localPath);
                        SetStyleAnchorByFile(remotePath);
                        SetStatus($"✓ {assetName} 삽입 완료", ThemeResources.StatusSuccess.Color);
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"삽입 실패: {ex.Message}", ThemeResources.StatusError.Color);
                        Logger.Log($"[RemoteInsert] 삽입 실패: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    SetStatus($"다운로드 실패: {ex.Message}", ThemeResources.StatusError.Color));
                Logger.Log($"[RemoteInsert] 다운로드 실패: {ex}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ASSET TAB
        // ══════════════════════════════════════════════════════════════

        private DockPanel BuildAssetTab()
        {
            var dock = new DockPanel { LastChildFill = true };

            var filterBar = BuildCategoryBar();
            DockPanel.SetDock(filterBar, Dock.Top);
            dock.Children.Add(filterBar);

            _assetStack = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _assetStack,
                Background = ThemeResources.BgBase
            };
            dock.Children.Add(scroll);

            return dock;
        }

        private Border BuildCategoryBar()
        {
            var chips = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 6, 8, 6)
            };
            _categoryBtns = new Border[_categories.Length];

            for (int i = 0; i < _categories.Length; i++)
            {
                var cat = _categories[i];
                var lbl = new TextBlock
                {
                    Text = cat,
                    FontSize = 12,
                    FontFamily = ThemeResources.FontBase,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var chip = new Border
                {
                    Child = lbl,
                    CornerRadius = ThemeResources.RadiusChip,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 7, 12, 7),
                    Margin = new Thickness(2, 0, 2, 0),
                    Cursor = Cursors.Hand
                };
                chip.Tag = lbl;
                chip.MouseLeftButtonUp += (s, e) => FilterCategory(cat);
                _categoryBtns[i] = chip;
                chips.Children.Add(chip);
            }

            var scroll = new ScrollViewer
            {
                Content = chips,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            ApplyCategoryStyles();

            return new Border
            {
                Child = scroll,
                Background = ThemeResources.BgSurface,
                BorderBrush = ThemeResources.BorderBase,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private void FilterCategory(string category)
        {
            _activeCategory = category;
            ApplyCategoryStyles();

            foreach (var card in _assetStack.Children.OfType<AssetCard>())
            {
                var asset = card.Tag as HeaderAsset;
                card.Visibility = (category == "전체" || asset?.Category == category)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void ApplyCategoryStyles()
        {
            for (int i = 0; i < _categories.Length; i++)
            {
                bool active = _categories[i] == _activeCategory;
                var chip = _categoryBtns[i];
                var lbl = (TextBlock)chip.Tag;
                chip.Background = active ? ThemeResources.Accent : Brushes.Transparent;
                chip.BorderThickness = new Thickness(0);
                lbl.Foreground = active ? Brushes.White : ThemeResources.TextSub;
                lbl.FontWeight = active ? FontWeights.SemiBold : FontWeights.Regular;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  STYLE TAB
        // ══════════════════════════════════════════════════════════════

        private FrameworkElement BuildStyleTab()
        {
            _styleStack = new StackPanel { Margin = new Thickness(0, 4, 0, 12) };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _styleStack,
                Background = ThemeResources.BgBase
            };

            return new Border { Background = ThemeResources.BgBase, Child = scroll };
        }

        private void PopulateStylePanel()
        {
            if (_styleStack == null) return;
            var effective = BuildEffectiveStyleConfig();
            _styleStack.Children.Clear();

            var palettes = effective.Palettes ?? new List<StylePalette>();
            var fonts    = effective.Fonts    ?? new List<StyleFont>();

            if (palettes.Count == 0 && fonts.Count == 0) return;

            _selectedPalette = palettes.Count > 0 ? palettes[0] : null;
            _selectedFont    = fonts.Count    > 0 ? fonts[0]    : null;

            _styleStack.Children.Add(BuildSectionLabel("컬러 팔레트"));

            var paletteWrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(12, 0, 12, 0)
            };
            _paletteBtns = new Border[palettes.Count];
            for (int i = 0; i < palettes.Count; i++)
            {
                var idx = i;
                var swatch = BuildPaletteSwatch(palettes[i]);
                swatch.MouseLeftButtonUp += (s, e) =>
                {
                    Logger.Log($"[Style] Palette clicked: idx={idx}, palette={palettes[idx]?.Name ?? "NULL"}, fontNull={_selectedFont == null}");
                    _selectedPalette = palettes[idx];
                    RefreshPaletteSelection(idx);
                    StyleApplyRequested?.Invoke(_selectedPalette, _selectedFont);
                };
                _paletteBtns[i] = swatch;
                paletteWrap.Children.Add(swatch);
            }
            _styleStack.Children.Add(paletteWrap);
            if (palettes.Count > 0) RefreshPaletteSelection(0);

            _styleStack.Children.Add(BuildSectionLabel("폰트"));

            var fontPanel = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            _fontBtns = new Border[fonts.Count];
            for (int i = 0; i < fonts.Count; i++)
            {
                var idx = i;
                var row = BuildFontRow(fonts[i]);
                row.MouseLeftButtonUp += (s, e) =>
                {
                    Logger.Log($"[Style] Font row clicked: idx={idx}, font={fonts[idx]?.Name ?? "NULL"}, paletteNull={_selectedPalette == null}");
                    _selectedFont = fonts[idx];
                    RefreshFontSelection(idx);
                    StyleApplyRequested?.Invoke(_selectedPalette, _selectedFont);
                };
                _fontBtns[i] = row;
                fontPanel.Children.Add(row);
            }
            _styleStack.Children.Add(fontPanel);
            if (fonts.Count > 0) RefreshFontSelection(0);
        }

        private static TextBlock BuildSectionLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(14, 16, 0, 8)
            };
        }

        private static Border BuildPaletteSwatch(StylePalette p)
        {
            var colorGrid = new UniformGrid { Rows = 2, Columns = 2, Height = 56 };
            var hexColors = new[] { p.Colors?.Main, p.Colors?.Sub1, p.Colors?.Sub2, p.Colors?.Text };
            foreach (var hex in hexColors)
            {
                colorGrid.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Fill = BrushFromHex(hex ?? "#CCCCCC")
                });
            }

            var colorStrip = new Border
            {
                ClipToBounds = true,
                Child = colorGrid
            };

            var nameText = new TextBlock
            {
                Text = p.Name,
                FontSize = 9,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Padding = new Thickness(4, 3, 4, 5)
            };

            var content = new StackPanel();
            content.Children.Add(colorStrip);
            content.Children.Add(nameText);

            var check = new Border
            {
                Background = ThemeResources.Accent,
                CornerRadius = new CornerRadius(99),
                Width = 16, Height = 16,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var overlay = new Grid();
            overlay.Children.Add(content);
            overlay.Children.Add(check);

            var card = new Border
            {
                Width = 80,
                CornerRadius = new CornerRadius(12),
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Child = overlay
            };
            card.Tag = check;
            ThemeResources.ApplyRoundedClip(card, 12);

            card.MouseEnter += (s, e) =>
            {
                if (card.BorderBrush != ThemeResources.Accent)
                    card.BorderBrush = ThemeResources.BorderCardHover;
            };
            card.MouseLeave += (s, e) =>
            {
                if (card.BorderBrush != ThemeResources.Accent)
                    card.BorderBrush = ThemeResources.BorderCard;
            };

            return card;
        }

        private static Border BuildFontRow(StyleFont f)
        {
            var nameText = new TextBlock
            {
                Text = f.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            };

            var sampleText = new TextBlock
            {
                Text = "가나다라",
                FontSize = 12,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var leftPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftPanel.Children.Add(nameText);
            leftPanel.Children.Add(sampleText);

            var check = new Border
            {
                Background = ThemeResources.Accent,
                CornerRadius = new CornerRadius(99),
                Width = 18, Height = 18,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(check, 1);
            grid.Children.Add(leftPanel);
            grid.Children.Add(check);

            var row = new Border
            {
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(14, 11, 14, 11),
                Margin = new Thickness(0, 0, 0, 5),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = grid
            };
            row.Tag = check;
            ThemeResources.ApplyRoundedClip(row, 12);

            row.MouseEnter += (s, e) =>
            {
                if (row.Background != ThemeResources.BgCategoryActive)
                    row.Background = ThemeResources.BgChip;
            };
            row.MouseLeave += (s, e) =>
            {
                if (row.Background != ThemeResources.BgCategoryActive)
                    row.Background = Brushes.Transparent;
            };

            return row;
        }

        private void RefreshPaletteSelection(int selectedIdx)
        {
            for (int i = 0; i < _paletteBtns.Length; i++)
            {
                var btn   = _paletteBtns[i];
                var check = (Border)btn.Tag;
                bool active = i == selectedIdx;
                btn.BorderBrush = active ? ThemeResources.Accent : ThemeResources.BorderCard;
                if (check != null)
                    check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshFontSelection(int selectedIdx)
        {
            for (int i = 0; i < _fontBtns.Length; i++)
            {
                var row   = _fontBtns[i];
                var check = (Border)row.Tag;
                bool active = i == selectedIdx;
                row.Background  = active ? ThemeResources.BgCategoryActive : Brushes.Transparent;
                row.BorderBrush = active ? ThemeResources.Accent : ThemeResources.BorderCard;
                if (check != null)
                    check.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // ══════════════════════════════════════════════════════════════
        //  PUBLIC API (TaskPaneHost 호출용)
        // ══════════════════════════════════════════════════════════════

        public void SetAssets(List<HeaderAsset> assets)
        {
            _allAssets = assets ?? new List<HeaderAsset>();
        }

        public void SetStyleAnchorByFile(string pathOrFile)
        {
            if (string.IsNullOrEmpty(pathOrFile)) return;
            var key = System.IO.Path.GetFileName(pathOrFile);
            Logger.Log($"[Style] SetStyleAnchorByFile key={key}, allAssets={_allAssets?.Count ?? -1}");
            _anchorAsset = (_allAssets ?? new List<HeaderAsset>())
                .FirstOrDefault(a =>
                    string.Equals(System.IO.Path.GetFileName(a.File ?? ""), key,
                        StringComparison.OrdinalIgnoreCase)
                    || (a.Extra != null && a.Extra.TryGetValue("remote_file", out var rf)
                        && string.Equals(System.IO.Path.GetFileName(rf.ToString()), key,
                            StringComparison.OrdinalIgnoreCase)));
            Logger.Log($"[Style] anchor={_anchorAsset?.Name ?? "NULL"}, colors={_anchorAsset?.Colors?.Count ?? -1}, styleStack={(_styleStack != null ? "OK" : "NULL")}");
            PopulateStylePanel();
        }

        private StyleConfig BuildEffectiveStyleConfig()
        {
            var palettes = new List<StylePalette>();
            var fonts = new List<StyleFont>();

            if (_anchorAsset != null)
            {
                var np = PaletteRoleMapper.Map(_anchorAsset.Colors);
                palettes.AddRange(PaletteGenerator.Generate(np));

                if (_anchorAsset.Fonts != null)
                {
                    foreach (var f in _anchorAsset.Fonts)
                    {
                        if (f == null || string.IsNullOrWhiteSpace(f.Family)) continue;
                        if (fonts.Any(x => string.Equals(x.Name, f.Family,
                                StringComparison.OrdinalIgnoreCase))) continue;
                        fonts.Add(new StyleFont { Name = f.Family, Fallback = f.Fallback,
                            Mood = new List<string>(), UseWhen = "에셋 폰트" });
                    }
                }
            }

            if (_styleConfig?.Palettes != null) palettes.AddRange(_styleConfig.Palettes);
            if (_styleConfig?.Fonts != null)
            {
                foreach (var f in _styleConfig.Fonts)
                {
                    if (f == null || string.IsNullOrWhiteSpace(f.Name)) continue;
                    if (fonts.Any(x => string.Equals(x.Name, f.Name,
                            StringComparison.OrdinalIgnoreCase))) continue;
                    fonts.Add(f);
                }
            }

            return new StyleConfig { Palettes = palettes, Fonts = fonts };
        }

        public void InitAi(IAiService aiService, StyleConfig styles, RemoteAssetCache remoteCache = null, RedesignService redesign = null, RecommendationService recommend = null)
        {
            _aiService = aiService;
            _styleConfig = styles;
            _remoteCache = remoteCache;
            _redesign = redesign;
            _recommend = recommend;
            PopulateStylePanel();
        }

        public void ShowIngestButton()
        {
            if (_ingestButton != null)
                _ingestButton.Visibility = Visibility.Visible;
        }

        private static readonly SolidColorBrush IngestAccent = Freeze(new SolidColorBrush(Color.FromRgb(249, 115, 22)));
        private static readonly SolidColorBrush IngestAccentDim = Freeze(new SolidColorBrush(Color.FromArgb(30, 249, 115, 22)));
        private static readonly SolidColorBrush IngestTrack = Freeze(new SolidColorBrush(Color.FromRgb(63, 63, 70)));
        private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        private async Task RunIngestAsync()
        {
            var dlg = new System.Windows.Forms.OpenFileDialog
            {
                Title = "인제스트할 에셋 번들 선택",
                Filter = "PowerPoint 파일 (*.pptx)|*.pptx",
                Multiselect = false
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var bundlePath = dlg.FileName;
            var bundleName = System.IO.Path.GetFileName(bundlePath);

            SwitchTab(0);
            if (_emptyState != null) _emptyState.Visibility = Visibility.Collapsed;

            _ingestButton.IsEnabled = false;
            ((TextBlock)_ingestButton.Child).Text = "실행 중...";
            _ingestLastIndex = 0;

            BuildIngestPanel(bundleName);

            try
            {
                var outputDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeampptAddin", "ingest-output");

                if (Directory.Exists(outputDir))
                {
                    foreach (var f in Directory.GetFiles(outputDir))
                        System.IO.File.Delete(f);
                }

                SetIngestStage("슬라이드 분해 중");
                var items = IngestRunner.Run(bundlePath, outputDir);
                _ingestProgressText.Text = $"0/{items.Count} · 분해 완료";

                _retryOutputDir = outputDir;
                _retryBundleName = bundleName;
                _retryStartFrom = 0;

                var count = await RunUploadAsync(outputDir, bundleName, 0);

                StopIngestAnimations();
                ShowIngestComplete(count, items.Count);
                SetStatus($"인제스트 완료: {count}개", ThemeResources.StatusSuccess.Color);
                Logger.Log($"[IngestButton] 완료: split={items.Count}, uploaded={count}");
            }
            catch (Exception ex)
            {
                StopIngestAnimations();
                _retryStartFrom = Math.Max(0, _ingestLastIndex - 1);
                ShowIngestError(ex.Message);
                SetStatus("인제스트 실패", ThemeResources.StatusError.Color);
                Logger.Log($"[IngestButton] 실패: {ex}");
            }
            finally
            {
                _ingestButton.IsEnabled = true;
                ((TextBlock)_ingestButton.Child).Text = "인제스트";
            }
        }

        private async Task<int> RunUploadAsync(string outputDir, string bundleName, int startFrom)
        {
            var uploader = new AssetIngestUploader();
            var dispatcher = Dispatcher;
            return await uploader.UploadDirectoryAsync(outputDir, bundleName, p =>
            {
                dispatcher.Invoke(() => HandleIngestProgress(p));
            }, startFrom);
        }

        private async Task ResumeIngestAsync()
        {
            _ingestButton.IsEnabled = false;
            ((TextBlock)_ingestButton.Child).Text = "실행 중...";

            _ingestCurrentContainer.Children.Clear();
            _ingestLastIndex = -1;
            SetIngestStage("재시도 중");
            _ingestProgressText.Foreground = ThemeResources.TextSub;

            try
            {
                var count = await RunUploadAsync(_retryOutputDir, _retryBundleName, _retryStartFrom);

                StopIngestAnimations();
                var totalFiles = Directory.GetFiles(_retryOutputDir, "*.png").Length;
                // count는 UploadDirectoryAsync가 반환하는 "유효 파일 총합"(startFrom 무관).
                // _retryStartFrom을 더하면 이중계산되어 62/35처럼 초과 표시됨.
                ShowIngestComplete(count, totalFiles);
                SetStatus($"인제스트 완료: {count}개", ThemeResources.StatusSuccess.Color);
            }
            catch (Exception ex)
            {
                StopIngestAnimations();
                _retryStartFrom = Math.Max(0, _ingestLastIndex - 1);
                ShowIngestError(ex.Message);
                SetStatus("인제스트 실패", ThemeResources.StatusError.Color);
                Logger.Log($"[IngestRetry] 실패: {ex}");
            }
            finally
            {
                _ingestButton.IsEnabled = true;
                ((TextBlock)_ingestButton.Child).Text = "인제스트";
            }
        }

        // ── Ingest Panel ─────────────────────────────────────────────

        private void BuildIngestPanel(string bundleName)
        {
            var panel = new StackPanel { Margin = new Thickness(8, 8, 8, 4) };

            var card = new Border
            {
                Background = ThemeResources.BgAiResponse,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 14, 16, 14)
            };

            var inner = new StackPanel();

            inner.Children.Add(new TextBlock
            {
                Text = "INGEST",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = IngestAccent,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 2)
            });

            inner.Children.Add(new TextBlock
            {
                Text = bundleName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var progressGrid = new Grid { Height = 4, Margin = new Thickness(0, 0, 0, 6) };
            _ingestProgressTrack = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = IngestTrack
            };
            _ingestProgressFill = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = IngestAccent,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            progressGrid.Children.Add(_ingestProgressTrack);
            progressGrid.Children.Add(_ingestProgressFill);
            inner.Children.Add(progressGrid);

            _ingestProgressText = new TextBlock
            {
                Text = "준비 중...",
                FontSize = 10,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 12)
            };
            inner.Children.Add(_ingestProgressText);

            _ingestCurrentContainer = new Grid { MinHeight = 10 };
            inner.Children.Add(_ingestCurrentContainer);

            card.Child = inner;
            panel.Children.Add(card);

            _chatStack.Children.Add(panel);

            _completedCount = 0;
            _completedExpanded = false;
            _ingestCompletedStack = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };

            var latestThumbBorder = new Border
            {
                Width = 36,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Background = ThemeResources.BgChip,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _completedLatestThumb = new Image { Stretch = Stretch.UniformToFill };
            latestThumbBorder.Child = _completedLatestThumb;
            ThemeResources.ApplyRoundedClip(latestThumbBorder, 6);

            _completedLatestName = new TextBlock
            {
                Text = "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _completedCountText = new TextBlock
            {
                Text = "",
                FontSize = 10,
                Foreground = ThemeResources.TextDim,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            _completedChevron = new TextBlock
            {
                Text = "▾",
                FontSize = 12,
                Foreground = ThemeResources.TextDim,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            };

            var previewRow = new Grid();
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(latestThumbBorder, 0);
            Grid.SetColumn(_completedLatestName, 1);
            Grid.SetColumn(_completedCountText, 2);
            Grid.SetColumn(_completedChevron, 3);
            previewRow.Children.Add(latestThumbBorder);
            previewRow.Children.Add(_completedLatestName);
            previewRow.Children.Add(_completedCountText);
            previewRow.Children.Add(_completedChevron);

            _completedCollapsedBox = new Border
            {
                Background = ThemeResources.BgAiResponse,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 14, 10),
                Margin = new Thickness(8, 6, 8, 4),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };

            var collapsedContent = new StackPanel();
            collapsedContent.Children.Add(previewRow);
            collapsedContent.Children.Add(_ingestCompletedStack);
            _completedCollapsedBox.Child = collapsedContent;

            _completedCollapsedBox.MouseLeftButtonUp += (s, e) =>
            {
                _completedExpanded = !_completedExpanded;
                _completedChevron.Text = _completedExpanded ? "▴" : "▾";
                _ingestCompletedStack.Visibility = _completedExpanded ? Visibility.Visible : Visibility.Collapsed;
            };

            _chatStack.Children.Add(_completedCollapsedBox);
            _chatScroll.ScrollToBottom();
        }

        private void HandleIngestProgress(IngestProgress p)
        {
            if (p.Stage == IngestStage.Understanding && p.Index != _ingestLastIndex)
            {
                MoveCurrentToCompleted();
                ShowCurrentCard(p);
                _ingestLastIndex = p.Index;
            }

            UpdateProgressBar(p.Index, p.Total, p.Stage);

            switch (p.Stage)
            {
                case IngestStage.Understanding:
                    SetIngestStage("이해 중");
                    break;
                case IngestStage.Embedding:
                    if (!string.IsNullOrEmpty(p.Name) && _ingestNameText != null)
                        _ingestNameText.Text = p.Name;
                    SetIngestStage("임베딩 중");
                    break;
                case IngestStage.Uploading:
                    SetIngestStage("업로드 중");
                    break;
                case IngestStage.AssetDone:
                    StopIngestAnimations();
                    ShowAssetDone(p.Kind);
                    break;
            }

            _chatScroll.ScrollToBottom();
        }

        private BitmapImage _currentThumbBmp;

        private void ShowCurrentCard(IngestProgress p)
        {
            _ingestCurrentContainer.Children.Clear();
            _currentThumbBmp = null;

            var cardStack = new StackPanel();

            var thumbClip = new Border
            {
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Background = ThemeResources.BgChip,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var thumbGrid = new Grid();
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(p.PngPath);
                bmp.DecodePixelWidth = 400;
                bmp.EndInit();
                bmp.Freeze();
                _currentThumbBmp = bmp;
                thumbGrid.Children.Add(new Image
                {
                    Source = bmp,
                    Stretch = Stretch.Uniform
                });
            }
            catch { }

            _ingestScanLine = new Border
            {
                Height = 32,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Background = new LinearGradientBrush(
                    Color.FromArgb(0, 249, 115, 22),
                    Color.FromArgb(60, 249, 115, 22),
                    new Point(0, 0), new Point(0, 1))
            };
            thumbGrid.Children.Add(_ingestScanLine);

            try
            {
                var scanTransform = new TranslateTransform(0, -32);
                _ingestScanLine.RenderTransform = scanTransform;
                var scanAnim = new DoubleAnimation
                {
                    From = -32,
                    To = 160,
                    Duration = TimeSpan.FromSeconds(1.4),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase()
                };
                scanTransform.BeginAnimation(TranslateTransform.YProperty, scanAnim);
            }
            catch { }

            thumbClip.Child = thumbGrid;
            ThemeResources.ApplyRoundedClip(thumbClip, 10);
            cardStack.Children.Add(thumbClip);

            _ingestNameText = new TextBlock
            {
                Text = p.AssetId.Replace("_", " "),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain,
                FontFamily = ThemeResources.FontBase,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 0, 0, 2)
            };
            cardStack.Children.Add(_ingestNameText);

            _ingestStageText = new TextBlock
            {
                FontSize = 11,
                Foreground = IngestAccent,
                FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(2, 0, 0, 0)
            };
            cardStack.Children.Add(_ingestStageText);

            var wrapper = new Border
            {
                Child = cardStack,
                Background = IngestAccentDim,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 12, 12, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 10)
            };

            _ingestCurrentContainer.Children.Add(wrapper);

            try
            {
                var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new QuadraticEase() };
                var slideUp = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new QuadraticEase() };
                wrapper.BeginAnimation(OpacityProperty, fadeIn);
                ((TranslateTransform)wrapper.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
            catch { wrapper.Opacity = 1; }
        }

        private void SetIngestStage(string stage)
        {
            _ingestStageBase = stage;
            if (_ingestStageText != null)
                _ingestStageText.Text = stage + "···";

            if (_ingestDotsTimer != null) _ingestDotsTimer.Stop();
            int dotCount = 1;
            _ingestDotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _ingestDotsTimer.Tick += (s, e) =>
            {
                dotCount = dotCount % 3 + 1;
                if (_ingestStageText != null)
                    _ingestStageText.Text = _ingestStageBase + new string('·', dotCount);
            };
            _ingestDotsTimer.Start();
        }

        private void ShowAssetDone(string kind)
        {
            if (_ingestStageText == null) return;
            _ingestStageText.Text = $"완료 · {kind}";
            _ingestStageText.Foreground = ThemeResources.StatusSuccess;
        }

        private void MoveCurrentToCompleted()
        {
            if (_ingestCurrentContainer == null || _ingestCurrentContainer.Children.Count == 0) return;

            var current = _ingestCurrentContainer.Children[0] as Border;
            if (current == null) return;

            var cardStack = current.Child as StackPanel;
            if (cardStack == null) return;

            string name = "";
            string kind = "";
            BitmapImage thumbBmp = _currentThumbBmp;
            foreach (var child in cardStack.Children)
            {
                var tb = child as TextBlock;
                if (tb == null) continue;
                if (tb.FontWeight == FontWeights.SemiBold) name = tb.Text;
                else if (tb.Foreground == IngestAccent || tb.Foreground == ThemeResources.StatusSuccess) kind = tb.Text;
            }

            _ingestCurrentContainer.Children.Clear();
            _currentThumbBmp = null;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var miniThumb = new Border
            {
                Width = 34,
                Height = 24,
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Background = ThemeResources.BgChip,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (thumbBmp != null)
            {
                miniThumb.Child = new Image
                {
                    Source = thumbBmp,
                    Stretch = Stretch.UniformToFill
                };
            }
            ThemeResources.ApplyRoundedClip(miniThumb, 5);
            Grid.SetColumn(miniThumb, 0);
            row.Children.Add(miniThumb);

            var textStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            textStack.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.StatusSuccess,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            textStack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 11,
                Foreground = ThemeResources.TextSub,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (!string.IsNullOrEmpty(kind))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = $" · {kind.Replace("완료 · ", "")}",
                    FontSize = 10,
                    Foreground = ThemeResources.TextDim,
                    FontFamily = ThemeResources.FontBase,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Grid.SetColumn(textStack, 1);
            row.Children.Add(textStack);

            row.Opacity = 0;
            _ingestCompletedStack.Children.Insert(0, row);

            try
            {
                var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(200) };
                row.BeginAnimation(OpacityProperty, fadeIn);
            }
            catch { row.Opacity = 1; }

            _completedCount++;
            _completedCountText.Text = $"· {_completedCount}개";
            _completedLatestName.Text = "✓ " + name;
            if (thumbBmp != null)
                _completedLatestThumb.Source = thumbBmp;
            if (_completedCollapsedBox.Visibility == Visibility.Collapsed)
                _completedCollapsedBox.Visibility = Visibility.Visible;
        }

        private void UpdateProgressBar(int index, int total, IngestStage stage)
        {
            var trackWidth = _ingestProgressTrack.ActualWidth;
            if (trackWidth <= 0) trackWidth = 200;

            double stageOffset = 0;
            switch (stage)
            {
                case IngestStage.Understanding: stageOffset = 0; break;
                case IngestStage.Embedding: stageOffset = 0.33; break;
                case IngestStage.Uploading: stageOffset = 0.66; break;
                case IngestStage.AssetDone: stageOffset = 1.0; break;
            }

            var pct = ((index - 1) + stageOffset) / total;
            var targetWidth = pct * trackWidth;

            try
            {
                var anim = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                _ingestProgressFill.BeginAnimation(WidthProperty, anim);
            }
            catch { _ingestProgressFill.Width = targetWidth; }

            var pctInt = (int)(pct * 100);
            _ingestProgressText.Text = $"{index}/{total} · {pctInt}%";
        }

        private void ShowIngestComplete(int uploaded, int total)
        {
            MoveCurrentToCompleted();

            _ingestProgressFill.BeginAnimation(WidthProperty, null);
            _ingestProgressFill.Width = _ingestProgressTrack.ActualWidth > 0 ? _ingestProgressTrack.ActualWidth : 200;
            _ingestProgressText.Text = $"{uploaded}/{total} · 완료";
            _ingestProgressText.Foreground = ThemeResources.StatusSuccess;

            _ingestCurrentContainer.Children.Clear();
            var done = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 16, 185, 129)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 4, 0, 0)
            };
            var doneRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            doneRow.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.StatusSuccess,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            doneRow.Children.Add(new TextBlock
            {
                Text = $"{uploaded}개 에셋 Supabase 업로드 완료",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.StatusSuccess,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            });
            done.Child = doneRow;

            done.Opacity = 0;
            done.RenderTransform = new ScaleTransform(0.9, 0.9, 0.5, 0.5);
            done.RenderTransformOrigin = new Point(0.5, 0.5);
            _ingestCurrentContainer.Children.Add(done);

            try
            {
                var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuadraticEase() };
                var scaleX = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 8 } };
                var scaleY = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 8 } };
                done.BeginAnimation(OpacityProperty, fadeIn);
                ((ScaleTransform)done.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                ((ScaleTransform)done.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            }
            catch { done.Opacity = 1; }
        }

        private void ShowIngestError(string message)
        {
            MoveCurrentToCompleted();
            _ingestProgressText.Text = $"오류 · {_retryStartFrom}개 완료";
            _ingestProgressText.Foreground = ThemeResources.StatusError;

            _ingestCurrentContainer.Children.Clear();

            var errPanel = new StackPanel();
            var errCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 248, 113, 113)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10)
            };

            bool isServerBusy = message.Contains("503") || message.Contains("429")
                || message.Contains("high demand") || message.Contains("UNAVAILABLE");

            var errStack = new StackPanel();
            if (isServerBusy)
            {
                errStack.Children.Add(new TextBlock
                {
                    Text = "서버가 바빠요",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeResources.StatusError,
                    FontFamily = ThemeResources.FontBase,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            errStack.Children.Add(new TextBlock
            {
                Text = isServerBusy ? "일시적 과부하입니다. 잠시 후 다시 시도해주세요." : message,
                FontSize = 11,
                Foreground = ThemeResources.StatusError,
                FontFamily = ThemeResources.FontBase,
                TextWrapping = TextWrapping.Wrap
            });
            errCard.Child = errStack;
            errPanel.Children.Add(errCard);

            var retryBtn = new Border
            {
                Background = IngestAccent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 9, 16, 9),
                Margin = new Thickness(0, 10, 0, 0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var retryRow = new StackPanel { Orientation = Orientation.Horizontal };
            retryRow.Children.Add(new TextBlock
            {
                Text = "↻",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            retryRow.Children.Add(new TextBlock
            {
                Text = $"다시 시도 ({_retryStartFrom}번부터)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            });
            retryBtn.Child = retryRow;

            retryBtn.MouseEnter += (s, e) => retryBtn.Opacity = 0.85;
            retryBtn.MouseLeave += (s, e) => retryBtn.Opacity = 1;
            retryBtn.MouseLeftButtonUp += async (s, e) => await ResumeIngestAsync();

            errPanel.Children.Add(retryBtn);
            _ingestCurrentContainer.Children.Add(errPanel);
        }

        private void StopIngestAnimations()
        {
            if (_ingestDotsTimer != null) { _ingestDotsTimer.Stop(); _ingestDotsTimer = null; }
            try { _ingestScanLine?.RenderTransform?.BeginAnimation(TranslateTransform.YProperty, null); } catch { }
        }

        public void AddAssetCard(AssetCard card, HeaderAsset asset)
        {
            card.Tag = asset;
            card.ClickInsertRequested += c => CardClickInsert?.Invoke(c);
            card.DragStartRequested += c => CardDragStart?.Invoke(c);

            _cardByFile[asset.File] = card;
            _assetCards.Add(card);
            _assetStack?.Children.Add(card);
        }

        // ══════════════════════════════════════════════════════════════
        //  UTILS
        // ══════════════════════════════════════════════════════════════

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
