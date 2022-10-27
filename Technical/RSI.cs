namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	using Utils.Common.Localization;

	[DisplayName("RSI")]
	[LocalizedDescription(typeof(Resources), "RSI")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7085-rsi")]
	public class RSI : Indicator
	{
		#region Fields

		private readonly SMMA _negative = new() { Period = 10 };
        private readonly SMMA _positive = new() { Period = 10 };

		private LineSeries _downLine = new("Down")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 30,
			Width = 1
		};

		private int _lastDownAlert;

		private int _lastUpAlert;
		private decimal _lastValue;

		private LineSeries _upLine = new("Up")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 70,
			Width = 1
		};

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]	
		public int Period
		{
			get => _positive.Period;
			set
			{
				_positive.Period = _negative.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "UpAlert",
			Order = 100)]
		public bool UseUpAlert { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "UpAlert",
			Order = 110)]
		public string UpAlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "DownAlert",
			Order = 200)]
		public bool UseDownAlert { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "DownAlert",
			Order = 210)]
		public string DownAlertFile { get; set; } = "alert1";

		#endregion

		#region ctor

		public RSI()
		{
			Panel = IndicatorDataProvider.NewPanel;

			LineSeries.Add(_downLine);
			LineSeries.Add(_upLine);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				this[bar] = 0;
				_lastUpAlert = _lastDownAlert = 0;
				return;
			}

			var diff = (decimal)SourceDataSeries[bar] - (decimal)SourceDataSeries[bar - 1];
			var pos = _positive.Calculate(bar, diff > 0 ? diff : 0);
			var neg = _negative.Calculate(bar, diff < 0 ? -diff : 0);

			if (neg != 0)
			{
				var div = pos / neg;

				this[bar] = div == 1
					? 0m
					: 100m - 100m / (1m + div);
			}
			else
				this[bar] = 100m;

			if (bar == CurrentBar - 1 && _lastValue != 0)
			{
				if (UseUpAlert)
				{
					if ((_lastValue < _upLine.Value && this[bar] >= _upLine.Value || _lastValue > _upLine.Value && this[bar] <= _upLine.Value)
						&& _lastUpAlert != bar)
					{
						AddAlert(UpAlertFile, InstrumentInfo.Instrument, $"Up value alert {this[bar]:0.#####}", Colors.Black, _upLine.Color);
						_lastUpAlert = bar;
					}
				}

				if (UseDownAlert)
				{
					if ((_lastValue < _downLine.Value && this[bar] >= _downLine.Value || _lastValue > _downLine.Value && this[bar] <= _downLine.Value)
					    && _lastDownAlert != bar)
					{
						AddAlert(DownAlertFile, InstrumentInfo.Instrument, $"Down value alert {this[bar]:0.#####}", Colors.Black, _downLine.Color);
						_lastDownAlert = bar;
					}
				}
			}

			_lastValue = this[bar];
		}

		#endregion
	}
}