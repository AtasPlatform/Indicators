namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Aroon Indicator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43406-aroon-indicator")]
	public class AroonIndicator : Indicator
	{
		#region Nested types

		private class ExtValue
		{
			#region Properties

			public decimal High { get; set; }

			public decimal Low { get; set; }

			public int Bar { get; set; }

			#endregion
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _downSeries = new("DownSeries", Strings.Lowest);
		private readonly List<ExtValue> _extValues = new();
		private readonly ValueDataSeries _upSeries = new("UpSeries", Strings.Highest);
		private int _lastBar;

		private int _period;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Order = 110)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AroonIndicator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			_lastBar = -1;
			_upSeries.Color = DefaultColors.Blue.Convert();
			_downSeries.Color = DefaultColors.Red.Convert();

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			_extValues.Clear();
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (_lastBar == bar)
				_extValues.RemoveAt(_extValues.Count - 1);

			_extValues.Add(new ExtValue
				{ Bar = bar, High = candle.High, Low = candle.Low });

			if (_extValues.Count > _period)
				_extValues.RemoveAt(0);

			_lastBar = bar;

			if (bar < _period)
				return;

			var highValue = _extValues.OrderByDescending(x => x.High).First();
			var lowValue = _extValues.OrderBy(x => x.Low).First();

			_upSeries[bar] = 100m * (_period - (bar - highValue.Bar)) / _period;
			_downSeries[bar] = 100m * (_period - (bar - lowValue.Bar)) / _period;
		}

		#endregion
	}
}