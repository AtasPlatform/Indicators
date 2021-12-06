namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("BollingerBands")]
	[LocalizedDescription(typeof(Resources), "BollingerBands")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/6724-bollingerbands")]
	public class BollingerBands : Indicator
	{
		#region Fields

		private readonly RangeDataSeries _band = new("Background");
		private readonly StdDev _dev = new();

		private readonly SMA _sma = new();
		private int _lastAlertBot;
		private int _lastAlertMid;
		private int _lastAlertTop;
		private bool _onLineBot;
		private bool _onLineMid;
		private bool _onLineTop;
		private decimal _width;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = _dev.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "BBandsWidth",
			GroupName = "Common",
			Order = 22)]
		public decimal Width
		{
			get => _width;
			set
			{
				if (value <= 0)
					return;

				_width = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "TopBand",
			Order = 100)]
		public bool UseAlertsTop { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "RepeatAlert",
			GroupName = "TopBand",
			Order = 110)]
		[Range(0, 100000)]
		public bool RepeatAlertTop { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "ApproximationFilter",
			GroupName = "TopBand",
			Order = 120)]
		[Range(0, 100000)]
		public int AlertSensitivityTop { get; set; } = 1;

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "TopBand",
			Order = 130)]
		public string AlertFileTop { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "FontColor",
			GroupName = "TopBand",
			Order = 140)]
		public Color FontColorTop { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Resources),
			Name = "BackGround",
			GroupName = "TopBand",
			Order = 150)]
		public Color BackgroundColorTop { get; set; } = Colors.DimGray;

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "MiddleBand",
			Order = 200)]
		public bool UseAlertsMid { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "RepeatAlert",
			GroupName = "MiddleBand",
			Order = 210)]
		[Range(0, 100000)]
		public bool RepeatAlertMid { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "ApproximationFilter",
			GroupName = "MiddleBand",
			Order = 220)]
		[Range(0, 100000)]
		public int AlertSensitivityMid { get; set; } = 1;

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "MiddleBand",
			Order = 230)]
		public string AlertFileMid { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "FontColor",
			GroupName = "MiddleBand",
			Order = 240)]
		public Color FontColorMid { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Resources),
			Name = "BackGround",
			GroupName = "MiddleBand",
			Order = 250)]
		public Color BackgroundColorMid { get; set; } = Colors.DimGray;

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "BottomBand",
			Order = 300)]
		public bool UseAlertsBot { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "RepeatAlert",
			GroupName = "BottomBand",
			Order = 310)]
		[Range(0, 100000)]
		public bool RepeatAlertBot { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "ApproximationFilter",
			GroupName = "BottomBand",
			Order = 320)]
		[Range(0, 100000)]
		public int AlertSensitivityBot { get; set; } = 1;

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "BottomBand",
			Order = 330)]
		public string AlertFileBot { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "FontColor",
			GroupName = "BottomBand",
			Order = 340)]
		public Color FontColorBot { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Resources),
			Name = "BackGround",
			GroupName = "BottomBand",
			Order = 350)]
		public Color BackgroundColorBot { get; set; } = Colors.DimGray;

		#endregion

		#region ctor

		public BollingerBands()
		{
			((ValueDataSeries)DataSeries[0]).Color = Colors.Green;
			DataSeries[0].Name = "Bollinger Bands";

			DataSeries.Add(new ValueDataSeries("Up")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(new ValueDataSeries("Down")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(_band);
			Period = 10;
			Width = 1;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sma = _sma.Calculate(bar, value);
			var dev = _dev.Calculate(bar, value);

			this[bar] = sma;

			DataSeries[1][bar] = sma + dev * Width;
			DataSeries[2][bar] = sma - dev * Width;

			_band[bar].Upper = sma + dev * Width;
			_band[bar].Lower = sma - dev * Width;

			if (bar != CurrentBar - 1)
				return;

			if (UseAlertsTop && (RepeatAlertTop || _lastAlertTop != bar && !RepeatAlertTop) && !_onLineTop)
			{
				var close = GetCandle(bar).Close;
				var onLine = Math.Abs(_band[bar].Upper - close) / InstrumentInfo.TickSize <= AlertSensitivityTop;

				if (onLine && !_onLineTop)
				{
					AddAlert(AlertFileTop, InstrumentInfo.Instrument, "Bollinger top approximation alert", BackgroundColorTop, FontColorTop);
					_lastAlertTop = bar;
				}

				_onLineTop = onLine;
			}

			if (UseAlertsMid && (RepeatAlertMid || _lastAlertMid != bar && !RepeatAlertMid) && !_onLineMid)
			{
				var close = GetCandle(bar).Close;
				var onLine = Math.Abs(this[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivityMid;

				if (onLine && !_onLineMid)
				{
					AddAlert(AlertFileMid, InstrumentInfo.Instrument, "Bollinger middle approximation alert", BackgroundColorMid, FontColorMid);
					_lastAlertMid = bar;
				}

				_onLineMid = onLine;
			}

			if (UseAlertsBot && (RepeatAlertBot || _lastAlertBot != bar && !RepeatAlertBot) && !_onLineBot)
			{
				if (_lastAlertBot == bar && !RepeatAlertBot)
					return;

				var close = GetCandle(bar).Close;
				var onLine = Math.Abs(_band[bar].Lower - close) / InstrumentInfo.TickSize <= AlertSensitivityBot;

				if (onLine && !_onLineBot)
				{
					AddAlert(AlertFileTop, InstrumentInfo.Instrument, "Bollinger bottom approximation alert", BackgroundColorBot, FontColorBot);
					_lastAlertBot = bar;
				}

				_onLineBot = onLine;
			}
		}

		#endregion
	}
}