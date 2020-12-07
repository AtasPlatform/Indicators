namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Brushes = System.Drawing.Brushes;
	using Color = System.Windows.Media.Color;

	[DisplayName("Margin zones")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/20340-margin-zones")]
	public class MarginZones : Indicator
	{
		#region Nested types

		public enum ZoneDirection
		{
			[Display(ResourceType = typeof(Resources),
				Name = "Up")]
			Up = 0,

			[Display(ResourceType = typeof(Resources),
				Name = "Down")]
			Down = 1
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _100Line = new ValueDataSeries("100% line")
			{ Color = Colors.Maroon, Width = 2, ScaleIt = false, VisualType = VisualMode.Square, IsHidden = true };

		private readonly DrawingRectangle _100Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _150Line = new ValueDataSeries("150% line")
			{ Color = Colors.SkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

		private readonly DrawingRectangle _150Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _200Line = new ValueDataSeries("200% line")
			{ Color = Colors.CadetBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

		private readonly DrawingRectangle _200Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _25Line = new ValueDataSeries("25% line")
			{ Color = Colors.LightSkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Square, IsHidden = true };

		private readonly DrawingRectangle _25Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _50Line = new ValueDataSeries("50% line")
			{ Color = Colors.SkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Square, IsHidden = true };

		private readonly DrawingRectangle _50Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _75Line = new ValueDataSeries("75% line")
			{ Color = Colors.LightSkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

		private readonly DrawingRectangle _75Rectangle = new DrawingRectangle(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

		private readonly ValueDataSeries _baseLine = new ValueDataSeries("Base line")
			{ Color = Colors.Gray, Width = 2, ScaleIt = false, VisualType = VisualMode.Square, IsHidden = true };

		private readonly List<int> _newDays = new List<int>();
		private bool _autoPrice = true;
		private bool _calculated;
		private decimal _customPrice;
		private ZoneDirection _direction;
		private int _margin = 3200;
		private decimal _secondPrice;
		private decimal _tickCost = 6.25m;
		private decimal _zonePrice;
		private decimal _zoneWidth;

		private int _zoneWidthDays = 3;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone200",
			Order = 20)]
		public Color Zone200LineColor
		{
			get => _200Line.Color;
			set => _200Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone200",
			Order = 21)]
		public bool ShowZone200
		{
			get => _200Line.VisualType != VisualMode.Hide;
			set => _200Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone150",
			Order = 30)]
		public Color Zone150LineColor
		{
			get => _150Line.Color;
			set => _150Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone150",
			Order = 31)]
		public bool ShowZone150
		{
			get => _150Line.VisualType != VisualMode.Hide;
			set => _150Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone75",
			Order = 40)]
		public Color Zone75LineColor
		{
			get => _75Line.Color;
			set => _75Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone75",
			Order = 41)]
		public bool ShowZone75
		{
			get => _75Line.VisualType != VisualMode.Hide;
			set => _75Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone50",
			Order = 50)]
		public Color Zone50LineColor
		{
			get => _50Line.Color;
			set => _50Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone50",
			Order = 51)]
		public bool ShowZon50
		{
			get => _50Line.VisualType != VisualMode.Hide;
			set => _50Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone25",
			Order = 60)]
		public Color Zone25LineColor
		{
			get => _25Line.Color;
			set => _25Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone50",
			Order = 61)]
		public bool ShowZone25
		{
			get => _25Line.VisualType != VisualMode.Hide;
			set => _25Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "Zone100",
			Order = 70)]
		public Color Zone100LineColor
		{
			get => _100Line.Color;
			set => _100Line.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Zone100",
			Order = 71)]
		public bool ShowZone100
		{
			get => _100Line.VisualType != VisualMode.Hide;
			set => _100Line.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Color",
			GroupName = "BaseLine",
			Order = 80)]
		public Color BaseLineColor
		{
			get => _baseLine.Color;
			set => _baseLine.Color = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "BaseLine",
			Order = 81)]
		public bool ShowBaseLine
		{
			get => _baseLine.VisualType != VisualMode.Hide;
			set => _baseLine.VisualType = value ? VisualMode.Square : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Margin",
			GroupName = "InstrumentParameters",
			Order = 90)]

		public int Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "TickCost",
			GroupName = "InstrumentParameters",
			Order = 91)]
		public decimal TickCost
		{
			get => _tickCost;
			set
			{
				_tickCost = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "DirectionOfZone",
			GroupName = "Other",
			Order = 100)]
		public ZoneDirection Direction
		{
			get => _direction;
			set
			{
				_direction = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "ZoneWidth",
			GroupName = "Other",
			Order = 101)]
		public int ZoneWidth
		{
			get => _zoneWidthDays;
			set
			{
				_zoneWidthDays = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "AutoCalculation",
			GroupName = "StartPrice",
			Order = 110)]
		public bool AutoPrice
		{
			get => _autoPrice;
			set
			{
				_autoPrice = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "CustomPrice",
			GroupName = "StartPrice",
			Order = 111)]
		public decimal CustomPrice
		{
			get => _customPrice;
			set
			{
				_customPrice = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MarginZones()
			: base(true)
		{
			DenyToChangePanel = true;
			DataSeries[0].IsHidden = true;
			DataSeries.Add(_baseLine);
			DataSeries.Add(_100Line);
			DataSeries.Add(_25Line);
			DataSeries.Add(_50Line);
			DataSeries.Add(_75Line);
			DataSeries.Add(_150Line);
			DataSeries.Add(_200Line);

			_100Line.PropertyChanged += MarginZones_PropertyChanged;
			_25Line.PropertyChanged += MarginZones_PropertyChanged;
			_50Line.PropertyChanged += MarginZones_PropertyChanged;
			_75Line.PropertyChanged += MarginZones_PropertyChanged;
			_150Line.PropertyChanged += MarginZones_PropertyChanged;
			_200Line.PropertyChanged += MarginZones_PropertyChanged;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_calculated = false;
				_newDays.Clear();
				_newDays.Add(0);
				return;
			}

			if (IsNewSession(bar) && !_newDays.Contains(bar))
				_newDays.Add(bar);

			if (bar != CurrentBar - 1)
				return;

			if (_calculated)
			{
				if (IsNewWeek(bar))
					_calculated = false;
				var candle = GetCandle(bar);

				if (_direction == ZoneDirection.Up)
				{
					//ищем low
					if (candle.Low < _zonePrice)
						_calculated = false;
				}
				else if (_direction == ZoneDirection.Down)
				{
					//ищем high
					if (candle.High > _zonePrice)
						_calculated = false;
				}
			}

			if (!_calculated)
			{
				_calculated = true;
				Rectangles.Clear();

				if (_autoPrice)
				{
					_zonePrice = 0;

					for (var i = bar; i > 0; i--)
					{
						var candle = GetCandle(i);

						if (_direction == ZoneDirection.Up)
						{
							//ищем low
							if (_zonePrice == 0 || candle.Low < _zonePrice)
								_zonePrice = candle.Low;
						}
						else if (_direction == ZoneDirection.Down)
						{
							//ищем high
							if (_zonePrice == 0 || candle.High > _zonePrice)
								_zonePrice = candle.High;
						}

						if (IsNewWeek(i - 1))
							break;
					}
				}
				else
					_zonePrice = _customPrice;

				var firstBarNumber = Math.Max(_newDays.Count - _zoneWidthDays, 0);
				var firstBar = _newDays.Any() ? _newDays[firstBarNumber] : 0;
				var zoneSize = Margin / _tickCost * (_direction == ZoneDirection.Up ? 1 : -1);
				_zoneWidth = zoneSize * 0.1m * TickSize;
				_secondPrice = _zonePrice + zoneSize * TickSize;

				for (var i = firstBar; i <= bar; i++)
				{
					_baseLine[i] = _zonePrice;
					_100Line[i] = _secondPrice;
					_25Line[i] = _zonePrice + zoneSize * 0.25m * TickSize;
					_50Line[i] = _zonePrice + zoneSize * 0.5m * TickSize;
					_75Line[i] = _zonePrice + zoneSize * 0.75m * TickSize;
					_150Line[i] = _zonePrice + zoneSize * 1.5m * TickSize;
					_200Line[i] = _zonePrice + zoneSize * 2m * TickSize;
				}

				if (_100Line.VisualType != VisualMode.Hide)
				{
					_100Rectangle.FirstBar = firstBar;
					_100Rectangle.SecondBar = bar;
					_100Rectangle.FirstPrice = _secondPrice;
					_100Rectangle.SecondPrice = _secondPrice + _zoneWidth;
					_100Rectangle.Brush = new SolidBrush(ConvertColor(_100Line.Color));
					_100Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_100Rectangle);
				}

				if (_25Line.VisualType != VisualMode.Hide)
				{
					_25Rectangle.FirstBar = firstBar;
					_25Rectangle.SecondBar = bar;
					_25Rectangle.FirstPrice = _25Line[bar];
					_25Rectangle.SecondPrice = _25Line[bar] + _zoneWidth / 4;
					_25Rectangle.Brush = new SolidBrush(ConvertColor(_25Line.Color));
					_25Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_25Rectangle);
				}

				if (_50Line.VisualType != VisualMode.Hide)
				{
					_50Rectangle.FirstBar = firstBar;
					_50Rectangle.SecondBar = bar;
					_50Rectangle.FirstPrice = _50Line[bar];
					_50Rectangle.SecondPrice = _50Line[bar] + _zoneWidth / 2;
					_50Rectangle.Brush = new SolidBrush(ConvertColor(_50Line.Color));
					_50Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_50Rectangle);
				}

				if (_75Line.VisualType != VisualMode.Hide)
				{
					_75Rectangle.FirstBar = firstBar;
					_75Rectangle.SecondBar = bar;
					_75Rectangle.FirstPrice = _75Line[bar];
					_75Rectangle.SecondPrice = _75Line[bar] + _zoneWidth / 4;
					_75Rectangle.Brush = new SolidBrush(ConvertColor(_75Line.Color));
					_75Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_75Rectangle);
				}

				if (_150Line.VisualType != VisualMode.Hide)
				{
					_150Rectangle.FirstBar = firstBar;
					_150Rectangle.SecondBar = bar;
					_150Rectangle.FirstPrice = _150Line[bar];
					_150Rectangle.SecondPrice = _150Line[bar] + _zoneWidth;
					_150Rectangle.Brush = new SolidBrush(ConvertColor(_150Line.Color));
					_150Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_150Rectangle);
				}

				if (_200Line.VisualType != VisualMode.Hide)
				{
					_200Rectangle.FirstBar = firstBar;
					_200Rectangle.SecondBar = bar;
					_200Rectangle.FirstPrice = _200Line[bar];
					_200Rectangle.SecondPrice = _200Line[bar] + _zoneWidth;
					_200Rectangle.Brush = new SolidBrush(ConvertColor(_200Line.Color));
					_200Rectangle.Pen = Pens.Transparent;
					Rectangles.Add(_200Rectangle);
				}
			}

			foreach (var dataSeries in DataSeries)
			{
				var series = (ValueDataSeries)dataSeries;
				series[bar] = series[bar - 1];
			}

			foreach (var drawingRectangle in Rectangles)
				drawingRectangle.SecondBar = bar;
		}

		#endregion

		#region Private methods

		private void MarginZones_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			RecalculateValues();
		}

		private System.Drawing.Color ConvertColor(Color color)
		{
			return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
		}

		#endregion
	}
}