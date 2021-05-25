namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Bollinger Squeeze 2")]
	[FeatureId("NotReady")]
	public class BollingerSqueezeV2 : Indicator
	{
		#region Fields

		private readonly BollingerBands _bb = new();
		private readonly ValueDataSeries _downSeries = new(Resources.Down);
		private readonly EMA _emaMomentum = new();
		private readonly KeltnerChannel _kb = new();
		private readonly ValueDataSeries _lowEmaSeries = new("EMA Low");
		private readonly ValueDataSeries _lowerEmaSeries = new("EMA Lower");
		private readonly Momentum _momentum = new();
		private readonly ValueDataSeries _upEmaSeries = new("EMA Up");
		private readonly ValueDataSeries _upperEmaSeries = new("EMA Upper");

		private readonly ValueDataSeries _upSeries = new(Resources.Up);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "BollingerBands", Order = 100)]
		public int BbPeriod
		{
			get => _bb.Period;
			set
			{
				if (value <= 0)
					return;

				_bb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "BollingerBands", Order = 110)]
		public decimal BbWidth
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "KeltnerChannel", Order = 200)]
		public int KbPeriod
		{
			get => _kb.Period;
			set
			{
				if (value <= 0)
					return;

				_kb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "OffsetMultiplier", GroupName = "KeltnerChannel", Order = 210)]
		public decimal KbMultiplier
		{
			get => _kb.Koef;
			set
			{
				if (value <= 0)
					return;

				_kb.Koef = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Momentum", Order = 300)]
		public int MomentumPeriod
		{
			get => _momentum.Period;
			set
			{
				if (value <= 0)
					return;

				_momentum.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EMA", GroupName = "Momentum", Order = 310)]
		public int EmaMomentum
		{
			get => _emaMomentum.Period;
			set
			{
				if (value <= 0)
					return;

				_emaMomentum.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerSqueezeV2()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_bb.Period = 10;
			_bb.Width = 1;

			_kb.Period = 10;
			_kb.Koef = 1;

			_upSeries.Color = Colors.Green;
			_downSeries.Color = Colors.Firebrick;

			_upSeries.VisualType = _downSeries.VisualType = VisualMode.Dots;
			_upSeries.Width = _downSeries.Width = 3;

			_upperEmaSeries.Color = Colors.LimeGreen;
			_upEmaSeries.Color = Colors.DarkGreen;
			_lowerEmaSeries.Color = Colors.Red;
			_lowEmaSeries.Color = Colors.DarkRed;

			_upperEmaSeries.ShowZeroValue = _upEmaSeries.ShowZeroValue =
				_lowerEmaSeries.ShowZeroValue = _lowEmaSeries.ShowZeroValue =
					_upSeries.ShowZeroValue = _downSeries.ShowZeroValue = false;

			_upperEmaSeries.ShowCurrentValue = _upEmaSeries.ShowCurrentValue =
				_lowerEmaSeries.ShowCurrentValue = _lowEmaSeries.ShowCurrentValue =
					_upSeries.ShowCurrentValue = _downSeries.ShowCurrentValue = false;

			Add(_kb);

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
			DataSeries.Add(_upEmaSeries);
			DataSeries.Add(_upperEmaSeries);
			DataSeries.Add(_lowEmaSeries);
			DataSeries.Add(_lowerEmaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			_momentum.Calculate(bar, value);
			_bb.Calculate(bar, value);
			_emaMomentum.Calculate(bar, _momentum[bar]);

			if (bar == 0)
				return;

			if (_emaMomentum[bar] > 0 && _emaMomentum[bar] >= _emaMomentum[bar - 1])
				SetSeriesValue(bar, _upperEmaSeries, _emaMomentum[bar]);

			if (_emaMomentum[bar] > 0 && _emaMomentum[bar] < _emaMomentum[bar - 1])
				SetSeriesValue(bar, _upEmaSeries, _emaMomentum[bar]);

			if (_emaMomentum[bar] < 0 && _emaMomentum[bar] <= _emaMomentum[bar - 1])
				SetSeriesValue(bar, _lowerEmaSeries, _emaMomentum[bar]);

			if (_emaMomentum[bar] < 0 && _emaMomentum[bar] > _emaMomentum[bar - 1])
				SetSeriesValue(bar, _lowEmaSeries, _emaMomentum[bar]);

			var bbTop = ((ValueDataSeries)_bb.DataSeries[1])[bar];
			var bbBot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

			var kbTop = ((ValueDataSeries)_kb.DataSeries[1])[bar];
			var kbBot = ((ValueDataSeries)_kb.DataSeries[2])[bar];

			if (bbTop > kbTop && bbBot < kbBot)
				_upSeries[bar] = 0.00001m;
			else
				_downSeries[bar] = 0.00001m;
		}

		#endregion

		#region Private methods

		private void SetSeriesValue(int bar, ValueDataSeries series, decimal value)
		{
			if (series[bar - 1] == 0)
			{
				series.SetPointOfEndLine(bar - 2);
				series[bar - 1] = LastSeriesValue(bar);
			}

			series[bar] = value;

			if (_upEmaSeries[bar] == 0 && _upEmaSeries != series)
				_upEmaSeries.SetPointOfEndLine(bar - 1);

			if (_upperEmaSeries[bar] == 0 && _upperEmaSeries != series)
				_upperEmaSeries.SetPointOfEndLine(bar - 1);

			if (_lowerEmaSeries[bar] == 0 && _lowerEmaSeries != series)
				_lowerEmaSeries.SetPointOfEndLine(bar - 1);

			if (_lowEmaSeries[bar] == 0 && _lowEmaSeries != series)
				_lowEmaSeries.SetPointOfEndLine(bar - 1);
		}

		private decimal LastSeriesValue(int bar)
		{
			if (_upEmaSeries[bar - 1] != 0)
			{
				_upEmaSeries.SetPointOfEndLine(bar - 1);
				return _upEmaSeries[bar - 1];
			}

			if (_upperEmaSeries[bar - 1] != 0)
			{
				_upperEmaSeries.SetPointOfEndLine(bar - 1);
				return _upperEmaSeries[bar - 1];
			}

			if (_lowEmaSeries[bar - 1] != 0)
			{
				_lowEmaSeries.SetPointOfEndLine(bar - 1);
				return _lowEmaSeries[bar - 1];
			}

			if (_lowerEmaSeries[bar - 1] != 0)
			{
				_lowerEmaSeries.SetPointOfEndLine(bar - 1);
				return _lowerEmaSeries[bar - 1];
			}

			return 0;
		}

		#endregion
	}
}