using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SpaceEngineersOreRedistribution
{
    /// <summary></summary>
    public class PieChart : Canvas
    {
        #region Dependency Properties

        #region ItemsSource

        /// <summary>
        /// Item source
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(BindingList<PieChartDataItem>),
            typeof(PieChart), new FrameworkPropertyMetadata(null));

        //DependencyPropertyDescriptor ItemsSourceDescriptor;
        BindingList<PieChartDataItem> _lastItemsSourceValue;
        DependencyPropertyDescriptor _itemsSourceDescriptor;

        /// <summary>
        /// Data sets to be drawn.
        /// </summary>
        /// <remarks>
        /// ObservableCollection is normally recommended for WPF.
        /// In this case BindingList is better since it has more options for listening to changes.</remarks>
        public BindingList<PieChartDataItem> ItemsSource
        {
            get
            {
                //Debug.WriteLine("PieChart::ItemsSource::get");
                var val = (BindingList<PieChartDataItem>)GetValue(ItemsSourceProperty);

                // Just in case the binding changes, check the reference and register event handlers
                if (_lastItemsSourceValue != val)
                {
                    if (_lastItemsSourceValue != null)
                    {
                        _lastItemsSourceValue.ListChanged -= ItemsSourceOnListChanged;
                        //Debug.WriteLine("PieChart: Unregister ListChanged event");
                    }
                    if (val != null)
                    {
                        val.ListChanged += ItemsSourceOnListChanged;
                        //Debug.WriteLine("PieChart: Register ListChanged event");
                    }
                    _lastItemsSourceValue = val;
                }
                return val;
            }
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        #endregion

        #region BorderPen

        /// <summary></summary>
        public static readonly DependencyProperty BorderPenProperty =
        DependencyProperty.Register("BorderPen", typeof(Pen),
           typeof(PieChart), new FrameworkPropertyMetadata(new Pen(Brushes.Black, 1)));

        /// <summary></summary>
        DependencyPropertyDescriptor BorderPenDescriptor;

        /// <summary>
        /// Wrapper for BorderPenProperty
        /// </summary>
        public Pen BorderPen
        {
            get { return (Pen)GetValue(BorderPenProperty); }
            set { SetValue(BorderPenProperty, value); }
        }

        #endregion

        #region Fill

        /// <summary>
        /// Fill brush
        /// </summary>
        public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register("Fill", typeof(Brush),
            typeof(PieChart), new FrameworkPropertyMetadata(null));

        DependencyPropertyDescriptor FillDescriptor;

        /// <summary>
        /// Wrapper for FillProperty
        /// </summary>
        public Brush Fill
        {
            get { return (Brush)GetValue(FillProperty); }
            set { SetValue(FillProperty, value); }
        }

        #endregion

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public PieChart()
        {
            FillDescriptor = DependencyPropertyDescriptor.FromProperty(FillProperty, typeof(PieChart));
            FillDescriptor?.AddValueChanged(this, (sender, args) =>
            {
                //Debug.WriteLine("PieChart: Fill changed to " + Fill);
                InvalidateVisual();
            });

            BorderPenDescriptor = DependencyPropertyDescriptor.FromProperty(BorderPenProperty, typeof(PieChart));
            BorderPenDescriptor?.AddValueChanged(this, (sender, args) =>
            {
                //Debug.WriteLine("PieChart: BorderPen changed to " + BorderPen);
                InvalidateVisual();
            });

            _itemsSourceDescriptor = DependencyPropertyDescriptor.FromProperty(ItemsSourceProperty, typeof(PieChart));
            if (_itemsSourceDescriptor != null)
            {
                _itemsSourceDescriptor.AddValueChanged(this, (sender, args) =>
                {
                    InvalidateVisual();
                });
            }
        }

        void ItemsSourceOnListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            //Debug.WriteLine("PieChart::ItemsSourceOnListChanged");
            InvalidateVisual();
        }

        /// <summary>
        /// Redraw the control.
        /// </summary>
        /// <param name="dc"></param>
        protected override void OnRender(DrawingContext dc)
        {
            //RenderOptions.SetEdgeMode(this,EdgeMode.Unspecified);
            ClipToBounds = false;
            base.OnRender(dc);

            var rect = new Rect(0, 0, ActualWidth, ActualHeight);

            //Debug.WriteLine("PieChart::ActualWidth: " + rect.Width);
            //Debug.WriteLine("PieChart::ActualHeight: " + rect.Height);
            if (ActualWidth < 1 || ActualHeight < 1)
            {
                //Debug.WriteLine("PieChart::OnRender: Dimensions too small!");
                return;
            }

            // Radius
            double dx = rect.Width / 2;
            double dy = rect.Height / 2;

            try // List might be changed while rendering
            {
                if (Fill != null)
                {
                    dc.DrawEllipse(Fill, null, new Point(dx, dy), dx, dy);
                }

                float max = 0;
                var source = ItemsSource;
                if (source != null && source.Count > 0)
                {
                    max += source.Sum(item => (item.Value > 0 ? item.Value : 0));

                    double currentValue = 0;

                    foreach (var item in source)
                    {
                        double value = (item.Value > 0 ? item.Value : 0);

// ReSharper disable once CompareOfFloatsByEqualityOperator
                        if (value == 0) continue;

                        double percentage = value / max;
                        double overallPercentage = currentValue / max;
                        currentValue += value;
                        double startDegrees = -90 + (360 * overallPercentage);
                        double sweepDegrees = (360 * percentage);
                        double startRadians = startDegrees * Math.PI / 180.0;

                        // Fix percentage = 1.0
                        if (sweepDegrees >= 360.0)
                        {
                            dc.DrawEllipse(item.FillBrush, null, new Point(dx, dy), dx, dy);
                            continue;
                        }

                        double sweepRadians = sweepDegrees * Math.PI / 180.0;


                        // Start
                        double xs = rect.X + dx + (Math.Cos(startRadians) * dx);
                        double ys = rect.Y + dy + (Math.Sin(startRadians) * dy);
                        // End
                        double xe = rect.X + dx + (Math.Cos(startRadians + sweepRadians) * dx);
                        double ye = rect.Y + dy + (Math.Sin(startRadians + sweepRadians) * dy);

                        var streamGeom = new StreamGeometry();
                        using (var ctx = streamGeom.Open())
                        {
                            bool isLargeArc = Math.Abs(sweepDegrees) > 180;
                            var sweepDirection = sweepDegrees < 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

                            ctx.BeginFigure(new Point(dx, dy), true, true);
                            //ctx.BeginFigure(new Point(xs, ys), false, false);
                            ctx.LineTo(new Point(xs, ys), true, false);
                            ctx.ArcTo(new Point(xe, ye), new Size(dx, dy), 0, isLargeArc, sweepDirection, true, false);
                            ctx.LineTo(new Point(dx, dy), true, false);
                        }
                        streamGeom.Freeze();
                        dc.DrawGeometry(item.FillBrush, null, streamGeom);
                    }
                }
                var pen = BorderPen;
                if (pen != null)
                {
                    dc.DrawEllipse(null, pen, new Point(dx, dy), dx, dy);
                }
            }
            catch {  }
        }
    }

    /// <summary>
    /// Data item for Pie Charts.
    /// </summary>
    public class PieChartDataItem : PropChangeNotifier
    {
        float _value;
        Brush _fillBrush;

        /// <summary>
        /// Current value
        /// </summary>
        public float Value
        {
            get
            {
                return _value;
            }
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_value == value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Brush used to fill chart.
        /// </summary>
        public Brush FillBrush
        {
            get
            {
                return _fillBrush;
            }
            set
            {
                if (value.Equals(_fillBrush)) return;
                _fillBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public PieChartDataItem()
        {
            Value = 0;
            FillBrush = Brushes.White;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public PieChartDataItem(float value, Brush brush)
        {
            _value = value;
            _fillBrush = brush;
        }
    }
}
