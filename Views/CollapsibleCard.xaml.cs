using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Size = System.Windows.Size;
using UserControl = System.Windows.Controls.UserControl;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable LocalizableElement

namespace SpineForge.Views
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
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object CardContent
        {
            get => GetValue(CardContentProperty);
            set => SetValue(CardContentProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }
        
        public int RefreshTrigger
        {
            get => (int)GetValue(RefreshTriggerProperty);
            set => SetValue(RefreshTriggerProperty, value);
        }

        private double _savedContentHeight = double.NaN;
        private bool _isAnimating = false;
        private bool _pendingStateChange = false;
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
            // 输出“Header clicked, toggling IsExpanded”
            Console.WriteLine("Header clicked, toggling IsExpanded");
            // 切换IsExpanded的值
            IsExpanded = !IsExpanded;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // 输出Toggle button clicked, toggling IsExpanded
            Console.WriteLine("Toggle button clicked, toggling IsExpanded");
            // 切换IsExpanded的值
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

// 当CollapsibleCard的DataContext属性发生变化时，调用此方法
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
    
            // 如果正在动画中，记录待处理的状态变更
            if (card._isAnimating)
            {
                card._pendingStateChange = true;
                return;
            }
    
            card.UpdateVisualState(true);
            card.UpdateIconRotation(true);
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
    
            double targetHeight = MeasureContentHeight();
            Console.WriteLine($"Target height: {targetHeight}");
    
            if (!useTransitions || targetHeight <= 0)
            {
                _contentPresenter.Height = double.NaN;
                Console.WriteLine("No animation, set to Auto directly");
                return;
            }
    
            _contentPresenter.Height = 0;
    
            double animationTargetHeight = targetHeight * 0.9;
    
            var animation = new DoubleAnimation
            {
                From = 0,
                To = animationTargetHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
    
            animation.Completed += (s, e) =>
            {
                // 清除动画并设置为Auto
                _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, null);
                _contentPresenter.Height = double.NaN;
        
                _isAnimating = false;
        
                // 处理待处理的状态变更
                HandlePendingStateChange();
            };
    
            _isAnimating = true;
            _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
        
        private void CollapseWithAnimation(bool useTransitions)
        {
            Console.WriteLine("Starting collapse animation");
    
            double currentHeight = _contentPresenter.ActualHeight;
            Console.WriteLine($"Current height: {currentHeight}");
    
            if (double.IsNaN(_contentPresenter.Height))
            {
                _contentPresenter.Height = currentHeight;
            }
    
            if (!useTransitions || currentHeight <= 0)
            {
                _contentPresenter.Height = 0;
                Console.WriteLine("No animation, set to 0 directly");
                return;
            }
    
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
        
                // 处理待处理的状态变更
                HandlePendingStateChange();
            };
    
            _isAnimating = true;
            _contentPresenter.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
        
        private void HandlePendingStateChange()
        {
            if (_pendingStateChange)
            {
                _pendingStateChange = false;
                // 延迟一帧再处理，确保当前动画完全结束
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVisualState(true);
                    UpdateIconRotation(true);
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
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
