namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("BollingerBands")]
	[LocalizedDescription(typeof(Resources), "BollingerBands")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/6724-bollingerbands")]
	public class BollingerBands : Indicator
	{
		#region Fields

		private readonly RangeDataSeries _band = new("Background Neutral");
		private readonly StdDev _dev = new();

		private readonly ObjectDataSeries _dirSeries = new("direction");

		private readonly RangeDataSeries _downBand = new("Background Down")
		{
			RangeColor = Color.FromArgb(90, 255, 0, 0),
		};

		private readonly RangeDataSeries _downReserveBand = new("Down Reserve")
		{
			RangeColor = Color.FromArgb(90, 255, 0, 0)
		};

		private readonly ValueDataSeries _downSeries = new("Down")
		{
			VisualType = VisualMode.Line,
			IgnoredByAlerts = true
        };

		private readonly RangeDataSeries _reserveBand = new("Neutral Reserve");

		private readonly SMA _sma = new();

		private readonly ValueDataSeries _smaSeries = new("Bollinger Bands") { Color = Colors.Green };

		private readonly RangeDataSeries _upBand = new("Background Up")
		{
			RangeColor = Color.FromArgb(90, 0, 255, 0)
		};

		private readonly RangeDataSeries _upReserveBand = new("Up Reserve")
		{
			RangeColor = Color.FromArgb(90, 0, 255, 0)
		};

		private readonly ValueDataSeries _upSeries = new("Up")
		{
			VisualType = VisualMode.Line,
			IgnoredByAlerts = true
        };

		private int _lastAlertBot;
		private int _lastAlertMid;
		private int _lastAlertTop;
		private int _lastBar;
		private bool _onLineBot;
		private bool _onLineMid;
		private bool _onLineTop;
		private int _shift;
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
			Name = "Shift",
			GroupName = "Common",
			Order = 22)]
		public int Shift
		{
			get => _shift;
			set
			{
				_shift = value;
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
			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Green.Convert();
			DataSeries[0] = _smaSeries;

			DataSeries.Add(_upSeries);
			DataSeries.Add(_downSeries);

			_reserveBand.IsHidden = _upReserveBand.IsHidden = _downReserveBand.IsHidden = true;

			_reserveBand.RangeColor = _band.RangeColor;
			_upReserveBand.RangeColor = _upBand.RangeColor;
			_downReserveBand.RangeColor = _downBand.RangeColor;

			_band.PropertyChanged += RangeChanged;
			_upBand.PropertyChanged += RangeChanged;
			_downBand.PropertyChanged += RangeChanged;
			DataSeries.Add(_band);
			DataSeries.Add(_reserveBand);
			DataSeries.Add(_upBand);
			DataSeries.Add(_upReserveBand);
			DataSeries.Add(_downBand);
			DataSeries.Add(_downReserveBand);
			Period = 10;
			Width = 1;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sma = _sma.Calculate(bar, value);
			var dev = _dev.Calculate(bar, value);

			if (bar == 0)
			{
				_dirSeries.Clear();
				DataSeries.ForEach(x => x.Clear());

				var startBar = Math.Max(0, Shift);

				if (startBar != 0)
				{
					_smaSeries.SetPointOfEndLine(startBar - 1);
					_upSeries.SetPointOfEndLine(startBar - 1);
					_downSeries.SetPointOfEndLine(startBar - 1);
				}
			}

			if (bar < Shift)
				return;

			var calcBar = bar;


			if (Shift < 0)
			{
				calcBar = bar + Shift;
				_smaSeries[bar + Shift] = sma;
				_upSeries[bar + Shift] = sma + dev * Width;
				_downSeries[bar + Shift] = sma - dev * Width;

				if (bar == CurrentBar - 1)
				{
					for (var i = bar + Shift + 1; i < CurrentBar; i++)
					{
						_smaSeries[i] = _smaSeries[bar + Shift];
						_upSeries[i] = _upSeries[bar + Shift];
						_downSeries[i] = _downSeries[bar + Shift];
					}
				}
			}
			else
			{
				_smaSeries[bar] = _sma[bar - Shift];
				_upSeries[bar] = _sma[bar - Shift] + _dev[bar - Shift] * Width;
				_downSeries[bar] = _sma[bar - Shift] - _dev[bar - Shift] * Width;
			}

			if (_lastBar != bar && calcBar != 0)
				CalcPaint(calcBar - 1);

			_lastBar = bar;

			if (bar != CurrentBar - 1)
				return;

			if (UseAlertsTop && (RepeatAlertTop || _lastAlertTop != bar && !RepeatAlertTop))
			{
				var close = GetCandle(bar).Close;
				var onLine = Math.Abs(_upSeries[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivityTop;

				if (onLine && !_onLineTop)
				{
					AddAlert(AlertFileTop, InstrumentInfo.Instrument, "Bollinger top approximation alert", BackgroundColorTop, FontColorTop);
					_lastAlertTop = bar;
				}

				_onLineTop = onLine;
			}

			if (UseAlertsMid && (RepeatAlertMid || _lastAlertMid != bar && !RepeatAlertMid))
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

			if (UseAlertsBot && (RepeatAlertBot || _lastAlertBot != bar && !RepeatAlertBot))
			{
				var close = GetCandle(bar).Close;
				var onLine = Math.Abs(_downSeries[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivityBot;

				if (onLine && !_onLineBot)
				{
					AddAlert(AlertFileTop, InstrumentInfo.Instrument, "Bollinger bottom approximation alert", BackgroundColorBot, FontColorBot);
					_lastAlertBot = bar;
				}

				_onLineBot = onLine;
			}
		}

		#endregion

		#region Private methods

		private void CalcPaint(int bar)
		{
			if (_smaSeries[bar] > _smaSeries[bar - 1])
			{
				_dirSeries[bar] = TradeDirection.Buy;

				if (AltRequired(bar, TradeDirection.Buy))
				{
					_upReserveBand[bar].Upper = _upSeries[bar];
					_upReserveBand[bar].Lower = _downSeries[bar];
					_upReserveBand[bar - 1].Upper = _upSeries[bar - 1];
					_upReserveBand[bar - 1].Lower = _downSeries[bar - 1];
				}
				else
				{
					_upBand[bar].Upper = _upSeries[bar];
					_upBand[bar].Lower = _downSeries[bar];
					_upBand[bar - 1].Upper = _upSeries[bar - 1];
					_upBand[bar - 1].Lower = _downSeries[bar - 1];
				}
			}
			else if (_smaSeries[bar] < _smaSeries[bar - 1])
			{
				_dirSeries[bar] = TradeDirection.Sell;

				if (AltRequired(bar, TradeDirection.Sell))
				{
					_downReserveBand[bar].Upper = _upSeries[bar];
					_downReserveBand[bar].Lower = _downSeries[bar];
					_downReserveBand[bar - 1].Upper = _upSeries[bar - 1];
					_downReserveBand[bar - 1].Lower = _downSeries[bar - 1];
				}
				else
				{
					_downBand[bar].Upper = _upSeries[bar];
					_downBand[bar].Lower = _downSeries[bar];
					_downBand[bar - 1].Upper = _upSeries[bar - 1];
					_downBand[bar - 1].Lower = _downSeries[bar - 1];
				}
			}
			else
			{
				_dirSeries[bar] = TradeDirection.Between;

				if (AltRequired(bar, TradeDirection.Between))
				{
					_reserveBand[bar].Upper = _upSeries[bar];
					_reserveBand[bar].Lower = _downSeries[bar];
					_reserveBand[bar - 1].Upper = _upSeries[bar - 1];
					_reserveBand[bar - 1].Lower = _downSeries[bar - 1];
				}
				else
				{
					_band[bar].Upper = _upSeries[bar];
					_band[bar].Lower = _downSeries[bar];
					_band[bar - 1].Upper = _upSeries[bar - 1];
					_band[bar - 1].Lower = _downSeries[bar - 1];
				}
			}
		}

		private bool AltRequired(int bar, TradeDirection dir)
		{
			if (bar <= 3 + Shift)
				return true;

			var prevAltValue = dir switch
			{
				TradeDirection.Buy => _upReserveBand[bar - 2],
				TradeDirection.Sell => _downReserveBand[bar - 2],
				TradeDirection.Between => _reserveBand[bar - 2],
				_ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
			};

			var altRequired = (TradeDirection)_dirSeries[bar - 1] != (TradeDirection)_dirSeries[bar] &&
				(TradeDirection)_dirSeries[bar - 2] == (TradeDirection)_dirSeries[bar];

			return altRequired && prevAltValue.Lower == 0;
		}

		private void RangeChanged(object sender, PropertyChangedEventArgs e)
		{
			if ((RangeDataSeries)sender == _band)
			{
				_reserveBand.RangeColor = _band.RangeColor;
				_reserveBand.Visible = _band.Visible;
				_reserveBand.DrawAbovePrice = _band.DrawAbovePrice;
				_reserveBand.IgnoredByAlerts = _band.IgnoredByAlerts;
			}
			else if ((RangeDataSeries)sender == _upBand)
			{
				_upReserveBand.RangeColor = _upBand.RangeColor;
				_upReserveBand.Visible = _upBand.Visible;
				_upReserveBand.DrawAbovePrice = _upBand.DrawAbovePrice;
				_upReserveBand.IgnoredByAlerts = _upBand.IgnoredByAlerts;
            }
			else if ((RangeDataSeries)sender == _downBand)
			{
				_downReserveBand.RangeColor = _downBand.RangeColor;
				_downReserveBand.Visible = _downBand.Visible;
				_downReserveBand.DrawAbovePrice = _downBand.DrawAbovePrice;
				_downReserveBand.IgnoredByAlerts = _downBand.IgnoredByAlerts;
            }
		}

		#endregion
	}
}