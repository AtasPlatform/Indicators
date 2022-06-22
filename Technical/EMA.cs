namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("EMA")]
	[LocalizedDescription(typeof(Resources), "EMA")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/384-ema")]
	public class EMA : Indicator
	{
		#region Fields

		private int _period;
		private bool _onLine;
		private int _lastAlert;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (_period == value)
					return;

				if (value <= 0)
					return;

				_period = value;

				RaisePropertyChanged("Period");
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

		#region ctor

		public EMA()
		{
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				this[bar] = value;
			else
				this[bar] = value * (2.0m / (1 + Period)) + (1 - 2.0m / (1 + Period)) * this[bar - 1];

			if (bar != CurrentBar - 1 || !UseAlerts)
				return;

			if (_lastAlert == bar && !RepeatAlert)
				return;

			var close = GetCandle(bar).Close;
			var onLine = Math.Abs(this[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivity;

			if (onLine && !_onLine)
			{
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"EMA approximation alert: {this[bar]:0.#####}", BackgroundColor, FontColor);
				_lastAlert = bar;
			}

			_onLine = onLine;
		}

		#endregion
	}
}