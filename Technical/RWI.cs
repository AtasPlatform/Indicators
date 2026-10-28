namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Random Walk Indicator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.RWIDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602453")]
	public class RWI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeries = new("HighSeries", Strings.Highest) 
		{ 
			Color = DefaultColors.Green.Convert(),
            DescriptionKey = nameof(Strings.UpTrendSettingsDescription)
        };

		private readonly ValueDataSeries _lowSeries = new("LowSeries", Strings.Lowest)
		{
			DescriptionKey = nameof(Strings.DownTrendSettingsDescription)
		};

		private readonly TrueRange _trueRange = new();
		private decimal[] _squareRoots = Array.Empty<decimal>();
		private int _period = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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

		#endregion

		#region ctor

		public RWI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Add(_trueRange);

			DataSeries[0] = _lowSeries;
			DataSeries.Add(_highSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			if (bar < _period)
				return;

			if (_squareRoots.Length < _period)
			{
				_squareRoots = new decimal[_period];
				for (var i = 1; i <= _period; i++)
					_squareRoots[i - 1] = (decimal)Math.Sqrt(i);
			}

			var maxHigh = 0m;
			var maxLow = 0m;
			var candle = GetCandle(bar);
			var trueRange = (ValueDataSeries)_trueRange.DataSeries[0];
			var rangeSum = 0m;

			for (var i = 1; i <= _period; i++)
			{
				var stepCandle = GetCandle(bar - i);
				// Each longer ATR window adds one older closed candle.
				rangeSum += trueRange[bar - i];
				var atr = rangeSum / i;
				var denominator = atr * _squareRoots[i - 1];
				var high = atr == 0 ? 0 : (candle.High - stepCandle.Low) / denominator;
				var low = atr == 0 ? 0 : (stepCandle.High - candle.Low) / denominator;

				if (high > maxHigh)
					maxHigh = high;

				if (low > maxLow)
					maxLow = low;
			}

			_highSeries[bar] = maxHigh;
			_lowSeries[bar] = maxLow;
		}

		#endregion
	}
}
