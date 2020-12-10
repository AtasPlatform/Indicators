namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("BollingerBands: Percaentage")]
	public class BollingerBandsPercent : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "BottomBand")]
			Bottom,

			[Display(ResourceType = typeof(Resources), Name = "MiddleBand")]
			Middle
		}

		#endregion

		#region Fields

		private readonly BollingerBands _bb = new BollingerBands();

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private Mode _calcMode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{

				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _bb.Period;
			set
			{
				if (value <= 0)
					return;

				_bb.Period = _bb.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBandsPercent()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_calcMode = Mode.Bottom;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_bb.Calculate(bar, value);
			var top = ((ValueDataSeries)_bb.DataSeries[1])[bar];

			switch (_calcMode)
			{
				case Mode.Bottom:
					var bot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

					if (top - bot == 0)
						return;

					_renderSeries[bar] = (value - bot) / (top - bot);
					break;
				case Mode.Middle:
					var sma = ((ValueDataSeries)_bb.DataSeries[0])[bar];

					if (top - sma == 0)
						return;

					_renderSeries[bar] = (value - sma) / (top - sma);
					break;
			}
		}

		#endregion
	}
}