namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Starc Bands")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45497-starc-bands")]
	public class StarcBands : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly ValueDataSeries _botSeries = new(Strings.BottomBand);
		private readonly SMA _sma = new();
		private readonly ValueDataSeries _smaSeries = new(Strings.SMA);
		private readonly ValueDataSeries _topSeries = new(Strings.TopBand);
		private decimal _botBand;
		private decimal _topBand;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "ATR", GroupName = "Settings", Order = 110)]
		public int SmaPeriod
		{
			get => _atr.Period;
			set
			{
				if (value <= 0)
					return;

				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "TopBand", GroupName = "BBandsWidth", Order = 200)]
		public decimal TopBand
		{
			get => _topBand;
			set
			{
				if (value <= 0)
					return;

				_topBand = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "BottomBand", GroupName = "BBandsWidth", Order = 210)]
		public decimal BotBand
		{
			get => _botBand;
			set
			{
				if (value <= 0)
					return;

				_botBand = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StarcBands()
		{
			_topSeries.Color = _botSeries.Color = Colors.DodgerBlue;
			_sma.Period = 10;
			_atr.Period = 10;
			_topBand = 1;
			_botBand = 1;

			Add(_atr);

			DataSeries[0] = _topSeries;
			DataSeries.Add(_botSeries);
			DataSeries.Add(_smaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_topSeries[bar] = _sma[bar] + _topBand * _atr[bar];
			_botSeries[bar] = _sma[bar] - _botBand * _atr[bar];
			_smaSeries[bar] = _sma[bar];
		}

		#endregion
	}
}