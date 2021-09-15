namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Super Trend")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/14383-super-trend")]
	public class SuperTrend : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();
		private ValueDataSeries _dnTrend = new("Down Trend") { VisualType = VisualMode.Square, Color = Colors.Maroon, Width = 2 };

		private decimal _multiplier = 1.7m;

		private ValueDataSeries _trend = new("trend");
		private ValueDataSeries _upTrend;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
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

		[Display(ResourceType = typeof(Strings), Name = "Multiplier", GroupName = "Common")]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SuperTrend()
			: base(true)
		{
			Period = 14;
			_upTrend = (ValueDataSeries)DataSeries[0];
			_upTrend.Name = "Up Trend";
			_upTrend.Width = 2;
			_upTrend.VisualType = VisualMode.Square;
			_upTrend.ShowZeroValue = false;
			_upTrend.Color = Colors.Blue;
			_dnTrend.ShowZeroValue = false;
			DataSeries.Add(_dnTrend);

			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			_upTrend[bar] = _dnTrend[bar] = 0;
			var candle = GetCandle(bar);
			var prevcandle = GetCandle(bar - 1);
			var median = (candle.Low + candle.High) / 2;
			var atr = _atr[bar];
			var dUpperLevel = median + atr * Multiplier;
			var dLowerLevel = median - atr * Multiplier;

			// Set supertrend levels
			if (candle.Close > _trend[bar - 1] && prevcandle.Close <= _trend[bar - 1])
				_trend[bar] = dLowerLevel;
			else if (candle.Close < _trend[bar - 1] && prevcandle.Close >= _trend[bar - 1])
				_trend[bar] = dUpperLevel;
			else if (_trend[bar - 1] < dLowerLevel)
				_trend[bar] = dLowerLevel;
			else if (_trend[bar - 1] > dUpperLevel)
				_trend[bar] = dUpperLevel;
			else
				_trend[bar] = _trend[bar - 1];

			if (candle.Close > _trend[bar] || candle.Close == _trend[bar] && prevcandle.Close > _trend[bar - 1])
				_upTrend[bar] = _trend[bar];
			else if (candle.Close < _trend[bar] || candle.Close == _trend[bar] && prevcandle.Close < _trend[bar - 1])
				_dnTrend[bar] = _trend[bar];
		}

		#endregion
	}
}