using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using UserControl = System.Windows.Controls.UserControl;

namespace SpineForge.Controls
{
    public partial class CollapsibleCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(CollapsibleCard),
                new PropertyMetadata("Card Title"));

        public static readonly DependencyProperty CardContentProperty =
            DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(CollapsibleCard),
                new PropertyMetadata(null, OnCardContentChanged));

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(CollapsibleCard),
                new PropertyMetadata(true, OnIsExpandedChanged));
        
        public static readonly DependencyProperty RefreshTriggerProperty =
            DependencyProperty.Register(nameof(RefreshTrigger), typeof(int), typeof(CollapsibleCard), 
                new PropertyMetadata(0, OnRefreshTriggerChanged));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public object CardContent
        {
            get { return GetValue(CardContentProperty); }
            set { SetValue(CardContentProperty, value); }
        }

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }
        
        public int RefreshTrigger
        {
            get { return (int)GetValue(RefreshTriggerProperty); }
            set { SetValue(RefreshTriggerProperty, value); }
        }

        private double _savedContentHeight = double.NaN;
        private bool _isAnimating = false;
        private bool _isInitialized = false;
        private bool _heightCached = false;

        // 直接引用XAML中的元素
        private ContentPresenter _contentPresenter;
        private FrameworkElement _toggleIconElement;
        private RotateTransform _iconRotateTransform;

        public CollapsibleCard()
        {
            InitializeComponent();
            Loaded += CollapsibleCard_Loaded;
            SizeChanged += CollapsibleCard_SizeChanged;
            DataContextChanged += CollapsibleCard_DataContextChanged;
        }

        private void CollapsibleCard_Loaded(object sender, RoutedEventArgs e)
        {
            // 直接从XAML中获取元素
            _contentPresenter = FindName("PART_ContentPresenter") as ContentPresenter;
            _toggleIconElement = FindName("PART_ToggleIcon") as FrameworkElement;
            _iconRotateTransform = FindName("IconRotateTransform") as RotateTransform;
            
            if (!_isInitialized)
            {
                _isInitialized = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVisualState(false);
                    UpdateIconRotation(false); // 初始化图标状态
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // 事件处理程序
        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Header clicked, toggling IsExpanded");
            IsExpanded = !IsExpanded;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Toggle button clicked, toggling IsExpanded");
            IsExpanded = !IsExpanded;
        }

        /// <summary>
        /// 强制刷新内容高度（在内容动态改变后调用）
        /// </summary>
        public void RefreshContentHeight()
        {
            _heightCached = false;
            _savedContentHeight = double.NaN;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                CacheContentHeight();
                if (IsExpanded)
                {
                    UpdateVisualState(true);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void CollapsibleCard_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue != null)
            {
                UnsubscribeFromCollectionChanges(e.OldValue);
            }

            if (e.NewValue != null)
            {
                SubscribeToCollectionChanges(e.NewValue);
            }
        }

        private void SubscribeToCollectionChanges(object dataContext)
        {
            var spineFilePathsProperty = dataContext.GetType().GetProperty("SpineFilePaths");
            if (spineFilePathsProperty != null)
            {
                var collection = spineFilePathsProperty.GetValue(dataContext) as INotifyCollectionChanged;
                if (collection != null)
                {
                    collection.CollectionChanged += OnSpineFilePathsCollectionChanged;
                }
            }
        }

        private void UnsubscribeFromCollectionChanges(object dataContext)
        {
            var spineFilePathsProperty = dataContext.GetType().GetProperty("SpineFilePaths");
            if (spineFilePathsProperty != null)
            {
                var collection = spineFilePathsProperty.GetValue(dataContext) as INotifyCollectionChanged;
                if (collection != null)
                {
                    collection.CollectionChanged -= OnSpineFilePathsCollectionChanged;
                }
            }
        }

        private void OnSpineFilePathsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshContentHeight();
        }

        private void CollapsibleCard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized && IsExpanded && !_isAnimating)
            {
                _heightCached = false;
                _savedContentHeight = double.NaN;
            }
        }

        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (CollapsibleCard)d;
            Console.WriteLine($"IsExpanded changed to: {e.NewValue}");
            card.UpdateVisualState(true);
            card.UpdateIconRotation(true); // 更新图标旋转
        }

        private static void OnCardContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (CollapsibleCard)d;
            card._heightCached = false;
            card._savedContentHeight = double.NaN;
            
            if (card.IsExpanded)
            {
                card.Dispatcher.BeginInvoke(new Action(() =>
                {
                    card.UpdateVisualState(true);
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private static void OnRefreshTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (CollapsibleCard)d;
            card.RefreshContentHeight();
        }

        private void UpdateIconRotation(bool useTransitions)
        {
            if (_iconRotateTransform == null) return;

            double targetAngle = IsExpanded ? 180 : 0;
            
            if (!useTransitions)
            {
                _iconRotateTransform.Angle = targetAngle;
                return;
            }

            var animation = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            _iconRotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void UpdateVisualState(bool useTransitions)
        {
            Console.WriteLine($"UpdateVisualState called: IsExpanded={IsExpanded}, useTransitions={useTransitions}");
            
            if (_contentPresenter == null)
            {
                Console.WriteLine("ContentPresenter is null, cannot update visual state");
                return;
            }
            
            if (_isAnimating)
            {
                Console.WriteLine("Already animating, skipping");
                return;
            }

            // 停止任何正在进行的动画
            _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, null);

            if (IsExpanded)
            {
                ExpandWithAnimation(useTransitions);
            }
            else
            {
                CollapseWithAnimation(useTransitions);
            }
        }

        private void ExpandWithAnimation(bool useTransitions)
        {
            Console.WriteLine("Starting expand animation");
            
            // 1. 测量内容的期望高度
            double targetHeight = MeasureContentHeight();
            Console.WriteLine($"Target height: {targetHeight}");
            
            if (!useTransitions || targetHeight <= 0)
            {
                // 无动画情况直接设置Auto
                _contentPresenter.Height = double.NaN;
                Console.WriteLine("No animation, set to Auto directly");
                return;
            }
            
            // 2. 先设置为0准备动画
            _contentPresenter.Height = 0;
            
            // 3. 创建动画，但不播放到完整高度，而是播放到90%左右
            double animationTargetHeight = targetHeight * 0.9; // 播放到90%
            
            var animation = new DoubleAnimation
            {
                From = 0,
                To = animationTargetHeight,
                Duration = TimeSpan.FromMilliseconds(200), // 稍微缩短动画时间
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            // 4. 动画完成后立即切换到Auto，避免弹跳
            animation.Completed += (s, e) =>
            {
                // 立即清除动画控制并设置为Auto
                _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, null);
                _contentPresenter.Height = double.NaN;
                
                _isAnimating = false;
                // Console.WriteLine("展开完成，已恢复Auto状态");
            };
            
            _isAnimating = true;
            _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
        
        private void CollapseWithAnimation(bool useTransitions)
        {
            Console.WriteLine("Starting collapse animation");
            
            // 1. 获取当前实际高度
            double currentHeight = _contentPresenter.ActualHeight;
            Console.WriteLine($"Current height: {currentHeight}");
            
            // 2. 如果当前是Auto状态，先固定为实际高度
            if (double.IsNaN(_contentPresenter.Height))
            {
                _contentPresenter.Height = currentHeight;
            }
            
            if (!useTransitions || currentHeight <= 0)
            {
                // 无动画情况直接设置0
                _contentPresenter.Height = 0;
                Console.WriteLine("No animation, set to 0 directly");
                return;
            }
            
            // 3. 创建收缩动画
            var animation = new DoubleAnimation
            {
                From = currentHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            animation.Completed += (s, e) =>
            {
                _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, null);
                _contentPresenter.Height = 0;
                
                _isAnimating = false;
                Console.WriteLine("收缩完成");
            };
            
            _isAnimating = true;
            _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
        
        private double MeasureContentHeight()
        {
            if (_contentPresenter == null) 
                return 0;

            // 临时设置为Auto来测量真实高度
            var originalHeight = _contentPresenter.Height;
            _contentPresenter.Height = double.NaN;
            
            // 强制重新布局测量
            _contentPresenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _contentPresenter.UpdateLayout();
            
            double measuredHeight = _contentPresenter.DesiredSize.Height;
            
            // 如果DesiredSize为0，使用ActualHeight
            if (measuredHeight <= 0)
            {
                measuredHeight = _contentPresenter.ActualHeight;
            }
            
            // 恢复原始高度
            _contentPresenter.Height = originalHeight;
            
            return Math.Max(0, measuredHeight);
        }

        private void CacheContentHeight()
        {
            if (!_heightCached && _contentPresenter != null)
            {
                _savedContentHeight = MeasureContentHeight();
                _heightCached = true;
                Console.WriteLine($"缓存内容高度: {_savedContentHeight}");
            }
        }
    }
}
