using RightEdge.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MLM2PRO_BT_APP.RightEdge.Controls
{
    /// <summary>
    /// Interaction logic for HandednessSelector.xaml
    /// </summary>
    public partial class HandednessSelector : UserControl
    {
        private Brush defaultButtonBackground;
        private Brush defaultButtonForeground;
        private Brush defaultButtonBorderBrush;

        public delegate void HandednessSelectionChangedEventHandler(object sender, SwingDirection newSelection);

        public event HandednessSelectionChangedEventHandler? SelectionChanged;

        public HandednessSelector()
        {
            InitializeComponent();
            this.Loaded += onLoaded;
        }

        private void onLoaded(object sender, RoutedEventArgs e)
        {
            defaultButtonBackground = LeftButton.Background;
            defaultButtonForeground = LeftButton.Foreground;
            defaultButtonBorderBrush = LeftButton.BorderBrush;

            UpdateVisualState();
        }

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.Register(
                nameof(Width),
                typeof(double),
                typeof(HandednessSelector),
                new PropertyMetadata(150.0, OnWidthChanged));

        private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HandednessSelector control && e.NewValue is double newValue)
            {
                control.LeftButton.Width = newValue / 2.0;
                control.RightButton.Width = newValue / 2.0;
            }
        }

        public SwingDirection SelectedHandedness
        {
            get => (SwingDirection)GetValue(SelectedHandednessProperty);
            set => SetValue(SelectedHandednessProperty, value);
        }

        public static readonly DependencyProperty SelectedHandednessProperty =
            DependencyProperty.Register(
                nameof(SelectedHandedness),
                typeof(SwingDirection),
                typeof(HandednessSelector),
                new PropertyMetadata(SwingDirection.RIGHT, OnSelectedHandednessChanged));

        private static void OnSelectedHandednessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HandednessSelector control && e.NewValue is SwingDirection newValue)
            {
                control.UpdateVisualState();
                control.SelectionChanged?.Invoke(control, newValue);
            }
        }

        private void LeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedHandedness != SwingDirection.LEFT)
            {
                SelectedHandedness = SwingDirection.LEFT;
            }
        }

        private void RightButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedHandedness != SwingDirection.RIGHT)
            {
                SelectedHandedness = SwingDirection.RIGHT;
            }
        }

        private void UpdateVisualState()
        {
            bool leftSelected = SelectedHandedness == SwingDirection.LEFT;
            bool rightSelected = SelectedHandedness == SwingDirection.RIGHT;

            ApplyButtonStyle(LeftButton, leftSelected);
            ApplyButtonStyle(RightButton, rightSelected);
        }

        private void ApplyButtonStyle(Button button, bool isSelected)
        {
            if (isSelected)
            {
                button.Background = new SolidColorBrush(Colors.DarkGreen);
                button.Foreground = new SolidColorBrush(Colors.White);
                button.BorderBrush = new SolidColorBrush(Colors.DarkGreen);
            }
            else
            {
                button.Background = defaultButtonBackground;
                button.Foreground = defaultButtonForeground;
                button.BorderBrush = defaultButtonBorderBrush; 
            }
        }
    }
}
