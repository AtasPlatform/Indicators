namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context.GDIPlus;
	using OFT.Rendering.Settings;

	using Utils.Common.Logging;

	using Color = System.Drawing.Color;
	using Pen = System.Drawing.Pen;

	[DisplayName("Speed of Tape")]
	[Category("Order Flow")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/430-speed-of-tape")]
	public class SpeedOfTape : Indicator
	{
		#region Nested types

		[Serializable]
		public enum SpeedOfTapeType
		{
			[Display(ResourceType = typeof(Resources), Name = "Volume")]
			Volume,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks,

			[Display(ResourceType = typeof(Resources), Name = "Buys")]
			Buys,

			[Display(ResourceType = typeof(Resources), Name = "Sells")]
			Sells,

			[Display(ResourceType = typeof(Resources), Name = "Delta")]
			Delta
		}

		#endregion

		#region Fields
		
		private readonly PaintbarsDataSeries _paintBars = new("Paint bars");

		private readonly SMA _sma = new()
			{ Name = "Filter line" };

		private readonly ValueDataSeries _smaSeries;
		private readonly ValueDataSeries _renderSeries = new("Speed of tape")
		{
			ResetAlertsOnNewBar = true,
			VisualType = VisualMode.Histogram,
			Color = System.Windows.Media.Color.FromArgb(255, 0, 255, 255)
		};
		private bool _autoFilter = true;
		private int _barsLength = 10;
		private bool _drawLines;
		private int _lastAlertBar = -1;
		private Pen _negPen = new(Color.Red, 1);

		private Pen _posPen = new(Color.Green, 1);
		private int _sec = 15;
		private int _trades = 100;

		private SpeedOfTapeType _type = SpeedOfTapeType.Ticks;
		private Color _maxSpeedColor = Color.Yellow;

		#endregion

        #region Properties

        [Display(Name = "Maximum Speed", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color MaxSpeedColor
        {
	        get => _maxSpeedColor.Convert();
	        set
	        {
		        _maxSpeedColor = value.Convert();
		        for (var i = 0; i < _paintBars.Count; i++)
		        {
			        if (_paintBars[i] != null)
				        _paintBars[i] = value;
		        }

                RecalculateValues();
	        }
        }
		
        [Display(ResourceType = typeof(Resources), Name = "PaintBars")]
		public bool PaintBars
		{
			get => _paintBars.Visible;
			set
			{
				_paintBars.Visible = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "AutoFilter", GroupName = "Filters")]
		public bool AutoFilter
		{
			get => _autoFilter;
			set
			{
				_autoFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "AutoFilterPeriod", GroupName = "Filters")]
		public int AutoFilterPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TimeFilterSec", GroupName = "Filters")]
		public int Sec
		{
			get => _sec;
			set
			{
				_sec = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TradesFilter", GroupName = "Filters")]
		public int Trades
		{
			get => _trades;
			set
			{
				_trades = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Filters")]
		public SpeedOfTapeType Type
		{
			get => _type;
			set
			{
				_type = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts")]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts")]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts")]
		public System.Windows.Media.Color AlertForeColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts")]
		public System.Windows.Media.Color AlertBgColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 75, 72, 72);

		[Display(ResourceType = typeof(Resources), Name = "ShowPriceSelection", GroupName = "Visualization")]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Length", GroupName = "Visualization")]
		[Range(0, 1000)]
		public int BarsLength
		{
			get => _barsLength;
			set
			{
				_barsLength = value;

				if (value == 0)
					TrendLines.ForEach(x => x.IsRay = true);
				else
				{
					TrendLines.ForEach(x =>
					{
						x.IsRay = false;
						x.SecondBar = x.FirstBar + value;
					});
				}
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PositiveDelta", GroupName = "Visualization")]
		public PenSettings PosPen { get; set; } = new() { Color = Colors.Green };

		[Display(ResourceType = typeof(Resources), Name = "NegativeDelta", GroupName = "Visualization")]
		public PenSettings NegPen { get; set; } = new() { Color = Colors.Red };

		#endregion

		#region ctor

		public SpeedOfTape()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			var main = (ValueDataSeries)DataSeries[0];
			main.Color = Colors.Aqua;
			main.VisualType = VisualMode.Histogram;
			main.UseMinimizedModeIfEnabled = true;


			DataSeries[0] = _renderSeries;

			((ValueDataSeries)_sma.DataSeries[0]).Name = "Filter line";
			_smaSeries = (ValueDataSeries)_sma.DataSeries[0];
			_smaSeries.Width = 2;
			_smaSeries.Color = Colors.LightBlue;
			_smaSeries.UseMinimizedModeIfEnabled = true;
			_smaSeries.IgnoredByAlerts = true;
			
			DataSeries.Add(_smaSeries);
			DataSeries.Add(_paintBars);

			_paintBars.IsHidden = true;

			PosPen.PropertyChanged += PosPenChanged;
			NegPen.PropertyChanged += NegPenChanged;
		}

        #endregion

        #region Protected methods
		
        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        PosPen.Color = ChartInfo.ColorsStore.DownCandleColor.Convert();
	        NegPen.Color = ChartInfo.ColorsStore.UpCandleColor.Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				TrendLines.Clear();

			var j = bar;
			var pace = 0m;
			var currentCandle = GetCandle(bar);

			while (j >= 0)
			{
				var candle = GetCandle(j);
				var ts = currentCandle.Time - candle.Time;

				if (ts.TotalSeconds < Sec)
				{
					if (_type == SpeedOfTapeType.Volume)
						pace += candle.Volume;

					if (_type == SpeedOfTapeType.Ticks)
						pace += candle.Ticks;
					else if (_type == SpeedOfTapeType.Buys)
						pace += candle.Ask;
					else if (_type == SpeedOfTapeType.Sells)
						pace += candle.Bid;
					else if (_type == SpeedOfTapeType.Delta)
						pace += candle.Delta;
				}
				else
				{
					pace = pace * Sec / (decimal)ts.TotalSeconds;
					break;
				}

				j--;
			}

			_sma.Calculate(bar, pace * 1.5m);

			if (!AutoFilter)
				_smaSeries[bar] = Trades;

			if (bar > 0)
				_smaSeries.Colors[bar] = _smaSeries[bar] > _smaSeries[bar - 1]
					? _posPen.Color
					: _negPen.Color;

			_renderSeries[bar] = pace;

            if (Math.Abs(pace) > _smaSeries[bar])
            {
	            _renderSeries.Colors[bar] = _maxSpeedColor;
				_paintBars[bar] = MaxSpeedColor;

				if (ChartInfo.ChartType != "TimeFrame" && DrawLines)
				{
					var price = (currentCandle.High + currentCandle.Low) / 2;
					TrendLines.RemoveAll(x => x.FirstBar == bar);

					var line = new TrendLine(bar, price, bar + BarsLength, price, currentCandle.Delta >= 0 ? _posPen : _negPen);

					if (line.FirstBar == line.SecondBar)
					{
						line.SecondBar = line.FirstBar + 1;
						line.IsRay = true;
					}

					TrendLines.Add(line);
				}

				if (UseAlerts && bar == CurrentBar - 1 && bar != _lastAlertBar)
				{
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Speed of tape is increased to {pace:0.####} value", AlertBgColor, AlertForeColor);
					_lastAlertBar = bar;
				}
			}
			else
			{
				_paintBars[bar] = null;
			}
		}

		#endregion

		#region Private methods

		private void PosPenChanged(object sender, PropertyChangedEventArgs e)
		{
			_posPen = ((PenSettings)sender).RenderObject.ToPen();

			if (ChartInfo == null)
				return;

			if (ChartInfo.ChartType != "TimeFrame")
				RecalculateValues();
		}

		private void NegPenChanged(object sender, PropertyChangedEventArgs e)
		{
			_negPen = ((PenSettings)sender).RenderObject.ToPen();

			if (ChartInfo == null)
				return;

			if (ChartInfo.ChartType != "TimeFrame")
				RecalculateValues();
		}

		#endregion
	}
}