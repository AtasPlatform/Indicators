namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("MFI")]
	public class MFI : Indicator
	{
		#region Fields

		private readonly LineSeries _overbought = new LineSeries("Overbought");
		private readonly LineSeries _oversold = new LineSeries("Oversold");

		private List<decimal> _flows = new List<decimal>();
		private int _period;
		private int _prevBar;
		private decimal _previousTypical;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				_previousTypical = -1;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "Overbought", GroupName = "Common", Order = 10)]
		public decimal Overbought
		{
			get => _overbought.Value;
			set
			{
				if (value < _oversold.Value)
					return;

				_overbought.Value = value;
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "Oversold", GroupName = "Common", Order = 20)]
		public decimal Oversold
		{
			get => _oversold.Value;
			set
			{
				if (value > _overbought.Value)
					return;

				_oversold.Value = value;
			}
		}

		#endregion

		#region ctor

		public MFI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 14;
			_prevBar = -1;

			_overbought.Color = _oversold.Color = Colors.Green;
			_overbought.Value = 80;
			_oversold.Value = 20;

			DataSeries.Clear();

			var mfiSeries = new ValueDataSeries("MFI")
			{
				ShowZeroValue = false
			};
			DataSeries.Add(mfiSeries);

			LineSeries.Add(_overbought);
			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);
			var typical = (currentCandle.High + currentCandle.Low + currentCandle.Close) / 3.0m;

			if (bar == 0)
			{
				_flows.Clear();
				_previousTypical = typical;

				var series = (ValueDataSeries)DataSeries[0];
				series.SetPointOfEndLine(_period - 2);
			}

			var moneyFlow = typical * currentCandle.Volume;

			if (typical < _previousTypical)
				moneyFlow = -moneyFlow;

			if (bar != _prevBar)
			{
				if (_flows.Count >= Period)
					_flows = _flows.Skip(1).ToList();
			}
			else
				_flows.RemoveAt(_flows.Count - 1);

			_flows.Add(moneyFlow);

			if (_flows.Count < Period)
			{
				DataSeries[0][bar] = 0m;
				return;
			}

			var positiveFlow = _flows.Where(x => x > 0).Sum();
			var negativeFlow = _flows.Where(x => x < 0).Sum(x => -x);

			if (negativeFlow == 0.0m)
				DataSeries[0][bar] = 100.0m;
			else
			{
				var moneyRatio = positiveFlow / negativeFlow;
				DataSeries[0][bar] = 100.0m - 100.0m / (1.0m + moneyRatio);
			}

			_previousTypical = typical;
			_prevBar = bar;
		}

		#endregion
	}
}