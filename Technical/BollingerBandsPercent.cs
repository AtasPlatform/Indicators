namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Bollinger Bands: Percentage")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45187-bollinger-bands-percentage")]
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

		private readonly BollingerBands _bb = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
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

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "Settings", Order = 120)]
		public decimal Width
		{
			get => _bb.Width;
			set
			{
				if (value <= 0)
					return;

				_bb.Width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBandsPercent()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_bb.Period = 10;
			_bb.Width = 1;
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

					_renderSeries[bar] = 100 * (value - bot) / (top - bot);
					break;
				case Mode.Middle:
					var sma = ((ValueDataSeries)_bb.DataSeries[0])[bar];

					if (top - sma == 0)
						return;

					_renderSeries[bar] = 100 * (value - sma) / (top - sma);
					break;
			}
		}

		#endregion
	}
}