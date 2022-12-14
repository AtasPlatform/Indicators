namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("SMA")]
	[LocalizedDescription(typeof(Resources), "SMA")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/9197-sma")]
	public class SMA : Indicator
	{
		#region Fields

		private int _lastBar = -1;
		private int _period = 10;
		private decimal _sum;
		private bool _onLine;
		private int _lastAlert;

		private ValueDataSeries _renderSeries = new("SMA");
		private System.Drawing.Color _bullishColor = System.Drawing.Color.Red;
		private System.Drawing.Color _bearishColor = System.Drawing.Color.Red;
        #endregion

        public SMA()
		{
			DataSeries[0] = _renderSeries;
		}

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "BullishColor",
			GroupName = "Common",
			Order = 30)]
		public System.Drawing.Color BullishColor
        {
			get => _bullishColor;
			set
			{
				_bullishColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "BearlishColor",
			GroupName = "Common",
			Order = 50)]
		public System.Drawing.Color BearishColor
        {
			get => _bearishColor;
			set
			{
				_bearishColor = value;
				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "ApproximationAlert",
			Order = 100)]
		public bool UseAlerts { get; set; }


		[Display(ResourceType = typeof(Resources),
			Name = "RepeatAlert",
			GroupName = "ApproximationAlert",
			Order = 110)]
		[Range(0, 100000)]
		public bool RepeatAlert { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "ApproximationFilter",
			GroupName = "ApproximationAlert",
			Order = 120)]
		[Range(0, 100000)]
		public int AlertSensitivity { get; set; } = 1;

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "ApproximationAlert",
			Order = 130)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "FontColor",
			GroupName = "ApproximationAlert",
			Order = 140)]
		public Color FontColor { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Resources),
			Name = "BackGround",
			GroupName = "ApproximationAlert",
			Order = 150)]
		public Color BackgroundColor { get; set; } = Colors.DimGray;

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_onLine = false;
				_sum = 0;
				_renderSeries[bar] = value;
				return;
			}

			if (bar != _lastBar)
			{
				_lastBar = bar;
				_sum += (decimal)SourceDataSeries[bar - 1];

				if (bar >= Period)
					_sum -= (decimal)SourceDataSeries[bar - Period];
			}

			var sum = _sum + value;
			_renderSeries[bar] = sum / Math.Min(Period, bar + 1);
			
			_renderSeries.Colors[bar] = _renderSeries[bar] > _renderSeries[bar - 1] 
				? BullishColor
                : BearishColor;

            if (bar != CurrentBar - 1 || !UseAlerts)
				return;

			if (_lastAlert == bar && !RepeatAlert)
				return;

			var close = GetCandle(bar).Close;
			var onLine = Math.Abs(_renderSeries[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivity;

			if (onLine && !_onLine)
			{
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"SMA approximation alert: {_renderSeries[bar]:0.#####}", BackgroundColor, FontColor);
				_lastAlert = bar;
			}

			_onLine = onLine;
		}

		#endregion
	}
}