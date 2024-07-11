namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Windows.Media;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
    using OFT.Rendering.Settings;

    using Color = System.Drawing.Color;

    [DisplayName("Speed of Tape")]
	[Category("Order Flow")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SpeedOfTapeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602472")]
	public class SpeedOfTape : Indicator
	{
		#region Nested types

		[Serializable]
		public enum SpeedOfTapeType
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
			Volume,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Ticks,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Buys))]
			Buys,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Sells))]
			Sells,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Delta))]
			Delta
		}

		internal class Signal
		{
			internal int Bar { get; set; }
            internal decimal Price { get; set; }
            internal bool IsBullish { get; set; }
        }

		#endregion

		#region Fields

		private readonly ConcurrentBag<Signal> _signals = [];
		private readonly PaintbarsDataSeries _paintBars = new("PaintBars", "Paint bars") { IsHidden = true };

		private readonly SMA _sma = new()
			{ Name = "Filter line" };

		private readonly ValueDataSeries _smaSeries;
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", "Speed of tape")
		{
			ResetAlertsOnNewBar = true,
			VisualType = VisualMode.Histogram,
			Color = System.Windows.Media.Color.FromArgb(255, 0, 255, 255)
		};

		private bool _autoFilter = true;
		private int _lastAlertBar = -1;
		private int _sec = 15;
		private int _trades = 100;

		private SpeedOfTapeType _type = SpeedOfTapeType.Ticks;
		private Color _maxSpeedColor = DefaultColors.Yellow;

        #endregion

        #region Properties

        #region Filters

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AutoFilter), GroupName = nameof(Strings.Filters), Description = nameof(Strings.SpeedOfTapeAutoFilterDescription))]
		public bool AutoFilter
		{
			get => _autoFilter;
			set
			{
				_autoFilter = value;
				RecalculateValues();
			}
		}

        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AutoFilterPeriod), GroupName = nameof(Strings.Filters), Description = nameof(Strings.SMAPeriodDescription))]
		public int AutoFilterPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFilterSec), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MaxTimeFilterDescription))]
		public int Sec
		{
			get => _sec;
			set
			{
				_sec = Math.Max(1, value);
				RecalculateValues();
			}
		}

        [Range(0, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TradesFilter), GroupName = nameof(Strings.Filters), Description = nameof(Strings.TradesCountDescription))]
		public int Trades
		{
			get => _trades;
			set
			{
				_trades = Math.Max(1, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Filters), Description = nameof(Strings.CalculationModeDescription))]
		public SpeedOfTapeType Type
		{
			get => _type;
			set
			{
				_type = value;
				RecalculateValues();
			}
		}

        #endregion

        #region Alerts

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription))]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription))]
		public System.Windows.Media.Color AlertForeColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription))]
		public System.Windows.Media.Color AlertBgColor { get; set; } = System.Windows.Media.Color.FromArgb(255, 75, 72, 72);

        #endregion

        #region Visualization

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowPriceSelection), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.DrawLinesDescription))]
		public bool DrawLines { get; set; } = true;

        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Length), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LineLengthDescription))]
		public int BarsLength { get; set; } = 10;

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveDelta), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PenSettingsDescription))]
		public PenSettings PosPen { get; set; } = new PenSettings() { Color = DefaultColors.Green.Convert() };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeDelta), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PenSettingsDescription))]
		public PenSettings NegPen { get; set; } = new PenSettings() { Color = DefaultColors.Red.Convert() };

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FilterColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.FilterCandleColorDescription))]
        public Color MaxSpeedColor
        {
            get => _maxSpeedColor;
            set
            {
                _maxSpeedColor = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PaintBars), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PaintBarsDescription))]
        public bool PaintBars
        {
            get => _paintBars.Visible;
            set
            {
                _paintBars.Visible = value;
                RecalculateValues();
            }
        }


        #endregion

        #endregion

        #region ctor

        public SpeedOfTape()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			EnableCustomDrawing = true;

			DataSeries[0] = _renderSeries;

			_sma.ColoredDirection = false;
            _smaSeries = (ValueDataSeries)_sma.DataSeries[0];
            _smaSeries.Id = "FilterLineDataSeries";
			_smaSeries.Name = "Filter line";
            _smaSeries.Width = 2;
			_smaSeries.Color = Colors.LightBlue;
			_smaSeries.UseMinimizedModeIfEnabled = true;
			_smaSeries.IgnoredByAlerts = true;
			
			DataSeries.Add(_smaSeries);
			DataSeries.Add(_paintBars);

            PosPen.PropertyChanged += Pen_PropertyChanged;
            NegPen.PropertyChanged += Pen_PropertyChanged;
        }

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

			PosPen.Color = ChartInfo.ColorsStore.UpCandleColor.Convert();
			NegPen.Color = ChartInfo.ColorsStore.DownCandleColor.Convert();
		}

        protected override void OnRecalculate()
        {
			_signals.Clear();
        }

        protected override void OnCalculate(int bar, decimal value)
		{
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
			
			_renderSeries[bar] = pace;

            if (Math.Abs(pace) > _smaSeries[bar])
            {
	            _renderSeries.Colors[bar] = _maxSpeedColor;
				_paintBars[bar] = _maxSpeedColor.Convert();

                var signal = _signals.LastOrDefault(s => s.Bar == bar) ?? new Signal() { Bar = bar };
                signal.Price = (currentCandle.High + currentCandle.Low) / 2;
                signal.IsBullish = currentCandle.Delta >= 0;
                _signals.Add(signal);

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

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
			if (ChartInfo is null) return;

			if (DrawLines) DrawSignalLines(context);
        }

        #endregion

        #region Private methods

        private void DrawSignalLines(RenderContext context)
        {
            foreach (var signal in _signals)
            {
                if (signal.Bar > LastVisibleBarNumber || (signal.Bar + BarsLength) < FirstVisibleBarNumber)
                    continue;

                if (signal.Price <= ChartInfo.PriceChartContainer.Low) continue;

                var x1 = ChartInfo.GetXByBar(signal.Bar);
                var y = ChartInfo.GetYByPrice(signal.Price, false);
                var x2 = Math.Min(ChartInfo.GetXByBar(signal.Bar + BarsLength), ChartInfo.Region.Width);
                var pen = signal.IsBullish ? PosPen : NegPen;

                context.DrawLine(pen.RenderObject, x1, y, x2, y);
            }
        }

        private void Pen_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
			if ((PenSettings)sender == PosPen && e.PropertyName == nameof(PosPen.Color))
				_sma.BullishColor = PosPen.Color;

            if ((PenSettings)sender == NegPen && e.PropertyName == nameof(PosPen.Color))
                _sma.BearishColor = NegPen.Color;
        }

        #endregion
    }
}