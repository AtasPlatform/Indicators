namespace ATAS.Indicators.Technical
{

	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Rahul Mohindar Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45450-rahul-mohindar-oscillator")]
	public class RMO : Indicator
	{
        #region Fields

        private readonly EMA _emaSignal = new() { Period = 15 };
        private readonly EMA _emaSt1 = new() { Period = 10 };
        private readonly EMA _emaSt2 = new() { Period = 10 };

        private readonly ValueDataSeries _buySignal = new(Resources.Buys)
		{
			Color = DefaultColors.Green.Convert(),
			VisualType = VisualMode.UpArrow,
			ShowTooltip = false,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true
		};
        private readonly ValueDataSeries _emaSt1Series = new(Resources.EmaPeriod1)
		{
			Color = DefaultColors.DarkRed.Convert(),
			LineDashStyle = LineDashStyle.Dash,
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true
        };
        private readonly ValueDataSeries _emaSt2Series = new(Resources.EmaPeriod2)
		{
			Color = DefaultColors.Green.Convert(),
			LineDashStyle = LineDashStyle.Dash,
            UseMinimizedModeIfEnabled = true,
            IgnoredByAlerts = true
        };
        private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
        {
	        Color = Colors.DodgerBlue,
	        Width = 2,
	        UseMinimizedModeIfEnabled = true
        };
        private readonly ValueDataSeries _sellSignal = new(Resources.Sells)
        {
	        Color = DefaultColors.Red.Convert(),
	        VisualType = VisualMode.DownArrow,
	        ShowTooltip = false,
	        ShowZeroValue = false,
	        UseMinimizedModeIfEnabled = true,
	        IgnoredByAlerts = true
        };

        private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };

		private readonly List<SMA> _smaTen = new();
        private int _period = 10;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SMA", GroupName = "Period", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_smaTen.ForEach(x => x.Period = value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HighLow", GroupName = "Period", Order = 110)]
		[Range(1, 10000)]
        public int HighLow
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EMA", GroupName = "Period", Order = 120)]
		[Range(1, 10000)]
        public int EmaPeriod1
		{
			get => _emaSt1.Period;
			set
			{
				_emaSt1.Period = _emaSt2.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Period", Order = 130)]
		[Range(1, 10000)]
        public int SignalPeriod
		{
			get => _emaSignal.Period;
			set
			{
				_emaSignal.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public RMO()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			for (var i = 0; i < 10; i++)
			{
				_smaTen.Add(new SMA
					{ Period = _period });
			}
			
			DataSeries[0] = _renderSeries;
			DataSeries.Add(_buySignal);
			DataSeries.Add(_sellSignal);
			DataSeries.Add(_emaSt1Series);
			DataSeries.Add(_emaSt2Series);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			_smaTen[0].Calculate(bar, value);

			for (var i = 1; i < _smaTen.Count; i++)
				_smaTen[i].Calculate(bar, _smaTen[i - 1][bar]);

			var swingTrade = 0m;

			if (_highest.Calculate(bar, value) != _lowest.Calculate(bar, value))
			{
				var smaSum = _smaTen.Sum(x => x[bar]);
				swingTrade = 100 * (value - smaSum / 10) / (_highest[bar] - _lowest[bar]);
			}

			_emaSt1Series[bar] = _emaSt1.Calculate(bar, swingTrade);
			_emaSt2Series[bar] = _emaSt2.Calculate(bar, _emaSt1[bar]);

			_renderSeries[bar] = _emaSignal.Calculate(bar, swingTrade);

			if (bar == 0)
				return;

			if (_emaSt2[bar - 1] < _emaSt1[bar - 1] && _emaSt2[bar] > _emaSt1[bar])
				_buySignal[bar] = _renderSeries[bar];

			if (_emaSt2[bar - 1] > _emaSt1[bar - 1] && _emaSt2[bar] < _emaSt1[bar])
				_sellSignal[bar] = _renderSeries[bar];
		}

		#endregion
	}
}