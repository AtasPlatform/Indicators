namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/2471-volume")]
	public class Volume : Indicator
	{
		#region Nested types

		public enum InputType
		{
			Volume = 0,
			Ticks = 1
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _filterSeries;
		private readonly ValueDataSeries _negative;
		private readonly ValueDataSeries _neutral;
		private readonly ValueDataSeries _positive;
		private bool _alerted;

		private bool _deltaColored;

		private decimal _filter;
		private Color _fontColor;
		private RenderStringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

		private InputType _input = InputType.Volume;
		private int _lastBar;
		private bool _useFilter;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "DeltaColored", GroupName = "Colors")]
		public bool DeltaColored
		{
			get => _deltaColored;
			set
			{
				_deltaColored = value;
				RaisePropertyChanged("DeltaColored");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseFilter", GroupName = "Filter")]
		public bool UseFilter
		{
			get => _useFilter;
			set
			{
				_useFilter = value;
				RaisePropertyChanged("UseFilter");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filter")]
		public decimal FilterValue
		{
			get => _filter;
			set
			{
				_filter = value;
				RaisePropertyChanged("Filter");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation")]
		public InputType Input
		{
			get => _input;
			set
			{
				_input = value;
				RaisePropertyChanged("Type");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts")]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts")]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Visualization", Order = 200)]
		public bool ShowVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Visualization", Order = 210)]
		public FontSetting Font { get; set; } = new("Arial", 10);

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Visualization", Order = 220)]
		public System.Windows.Media.Color FontColor
		{
			get => _fontColor.Convert();
			set => _fontColor = value.Convert();
		}

		#endregion

		#region ctor

		public Volume()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			FontColor = Colors.Blue;

			Panel = IndicatorDataProvider.NewPanel;
			_positive = (ValueDataSeries)DataSeries[0];
			_positive.Color = Colors.Green;
			_positive.VisualType = VisualMode.Histogram;
			_positive.ShowZeroValue = false;
			_positive.Name = "Positive";

			_lastBar = -1;

			_negative = new ValueDataSeries("Negative")
			{
				Color = Colors.Red,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_negative);

			_neutral = new ValueDataSeries("Neutral")
			{
				Color = Colors.Gray,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_neutral);

			_filterSeries = new ValueDataSeries("Filter")
			{
				Color = Colors.LightBlue,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_filterSeries);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Volume";
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters || Panel == IndicatorDataProvider.CandlesPanel)
				return;

			var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
			var y = Container.Region.Y + (Container.Region.Bottom - Container.Region.Y) / 2;

			for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
			{
				var value = GetBarValue(i);
				var renderText = value.ToString(CultureInfo.InvariantCulture);

				var strRect = new Rectangle(ChartInfo.GetXByBar(i),
					y,
					barWidth,
					context.MeasureString(renderText, Font.RenderObject).Height);
				context.DrawString(renderText, Font.RenderObject, _fontColor, strRect, _format);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (_lastBar != bar)
				_alerted = false;

			var candle = GetCandle(bar);
			var val = candle.Volume;

			if (UseAlerts && bar == CurrentBar - 1 && !_alerted && val >= _filter && _filter != 0)
			{
				AddAlert(AlertFile, $"Candle volume: {val}");
				_alerted = true;
			}

			_lastBar = bar;

			if (Input == InputType.Ticks)
				val = candle.Ticks;

			if (_useFilter && val > _filter)
			{
				_filterSeries[bar] = val;
				_positive[bar] = _negative[bar] = _neutral[bar] = 0;
				return;
			}

			_filterSeries[bar] = 0;

			if (_deltaColored)
			{
				if (candle.Delta > 0)
				{
					_positive[bar] = val;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Delta < 0)
				{
					_negative[bar] = val;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = val;
					_positive[bar] = _negative[bar] = 0;
				}
			}
			else
			{
				if (candle.Close > candle.Open)
				{
					_positive[bar] = val;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Close < candle.Open)
				{
					_negative[bar] = val;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = val;
					_positive[bar] = _negative[bar] = 0;
				}
			}
		}

		#endregion

		#region Private methods

		private decimal GetBarValue(int bar)
		{
			if (_positive[bar] != 0)
				return _positive[bar];

			if (_negative[bar] != 0)
				return _negative[bar];

			return _neutral[bar] != 0
				? _neutral[bar]
				: _filterSeries[bar];
		}

		#endregion
	}
}