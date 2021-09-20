﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("True Strength Index")]
	[FeatureId("NotReady")]
	public class TSI : Indicator
	{
		#region Fields

		private readonly EMA _absEma = new();
		private readonly EMA _absSecEma = new();
		private readonly EMA _ema = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Values);
		private readonly ValueDataSeries _renderSmoothedSeries = new(Resources.Smooth);
		private readonly EMA _secEma = new();
		private readonly EMA _smoothEma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod1", GroupName = "Settings", Order = 100)]
		public int EmaPeriod
		{
			get => _ema.Period;
			set
			{
				_ema.Period = _absEma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EmaPeriod2", GroupName = "Settings", Order = 110)]
		public int EmaSecPeriod
		{
			get => _secEma.Period;
			set
			{
				_secEma.Period = _absSecEma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		public int SmoothPeriod
		{
			get => _smoothEma.Period;
			set
			{
				_smoothEma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TSI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_renderSeries.Color = Colors.Blue;

			EmaPeriod = 13;
			EmaSecPeriod = 25;
			SmoothPeriod = 10;

			DataSeries[0] = _renderSeries;
			DataSeries.Add(_renderSmoothedSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var diff = candle.Close - prevCandle.Close;
			_ema.Calculate(bar, diff);
			_secEma.Calculate(bar, _ema[bar]);

			_absEma.Calculate(bar, Math.Abs(diff));
			_absSecEma.Calculate(bar, _absEma[bar]);

			_renderSeries[bar] = 100 * _secEma[bar] / _absSecEma[bar];
			_renderSmoothedSeries[bar] = _smoothEma.Calculate(bar, _renderSeries[bar]);
		}

		#endregion
	}
}