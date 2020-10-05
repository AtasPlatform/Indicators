namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using Utils.Common.Attributes;

	using Brushes = System.Drawing.Brushes;
	using Color = System.Windows.Media.Color;
	using Pen = System.Drawing.Pen;

	[DisplayName("Initial Balance")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/22014-initial-balance")]
	public class InitialBalance : Indicator
	{
		#region Properties

		[Display(Name = "Show",
			GroupName = "Open range",
			Order = 10)]
		public bool ShowOpenRange
		{
			get => _showOpenRange;
			set
			{
				_showOpenRange = value;
				RecalculateValues();
			}
		}

		[Display(Name = "Border width",
			GroupName = "Open range",
			Order = 20)]
		public int BorderWidth
		{
			get => _borderWidth;
			set
			{
				_borderWidth = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(Name = "Border color",
			GroupName = "Open range",
			Order = 30)]
		public Color BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				RecalculateValues();
			}
		}

		[Display(Name = "Fill color",
			GroupName = "Open range",
			Order = 40)]
		public Color FillColor
		{
			get => _fillColor;
			set
			{
				_fillColor = value;
				RecalculateValues();
			}
		}

		[Display(Name = "Custom session start",
			GroupName = "Session time",
			Order = 10)]
		public bool CustomSessionStart
		{
			get => _customSessionStart;
			set
			{
				_customSessionStart = value;
				RecalculateValues();
			}
		}

		[Display(Name = "Start time(GMT)",
			GroupName = "Session time",
			Order = 20)]
		public TimeSpan StartDate
		{
			get => _startDate;
			set
			{
				_startDate = value;
				RecalculateValues();
			}
		}

		[Display(Name = "Period",
			GroupName = "Session time",
			Order = 30)]
		public int Period
		{
			get => _period;
			set
			{
				_period = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[DisplayName("Multiplier 1")]
		public decimal X1
		{
			get => _x1;
			set
			{
				_x1 = value;
				RecalculateValues();
			}
		}

		[DisplayName("Multiplier 2")]
		public decimal X2
		{
			get => _x2;
			set
			{
				_x2 = value;
				RecalculateValues();
			}
		}

		[DisplayName("Multiplier 3")]
		public decimal X3
		{
			get => _x3;
			set
			{
				_x3 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public InitialBalance()
			: base(true)
		{
			DataSeries[0] = _mid;
			DenyToChangePanel = true;

			DataSeries.Add(_ibh);
			DataSeries.Add(_ibl);
			DataSeries.Add(_ibm);
			DataSeries.Add(_ibhx1);
			DataSeries.Add(_ibhx2);
			DataSeries.Add(_ibhx3);
			DataSeries.Add(_iblx1);
			DataSeries.Add(_iblx2);
			DataSeries.Add(_iblx3);

			_ibh.PropertyChanged += DataSetiesPropertyChanged;
			_ibl.PropertyChanged += DataSetiesPropertyChanged;
			_ibm.PropertyChanged += DataSetiesPropertyChanged;
			_ibhx1.PropertyChanged += DataSetiesPropertyChanged;
			_ibhx2.PropertyChanged += DataSetiesPropertyChanged;
			_ibhx3.PropertyChanged += DataSetiesPropertyChanged;
			_iblx1.PropertyChanged += DataSetiesPropertyChanged;
			_iblx2.PropertyChanged += DataSetiesPropertyChanged;
			_iblx3.PropertyChanged += DataSetiesPropertyChanged;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_initialized = false;
				return;
			}

			_initialized = true;
			var candle = GetCandle(bar);
			var isStart = _customSessionStart ? candle.Time.TimeOfDay >= _startDate && GetCandle(bar - 1).Time.TimeOfDay < _startDate : IsNewSession(bar);
			var isEnd = candle.Time >= _endTime && GetCandle(bar - 1).Time < _endTime;
			if (isStart)
			{
				//Clear all values
				_maxValue = decimal.MinValue;
				_minValue = decimal.MaxValue;
				_ibMax = decimal.MinValue;
				_ibMin = decimal.MaxValue;
				_ibmValue = decimal.Zero;
				ibhx1 = decimal.Zero;
				ibhx2 = decimal.Zero;
				ibhx3 = decimal.Zero;
				iblx1 = decimal.Zero;
				iblx2 = decimal.Zero;
				iblx3 = decimal.Zero;
				_calculate = true;
				_highLowIsSet = false;
				_lastStartBar = bar;
				_endTime = candle.Time.AddMinutes(_period);
				foreach (var dataSeries in DataSeries)
					((ValueDataSeries)dataSeries).SetPointOfEndLine(bar - 1);

				if (ShowOpenRange)
				{
					var pen = new Pen(ConvertColor(_borderColor))
					{
						Width = _borderWidth
					};
					var brush = new SolidBrush(ConvertColor(_fillColor));
					_rectangle = new DrawingRectangle(bar, decimal.Zero, bar, decimal.Zero, pen, brush);
					Rectangles.Add(_rectangle);
				}
			}
			else if (isEnd)
				_calculate = false;

			if (_calculate)
			{
				if (candle.High > _maxValue)
				{
					_highLowIsSet = true;
					_ibMax = _maxValue = candle.High;
				}

				if (candle.Low < _minValue)
				{
					_highLowIsSet = true;
					_ibMin = _minValue = candle.Low;
				}

				if (ShowOpenRange)
				{
					_rectangle.SecondBar = bar;
					_rectangle.FirstPrice = _ibMax;
					_rectangle.SecondPrice = _ibMin;
				}
			}

			if (candle.High > _maxValue)
				_maxValue = candle.High;
			if (candle.Low < _minValue)
				_minValue = candle.Low;

			if (!_highLowIsSet)
				return;

			_mid[bar] = mid = (_minValue + _maxValue) / 2m;
			_ibh[bar] = _ibMax;
			_ibl[bar] = _ibMin;
			_ibmValue = _ibm[bar] = (_ibMin + _ibMax) / 2m;
			var diff = _ibMax - _ibMin;

			ibhx1 = _ibhx1[bar] = _ibMax + diff * _x1;
			ibhx2 = _ibhx2[bar] = _ibMax + diff * _x2;
			ibhx3 = _ibhx3[bar] = _ibMax + diff * _x3;
			iblx1 = _iblx1[bar] = _ibMin - diff * _x1;
			iblx2 = _iblx2[bar] = _ibMin - diff * _x2;
			iblx3 = _iblx3[bar] = _ibMin - diff * _x3;

			AddText(_lastStartBar + "Mid", "Mid", true, bar, mid, 0, 0, ConvertColor(_mid.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBH", "IBH", true, bar, _ibMax, 0, 0, ConvertColor(_ibh.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBL", "IBL", true, bar, _ibMin, 0, 0, ConvertColor(_ibl.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBM", "IBM", true, bar, _ibmValue, 0, 0, ConvertColor(_ibm.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBHX1", "IBHX1", true, bar, ibhx1, 0, 0, ConvertColor(_ibhx1.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBHX2", "IBHX2", true, bar, ibhx2, 0, 0, ConvertColor(_ibhx2.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBHX3", "IBHX3", true, bar, ibhx3, 0, 0, ConvertColor(_ibhx3.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBLX1", "IBLX1", true, bar, iblx1, 0, 0, ConvertColor(_iblx1.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBLX2", "IBLX2", true, bar, iblx2, 0, 0, ConvertColor(_iblx2.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
			AddText(_lastStartBar + "IBLX3", "IBLX3", true, bar, iblx3, 0, 0, ConvertColor(_iblx3.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
		}

		#endregion

		#region Private methods

		private void DataSetiesPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (!_initialized)
				return;

			RecalculateValues();
		}

		private System.Drawing.Color ConvertColor(Color color)
		{
			return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		#endregion

		#region DataSerieces

		private readonly ValueDataSeries _ibh = new ValueDataSeries("IBH")
		{
			Color = Colors.Blue,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _ibl = new ValueDataSeries("IBL")
		{
			Color = Colors.Red,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _ibm = new ValueDataSeries("IBM")
		{
			Color = Colors.Green,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _ibhx1 = new ValueDataSeries("IBHX1")
		{
			Color = Colors.Magenta,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _ibhx2 = new ValueDataSeries("IBHX2")
		{
			Color = Colors.Magenta,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _ibhx3 = new ValueDataSeries("IBHX3")
		{
			Color = Colors.Magenta,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _iblx1 = new ValueDataSeries("IBLX1")
		{
			Color = Colors.Purple,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _iblx2 = new ValueDataSeries("IBLX2")
		{
			Color = Colors.Purple,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _iblx3 = new ValueDataSeries("IBLX3")
		{
			Color = Colors.Purple,
			LineDashStyle = LineDashStyle.Dash,
			VisualType = VisualMode.Square,
			Width = 1
		};

		private readonly ValueDataSeries _mid = new ValueDataSeries("Mid")
		{
			Color = Color.FromArgb(0, 0, 255, 0),
			LineDashStyle = LineDashStyle.Solid,
			VisualType = VisualMode.Square,
			Width = 1
		};

		#endregion

		#region private vars

		private bool _initialized;
		private TimeSpan _startDate = new TimeSpan(9, 0, 0);
		private decimal _x1 = 1m;
		private decimal _x2 = 2m;
		private decimal _x3 = 3m;
		private bool _showOpenRange = true;
		private int _borderWidth = 1;
		private Color _borderColor = Colors.Red;
		private Color _fillColor = Colors.Yellow;
		private bool _customSessionStart;
		private int _period = 60;
		private decimal _maxValue = decimal.MinValue;
		private decimal _minValue = decimal.MaxValue;
		private decimal _ibMax = decimal.MinValue;
		private decimal _ibMin = decimal.MaxValue;
		private decimal _ibmValue = decimal.Zero;
		private decimal ibhx1 = decimal.Zero;
		private decimal ibhx2 = decimal.Zero;
		private decimal ibhx3 = decimal.Zero;
		private decimal iblx1 = decimal.Zero;
		private decimal iblx2 = decimal.Zero;
		private decimal iblx3 = decimal.Zero;
		private decimal mid = decimal.Zero;
		private DateTime _endTime = DateTime.MaxValue;
		private bool _calculate;
		private int _lastStartBar = -1;
		private bool _highLowIsSet;
		private DrawingRectangle _rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Yellow);

		#endregion
	}
}